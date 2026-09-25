<p align="center">
  <img src="assets/logo-black.png" alt="RecallDB" style="max-width: 256px; max-height: 256px;" width="256" height="256">
</p>

<h3 align="center">The persistence and retrieval layer your RAG pipeline is missing.</h3>

<p align="center">
  <a href="#quick-start">Quick Start</a> &middot;
  <a href="REST_API.md">API Docs</a> &middot;
  <a href="MCP_API.md">MCP</a> &middot;
  <a href="#sdks">SDKs</a> &middot;
  <a href="#search">Search</a> &middot;
  <a href="#observability">Observability</a> &middot;
  <a href="CHANGELOG.md">Changelog</a>
</p>

---

## The Problem

You're building a RAG pipeline. You need to store embeddings, but you also need the raw content, chunk positions, content types, labels, tags, hashes, and ETags. You need multi-tenant isolation. You need filtering that goes beyond "nearest neighbor."

So you bolt pgvector onto Postgres, write migration scripts, hand-roll a chunking schema, build an API layer, add auth, wire up metadata tables, and six weeks later you have a fragile bespoke system that solves one project's needs.

**RecallDB is the architecture you'd build if you had the time to do it right.**

## Why RecallDB

Most vector databases store embeddings and call it a day. RecallDB stores the **complete context** your AI application needs to retrieve, rank, and act on information:

| What you store | Why it matters |
|---|---|
| **Vector embeddings** | Semantic similarity search via pgvector with HNSW indexing |
| **Raw content** | Full text, code, tables, lists, hyperlinks, binary data, images |
| **Content types** | 9 typed content categories so your retrieval pipeline knows *what* it's looking at |
| **Chunk positions** | Ordered document segments with `document_id` + `position` grouping |
| **Labels** | Categorical filters with AND/AND-NOT logic for scoped retrieval |
| **Key-value tags** | Structured metadata with 10 filter operators (equals, contains, range, null checks) |
| **SHA256 hashes + ETags** | Deduplication, cache invalidation, and change detection out of the box |
| **Content length** | Token budget awareness without recomputing |
| **Full-text search** | Relevance-ranked keyword search with ts_rank, backed by a stored, GIN-indexed `tsvector`. Find documents by lexical relevance, not just semantic similarity |

This isn't a thin wrapper around pgvector. It's an **opinionated persistence schema** that normalizes how AI-ready data is stored, indexed, and retrieved, so you stop reinventing the storage layer for every project.

## What You Get

- **Multi-tenant isolation** &mdash; tenants, users, credentials, and collections are fully scoped. One deployment serves many clients.
- **Per-collection vector tables** &mdash; each collection gets its own Postgres table with dedicated HNSW indexes (`m=16`, `ef_construction=64`). No noisy-neighbor problems.
- **5 distance metrics** &mdash; cosine similarity, cosine distance, Euclidean similarity, Euclidean distance, inner product. Pick what fits your embedding model.
- **Three search modes**: vector similarity (nearest-neighbor), full-text relevance (any-term, all-terms, phrase, or web-search syntax, ranked with ts_rank/ts_rank_cd), and hybrid (the vector and text results fused by weighted Reciprocal Rank Fusion, with a tunable text weight). Mix and match in a single request.
- **Compound search queries** &mdash; combine any search mode with label filters, tag conditions, content term matching, and date ranges in a single request.
- **Bring your own embeddings** &mdash; no vendor lock-in to any embedding provider. Use OpenAI, Cohere, Ollama, or anything that outputs a float array.
- **40+ REST endpoints** &mdash; full CRUD for tenants, users, credentials, collections, documents, labels, and tags. Includes batch delete by keys and filter-based delete for bulk operations.
- **SDKs in C#, Python, and JavaScript** &mdash; typed clients ready to drop into your stack.
- **React dashboard** &mdash; manage tenants, collections, and documents visually. Search with a query builder.
- **Built-in observability** &mdash; OpenTelemetry metrics and distributed tracing across every HTTP, MCP, application, search, and storage path, exported to a pre-provisioned Prometheus + Tempo + Loki + Grafana stack.
- **Docker Compose deployment** &mdash; Postgres + pgvector, API server, dashboard, and the full observability stack in one command.
- **MIT licensed** &mdash; use it however you want.

