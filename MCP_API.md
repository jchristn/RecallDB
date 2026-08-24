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
- `tools/call` — invokes a tool.
- `ping` — liveness check (bypasses authentication).

## Authentication

Authentication is **per-caller bearer**, reusing RecallDB's existing authentication (an admin API key **or** a credential bearer token), giving the same multi-tenant scoping as REST.

Because MCP tool handlers do not receive transport/session context, **every authenticated tool takes a `bearerToken` argument** carrying the caller's token. The service layer authorizes it exactly as the REST `Authorization: Bearer` header is authorized:

- Admin API keys (from `AdminApiKeys`) act as a global administrator.
- A credential bearer token resolves to its tenant/user and is scoped to that tenant.

Additionally, when an `Authorization: Bearer <token>` header is present on the HTTP request, the server validates it at the transport layer and rejects an invalid token early with HTTP 401.

Two tools do **not** require authentication: `server/info` and `auth/authenticate`.

## Request and response envelope

Tools follow JSON-RPC 2.0. A `tools/call` request:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "collection/read",
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
- On success the tool returns the operation's payload (the same object shape REST returns). Delete-style operations return `{ "Success": true }`.
- On failure the tool raises a JSON-RPC error whose message begins with the HTTP-equivalent status code (for example `403 Forbidden: Access denied.`, `404 Not found: ...`, `400 Bad request: ...`).

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
| `server/info` | _(none, no auth)_ | Server name, version, uptime, and the MCP endpoint. |

### auth

| Tool | Arguments | Purpose |
|------|-----------|---------|
| `auth/authenticate` | _(no auth)_ `bearerToken?` or (`tenantId`,`email`,`password`) | Validate a credential; returns tenant, redacted user, credential. |

### tenant

| Tool | Arguments | Purpose |
|------|-----------|---------|
| `tenant/read` | `tenantId` | Read a tenant. |
| `tenant/exists` | `tenantId` | Boolean existence check. |
| `tenant/enumerate` | `query?` | Paginated list (admin). |
| `tenant/create` | `tenant` | Create a tenant (admin). |
| `tenant/update` | `tenantId`, `tenant` | Update a tenant. |
| `tenant/delete` | `tenantId` | Delete a tenant and cascade (admin). |

### user

| Tool | Arguments | Purpose |
|------|-----------|---------|
| `user/read` | `tenantId`, `userId` | Read a user (password redacted). |
| `user/exists` | `tenantId`, `userId` | Boolean existence check. |
| `user/enumerate` | `tenantId`, `query?` | Paginated list. |
| `user/create` | `tenantId`, `user` | Create a user (admin/tenant admin). |
| `user/update` | `tenantId`, `userId`, `user` | Update a user (admin/tenant admin). |
| `user/delete` | `tenantId`, `userId` | Delete a user and its credentials. |

### credential

| Tool | Arguments | Purpose |
|------|-----------|---------|
| `credential/read` | `tenantId`, `credentialId` | Read a credential. |
| `credential/exists` | `tenantId`, `credentialId` | Boolean existence check. |
| `credential/enumerate` | `tenantId`, `query?` | Paginated list. |
| `credential/create` | `tenantId`, `credential` | Create a credential (admin/tenant admin). |
| `credential/update` | `tenantId`, `credentialId`, `credential` | Update a credential. |
| `credential/delete` | `tenantId`, `credentialId` | Delete a credential. |

### collection

| Tool | Arguments | Purpose |
|------|-----------|---------|
| `collection/read` | `tenantId`, `collectionId` | Read a collection. |
| `collection/exists` | `tenantId`, `collectionId` | Boolean existence check. |
| `collection/enumerate` | `tenantId`, `query?` | Paginated list. |
| `collection/create` | `tenantId`, `collection` | Create a collection and backing tables. |
| `collection/update` | `tenantId`, `collectionId`, `collection` | Update a collection. |
| `collection/delete` | `tenantId`, `collectionId` | Delete a collection and drop its tables. |
| `collection/stats` | `tenantId`, `collectionId` | Document/label/tag counts. |

