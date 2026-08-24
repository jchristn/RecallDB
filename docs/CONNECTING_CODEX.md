# Connecting OpenAI Codex CLI to RecallDB (MCP)

RecallDB exposes an in-process MCP server over **Streamable HTTP** at `http://localhost:8620/mcp`. This guide connects the [OpenAI Codex CLI](https://developers.openai.com/codex/cli) to it.

## Prerequisites

- A running RecallDB server (`curl http://localhost:8620/` returns 200).
- A bearer token: your admin API key (default `recalldbadmin`) or a credential bearer token.

## Automatic setup (recommended)

```bash
recalldb mcp install --only codex
```

This writes an `[mcp_servers.recalldb]` block to `~/.codex/config.toml` (`%USERPROFILE%\.codex\config.toml` on Windows) using native Streamable HTTP. Preview with `--dry-run`; override the token with `--token <token>`. Restart Codex afterward.

## Manual setup

### Native HTTP (current Codex versions)

Add to `~/.codex/config.toml`:

```toml
[mcp_servers.recalldb]
url = "http://localhost:8620/mcp"
http_headers = { "Authorization" = "Bearer recalldbadmin" }
```

Prefer keeping the token out of the file by using an env var Codex reads and sends as `Authorization: Bearer <value>`:

```toml
[mcp_servers.recalldb]
url = "http://localhost:8620/mcp"
bearer_token_env_var = "RECALLDB_TOKEN"
```

If your Codex build only picks up stdio servers, enable the newer MCP client:

```toml
[features]
experimental_use_rmcp_client = true
```

Or via the CLI:

```bash
codex mcp add recalldb --url http://localhost:8620/mcp --bearer-token-env-var RECALLDB_TOKEN
```

### stdio bridge fallback (older Codex without HTTP MCP)

```toml
[mcp_servers.recalldb]
command = "npx"
args = ["-y", "mcp-remote", "http://localhost:8620/mcp", "--header", "Authorization: Bearer ${RECALLDB_TOKEN}"]
```

## Authentication

The header/token above authenticates the transport. **Every authenticated RecallDB tool call must also include a `bearerToken` argument.** `recalldb mcp install` writes a managed block into `AGENTS.md` in your working directory telling Codex to pass `bearerToken="<token>"` on every RecallDB call. If configuring manually, add the same guidance to your project's `AGENTS.md`:

```markdown
## RecallDB MCP
Use the recalldb MCP tools. Pass `bearerToken="recalldbadmin"` on every authenticated call.
Listing is paginated via `*/enumerate` (optional `query` JSON string). `server/info` needs no token.
```

## Verify

Start Codex, confirm `recalldb` appears among MCP servers, and ask it to call `server/info`.

## Uninstall

```bash
recalldb mcp uninstall --only codex
```