## Quick Start

```bash
cd docker
docker compose up
```

API at `http://localhost:8600`, dashboard at `http://localhost:8601`, and Grafana at `http://localhost:3000` (`admin` / `admin`). See [Observability](#observability) for the full stack.

### Default Credentials

| Credential | Value |
|---|---|
| Admin API Key | `recalldbadmin` |
| User | `admin@recall` / `password` |
| Bearer Token | `default` |

### Store a Document

```bash
curl -X PUT http://localhost:8600/v1.0/tenants/ten_default/collections/col_default/documents \
  -H "Authorization: Bearer recalldbadmin" \
  -H "Content-Type: application/json" \
  -d '{
    "DocumentId": "readme-guide",
    "Position": 0,
    "ContentType": "Text",
    "Content": "RecallDB stores embeddings alongside rich metadata.",
    "Embeddings": [0.1, 0.2, 0.3],
    "Labels": ["documentation", "guide"],
    "Tags": [
      { "Key": "source", "Value": "readme" },
      { "Key": "version", "Value": "1.0" }
    ]
  }'
```

### Search

```bash
curl -X POST http://localhost:8600/v1.0/tenants/ten_default/collections/col_default/search \
  -H "Authorization: Bearer recalldbadmin" \
  -H "Content-Type: application/json" \
  -d '{
    "Vector": {
      "SearchType": "CosineSimilarity",
      "Embeddings": [0.1, 0.2, 0.3],
      "MinimumScore": 0.7
    },
    "LabelFilter": {
      "Required": ["documentation"]
    },
    "Terms": {
      "Required": ["metadata"]
    },
    "MaxResults": 10
  }'
```

### Full-Text Search

```bash
curl -X POST http://localhost:8600/v1.0/tenants/default/collections/default/search \
  -H "Authorization: Bearer default" \
  -H "Content-Type: application/json" \
  -d '{
    "FullText": {
      "Query": "machine learning neural networks",
      "MatchMode": "Any",
      "SearchType": "TsRank",
      "MinimumScore": 0.01
    },
    "MaxResults": 10
  }'
```

### Hybrid Search

```bash
curl -X POST http://localhost:8600/v1.0/tenants/default/collections/default/search \
  -H "Authorization: Bearer default" \
  -H "Content-Type: application/json" \
  -d '{
    "Vector": {
      "SearchType": "CosineSimilarity",
      "Embeddings": [0.1, 0.2, 0.3]
    },
    "FullText": {
      "Query": "how do I run the test suite",
      "TextWeight": 0.5
    },
    "Hybrid": {
      "Strategy": "Rrf",
      "RrfK": 60
    },
    "MaxResults": 10
  }'
```

## Search

RecallDB search goes well beyond nearest-neighbor. A single query can combine any of these filters:

**Vector** &mdash; similarity or distance search across 5 metrics with score/distance thresholds.

**Labels** &mdash; require or exclude categorical labels with boolean logic.

**Tags** &mdash; filter on key-value metadata using `Equals`, `NotEquals`, `GreaterThan`, `LessThan`, `Contains`, `ContainsNot`, `StartsWith`, `EndsWith`, `IsNull`, `IsNotNull`.

**Terms** &mdash; case-insensitive substring matching on document content. Require terms, exclude terms, or both.

**Full-Text Search**: scored full-text search powered by Postgres `tsvector` and `tsquery`, with stemming, stop word removal, and any installed Postgres text search language. By default a document matches if it contains any meaningful word in the query, and documents that match more (and rarer) words rank higher, so a natural-language question like "how do I run the test suite" finds useful results instead of only the documents that happen to contain every word. Set `MatchMode` to `All` to require every term, `Phrase` for words in order, or `WebSearch` for `"quoted phrase"`, `or` and `-exclude` syntax. Ranking uses `ts_rank` or `ts_rank_cd`, and `MinimumScore` drops low-relevance results. Don't confuse it with the `Terms` filter: `Terms` does literal substring matching with no ranking, while `FullText` ranks results by lexical relevance.

**Hybrid Search**: send a vector and a text query together and RecallDB runs both, then fuses the two ranked lists with weighted Reciprocal Rank Fusion. The result is a union, so a document can come back because it is semantically close, because it matches the keywords, or both. It no longer comes back empty just because no document contains every keyword. Scores are normalized to [0, 1], and each hit reports its `VectorRank` and `TextRank`. `FullText.TextWeight` sets how much the text leg counts. Two other strategies are available through `Hybrid.Strategy`: `Linear` blends normalized scores, and `Filter` keeps the older behavior of ranking by vector only within documents that match the text query.

**Date ranges** &mdash; `CreatedBefore` and `CreatedAfter` for temporal scoping.

**Neighbor retrieval** &mdash; include surrounding chunks for contextual windows (`IncludeNeighbors: N` returns up to N chunks before and after each match).

**Pagination** &mdash; `MaxResults` (1-1000) with continuation tokens for large result sets.

**Sort** &mdash; by score, distance, or creation date in ascending or descending order.

## SDKs

### C\#

```csharp
var client = new RecallDbClient("http://localhost:8600", "recalldbadmin");

// Vector search
var results = await client.SearchAsync("ten_default", "col_default", new SearchQuery
{
    Vector = new VectorQuery
    {
        SearchType = "CosineSimilarity",
        Embeddings = new List<float> { 0.1f, 0.2f, 0.3f },
        MinimumScore = 0.7
    },
    MaxResults = 10
});

// Vector search with neighbor retrieval
var neighborResults = await client.SearchAsync("ten_default", "col_default", new SearchQuery
{
    Vector = new VectorQuery
    {
        SearchType = "CosineSimilarity",
        Embeddings = new List<float> { 0.1f, 0.2f, 0.3f }
    },
    IncludeNeighbors = 2,
    MaxResults = 10
});

// Full-text search
var ftResults = await client.SearchAsync("ten_default", "col_default", new SearchQuery
{
    FullText = new FullTextQuery
    {
        Query = "machine learning neural networks",
        MatchMode = "Any",
        SearchType = "TsRank",
        MinimumScore = 0.01
    },
    MaxResults = 10
});

// Hybrid search (vector and text fused with RRF)
var hybridResults = await client.SearchAsync("ten_default", "col_default", new SearchQuery
{
    Vector = new VectorQuery
    {
        SearchType = "CosineSimilarity",
        Embeddings = new List<float> { 0.1f, 0.2f, 0.3f }
    },
    FullText = new FullTextQuery
    {
        Query = "how do I run the test suite",
        TextWeight = 0.5
    },
    Hybrid = new HybridQuery
    {
        Strategy = "Rrf"
    },
    MaxResults = 10
});
```

The C# SDK models take enum values as strings (`SearchType`, `MatchMode`, `Strategy`), matching what goes over the wire. The server-side enums are `SearchTypeEnum`, `TextSearchTypeEnum`, `TextMatchModeEnum` and `HybridStrategyEnum`; see [REST_API.md](REST_API.md#enumerations).

### Python

```python
from recalldb_sdk import RecallDbClient

client = RecallDbClient("http://localhost:8600", "recalldbadmin")
results = client.search("ten_default", "col_default", {
    "Vector": {
        "SearchType": "CosineSimilarity",
        "Embeddings": [0.1, 0.2, 0.3],
        "MinimumScore": 0.7
    },
    "MaxResults": 10
})

# Vector search with neighbor retrieval
neighbor_results = client.search("ten_default", "col_default", {
    "Vector": {
        "SearchType": "CosineSimilarity",
        "Embeddings": [0.1, 0.2, 0.3]
    },
    "IncludeNeighbors": 2,
    "MaxResults": 10
})
```

### JavaScript

```javascript
import { RecallDbClient } from 'recalldb-sdk';

const client = new RecallDbClient('http://localhost:8600', 'recalldbadmin');
const results = await client.search('ten_default', 'col_default', {
  Vector: {
    SearchType: 'CosineSimilarity',
    Embeddings: [0.1, 0.2, 0.3],
    MinimumScore: 0.7,
  },
  MaxResults: 10,
});

// Vector search with neighbor retrieval
const neighborResults = await client.search('ten_default', 'col_default', {
  Vector: {
    SearchType: 'CosineSimilarity',
    Embeddings: [0.1, 0.2, 0.3],
  },
  IncludeNeighbors: 2,
  MaxResults: 10,
});
```

## Configuration

Server settings live in `recalldb.json`. Environment variables override selected settings:

| Variable | Description |
|---|---|
| `RECALLDB_DB_HOST` | PostgreSQL hostname |
| `RECALLDB_DB_PORT` | PostgreSQL port |
| `RECALLDB_DB_NAME` | Database name |
| `RECALLDB_DB_USER` | Database username |
| `RECALLDB_DB_PASS` | Database password |
| `RECALLDB_DB_MIGRATE_FTS_COLUMN` | Overrides `Database.MigrateFullTextColumn` (`true` or `false`) |
| `RECALLDB_MCP_ENABLED` | Enable/disable the MCP server |
| `RECALLDB_MCP_HOSTNAME` | MCP bind hostname |
| `RECALLDB_MCP_PORT` | MCP port |
| `RECALLDB_OBS_ENABLED` | Enable/disable observability |
| `RECALLDB_OBS_SERVICE_NAME` | OpenTelemetry `service.name` |
| `RECALLDB_OBS_PROM_HOSTNAME` | Prometheus scrape endpoint bind hostname |
| `RECALLDB_OBS_PROM_PORT` | Prometheus scrape endpoint port (default `9464`) |
| `RECALLDB_OTLP_ENDPOINT` | OTLP trace endpoint (e.g. `http://tempo:4317`) |
| `RECALLDB_OTLP_PROTOCOL` | OTLP protocol (`grpc` or `httpprotobuf`) |

`Database.MigrateFullTextColumn` (default `true`) controls a one-time schema upgrade at startup. Every collection table carries a stored `content_tsv` column with a GIN index so full-text ranking reads a precomputed `tsvector` instead of re-tokenizing each candidate row. New collections get it when they are created. Collections created by an earlier build get it the next time the server starts, and that is the part to plan for: adding a stored generated column rewrites the table under an `ACCESS EXCLUSIVE` lock, so a large collection is unavailable, and startup waits, until the rewrite finishes. If you have very large collections, set the flag to `false`, then restart with it set to `true` during a maintenance window to run the upgrade. Search still works without the column; the text match falls back to the older expression index and is slower, not wrong.

Expect the rewrite to take longer than the row count suggests. PostgreSQL rebuilds every index on the table as part of it, HNSW included, so a 50,000-document collection with 64-dimension embeddings took about 46 seconds on a laptop. Those schema statements run without a command timeout by default; `Database.SchemaCommandTimeoutSeconds` (default `0`, meaning no limit, maximum `86400`) caps them if you would rather have a slow migration fail and log a warning than hold startup.

The same startup pass also brings each collection's indexes up to the current naming scheme. Index names derive from the full collection id (earlier builds used only the id's millisecond component, so two collections created in the same millisecond could collide and leave one without some of its indexes). Existing indexes are renamed in place — an instant `ALTER INDEX ... RENAME`, not a rebuild — and any index or side table that a past collision left missing is created. A collection whose documents table holds duplicate `document_key` values keeps working without its unique index; the duplicates are logged at warning level and left for you to resolve, since removing them means choosing which rows to drop. To see which collections are affected before upgrading, run `migrations/002_inspect_damaged_collections.sql` against the database.

