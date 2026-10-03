# RecallDB Architecture

This document describes how RecallDB is built: the processes and components, how a request travels through them, how
data is laid out in PostgreSQL, and what happens at startup. Two companion documents go deeper on the data path:

- [INGESTION.md](INGESTION.md): the data model and every way data gets in, changes, and leaves.
- [RETRIEVAL.md](RETRIEVAL.md): every way data comes back out, how each search mode ranks, and how to tune it.

Field-by-field request and response formats are in [REST_API.md](../REST_API.md) and [MCP_API.md](../MCP_API.md).

---

## 1. What RecallDB is

RecallDB is a multi-tenant retrieval store for AI applications. It keeps text chunks, their embedding vectors, and the
metadata a retrieval pipeline needs (document grouping and order, content type, labels, key-value tags, timestamps)
in PostgreSQL with the pgvector extension, and answers vector, full-text, and hybrid queries over them in one request.

Two things are deliberately **not** part of RecallDB:

- **Embedding.** RecallDB never calls an embedding model. The caller computes vectors (with OpenAI, Cohere, Ollama, a
  local model, anything that returns floats) and sends them with each document and each vector query.
- **Chunking.** RecallDB stores whatever chunks it is given. Splitting source documents into chunks, and deciding their
  `DocumentId` and `Position`, happens in the caller. [INGESTION.md](INGESTION.md#4-modeling-chunked-documents) shows
  how to shape chunks so collapse and neighbor retrieval work.

Everything else (storage schema, indexing, filtering, ranking, fusion, grouping, multi-tenancy, auth, observability)
is RecallDB's job.

---

## 2. Components

```
                 ┌──────────────────────────────── RecallDb.Server (one .NET process) ───────────────────────────────┐
 REST client ───▶│ Watson HTTP :8600 ─▶ REST routes ─┐                                                               │
 (SDKs, curl,    │                                   ├─▶ RequestContext ─▶ Services ─▶ DatabaseDriver ─▶ SQL builders ──┼──▶ PostgreSQL 15
  dashboard)     │                                   │    (auth, tenant,   (Document,    (Postgresql)    (Search,        │    + pgvector 0.5.1
 MCP client  ───▶│ Voltaic HTTP :8620/mcp ─▶ tools ──┘     ids, payload)    Search, ...)                  Document, ...)  │    + pg_trgm
 (Claude Code,   │                                                                                                    │
  Cursor, ...)   │ OpenTelemetry host ─▶ Prometheus :9464/metrics, OTLP traces ─▶ Tempo                               │
                 └────────────────────────────────────────────────────────────────────────────────────────────────────┘
 Dashboard (React SPA, nginx :8601) ─▶ REST API
```

| Component | Where | Role |
|---|---|---|
| **RecallDb.Server** | `src/RecallDb.Server` | The server process. Hosts the REST API, the MCP server, the service layer, and the OpenTelemetry host. |
| **REST API** | `RecallDbServer.cs` (routes) | Watson Webserver on port 8600. About 60 routes under `/v1.0/...`, plus `GET /` (health and capabilities) and an OpenAPI document at `/openapi.json`. |
| **MCP server** | `src/RecallDb.Server/Mcp` | Voltaic, in the same process, Streamable HTTP on port 8620 (`/mcp`). Exposes the same operations as tools (`document_create`, `search_query`, ...). |
| **Service layer** | `src/RecallDb.Server/Services` | One service per resource (`DocumentService`, `SearchService`, `CollectionService`, ...). Both transports call the same methods, so REST and MCP behave identically. Services enforce tenant access, cross-field validation, and label/tag stitching. |
| **Database driver** | `src/RecallDb.Core/Database/Postgresql` | Builds and runs SQL. `DocumentMethods` (writes and enumeration), `SearchMethods` (every search mode), `CollectionMethods` (DDL), `LabelMethods`, `TagMethods`, and the startup schema pass in `PostgresqlDatabaseDriver`. |
| **Models** | `src/RecallDb.Core/Models` | Request and record types. Range checks live in the property setters, so an out-of-range value fails while the body is deserialized and becomes a 400. |
| **PostgreSQL + pgvector** | `docker/` (`ankane/pgvector:v0.5.1`, PostgreSQL 15.4) | Stores everything. pgvector provides the `vector` type, the distance operators, and HNSW indexes. `pg_trgm` backs a trigram index on content. |
| **Dashboard** | `dashboard/` | React SPA served by nginx on port 8601. Manages tenants, collections, and documents, and has a full search form (vector, full-text, hybrid, collapse, recency) and an API Explorer driven by the OpenAPI document. |
| **SDKs** | `sdk/csharp`, `sdk/js`, `sdk/python` | Thin typed clients over the REST API (version 0.2.2). |
| **Observability stack** | `docker/observability` | Prometheus, Tempo, Loki, Grafana (pre-provisioned dashboards), and Alloy. Optional. |

---

## 3. Multi-tenancy and authentication

Every resource lives under a **tenant**: users, credentials, and collections belong to one tenant, and a collection's
documents, labels, and tags belong to that collection. Nothing is shared across tenants.

A request authenticates with `Authorization: Bearer <token>`. Two kinds of token exist:

- **Admin API keys**, listed in `recalldb.json` (`AdminApiKeys`, default `recalldbadmin`). They can act on any tenant and
  reach admin-only routes such as tenant management and request history.
- **Credential bearer tokens**, created through the Credentials API for a user in a tenant. They can act only on their
  own tenant; every service method checks the route's tenant against the caller's (`ValidateTenantAccess`) and answers
  403 otherwise. Users flagged `IsTenantAdmin` can also manage users and credentials in their tenant.

`POST /v1.0/authenticate` accepts either a tenant id, email, and password, or a bearer token, and returns the
authenticated identity.
MCP tools take the same token as a `bearerToken` argument and apply the same checks.

---

## 4. The request path

A request to either transport follows the same steps:

1. **Transport.** Watson (REST) or Voltaic (MCP) receives the request. REST path segments (tenant id, collection id,
   document key, document id) are URL-decoded, so keys containing `/`, `?`, `#`, `%`, spaces, or non-ASCII characters
   round-trip when the client encodes them.
2. **Deserialization and model checks.** The body becomes a model (`DocumentRecord`, `SearchQuery`, ...). Enum values
   are strings (`"CosineSimilarity"`, `"Tag"`). Setters reject out-of-range values (for example `Hybrid.RrfK` 0,
   `FullText.TextWeight` 1.5, an empty `DocumentKey`) and the server answers 400.
3. **RequestContext.** The transport builds one `RequestContext` carrying the authenticated identity, the tenant and
   collection ids, the payload, and the origin (`rest` or `mcp`).
4. **Service.** The service checks tenant access, runs cross-field validation (for example "`Collapse.TagKey` is
   required when `Collapse.Field` is `Tag`"), reads the collection's metadata (dimensionality), and calls the driver.
5. **SQL.** The driver builds SQL against the collection's own tables (section 5) and runs it on a pooled Npgsql
   connection. Search statements that need a larger HNSW candidate list run `SET LOCAL hnsw.ef_search` in the same
   transaction first.
6. **Enrichment.** Documents come back from their table without labels and tags; the service attaches both with one
   batched query each (`AttachLabelsAndTagsAsync`), and a search with `IncludeNeighbors` fetches the surrounding chunks.
7. **Response.** The result is serialized to JSON. Every REST request except CORS preflights is also recorded in
   `request_history` (method, path, status, timing), which admins can browse through `/v1.0/requesthistory`, and both
   transports are measured by the OpenTelemetry host.

Errors from the service layer come back as JSON with the status code and a message: 400 for invalid input (including
a vector of the wrong length and a collection above 2000 dimensions), 403 for a tenant the caller cannot reach, 404 for
a missing resource, and 409 for a conflict (a duplicate collection name or `DocumentKey`). The driver turns the
PostgreSQL unique violations it expects into typed exceptions that the services map to 409. Anything the service does
not anticipate becomes a 500 whose `Message` carries the underlying error.

---

## 5. Storage layout

### 5.1 Global tables

Tenants, users, credentials, collection metadata, and request history live in fixed tables (`tenants`, `users`,
`credentials`, `collections`, `request_history`). The `collections` row holds the collection's id, name, tenant, and
**dimensionality**, which is fixed at creation.

### 5.2 Per-collection tables

Creating a collection creates three tables of its own, named from the collection id (lower-cased, `-` and `.` mapped
to `_`), in one transaction together with the `collections` row and every index. Either all of it exists afterwards or
none of it does.

**`collection_<id>`**: one row per document (chunk).

| Column | Type | Notes |
|---|---|---|
| `id` | `BIGSERIAL` primary key | Internal row id, returned as `Id`. Used as the final tie-breaker in every ordering. |
| `document_key` | `VARCHAR(256)`, unique | The caller-facing key. Auto-generated (K-sortable, `doc_` prefix) when omitted. |
| `document_id` | `VARCHAR(256)` | Optional. Groups the chunks of one source document. |
| `position` | `INTEGER`, default 0 | The chunk's order within its `document_id`. |
| `content_type` | `VARCHAR(32)`, default `Text` | One of nine `ContentTypeEnum` values. |
| `content` | `TEXT` | The chunk text. What full-text search and the `Terms` filter read. |
| `binary_data` | `BYTEA` | Optional raw bytes (base64 in JSON). Not searched. |
| `embeddings` | `vector(<dimensionality>)` | Optional. What vector search reads. |
| `content_length` | `BIGINT` | UTF-8 byte length of `content` (or `binary_data`), computed on create when not supplied. |
| `sha256`, `etag` | `VARCHAR(64)` | Stored as supplied by the caller. The server does not compute them. |
| `created_utc` | `TIMESTAMPTZ(6)` | Write time; the caller may supply it. Drives date filters, `Created*` sorting, and the hybrid recency signal. |
| `content_tsv` | `tsvector`, `GENERATED ALWAYS AS (to_tsvector('english', COALESCE(content, ''))) STORED` | Maintained by PostgreSQL on every insert and update. Full-text search reads it instead of re-tokenizing rows. |

**`collection_<id>_labels`**: one row per (document, label): `id`, `document_key`, `document_id`, `position`, `label`,
`created_utc`.

**`collection_<id>_tags`**: one row per (document, tag): `id`, `document_key`, `document_id`, `position`, `key`,
`value` (`TEXT`), `created_utc`.

Labels and tags are in side tables, not columns, so filters on them are indexed `IN (SELECT document_key ...)`
subqueries and the documents table (and its HNSW index) stays narrow.

### 5.3 Indexes

Index names are `idx_col_<id>_<suffix>`, derived from the full collection id (a hash when the id is too long for
PostgreSQL's 63-byte identifier limit).

| Table | Suffix | Definition | Used by |
|---|---|---|---|
| documents | `dkey` | `UNIQUE (document_key)` | Reads, updates, deletes by key; enforces key uniqueness |
| documents | `did` | `(document_id)` | `DocumentIds` filter, document stats |
| documents | `didp` | `(document_id, position)` | Read by position, neighbor retrieval |
| documents | `crt` | `(created_utc)` | Date filters, enumeration order |
| documents | `hnsw` | `hnsw (embeddings vector_cosine_ops) WITH (m = 16, ef_construction = 64)` | Vector search with the cosine metrics |
| documents | `tsv` | `gin (content_tsv)` | Full-text search in `english` |
| documents | `trgm` | `gin (content gin_trgm_ops)` | The `Terms` filter's case-insensitive substring match (`ILIKE '%term%'`) |
| labels | `l_dkey`, `l_did`, `l_lbl`, `l_crt` | document key, document id, label, created | Label filters and label reads |
| tags | `t_dkey`, `t_did`, `t_key`, `t_kv`, `t_crt` | document key, document id, key, (key, value), created | Tag filters, tag reads, collapse by tag |

The HNSW index uses the cosine operator class, so only `CosineSimilarity` and `CosineDistance` searches can use it.
Euclidean and inner-product searches are correct but compute the distance for every candidate row
([RETRIEVAL.md](RETRIEVAL.md#9-performance)).

---

## 6. Startup

On start the server reads `recalldb.json` from its working directory (environment variables override selected
settings; see the README's Configuration section), connects to PostgreSQL, and runs a **schema pass**:

1. Creates the `vector` and `pg_trgm` extensions and the global tables if missing. On first run (no tenants yet) it
   seeds a `default` tenant, an `admin@recall` user, a credential whose bearer token is `default`, and a `default`
   collection with 384 dimensions.
2. For every existing collection, re-runs the idempotent table and index DDL, so collections created by older builds
   gain indexes added since (HNSW, GIN).
3. Adds the stored `content_tsv` column and its GIN index to collections that lack it (`Database.MigrateFullTextColumn`,
   default `true`). This rewrites the table under an exclusive lock; the README's Configuration section covers how to
   defer it for large collections.
4. Renames indexes to the current naming scheme in place and builds any that are missing, repairing collections left
   incomplete by an old index-name collision.

Each collection is handled independently and failures are logged without blocking startup. Then the REST server, the
MCP server, and the observability host start.

---

## 7. Capabilities

Builds that share a version number can differ in which search features they support, so the server advertises them:
`GET /` and the MCP `server_info` tool return a `Capabilities` list.

| Capability | Meaning |
|---|---|
| `search.hybrid.rrf` | Hybrid search is the rank-fused union of the two legs (not the legacy text filter) |
| `search.hybrid.recency` | `Hybrid.RecencyWeight` is honored |
| `search.collapse` | `SearchQuery.Collapse` is honored |
| `search.include-embeddings` | `SearchQuery.IncludeEmbeddings` is honored |
| `search.fulltext.minimum-should-match` | `FullText.MinimumShouldMatch` is honored |
| `search.vector.ef-search` | `Vector.EfSearch` is honored, and vector-only searches size the HNSW candidate list to the page (no 40-hit limit) |
| `search.label-filter.required-mode` | `LabelFilter.RequiredMode` is honored; `Required` means all labels unless `Any` is asked for |

An older server ignores request fields it does not know, silently, so a client should check the list before relying on
a feature. The SDKs wrap this (`SupportsAsync`, `supports`). One difference matters in particular: a server without
`search.label-filter.required-mode` treats several `Required` labels as any-of.

---

## 8. Observability

The server emits OpenTelemetry metrics and traces from the base class library (`Meter`, `ActivitySource`); a single
in-process host exports them. Metrics are scraped from `:9464/metrics`; traces go over OTLP. Instrumented layers:
HTTP, MCP tools, a unified application-operation family (`origin=rest|mcp`), search, the PostgreSQL layer, and the .NET
runtime. Search metrics carry the mode, text match mode, hybrid strategy, collapse field, and recency on/off, and the
Grafana Search dashboard breaks rate and latency down by each. See the README's Observability section for the stack.

---

## 9. Source map

| To understand | Read |
|---|---|
| Routes and OpenAPI metadata | `src/RecallDb.Server/RecallDbServer.cs` |
| Document writes and stitching labels/tags | `src/RecallDb.Server/Services/DocumentService.cs`, `src/RecallDb.Core/Database/Postgresql/Implementations/DocumentMethods.cs` |
| Search validation, notices, neighbors | `src/RecallDb.Server/Services/SearchService.cs` |
| Every search SQL statement | `src/RecallDb.Core/Database/Postgresql/Implementations/SearchMethods.cs` |
| Table and index DDL | `src/RecallDb.Core/Database/Postgresql/Queries/DynamicTableQueries.cs` |
| Startup schema pass | `src/RecallDb.Core/Database/Postgresql/PostgresqlDatabaseDriver.cs` |
| Capability strings | `src/RecallDb.Core/SearchCapabilities.cs` |
