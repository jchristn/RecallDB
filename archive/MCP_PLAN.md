# RecallDB MCP Server — Implementation Plan

> **Status legend:** `[ ]` not started · `[~]` in progress · `[x]` done · `[!]` blocked
> Annotate each task inline (owner / date / notes) as you go. Keep this file the source of truth for progress.

> **✅ IMPLEMENTED (2026-08-19).** All phases complete. Full solution builds clean (0 warnings / 0 errors).
> End-to-end integration run against a live server + pgvector: **195/195 tests pass, 0 failures**, including
> 19 MCP tests (positive + negative) and the entire pre-existing REST suite (regression gate — the REST refactor
> onto the shared `RequestContext`/service layer is behavior-identical). REST on 8610, MCP Streamable HTTP on 8620.
> Version set to `1.1.0` everywhere.
>
> **Follow-up shipped:** default MCP port moved to `8620`; a `recalldb mcp install|uninstall|print` CLI
> (`RecallDb.Server/Mcp/McpInstaller.cs`) auto-configures Claude Code, Cursor, Gemini, Codex, and Mux (detected);
> per-harness guides in `docs/CONNECTING_*.md`. Auth note: Voltaic does not flow the Authorization-header identity
> into tool handlers (verified), so the `bearerToken` tool argument remains required — the installer writes agent
> instruction files that carry the token.

---

## 1. Goal & Scope

Add a **Model Context Protocol (MCP)** server to RecallDB, built with **Voltaic `0.6.1`**, **hosted inside the existing `RecallDb.Server` process** (not a separate container or `compose.yaml` service). The MCP surface exposes the full set of REST operations (minus a few non-sensical ones), is fully documented, and follows the enumeration/pagination discipline (no "GET ALL" tools).

This is modeled on **LiteGraph** (`C:\Code\litegraph\litegraph`, `src/LiteGraph.McpServer`) — the reference MCP implementation — but adapted in two deliberate ways:

1. **In-process, not a separate process.** LiteGraph runs its MCP server as its own executable that forwards to REST over HTTP. RecallDB hosts the MCP transports *inside* `RecallDb.Server`.
2. **Shared `RequestContext` instead of a REST proxy.** Rather than MCP calling back into REST, **both REST and MCP normalize their inbound request into a single `RequestContext`** that is passed into a transport-agnostic **service layer**. `RequestContext` becomes the single source of truth; REST and MCP are just two feeds into it. This eliminates logic duplication and guarantees REST/MCP parity.

### Non-negotiable requirements (from the prompt + `C:\Code\agents\requirements`)
- MCP hosted **inside `RecallDb.Server`**; MCP ports published on the existing `recalldb-server` container, **no new compose service**.
- **HTTP with SSE/streaming only.** Selected transport set: **Streamable HTTP (`/mcp`, POST + GET-SSE) only** — TCP and WebSocket are out of scope.
- **Per-caller bearer authentication**, reusing RecallDB's existing `AuthenticationService` (admin API key **or** credential bearer token) → identical multi-tenant scoping to REST.
- **No "GET ALL" tools.** Every list is a paginated `*/enumerate` tool using the existing `EnumerationQuery` / `EnumerationResult<T>` (continuation-token) model.
- Settings: add an `Mcp` section to `recalldb.json`; **on startup, after the settings file is read it must be re-written** so newly-added properties are persisted.
- Full documentation: **`MCP_API.md`**, README section, CHANGELOG entry. **Version is bumped to `1.1.0` everywhere** (additive, backward-compatible feature).
- Tests: Touchstone suites covering MCP **positive and negative**; bind/target **`127.0.0.1`** (never `localhost`); run integration on port **8610**; build with `DOTNET_CLI_USE_MSBUILD_SERVER=0`.
- Dashboard updated to surface MCP.
- C# code-style rules enforced strictly (see §11).

### Included borderline operations (confirmed)
- ✅ Health / server-info tool
- ✅ Authenticate tool (validate credential → tenant/user/credential)
- ✅ Request-history tools (admin: enumerate / read / summary / delete)
- ❌ Distinct label/tag value tools — **excluded** (not requested)