## Architecture

```
┌─────────────┐      ┌─────────────────┐      ┌──────────────────────────┐
│  Your App   │────> |  RecallDB API   │────> │  PostgreSQL + pgvector   │
│  (SDK)      │      │  (REST, Auth)   │      │                          │
└─────────────┘      └─────────────────┘      │  tenants                 │
                     ┌─────────────────┐      │  users / credentials     │
                     │   Dashboard     │────> │  collections             │
                     │   (React SPA)   │      │  collection_{id}  [HNSW] │
                     └─────────────────┘      │  collection_{id}_labels  │
                                              │  collection_{id}_tags    │
                                              └──────────────────────────┘
```

Each collection creates its own Postgres tables with a dedicated HNSW vector index. Labels and tags are stored in separate relational tables and joined at query time, keeping the vector index lean and the metadata queryable.

## Building from Source

```bash
dotnet restore src/RecallDb.sln
dotnet build src/RecallDb.sln
```

## MCP Server

RecallDB ships an in-process **Model Context Protocol (MCP)** server so agents can drive the database directly. It is hosted inside `RecallDB.Server` (no separate container) over Streamable HTTP at `http://localhost:8620/mcp` (POST for JSON-RPC, GET for the SSE stream).

- The full REST operation set is exposed as MCP tools (`tenant/*`, `user/*`, `credential/*`, `collection/*`, `document/*`, `label/*`, `tag/*`, `search/query`, `requestHistory/*`, `auth/authenticate`, `server/info`).
- Listing is always paginated (`*/enumerate`) — there are no "get all" tools.
- Authentication is per-caller bearer, identical to REST: pass your admin API key or credential bearer token as the `bearerToken` tool argument.

