# RecallDB MCP API

RecallDB exposes its full operational surface over the **Model Context Protocol (MCP)**, in addition to the [REST API](REST_API.md). The MCP server is built with [Voltaic](https://www.nuget.org/packages/Voltaic/) and is **hosted in-process inside `RecallDB.Server`** — it is not a separate service. Both transports normalize each request into a single shared `RequestContext` and call the same service layer, so MCP and REST behave identically.

## Architecture

```
REST client ─▶ Watson HTTP (:8600) ─▶ REST handlers ─┐
                                                      ├─▶ RequestContext ─▶ Service layer ─▶ PostgreSQL + pgvector
MCP client  ─▶ Voltaic HTTP (:8620/mcp) ─▶ MCP tools ─┘
```

## Transport

The MCP server speaks **Streamable HTTP** on a single endpoint:

| Method | Path | Purpose |
|--------|------|---------|
| `POST` | `/mcp` | JSON-RPC 2.0 requests and notifications |
| `GET`  | `/mcp` | Opens the Server-Sent Events (SSE) stream for server notifications |

Sessions are tracked with the `MCP-Session-Id` header, which the server issues during initialization and the client echoes on subsequent requests. After initialization, clients send `MCP-Protocol-Version` on subsequent requests. (Voltaic also serves compatibility endpoints at `/rpc` and `/events`.)

Default endpoint: `http://127.0.0.1:8620/mcp`.

Voltaic's security defaults apply: a request carrying a browser `Origin` header other than a loopback origin is rejected with 403, and when `Hostname` is a loopback name (`localhost`, `127.0.0.1`, `::1`) only loopback clients are served. Bind to `*` (as the Docker image does) to accept remote clients. A `POST /mcp` without `MCP-Session-Id` is rejected with 400 unless it is `initialize`, so clients must initialize before any other request, including `ping`.

## Configuration

MCP settings live in the `Mcp` section of `recalldb.json`:

```json
"Mcp": {
  "Enabled": true,
  "Hostname": "127.0.0.1",
  "Port": 8620,
  "ServerName": "RecallDB.McpServer",
  "ServerVersion": "0.2.0",
  "LogOperations": false
}
```

Environment overrides: `RECALLDB_MCP_ENABLED`, `RECALLDB_MCP_HOSTNAME`, `RECALLDB_MCP_PORT`.

On startup, after the settings file is read it is re-written so that any newly introduced properties (such as this `Mcp` block) are materialized to disk.

## Connecting an agent harness

RecallDB ships a CLI installer that auto-configures the MCP server into every detected harness — **Claude Code, Cursor, Gemini CLI, OpenAI Codex CLI, and Mux**:

```bash
recalldb mcp install                 # configure all detected harnesses
recalldb mcp install --only claude   # just one (claude|cursor|gemini|codex|mux)
recalldb mcp install --dry-run       # preview without writing
recalldb mcp uninstall               # remove RecallDB entries everywhere
recalldb mcp print                   # print config snippets for manual setup
```

It merges a `recalldb` entry into each harness's config (keyed by name, preserving other servers) and writes an instruction file (a Claude subagent, or an `AGENTS.md`/`GEMINI.md` managed block) telling the model to pass the `bearerToken` on every call. Per-harness manual instructions live in `docs/`:

- [CONNECTING_CLAUDE.md](docs/CONNECTING_CLAUDE.md)
- [CONNECTING_CURSOR.md](docs/CONNECTING_CURSOR.md)
- [CONNECTING_GEMINI.md](docs/CONNECTING_GEMINI.md)
- [CONNECTING_CODEX.md](docs/CONNECTING_CODEX.md)
- [CONNECTING_MUX.md](docs/CONNECTING_MUX.md)

## Lifecycle

Every MCP connection begins with the standard handshake:

- `initialize` — the client sends its protocol version, capabilities, and client info; the server responds with the negotiated protocol version, its capabilities, and server info (`RecallDB.McpServer` / version).
- `notifications/initialized` — the client notification after successful initialization.
- `tools/list` — enumerates all available tools and their input schemas.
- `tools/call` — invokes a tool. This is the only way to call a tool: sending a tool name as the JSON-RPC `method` returns `-32601` (method not found).
- `ping` — liveness check within a session. Returns an empty object, `{}`. Like every other request, it is rejected with 401 when an invalid `Authorization` header is sent.

`tools/list` returns only RecallDB's tools; the server publishes no diagnostic tools such as `echo` or `getTime`.

### Tool names

Tools are named `family_operation`, for example `tenant_create`, `document_batchCreate`, and `search_query`. MCP tool names are limited to letters, digits, `_`, `-`, and `.`, and many model APIs also reject `.`, so the underscore is the only portable separator. Before the Voltaic 2.2 upgrade the tools were named `family/operation` (`search/query`); clients that hard-code tool names must use the new names. Clients that discover tools through `tools/list` need no change. Metrics and traces still label each call with its `family/operation` request type, which REST shares.

## Authentication

Authentication is **per-caller bearer**, reusing RecallDB's existing authentication (an admin API key **or** a credential bearer token), giving the same multi-tenant scoping as REST.

Because MCP tool handlers do not receive transport/session context, **every authenticated tool takes a `bearerToken` argument** carrying the caller's token. The service layer authorizes it exactly as the REST `Authorization: Bearer` header is authorized:

- Admin API keys (from `AdminApiKeys`) act as a global administrator.
- A credential bearer token resolves to its tenant/user and is scoped to that tenant.

Additionally, when an `Authorization: Bearer <token>` header is present on the HTTP request, the server validates it at the transport layer and rejects an invalid token early with HTTP 401. This applies to every request, including `initialize` and `ping`, so a client with an invalid header cannot open a session.

Two tools do **not** require authentication: `server_info` and `auth_authenticate`.

## Request and response envelope

Tools follow JSON-RPC 2.0. A `tools/call` request:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "collection_read",
    "arguments": {
      "bearerToken": "default",
      "tenantId": "default",
      "collectionId": "default"
    }
  }
}
```

Argument conventions:

- Identifiers are camelCase strings: `tenantId`, `collectionId`, `documentKey`, `documentId`, `position`, `userId`, `credentialId`, `labelId`, `tagId`, `guid`.
- Complex bodies are passed as a **single JSON string argument** and deserialized server-side: `tenant`, `user`, `credential`, `collection`, `document`, `documents`, `label`, `tag`, `search`, `batchDelete`, and the pagination `query` / `filter`.
- On success the tool returns the operation's payload (the same object shape REST returns), serialized as JSON in a single text content block: `{"content":[{"type":"text","text":"{\"Id\":\"default\",...}"}]}`. Delete-style operations return `{ "Success": true }`; `*/exists` tools return `true` or `false`.
- On failure the tool raises a JSON-RPC error (code `-32603`) whose message begins with the HTTP-equivalent status code (for example `403 Forbidden: Access denied.`, `404 Not found: ...`, `400 Bad request: ...`) and whose `data` is `{ "statusCode": 403 }`.
- Arguments are checked against the tool's input schema before the tool runs. A missing required argument or a value of the wrong type returns a tool result with `isError: true` whose text names the argument.

## Enumeration and pagination

There are **no "get all" tools**. Every listing is a `*/enumerate` tool that accepts an optional `query` argument — an `EnumerationQuery` serialized as a JSON string:

```json
{
  "MaxResults": 100,
  "ContinuationToken": null,
  "Ordering": "CreatedDescending"
}
```

`MaxResults` defaults to 100 and is clamped to 1–1000. The response is an `EnumerationResult<T>`:

```json
{
  "Success": true,
  "MaxResults": 100,
  "ContinuationToken": "100",
  "EndOfResults": false,
  "TotalRecords": 250,
  "RecordsRemaining": 150,
  "Objects": [ ... ],
  "TotalMs": 3.21
}
```

To page, pass the returned `ContinuationToken` back in the next `query`. `EndOfResults` is `true` and `ContinuationToken` is `null` on the final page.

## Tool catalog

All authenticated tools take `bearerToken`. Listed below are the additional arguments (required unless noted).

### server

| Tool | Arguments | Purpose |
|------|-----------|---------|
| `server_info` | _(none, no auth)_ | Server name, version, uptime, `Capabilities`, and the MCP endpoint. |

`Capabilities` is the same list of search feature strings that `GET /` returns (`search.hybrid.rrf`, `search.hybrid.recency`, `search.collapse`, `search.include-embeddings`, `search.fulltext.minimum-should-match`, `search.vector.ef-search`, `search.label-filter.required-mode`; see [REST_API.md, Health](REST_API.md#health)). Servers that report the same version can differ, so check this list before relying on a feature; older servers ignore unknown `search` fields silently.

### auth

| Tool | Arguments | Purpose |
|------|-----------|---------|
| `auth_authenticate` | _(no auth)_ `bearerToken?` or (`tenantId`,`email`,`password`) | Validate a credential; returns tenant, redacted user, credential. |

### tenant

| Tool | Arguments | Purpose |
|------|-----------|---------|
| `tenant_read` | `tenantId` | Read a tenant. |
| `tenant_exists` | `tenantId` | Boolean existence check. |
| `tenant_enumerate` | `query?` | Paginated list (admin). |
| `tenant_create` | `tenant` | Create a tenant (admin). |
| `tenant_update` | `tenantId`, `tenant` | Update a tenant. |
| `tenant_delete` | `tenantId` | Delete a tenant and cascade (admin). |

### user

| Tool | Arguments | Purpose |
|------|-----------|---------|
| `user_read` | `tenantId`, `userId` | Read a user (password redacted). |
| `user_exists` | `tenantId`, `userId` | Boolean existence check. |
| `user_enumerate` | `tenantId`, `query?` | Paginated list. |
| `user_create` | `tenantId`, `user` | Create a user (admin/tenant admin). |
| `user_update` | `tenantId`, `userId`, `user` | Update a user (admin/tenant admin). |
| `user_delete` | `tenantId`, `userId` | Delete a user and its credentials. |

### credential

| Tool | Arguments | Purpose |
|------|-----------|---------|
| `credential_read` | `tenantId`, `credentialId` | Read a credential. |
| `credential_exists` | `tenantId`, `credentialId` | Boolean existence check. |
| `credential_enumerate` | `tenantId`, `query?` | Paginated list. |
| `credential_create` | `tenantId`, `credential` | Create a credential (admin/tenant admin). |
| `credential_update` | `tenantId`, `credentialId`, `credential` | Update a credential. |
| `credential_delete` | `tenantId`, `credentialId` | Delete a credential. |

### collection

| Tool | Arguments | Purpose |
|------|-----------|---------|
| `collection_read` | `tenantId`, `collectionId` | Read a collection. |
| `collection_exists` | `tenantId`, `collectionId` | Boolean existence check. |
| `collection_enumerate` | `tenantId`, `query?` | Paginated list. |
| `collection_create` | `tenantId`, `collection` | Create a collection and backing tables. |
| `collection_update` | `tenantId`, `collectionId`, `collection` | Update a collection. |
| `collection_delete` | `tenantId`, `collectionId` | Delete a collection and drop its tables. |
| `collection_stats` | `tenantId`, `collectionId` | Document/label/tag counts. |

### document

Tool semantics match the REST endpoints; [docs/INGESTION.md](docs/INGESTION.md) describes the data model and what each write guarantees.

| Tool | Arguments | Purpose |
|------|-----------|---------|
| `document_read` | `tenantId`, `collectionId`, `documentKey` | Read a document. |
| `document_readByPosition` | `tenantId`, `collectionId`, `documentId`, `position` | Read a chunk by document ID + position. |
| `document_exists` | `tenantId`, `collectionId`, `documentKey` | Boolean existence check. |
| `document_enumerate` | `tenantId`, `collectionId`, `query?` | Paginated list. |
| `document_create` | `tenantId`, `collectionId`, `document` | Create a document (validates embedding dimensionality). An existing key fails with 409. |
| `document_update` | `tenantId`, `collectionId`, `documentKey`, `document` | Replace a document's stored fields (send them all). Omitted `Labels`/`Tags` are kept; sent ones replace them. A missing key fails with 404. |
| `document_delete` | `tenantId`, `collectionId`, `documentKey` | Delete a document and its labels/tags. |
| `document_batchCreate` | `tenantId`, `collectionId`, `documents` | Atomic batch create. A key repeated in the batch fails with 400, a key that already exists with 409, and nothing is stored. |
| `document_batchDelete` | `tenantId`, `collectionId`, `batchDelete` | Batch delete by keys. |
| `document_deleteByFilter` | `tenantId`, `collectionId`, `query?` | Delete all documents matching a filter. |
| `document_stats` | `tenantId`, `collectionId`, `documentKey` | Per-document statistics. |

### label

| Tool | Arguments | Purpose |
|------|-----------|---------|
| `label_read` | `tenantId`, `collectionId`, `labelId` | Read a label. |
| `label_enumerate` | `tenantId`, `collectionId`, `query?` | Paginated list. |
| `label_create` | `tenantId`, `collectionId`, `label` | Create a label. |
| `label_delete` | `tenantId`, `collectionId`, `labelId` | Delete a label. |

### tag

| Tool | Arguments | Purpose |
|------|-----------|---------|
| `tag_read` | `tenantId`, `collectionId`, `tagId` | Read a tag. |
| `tag_enumerate` | `tenantId`, `collectionId`, `query?` | Paginated list. |
| `tag_create` | `tenantId`, `collectionId`, `tag` | Create a tag. |
| `tag_delete` | `tenantId`, `collectionId`, `tagId` | Delete a tag. |

### search

| Tool | Arguments | Purpose |
|------|-----------|---------|
| `search_query` | `tenantId`, `collectionId`, `search` | Vector / full-text / hybrid search with filters and optional neighbor enrichment. |

The `search` argument is a JSON-encoded `SearchQuery`, the same body the REST search endpoint takes (see [REST_API.md, Search Modes](REST_API.md#search-modes) for the field semantics and [docs/RETRIEVAL.md](docs/RETRIEVAL.md) for how each mode ranks, filters, groups, and pages, including its known limits). The mode follows from what you send: a non-empty `Vector.Embeddings` gives a vector leg, a non-blank `FullText.Query` gives a text leg, and both together give a hybrid search.

Fields that shape full-text and hybrid results:

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| `Vector.EfSearch` | int or null | null | HNSW candidate list size, clamped to 1-1000. Null means four times the page (at least 100) for vector-only searches and the candidate pool otherwise. Raise it for recall, deep pages, or selective filters |
| `LabelFilter.RequiredMode` | string | `All` | `All` (every `Required` label) or `Any` (at least one). Also applies to `document_enumerate` and `document_deleteByFilter` |
| `FullText.MatchMode` | string | `Any` | `Any` (any meaningful term), `All` (every term), `Phrase` (adjacent, in order), `WebSearch` (`"quoted phrase"`, `or`, `-exclude`) |
| `FullText.TextWeight` | double | `0.5` | Text leg's share in hybrid, 0.0-1.0; the vector leg gets the rest |
| `FullText.Language` | string | `english` | Must be a PostgreSQL text search configuration; only `english` uses the index |
| `FullText.Normalization` | int | `32` | ts_rank normalization bitmask, 0-63 |
| `Hybrid.Strategy` | string | `Rrf` | `Rrf` (rank fusion over the union of both legs), `Linear` (normalized score blend over the union), `Filter` (legacy: text query required, raw score blend) |
| `Hybrid.RrfK` | int | `60` | RRF constant, 1-100000 |
| `Hybrid.CandidatePool` | int or null | null | Candidates per leg before fusion, 1-10000. Null means `max(MaxResults * 4, 100)` capped at 1000 |
| `Hybrid.RecencyWeight` | double | `0` | Weight of the recency signal, 0.0-1.0 (0 is off). `Rrf` only; `Linear` and `Filter` ignore it with a `Notice`. Rank-based, so a small weight breaks near-ties in favor of newer content rather than reordering strong matches |
| `FullText.MinimumShouldMatch` | int | `1` | Minimum number of distinct query terms a document must contain, 1-3. Only with `MatchMode` `Any`; only the first 16 distinct terms count |
| `Collapse.Field` | string | `DocumentId` | Return one hit per group: `DocumentId` (the `DocumentId` column) or `Tag` (the value of the tag named `Collapse.TagKey`). Ungrouped documents are their own group, keyed by `DocumentKey` |
| `Collapse.TagKey` | string | null | Required when `Collapse.Field` is `Tag`; at most 256 characters |
| `Collapse.CandidatePool` | int or null | null | Candidates retrieved before collapsing, 1-10000, for vector-only and full-text-only searches. Hybrid uses `Hybrid.CandidatePool` when set, else this |

The result is a `SearchResult`. In a hybrid `Rrf` or `Linear` search each document carries a fused `Score` in [0, 1], the raw `VectorScore` and `TextScore`, and its 1-based `VectorRank` and `TextRank` (omitted when the document is absent from that leg). `TotalRecords` is the size of the fused candidate set, at most `2 * CandidatePool`. With `Hybrid.RecencyWeight` above 0, each `Rrf` hit also carries `RecencyRank` (1 = newest). With `Collapse`, each hit is the best-scoring candidate of its group and carries `GroupKey` and `GroupHits` (candidates in the group), and `MaxResults`, `TotalRecords`, `RecordsRemaining` and continuation tokens count groups. Collapse works with vector-only, full-text-only, and hybrid `Rrf` and `Linear`; hybrid `Filter` is a 400. When the server wants to explain something about the run (a query of only stop words, a language with no index, `Hybrid` sent without both legs, `RecencyWeight` on a non-`Rrf` strategy, or a collapse pool too small for the page) the result includes a `Notice` string. Out-of-range values produce a 400.

Hybrid example (arguments for `tools/call`):

```json
{
  "bearerToken": "default",
  "tenantId": "default",
  "collectionId": "docs",
  "search": "{\"Vector\":{\"SearchType\":\"CosineSimilarity\",\"Embeddings\":[0.1,0.2,0.3]},\"FullText\":{\"Query\":\"how do I run the test suite\",\"MatchMode\":\"Any\",\"TextWeight\":0.5},\"Hybrid\":{\"Strategy\":\"Rrf\",\"RrfK\":60},\"MaxResults\":10}"
}
```

Collapse and recency example (one hit per `parentKey` tag value, newer groups favored on near-ties):

```json
{
  "bearerToken": "default",
  "tenantId": "default",
  "collectionId": "docs",
  "search": "{\"Vector\":{\"SearchType\":\"CosineSimilarity\",\"Embeddings\":[0.1,0.2,0.3]},\"FullText\":{\"Query\":\"how do I rotate the signing key\",\"MatchMode\":\"Any\",\"TextWeight\":0.5},\"Hybrid\":{\"Strategy\":\"Rrf\",\"RrfK\":60,\"CandidatePool\":40,\"RecencyWeight\":0.1},\"Collapse\":{\"Field\":\"Tag\",\"TagKey\":\"parentKey\"},\"MaxResults\":10}"
}
```

To get the pre-fix behavior (only documents containing every term, ranked by vector within them), send `"FullText":{"Query":"...","MatchMode":"All"}` with `"Hybrid":{"Strategy":"Filter"}`.

### requestHistory (admin)

| Tool | Arguments | Purpose |
|------|-----------|---------|
| `requestHistory_enumerate` | `filter?` | Offset-paginated request history. |
| `requestHistory_read` | `guid` | Read one entry. |
| `requestHistory_summary` | `filter?` | Time-bucketed summary. |
| `requestHistory_delete` | `guid` | Delete one entry. |

## Example: end-to-end (C#, Voltaic client)

```csharp
using System.Text.Json;
using Voltaic.Core;
using Voltaic.Mcp;

