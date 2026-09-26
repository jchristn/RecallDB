# Changelog

## SDKs v0.2.2 (C#, JavaScript, Python)

The three SDKs now share one version, 0.2.2 (C# `RecallDb.Sdk` was 0.2.1, JavaScript was 0.2.0, Python had none). They target the v0.2.1 server described below.

- Single-call hybrid search options and per-leg ranks and scores (`Hybrid.Strategy`, `RrfK`, `CandidatePool`; `VectorScore`, `TextScore`, `VectorRank`, `TextRank`), stored vectors on request (`IncludeEmbeddings`), search notices, and the new recency, collapse, and minimum-should-match options (`Hybrid.RecencyWeight`, `Collapse`, `FullText.MinimumShouldMatch`; `RecencyRank`, `GroupKey`, `GroupHits` on hits). New request fields are omitted when unset, so the SDKs still work against older servers
- Typed server info with a capabilities list (`GetServerInfoAsync`/`SupportsAsync`, `getServerInfo`/`supports`, `get_server_info`/`supports`), cached per client, so clients can detect features on servers that report the same version
- C# SDK: constructors that accept an `HttpClient` (caller-owned, never modified, not disposed) or an `HttpMessageHandler`; a per-request `Timeout` (default 100 s, throws `TimeoutException`); the bearer token is sent per request; cancellation reaches response reads; compact request JSON (was indented). JavaScript SDK: `timeoutMs` (default 100 s, `RecallDbTimeoutError`), a custom `fetch`, and a per-call `signal`. Python SDK: `timeout` (default 100 s; previously none, so a stalled server blocked forever) and a caller-supplied `session`, which the client never modifies
- Every caller-supplied path segment is URL-encoded (previously only document reads, updates, deletes, and exists)
- `RecallDbException` exposes the server's error code and message (`ErrorCode`/`ErrorMessage`, `errorCode`/`errorMessage`, `error_code`/`error_message`) for both error body shapes, and keeps the raw body
- Named constants for capabilities, hybrid strategies, match modes, full-text and vector search types, sort orders, and collapse fields
- C# SDK: nullable annotations on the public surface and complete XML documentation (the build no longer suppresses CS1591); typed calls throw `InvalidOperationException` on a success response with an empty body instead of returning null
- Docs and harnesses use `127.0.0.1`; each harness gains cases for all of the above
- **Behavior change:** `...ExistsAsync` / `exists` / `..._exists` now return false only for 404 and throw `RecallDbException` for any status other than 200 or 404, instead of returning false when the server is unreachable, unauthorized, or failing
- **Behavior change (JavaScript, Python):** requests now time out after 100 seconds by default; pass `timeoutMs: 0` or `timeout=None` for the previous unlimited behavior

## v0.2.1

Added end-to-end observability (metrics and distributed tracing) across the entire server.

- Instrumentation uses only the .NET base class library (`Meter`, `ActivitySource`); a single in-process OpenTelemetry host owns all wiring and subscribes to the `RecallDb.Server` and `RecallDb.Core` meters/sources by name, so instrumented code takes no dependency on OpenTelemetry
- 100% path coverage: HTTP (REST) transport, MCP tool transport, a unified application-operation family across both transports (origin=rest|mcp), vector/full-text/hybrid search, and the PostgreSQL storage layer, plus .NET runtime and process metrics
- Distributed traces nest naturally (MCP tool / REST operation → database query spans) and are exported over OTLP to Tempo; metrics are exposed on an in-process Prometheus scrape endpoint (`:9464/metrics`) with second-scale latency histogram buckets
- New `Observability` settings section (with `RECALLDB_OBS_*` / `RECALLDB_OTLP_*` environment overrides); disabled-safe and failure-safe so telemetry never blocks startup
- Docker Compose now provisions a full stack: Prometheus, Tempo, Loki, Grafana, and Grafana Alloy (container-log shipping to Loki), with no port conflicts
- Grafana ships pre-provisioned datasources (with trace↔log correlation) and dashboards organized into sections: HTTP, MCP, Application, Search, Database, and Runtime
- The product dashboard landing page now links out (in a new tab) to Grafana, Prometheus, Tempo, Loki, and Alloy, each card showing the service name, default credentials, and URL
- SDK NuGet package metadata fully populated and now produces a symbol package (snupkg) with SourceLink
- Vector-only searches now order by the raw pgvector distance operator (ascending) for best-first sorts, so the HNSW index can satisfy the query instead of a sequential scan; scores and ordering are unchanged
- Startup now re-runs the idempotent table and index DDL for every existing collection, so collections created before the HNSW and GIN indexes existed acquire them without a manual migration (best-effort per collection; failures are logged and never block startup)
- Search results no longer include each hit's stored embedding vector by default; set the new `SearchQuery.IncludeEmbeddings` to `true` to get them back
- Full-text search now matches ANY meaningful query term by default (ranked by relevance) instead of requiring every term; new `FullText.MatchMode` (`Any` default, `All` = previous behavior, `Phrase`, `WebSearch`)
- Hybrid search is now a rank-fused union of the vector and text legs (weighted Reciprocal Rank Fusion, k=60) instead of a vector ranking inside required text matches; new `SearchQuery.Hybrid` (`Strategy` = `Rrf` default, `Linear` = normalized score blend, `Filter` = previous behavior; `RrfK`, 1-100000; `CandidatePool`, 1-10000, default max(MaxResults x 4, 100) capped at 1000). `FullText.TextWeight` is the text leg's share in every strategy
- Hybrid `Rrf` and `Linear` scores are normalized to [0, 1]; each hit now carries `VectorScore`, `VectorRank`, and `TextRank`, and `TextScore` is null for hybrid hits that did not match the text query. `TotalRecords` for fused searches is the size of the fused candidate set (at most 2 x `CandidatePool`)
- `SearchResult.Notice` explains non-error conditions: a text query with no searchable terms (all stop words), a full-text language not served by the index, or `Hybrid` options sent without both a vector and a text query
- Collections gain a stored `content_tsv` column with a GIN index, so text ranking no longer re-tokenizes every candidate row; the old `_fts` expression index is dropped once the new one exists. New collections get it at creation. Existing collections are migrated at startup, and because `ADD COLUMN ... STORED` rewrites the table under an `ACCESS EXCLUSIVE` lock, large collections can delay startup; opt out with the new `Database.MigrateFullTextColumn` setting (default `true`, environment override `RECALLDB_DB_MIGRATE_FTS_COLUMN`) and restart with it set to `true` in a maintenance window. Without the column, search falls back to the expression index. The migration and index builds run under the new `Database.SchemaCommandTimeoutSeconds` (default `0`, no limit), because the rewrite also rebuilds the HNSW index and routinely outlasts the 30-second default command timeout
- `FullText` inputs are validated with 400 responses: `Language` must be a text search configuration in `pg_ts_config`, `Normalization` 0-63, `TextWeight` 0.0-1.0 (rejected instead of silently clamped), valid `MatchMode`; a blank `FullText.Query` with no vector is a 400, and next to a vector it is ignored
- `FullText.MinimumScore` and the `SearchQuery.MinimumScore`/`MaximumScore` thresholds on full-text and hybrid searches are applied in SQL, so pagination and `TotalRecords` are exact
- Search metrics and traces gain `recalldb_search_match_mode` (`any`, `all`, `phrase`, `websearch`, `none`) and `recalldb_search_hybrid_strategy` (`rrf`, `linear`, `filter`, `none`) labels, and `recalldb_search_mode` now uses the same vector/full-text test as the query engine; the Grafana Search dashboard breaks rate and latency down by both
- Dashboard search: match-mode and hybrid-strategy controls; a text weight or normalization of 0 is no longer replaced by the default
- The hybrid vector leg raises `hnsw.ef_search` to the candidate pool (transaction-scoped, at most 1000), so pgvector's HNSW scan can return the full pool instead of stopping at its default of 40 rows
- Request bodies whose values are rejected by a model's range check (for example `TextWeight` 1.5, `Normalization` 64, `Hybrid.RrfK` 0) now return 400 instead of 500
- Tests: the full-text and hybrid terms-filter tests (and two enumeration tests) sent `TermsFilter`, which the server silently ignored; they now send `Terms` and assert the filter's effect
- Behavior change: callers relying on all-terms matching or text-filtered hybrid should set `FullText.MatchMode = All` and `Hybrid.Strategy = Filter`
- Fixed: per-collection index names now derive from the full collection id, so two collections created in the same millisecond no longer share index names. Previously the second collection could be left without its unique `document_key`, HNSW, trigram, and full-text indexes — silently (sequential creates, via `CREATE INDEX IF NOT EXISTS` skipping a name owned by another table) or with a 500 and a half-created collection (concurrent creates)
- Fixed: collection creation is atomic. The `collections` row, the documents/labels/tags tables, and every index (including the stored `content_tsv` index) are created in a single transaction, so a failure no longer leaves a `collections` row without its tables or a documents table missing indexes
- Fixed: a client-supplied collection `Id` in a create body is now validated (`1-48` characters of letters, digits, underscore, hyphen, or dot) instead of flowing unescaped into the collection's DDL and DML as a table/index name; route path segments (tenant id, collection id, document id, document key) are URL-decoded on the server so a document key containing `#`, `?`, `/`, `%`, a space, or a non-ASCII character round-trips correctly
- Changed: creating a collection whose name already exists in the tenant returns `409 Conflict` (with the existing collection's id) instead of `500`
- Changed (upgrade note): the startup schema pass renames each existing collection's indexes to the new scheme in place (an instant `ALTER INDEX ... RENAME`, no rebuild) and builds any index that is missing; collections that were left damaged by the historical index-name collision have their missing tables and indexes created. A collection with duplicate `document_key` values keeps working without the unique index — the duplicates are logged at warning level and left for an operator to resolve. Damaged collections with many rows can delay startup while missing indexes build. See `migrations/002_inspect_damaged_collections.sql` to inspect before upgrading
- Fixed (SDK): document keys (and other path segments) are URL-encoded in the C#, Python, and JavaScript SDKs' get, read-by-position, update, delete, and exists calls
- Changed: the MCP server is built on Voltaic 2.0.0 (was 0.6.1). `tools/list` now returns only RecallDB's 51 tools; Voltaic's `ping`, `echo`, `getTime`, and `getSessions` demo tools are gone (`getSessions` exposed every client's `MCP-Session-Id`)
- Changed (MCP clients): tools are callable only through `tools/call`; calling a tool name as a bare JSON-RPC method now returns `-32601`. The protocol `ping` returns `{}` instead of `"pong"`. MCP clients such as Claude Code, Cursor, and the official SDKs are unaffected
- Fixed: a failed MCP tool call now reports its status in the JSON-RPC error message (`403 Forbidden: Access denied.`, `404 Not found: ...`) with `data.statusCode`, as MCP_API.md describes. Previously the message was a bare `Internal error` with the detail only in `data`, which clients such as Claude Code do not show
- Dependencies: OpenTelemetry 1.19.1 (Runtime instrumentation 1.19.0, Prometheus exporter 1.19.1-beta.1), Microsoft.NET.Test.Sdk 18.10.1, Microsoft.Extensions.DependencyModel 10.0.12, Microsoft.SourceLink.GitHub 10.0.401
- Tests: the MCP suite calls tools through `tools/call` and adds cases for the v2 surface: no demo tools in `tools/list`, `ping` returns `{}`, the `tools/call` result envelope, bare tool calls and demo tools rejected, unknown tool and missing name, schema-rejected argument types, status-bearing error messages, and the `Authorization` header gating `tools/call` but not `ping`
- Hybrid `Rrf` search can fuse a third, recency signal: new `Hybrid.RecencyWeight` (0.0-1.0, default 0, off) ranks candidates by newest `created_utc` per collapse group (or per document) and the fused score stays normalized to [0, 1]; hits carry `RecencyRank`. Ignored with a `Notice` for `Linear` and `Filter`. With the weight at 0 the generated SQL and scores are unchanged
- New `SearchQuery.Collapse` (`Field` = `DocumentId` or `Tag` with `TagKey`; `CandidatePool` for single-leg searches) returns one hit per group, its best-scoring chunk, with `GroupKey` and `GroupHits`; `MaxResults`, `TotalRecords`, and continuation tokens count groups, and score thresholds apply to the representative. Works with vector-only, full-text-only, and hybrid `Rrf` and `Linear` (400 with `Filter`). A `Notice` reports when a full candidate pool held fewer groups than the page asked for
- New opt-in `FullText.MinimumShouldMatch` (1-3, default 1) for `MatchMode = Any`: only documents containing at least that many distinct query terms match (the first 16 terms of the query are considered), which cuts ranking work on large collections
- `GET /` (now typed as `HealthInfo`) and MCP `server/info` report `Capabilities` (`search.hybrid.rrf`, `search.hybrid.recency`, `search.collapse`, `search.include-embeddings`, `search.fulltext.minimum-should-match`), so clients can detect these features on servers that share the same version
- Search metrics and traces gain `recalldb_search_collapse` (`none`, `documentid`, `tag`) and `recalldb_search_recency` (`on`, `off`) labels
- Fixed: document create (`PUT .../documents`) and batch create responses now carry each document's generated `Id` (previously `0`) and its stored `CreatedUtc`, so they match a later read. Batch create is now a single multi-row `INSERT ... RETURNING`
- Docs: the README's document example sends `Tags` as an object (the array form it showed is rejected with 400), and uses the seeded `default` tenant and collection ids
- Tests: a new `RecallDbSearchGrouping` suite (recency, collapse, minimum-should-match, and their validation, in its own collection), `HealthReportsCapabilities`, and MCP cases for capabilities and a collapsed, recency-weighted `search/query`

## v0.2.0

Added an in-process MCP (Model Context Protocol) server.

- MCP server built with Voltaic, hosted inside RecallDB.Server over Streamable HTTP (POST /mcp for JSON-RPC, GET /mcp for the SSE stream)
- Full parity with the REST API surface exposed as 51 MCP tools across tenant, user, credential, collection, document, label, tag, search, authentication, request-history, and server-info families
- Enumeration/pagination-based listing only (no "get all" tools), reusing the existing EnumerationQuery/EnumerationResult model
- Per-caller bearer authentication reusing the existing AuthenticationService, with identical multi-tenant scoping to REST
- REST and MCP now normalize into a shared RequestContext and a single transport-agnostic service layer
- New `Mcp` settings section; on startup the settings file is re-written after load so newly added properties are persisted
- MCP configuration surfaced in the dashboard
- `recalldb mcp install` / `uninstall` / `print` CLI that auto-configures the MCP server into Claude Code, Cursor, Gemini CLI, Codex CLI, and Mux (with `--dry-run`, `--only`, and `--token`)
- MCP_API.md documentation, per-harness `docs/CONNECTING_*.md` guides, and expanded positive/negative test coverage
- Deployment helper scripts: `build-all.bat` (builds and pushes both server and dashboard images) and `docker/update.bat` (pull + recreate the running stack)

## v0.1.0

Initial release.

- Multi-tenant vector database service
- PostgreSQL with pgvector backend
- REST API with bearer token authentication
- Per-collection dynamic tables with HNSW indexing
- Cosine similarity, Euclidean distance, and inner product search
- Label and tag filtering
- Document batch operations
- C#, Python, and JavaScript SDKs
- React dashboard
- Docker Compose deployment