### document

| Tool | Arguments | Purpose |
|------|-----------|---------|
| `document/read` | `tenantId`, `collectionId`, `documentKey` | Read a document. |
| `document/readByPosition` | `tenantId`, `collectionId`, `documentId`, `position` | Read a chunk by document ID + position. |
| `document/exists` | `tenantId`, `collectionId`, `documentKey` | Boolean existence check. |
| `document/enumerate` | `tenantId`, `collectionId`, `query?` | Paginated list. |
| `document/create` | `tenantId`, `collectionId`, `document` | Create a document (validates embedding dimensionality). |
| `document/update` | `tenantId`, `collectionId`, `documentKey`, `document` | Update a document. |
| `document/delete` | `tenantId`, `collectionId`, `documentKey` | Delete a document and its labels/tags. |
| `document/batchCreate` | `tenantId`, `collectionId`, `documents` | Transactional batch create. |
| `document/batchDelete` | `tenantId`, `collectionId`, `batchDelete` | Batch delete by keys. |
| `document/deleteByFilter` | `tenantId`, `collectionId`, `query?` | Delete all documents matching a filter. |
| `document/stats` | `tenantId`, `collectionId`, `documentKey` | Per-document statistics. |

### label

| Tool | Arguments | Purpose |
|------|-----------|---------|
| `label/read` | `tenantId`, `collectionId`, `labelId` | Read a label. |
| `label/enumerate` | `tenantId`, `collectionId`, `query?` | Paginated list. |
| `label/create` | `tenantId`, `collectionId`, `label` | Create a label. |
| `label/delete` | `tenantId`, `collectionId`, `labelId` | Delete a label. |

### tag

| Tool | Arguments | Purpose |
|------|-----------|---------|
| `tag/read` | `tenantId`, `collectionId`, `tagId` | Read a tag. |
| `tag/enumerate` | `tenantId`, `collectionId`, `query?` | Paginated list. |
| `tag/create` | `tenantId`, `collectionId`, `tag` | Create a tag. |
| `tag/delete` | `tenantId`, `collectionId`, `tagId` | Delete a tag. |

### search

| Tool | Arguments | Purpose |
|------|-----------|---------|
| `search/query` | `tenantId`, `collectionId`, `search` | Vector / full-text / hybrid search with filters and optional neighbor enrichment. |

### requestHistory (admin)

| Tool | Arguments | Purpose |
|------|-----------|---------|
| `requestHistory/enumerate` | `filter?` | Offset-paginated request history. |
| `requestHistory/read` | `guid` | Read one entry. |
| `requestHistory/summary` | `filter?` | Time-bucketed summary. |
| `requestHistory/delete` | `guid` | Delete one entry. |

## Example: end-to-end (C#, Voltaic client)

```csharp
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

// Create a collection
string collectionJson = "{\"Id\":\"docs\",\"Name\":\"Docs\",\"Dimensionality\":3}";
string created = await client.CallAsync<string>("collection/create", new
{
    bearerToken = "default",
    tenantId = "default",
    collection = collectionJson
});

// Enumerate documents (paginated, not "get all")
string page = await client.CallAsync<string>("document/enumerate", new
{
    bearerToken = "default",
    tenantId = "default",
    collectionId = "docs",
    query = "{\"MaxResults\":50}"
});
```

## Errors

| Status | Meaning |
|--------|---------|
| 400 | Bad request (missing/invalid arguments or body). |
| 401 | Authentication failed (invalid bearer token at the transport layer). |
| 403 | Authorization denied (wrong tenant, or admin/tenant-admin required). |
| 404 | Resource not found. |

Tool failures surface as JSON-RPC errors whose message is prefixed with the status code, so clients can branch on the failure class.
