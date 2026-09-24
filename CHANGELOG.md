# Changelog

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
