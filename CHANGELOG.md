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