### Explicitly excluded from MCP (non-sensical or superseded)
- All plain **`GET` list** endpoints (e.g. `GET /v1.0/tenants`) — superseded by paginated `*/enumerate` tools.
- Raw **HTTP HEAD `/`** connectivity — covered by the `server/info` tool.
- CORS/OPTIONS preflight, OpenAPI/Swagger surface — HTTP-transport concerns, not domain operations.

---

## 2. Reference Model Summary (LiteGraph + Voltaic 0.6.1)

**Confirmed Voltaic `0.6.1` API (differs from LiteGraph's `0.2.0` — do not copy 0.2.0 signatures blindly):**
- Namespaces: `Voltaic.Core`, `Voltaic.Mcp`. Targets `net8.0` + `net10.0`. Depends on `Watson >= 7.1.0`, `Google.Protobuf`.
- Servers: `McpHttpServer`, `McpTcpServer`, `McpWebsocketsServer` (and `McpServer` stdio — not used here).
- **`McpHttpServer(host, port)`** → Streamable HTTP on **`POST /mcp`** (JSON-RPC) + **`GET /mcp`** (SSE stream), sessions via **`MCP-Session-Id`** header. Also serves compat `/rpc` + `/events`. Constructor `includeDefaultMethods:true` auto-registers `initialize`, `notifications/initialized`, `tools/list`, `tools/call`, `ping`, `getSessions`, etc.
- **`RegisterTool(name, description, inputSchema, handler)`** overloads:
  - `Func<RpcParameters, object>` (sync)
  - `Func<RpcParameters, Task<object>>` (async) ← **use this**
  - `Func<RpcParameters, CancellationToken, Task<object>>` (async + cancellation) ← **preferred where a token is available**
  - plus `outputSchema` and `ToolDefinition` overloads.
- `RpcParameters`: `RawJson`, `HasValue`, `ContainsProperty(name)`, `GetString/GetDouble/GetInt64/GetBoolean(name)`, `Deserialize<T>()`. **No transport/session/auth context is exposed to handlers.**
- **`McpHttpServer.AuthenticationHandler`** = `Func<HttpListenerRequest, Task<AuthenticationResult>>` (Voltaic's `AuthenticationResult`: `IsAuthenticated`, `StatusCode`, `ErrorMessage`, `Principal`, `Claims`). Runs per HTTP request; `false` short-circuits with the given status. `/` health and `ping` and OPTIONS bypass. **`McpTcpServer` and `McpWebsocketsServer` have no `AuthenticationHandler`.**
- Client (for tests): `McpHttpClient` → `SetRequestHeader("Authorization", "Bearer …")` then `ConnectStreamableAsync(url)`, then `CallAsync<T>(toolName, argsObject)`.

**LiteGraph conventions we mirror:**
- Tool naming `family/operation` (e.g. `tenant/create`, `document/enumerate`).
- Flat, camelCase input schemas built from anonymous C# objects.
- Complex payloads passed as a **single JSON-string argument** (e.g. `query`, `document`, `search`) and `Deserialize<T>()`'d inside the handler.
- One `*Registrations` static class per resource family, registering tools on the HTTP server, delegating to a shared private handler. _(LiteGraph registers across HTTP/TCP/WS; RecallDB is HTTP-only, so only the HTTP registration is kept.)_
- An `MCP_API.md` catalog grouped by family.
- Touchstone tests: positive case table + `AssertThrows`-style arg-validation + a scoped-token RBAC boundary suite.

---

## 3. Target Architecture

```
                 ┌──────────────────────── RecallDb.Server (one process) ─────────────────────────┐
   REST client ──┼─▶ Watson HTTP (:8600) ─▶ REST handlers ─┐                                        │
                 │                                          ├─▶ RequestContext ─▶ Service layer ─┐  │
   MCP client  ──┼─▶ Voltaic MCP:                           │   (single source     (Tenant/User/ │  │
                 │     HTTP+SSE (:8620/mcp) ─▶ MCP tools ───┘    of truth)          Collection/   │  │
                 │     (POST /mcp + GET /mcp SSE)                                    Document/…    │  │
                 │                                                                    Services)    │  │
                 │                                                                       │        │  │
                 │                                              RecallDb.Core: DatabaseDriverBase │  │
                 └────────────────────────────────────────────────────────────────────┴─────────┘
                                                                                        ▼
                                                                             PostgreSQL + pgvector
```

- **`RequestContext`** (expanded, in `RecallDb.Server.Classes` — or promoted to `RecallDb.Core` if Core-level services are chosen) carries: origin (`Rest`/`Mcp`), resolved `AuthenticationResult`, classified `ResourceType` + `OperationType`, resource identifiers (tenant/collection/document/…), the typed request payload, pagination query, and accounting fields.
- **Service layer** (`RecallDb.Server.Services.*Service`) contains all orchestration currently trapped in REST handlers: authorization checks (`ValidateTenantAccess`, admin gates), embedding-dimension validation, label/tag stitch+persist, cascade deletes, stats SQL, search neighbor enrichment, and request-history/accounting. Services take a `RequestContext`, return typed results, and never know which transport called them.
- **REST handlers** shrink to: build `RequestContext` from `HttpContextBase` → call service → serialize + set status. Behavior must remain byte-identical (guarded by the existing test suite).
- **MCP tools** (Streamable HTTP only) build `RequestContext` from `RpcParameters` + bearer token → call the same service → return the result object.

---

## 4. Authentication & Authorization Design

**Reuse `AuthenticationService.AuthenticateBearerAsync` verbatim** — the same method REST uses. Admin API keys and credential bearer tokens both resolve to the same `RecallDb.Server.Classes.AuthenticationResult` (`IsAdmin`, `IsTenantAdmin`, `IsAdminApiKey`, `Tenant`, `User`, `Credential`).

**Token delivery (Streamable HTTP):** wire `McpHttpServer.AuthenticationHandler` = `Func<HttpListenerRequest, Task<AuthenticationResult>>` to read `Authorization: Bearer …` and authenticate via `AuthenticationService`, **early-rejecting** unauthenticated requests with 401. This is the primary gate. However, Voltaic tool handlers receive **only `RpcParameters`** — no transport/session/auth context — so the resolved identity cannot flow from `AuthenticationHandler` into the handler directly. Therefore each authenticated tool **also accepts a `bearerToken` argument**, which the `RequestContext` builder authenticates to obtain the `AuthenticationResult` used by the service layer. Resolution order in `RequestContext.FromMcp`: explicit `bearerToken` arg → else the `Authorization` header captured for the session. Unauthenticated authenticated-tool calls return a JSON-RPC error mapped from 401/403.

> If a reliable in-process correlation between the `AuthenticationHandler` and the tool dispatch is confirmed during the Phase 4 spike (e.g. `MCP-Session-Id` → identity map, or `AsyncLocal` set in the handler), the `bearerToken` argument can become optional (header-only auth). Until proven, treat the `bearerToken` argument as canonical.

**Authorization** stays exactly as REST does it, but centralized:
- **Operation Scope Mapping** (requirement): one static map from each MCP tool (and each REST request type) → `(ResourceType, OperationType)`. New tools must add an entry — this is the single source of truth for what a request "costs." (`RecallDb.Server.Services.OperationScopeMap`.)
- Enforcement in the service layer: `ValidateTenantAccess(auth, tenantId)`, admin/tenant-admin gates for mutations and admin resources, single-tenant resolution (a token for tenant A must never act on tenant B).
- **Accounting**: authorization denials and request history are recorded at the service boundary keyed on `RequestContext`, so **MCP calls are captured too** (today capture lives only in REST `PostRouting`).

---

## 5. MCP Tool Catalog (full surface)

All list operations are **`*/enumerate`** (paginated via `EnumerationQuery` JSON-string arg). Every authenticated tool takes `bearerToken`. Reads return the entity JSON (or `null`); `*/exists` returns bool; deletes return bool/`DeleteResult`; enumerate returns `EnumerationResult<T>`.

| Family | Tools |
|---|---|
| **server** | `server/info` (name, version, uptimeMs, enabled transports/ports) |
| **auth** | `auth/authenticate` (bearer OR tenantId+email+password → tenant/user(redacted)/credential) |
| **tenant** | `tenant/read` · `tenant/exists` · `tenant/enumerate` · `tenant/create` · `tenant/update` · `tenant/delete` |
| **user** | `user/read` · `user/exists` · `user/enumerate` · `user/create` · `user/update` · `user/delete` |
| **credential** | `credential/read` · `credential/exists` · `credential/enumerate` · `credential/create` · `credential/update` · `credential/delete` |
| **collection** | `collection/read` · `collection/exists` · `collection/enumerate` · `collection/create` · `collection/update` · `collection/delete` · `collection/stats` |
| **document** | `document/read` · `document/readByPosition` · `document/exists` · `document/enumerate` · `document/create` · `document/update` · `document/delete` · `document/batchCreate` · `document/batchDelete` · `document/deleteByFilter` · `document/stats` |
| **label** | `label/read` · `label/enumerate` · `label/create` · `label/delete` |
| **tag** | `tag/read` · `tag/enumerate` · `tag/create` · `tag/delete` |
| **search** | `search/query` (vector / full-text / hybrid, with filters + neighbor enrichment) |
| **requestHistory** | `requestHistory/enumerate` · `requestHistory/read` · `requestHistory/summary` · `requestHistory/delete` (admin only) |

> **No `*/all` or `*/list` tools.** Verified by a negative test that asserts `tools/list` contains no GET-ALL-style tool.

Argument conventions: identifiers as camelCase strings (`tenantId`, `collectionId`, `documentKey`, `documentId`, `position`); complex bodies as JSON strings (`tenant`, `user`, `credential`, `collection`, `document`, `documents`, `label`, `tag`, `query`, `search`, `filter`); optional flags via helper defaults (`includeData`, etc.).

---

## 6. Settings Changes

Add to `RecallDb.Core/Settings/`:
- **`McpSettings.cs`** — `bool Enabled` (default true); `string Hostname` (default `127.0.0.1`) + `int Port` (default `8620`) for the Streamable HTTP endpoint; `ServerName` (default `"RecallDB.McpServer"`); `ServerVersion` (default `"1.1.0"`); optional `Debug` flags. (No TCP/WebSocket blocks — HTTP only.)

Add `Mcp` property to `ServerSettings.cs`. Env overrides in the loader: `RECALLDB_MCP_ENABLED`, `RECALLDB_MCP_HOSTNAME`, `RECALLDB_MCP_PORT`. (Note: memory says 8610 is used for integration test runs — the *test harness* overrides the port via env; the default is fine here, but confirm no collision on the dev box.)

**Settings re-write on load (required):** in `RecallDbServer.Main`, after deserializing `recalldb.json`, immediately **re-serialize `_Settings` and write it back** so newly-introduced properties (the whole `Mcp` block) are materialized into the on-disk file. Current code only writes when the file is missing — change to: read → deserialize → (re)serialize → write, on every start. Use the existing `Serializer` (indented, enum converter). Guard against clobbering on serialization failure.

Update the checked-in **`src/RecallDb.Server/recalldb.json`** with the new `Mcp` block.

---

## 7. Work Breakdown (phased, checkable)

### Phase 0 — Scaffolding & dependency
- [ ] Add `<PackageReference Include="Voltaic" Version="0.6.1" />` to `RecallDb.Server.csproj`.
- [ ] Confirm build on `net10.0` with `DOTNET_CLI_USE_MSBUILD_SERVER=0`. _(notes: )_
- [ ] Decide service-layer home: **`RecallDb.Server.Services`** (default; both feeds live in Server, keeps Core web-free) vs promoting to `RecallDb.Core`. _(decision: )_

### Phase 1 — RequestContext + service layer (foundation)
- [ ] Expand `RequestContext`: add `RequestOriginEnum Origin`, `AuthenticationResult Auth`, `ResourceTypeEnum ResourceType`, `OperationTypeEnum Operation`, `CollectionId/DocumentKey/DocumentId/Position/ResourceId`, typed `Payload` accessors, `EnumerationQuery`/`SearchQuery` holders, accounting fields. _(One concern per file; XML docs; `_PascalCase` privates.)_
- [ ] Add `RequestOriginEnum` (`Rest`, `Mcp`), and reuse/extend existing enums for resource/operation classification.
- [ ] Add `OperationScopeMap` (static): request-type/tool → `(ResourceType, Operation)`; unparseable → `Write` (never `Read`).
- [ ] Create services (extract handler-only logic — do **not** duplicate):
  - [ ] `TenantService`, `UserService`, `CredentialService`
  - [ ] `CollectionService` (+ create/drop backing tables, `stats`)
  - [ ] `DocumentService` (embedding-dim validation, batch, delete-by-filter, label/tag stitch+persist, `stats`)
  - [ ] `LabelService`, `TagService`
  - [ ] `SearchService` (vector/full-text/hybrid + neighbor enrichment)
  - [ ] `AuthService` (bearer + tenant/email/password validation)
  - [ ] `RequestHistoryService` (enumerate/read/summary/delete)
  - [ ] `AccountingService` (request-history capture + denial audit, keyed on `RequestContext`)
- [ ] Move `AttachLabelsAndTagsAsync` / `PersistLabelsAndTagsAsync` / cascade-delete / stats-SQL / search-enrichment out of `RecallDbServer.cs` into the relevant services.
- [ ] Every async service method: `CancellationToken` + `.ConfigureAwait(false)`.

### Phase 2 — Refactor REST handlers onto the service layer
- [ ] Rewrite each handler in `RecallDbServer.cs` to: build `RequestContext` from `HttpContextBase` → call the matching service → serialize + set status code.
- [ ] Move request-history capture from `PostRouting` to `AccountingService` (or have `PostRouting` call it) so REST + MCP share one path.
- [ ] **Regression gate:** existing Touchstone suite passes unchanged (REST behavior byte-identical). _(run on :8610)_

### Phase 3 — MCP settings + settings re-write
- [ ] Add `McpSettings` class (HTTP-only: `Enabled`, `Hostname`, `Port`, `ServerName`, `ServerVersion`); wire into `ServerSettings`; add env overrides.
- [ ] Implement **re-write-after-load** in `Main`.
- [ ] Update checked-in `recalldb.json` with `Mcp` block.

### Phase 4 — MCP hosting inside RecallDb.Server
- [ ] `McpServerService`: construct **`McpHttpServer(hostname, port)`** only (`includeDefaultMethods:true`), set `ServerName`/`ServerVersion` (`1.1.0`), `StartAsync` with the existing `_TokenSource`, dispose on shutdown.
- [ ] Wire `McpHttpServer.AuthenticationHandler` → `AuthenticationService.AuthenticateBearerAsync` (early-reject; map failure → 401).
- [ ] **Spike:** determine whether the `AuthenticationHandler`-resolved identity can be correlated to the tool dispatch (`MCP-Session-Id`/`AsyncLocal`). Result decides whether `bearerToken` arg is canonical or optional (see §4).
- [ ] `RequestContext.FromMcp(toolName, RpcParameters, bearerToken)` → authenticate → classify via `OperationScopeMap` → build context.
- [ ] MCP arg helpers (`McpHelpers`: `GetStringRequired`, `GetBoolOrDefault`, `GetEnumerationQuery`, etc.).
- [ ] Start MCP only when `_Settings.Mcp.Enabled`; log the `/mcp` endpoint on boot.

### Phase 5 — MCP tool registrations (one file per family)
- [ ] `Registrations/ServerRegistrations.cs` (`server/info`)
- [ ] `Registrations/AuthRegistrations.cs` (`auth/authenticate`)
- [ ] `Registrations/TenantRegistrations.cs`
- [ ] `Registrations/UserRegistrations.cs`
- [ ] `Registrations/CredentialRegistrations.cs`
- [ ] `Registrations/CollectionRegistrations.cs`
- [ ] `Registrations/DocumentRegistrations.cs`
- [ ] `Registrations/LabelRegistrations.cs`
- [ ] `Registrations/TagRegistrations.cs`
- [ ] `Registrations/SearchRegistrations.cs`
- [ ] `Registrations/RequestHistoryRegistrations.cs`
- [ ] Each registers its tools on the single `McpHttpServer`, delegating to a shared private handler → `RequestContext.FromMcp` → service. Flat camelCase schemas; complex bodies as JSON-string args; `bearerToken` on every authenticated tool.

### Phase 6 — Documentation
- [ ] **`MCP_API.md`**: intro (in-process, shared RequestContext), transport section (Streamable HTTP `/mcp` on `:8620` — POST for JSON-RPC, GET for SSE), JSON-RPC envelope + `initialize`/`tools/list` notes, auth (per-caller bearer via `bearerToken` arg + `Authorization` header), enumeration/pagination contract, full tool catalog by family with args + examples, error semantics.
- [ ] README: add an "MCP Server" section (enable, endpoints, quick client example, link to `MCP_API.md`).
- [ ] CHANGELOG: add a new **`## v1.1.0`** section with the MCP feature bullets.
- [ ] Set `_Version = "1.1.0"` (RecallDbServer.cs) and Voltaic `ServerVersion = "1.1.0"`; `Mcp.ServerVersion` default `"1.1.0"`.

### Phase 7 — Dashboard
- [ ] Add an **MCP** nav entry (`dashboard/src/components/Sidebar.jsx`) + a read-only **MCP view** showing enabled state, the HTTP `/mcp` endpoint, and the tool catalog.
- [ ] Source the data from `server/info` (or a small REST `GET /v1.0/mcp` that returns MCP config from settings) via `dashboard/src/api/api.js`.
- [ ] Match existing dashboard styling/components (DataTable, etc.).

### Phase 8 — Tests (Touchstone; positive + negative)
- [ ] New MCP suite in `Test.Shared` (e.g. `RecallDbMcpSuites.cs`), added to `RecallDbSuites.All` so Automated/xUnit/NUnit all run it.
- [ ] Harness: start server, connect `McpHttpClient` to **`http://127.0.0.1:8620`** (`RECALLDB_MCP_ENDPOINT`) via `ConnectStreamableAsync`, `initialize`, reuse client; scoped clients per bearer token.
- [ ] **Positive:** `initialize` + `tools/list`; `server/info`; `auth/authenticate`; representative CRUD + `*/enumerate` for tenant/user/credential/collection/document/label/tag; `search/query`; `requestHistory/enumerate`; pagination correctness (continuation token walks pages).
- [ ] **Negative:** missing/invalid `bearerToken` (401/denied); missing required args (JSON-RPC/`ArgumentException`); cross-tenant access denied (scoped token vs other tenant); unknown id → `null`/404; non-admin calling admin tool → denied; **assert `tools/list` exposes no GET-ALL tool**; malformed JSON-string payloads rejected.
- [ ] Loopback rule: all clients target `127.0.0.1`; run on port **8610**; build with `DOTNET_CLI_USE_MSBUILD_SERVER=0`.

### Phase 9 — Packaging & verification
- [ ] `docker/compose.yaml`: publish the MCP HTTP port on `recalldb-server` (`8620`); add `RECALLDB_MCP_*` env as needed. **No new service.**
- [ ] `Dockerfile`: `EXPOSE` the MCP HTTP port.
- [ ] Full build (`DOTNET_CLI_USE_MSBUILD_SERVER=0`), zero warnings/errors; run REST + MCP suites on `:8610`; smoke-test MCP with `McpHttpClient`.
- [ ] Update `REST_API.md`/Postman only if REST surface changed (it should not).

---

## 8. Files — new & touched (map)

**New (Core/Server):**
- `RecallDb.Core/Settings/McpSettings.cs`
- `RecallDb.Core/Enums/RequestOriginEnum.cs` (+ resource/operation enums if not reusable)
- `RecallDb.Server/Services/{Tenant,User,Credential,Collection,Document,Label,Tag,Search,Auth,RequestHistory,Accounting}Service.cs`
- `RecallDb.Server/Services/OperationScopeMap.cs`
- `RecallDb.Server/Mcp/McpServerService.cs`, `RecallDb.Server/Mcp/McpHelpers.cs`
- `RecallDb.Server/Mcp/Registrations/*.cs` (11 files, §Phase 5) — all register on the single HTTP transport

**Touched:**
- `RecallDb.Server/RecallDbServer.cs` (settings re-write; handler refactor; MCP boot)
- `RecallDb.Server/Classes/RequestContext.cs` (expand)
- `RecallDb.Core/Settings/ServerSettings.cs` (add `Mcp`)
- `RecallDb.Server.csproj` (Voltaic ref)
- `src/RecallDb.Server/recalldb.json` (Mcp block)
- `src/Test.Shared/RecallDbMcpSuites.cs` (new) + `RecallDbSuites.cs`/`TestHelpers.cs` (aggregate + helpers)
- `dashboard/src/components/Sidebar.jsx`, new MCP view, `dashboard/src/api/api.js`
- `docker/compose.yaml`, `src/RecallDb.Server/Dockerfile`
- `MCP_API.md` (new), `README.md`, `CHANGELOG.md`

---

## 9. Enumeration / Pagination Contract (reused, unchanged)

- Request: `EnumerationQuery { MaxResults (1–1000, default 100, clamped), ContinuationToken, Ordering, filters… }`.
- Response: `EnumerationResult<T> { Success, MaxResults, ContinuationToken (null at end), EndOfResults, TotalRecords, RecordsRemaining, Objects, TotalMs }`.
- Continuation token is a stringified numeric offset (existing `EnumerationResult<T>.Create`). MCP `*/enumerate` tools accept the whole `EnumerationQuery` as a JSON-string `query` arg and return `EnumerationResult<T>` verbatim.

---

## 10. Risks & Decisions

| # | Risk / Decision | Resolution |
|---|---|---|
| R1 | Voltaic tool handlers get no transport/session/auth context, so the `AuthenticationHandler`-resolved identity can't flow into the handler directly. | `McpHttpServer.AuthenticationHandler` is the early-reject gate; canonical identity carrier is a `bearerToken` **tool argument** resolved by `RequestContext.FromMcp`. Phase 4 spike may make the arg optional (header-only). Documented in `MCP_API.md`. |
| R2 | REST-handler refactor could change REST behavior. | Existing Touchstone REST suite is the regression gate; refactor keeps responses byte-identical. |
| R3 | Request-history capture currently HTTP-only (`PostRouting`). | Moved to `AccountingService` at the service boundary so MCP is captured too. |
| R4 | Port collision (memory: 8610 used by REST integration runs; 8600 by verbex). | MCP default moved to `8620` to avoid the REST integration port (8610); test harness reads `RECALLDB_MCP_ENDPOINT`. |
| R5 | Voltaic `0.6.1` API differs from LiteGraph's `0.2.0`. | Plan targets the verified 0.6.1 surface (Streamable HTTP `/mcp`, `RpcParameters`, `AuthenticationHandler`). |
| D1 | Service-layer location. | Default `RecallDb.Server.Services` (keeps Core web-free); revisit if Core-level reuse needed. |
| D2 | Tool naming. | `family/operation` (LiteGraph model), no product prefix (server namespace disambiguates). |

---

## 11. Compliance Checklist (`C:\Code\agents\requirements`)

- [ ] No `var`; no tuples; no partial classes; one type per file.
- [ ] `using` directives **inside** namespace, System/Microsoft first then others, alphabetical.
- [ ] Public `LikeThis`, private `_LikeThis`; XML docs on all public types/members (document defaults/min/max, nullability, exceptions).
- [ ] Guard clauses + `Math.Clamp`; `ArgumentNullException`/`ArgumentException` with context; no generic `Exception`.
- [ ] Every async method takes `CancellationToken`; every `await` uses `.ConfigureAwait(false)`.
- [ ] **No `JsonElement`/DOM for fixed contracts** — define named types and `RpcParameters.Deserialize<T>()` into them.
- [ ] No `Console.WriteLine` in library/service code (use `LoggingModule`).
- [ ] Centralized Operation Scope Mapping; single-tenant resolution; audit authorization denials.
- [ ] Tests: `Test.Shared` no console output, self-cleaning; `127.0.0.1` everywhere; exit 0/1.
- [ ] Version set to `1.1.0` everywhere; CHANGELOG updated; README accurate.

---

## 12. Definition of Done

- [x] `RecallDb.Server` hosts MCP over Streamable HTTP (`/mcp`, POST + GET-SSE) only, in-process, gated by `Mcp.Enabled`.
- [x] Full tool catalog (§5) live — 51 tools; no GET-ALL tools (asserted by test); every list paginated.
- [x] Per-caller bearer auth reuses `AuthenticationService`; multi-tenant scoping identical to REST.
- [x] `recalldb.json` re-written on every start ("settings file synchronized"); `Mcp` block present.
- [x] REST + MCP Touchstone suites (positive + negative) green — 195/195 passed.
- [x] `MCP_API.md`, README, CHANGELOG updated; dashboard shows MCP; version set to `1.1.0`.
- [x] Clean build, zero warnings (`DOTNET_CLI_USE_MSBUILD_SERVER=0`).
```