Enable/configure it in the `Mcp` section of `recalldb.json` or via `RECALLDB_MCP_*` environment variables. See **[MCP_API.md](MCP_API.md)** for the full tool catalog and examples.

**Connect your agent in one command** — the CLI auto-configures Claude Code, Cursor, Gemini CLI, Codex CLI, and Mux:

```bash
recalldb mcp install          # all detected harnesses (add --dry-run to preview)
recalldb mcp install --only cursor
```

Per-harness guides: [Claude Code](docs/CONNECTING_CLAUDE.md) · [Cursor](docs/CONNECTING_CURSOR.md) · [Gemini](docs/CONNECTING_GEMINI.md) · [Codex](docs/CONNECTING_CODEX.md) · [Mux](docs/CONNECTING_MUX.md).

## Observability

RecallDB is instrumented end-to-end with [OpenTelemetry](https://opentelemetry.io/) using the .NET base class library (`System.Diagnostics.Metrics.Meter` and `ActivitySource`). A single in-process host owns all wiring, so the instrumentation is a cheap no-op until you turn it on and adds no third-party dependency to the emitting code.

**Coverage** spans every request path:

- **HTTP (REST)** &mdash; request rate, duration, in-flight count, and status classes for every inbound request.
- **MCP** &mdash; per-tool invocation rate, duration, in-flight count, and outcome.
- **Application** &mdash; a unified operation family across both transports (labeled `origin=rest|mcp`, resource, and operation).
- **Search**: latency and result counts by mode (vector, full-text, hybrid), text match mode, and hybrid strategy.
- **Database** &mdash; query rate, duration, in-flight count, and rows returned for the PostgreSQL layer.
- **Runtime / process** &mdash; .NET GC, threads, exceptions, working-set memory, and uptime.

Distributed traces nest naturally (a REST operation or MCP tool span parents its database query spans) and are exported over OTLP. Metrics are exposed on an in-process Prometheus scrape endpoint.

`docker compose up` provisions the whole stack alongside the database, server, and dashboard &mdash; no extra steps:

| Service | URL | Default credentials | Role |
|---|---|---|---|
| **Grafana** | `http://localhost:3000` | `admin` / `admin` | Dashboards (HTTP, MCP, Application, Search, Database, Runtime sections) |
| **Prometheus** | `http://localhost:9090` | none | Metrics store; scrapes the server at `:9464/metrics` |
| **Tempo** | `http://localhost:3200` | none | Trace backend (OTLP receiver on `4317`/`4318`) |
| **Loki** | `http://localhost:3100` | none | Log aggregation |
| **Alloy** | `http://localhost:12345` | none | Ships container logs to Loki |

The dashboards are pre-provisioned and grouped into folders (sections) by area, and the product dashboard's landing page links out to each service. Configure observability in the `Observability` section of `recalldb.json` or via environment variables (see [Configuration](#configuration)); set `Enabled` to `false` to disable it entirely.

## API Reference

See [REST_API.md](REST_API.md) for the complete REST endpoint reference and [MCP_API.md](MCP_API.md) for the MCP tool catalog.

A [Postman collection](RecallDB.postman_collection.json) is included for interactive exploration.

## License

MIT &mdash; see [LICENSE.md](LICENSE.md).