using McpHttpClient client = new McpHttpClient();
client.SetRequestHeader("Authorization", "Bearer default");
await client.ConnectStreamableAsync("http://127.0.0.1:8620");

await client.CallAsync("initialize", new
{
    protocolVersion = "2025-11-25",
    capabilities = new { },
    clientInfo = new { name = "example", version = "1.0.0" }
});

// Create a collection. Tools are invoked through tools/call; the payload is JSON in content[0].text.
string collectionJson = "{\"Id\":\"docs\",\"Name\":\"Docs\",\"Dimensionality\":3}";
JsonElement created = await client.CallAsync<JsonElement>("tools/call", new
{
    name = "collection_create",
    arguments = new
    {
        bearerToken = "default",
        tenantId = "default",
        collection = collectionJson
    }
});
string collectionText = created.GetProperty("content")[0].GetProperty("text").GetString();

// Enumerate documents (paginated, not "get all")
JsonElement page = await client.CallAsync<JsonElement>("tools/call", new
{
    name = "document_enumerate",
    arguments = new
    {
        bearerToken = "default",
        tenantId = "default",
        collectionId = "docs",
        query = "{\"MaxResults\":50}"
    }
});
```

`CallAsync<T>` throws on a JSON-RPC error. Use the untyped `CallAsync(...)`, which returns the `JsonRpcResponse`, to read `Error.Code`, `Error.Message`, and `Error.Data` instead.

## Errors

| Status | Meaning |
|--------|---------|
| 400 | Bad request (missing/invalid arguments or body). |
| 401 | Authentication failed (invalid bearer token at the transport layer). |
| 403 | Authorization denied (wrong tenant, or admin/tenant-admin required). |
| 404 | Resource not found. |

Tool failures surface as JSON-RPC errors (code `-32603`) whose message is prefixed with the status code and whose `data.statusCode` carries it as a number, so clients can branch on the failure class. The 401 above is an HTTP status on the `POST /mcp` response itself, returned before the request reaches a tool.

Protocol-level errors use the standard JSON-RPC codes: `-32601` for an unknown method (including a tool name sent as the method), and `-32602` for an unknown tool or a `tools/call` without a `name`.

Arguments that fail the tool's input schema (a missing required argument, a wrong type) are not JSON-RPC errors: the server returns a tool result with `isError: true` whose text names the offending argument, so the model can correct the call and retry.
