# Changelog

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
