# Connecting Cursor to RecallDB (MCP)

RecallDB exposes an in-process MCP server over **Streamable HTTP** at `http://localhost:8620/mcp`. This guide connects [Cursor](https://cursor.com) to it.

## Prerequisites

- A running RecallDB server (`curl http://localhost:8620/` returns 200).
- A bearer token: your admin API key (default `recalldbadmin`) or a credential bearer token.

## Automatic setup (recommended)

```bash
recalldb mcp install --only cursor
```

This writes a project-scoped `.cursor/mcp.json` in your current directory. Preview with `--dry-run`; override the token with `--token <token>`. Reload Cursor afterward.

## Manual setup

Create `.cursor/mcp.json` in your project root (project scope) or `~/.cursor/mcp.json` (global). Cursor negotiates Streamable HTTP (falling back to SSE) for remote `url` entries:

```json
{
  "mcpServers": {
    "recalldb": {
      "url": "http://localhost:8620/mcp",
      "headers": {
        "Authorization": "Bearer recalldbadmin"
      }
    }
  }
}
```

You can reference an environment variable, e.g. `"Authorization": "Bearer ${env:RECALLDB_TOKEN}"`. You can also add the server from **Settings → MCP → Add new MCP server** and paste the same block.

## Authentication

The `headers` value authenticates the transport. **Every authenticated RecallDB tool call must also include a `bearerToken` argument.** Add a note to your project's `AGENTS.md` (or Cursor Rules) telling the model to pass `bearerToken="<token>"` on every RecallDB call — `recalldb mcp install` writes this into `AGENTS.md` for you:

```markdown
## RecallDB MCP
Use the recalldb MCP tools. Pass `bearerToken="recalldbadmin"` on every authenticated call.
Listing is paginated via `*/enumerate` (optional `query` JSON string). `server/info` needs no token.
```

## Verify

Open Cursor's MCP settings and confirm `recalldb` shows a green/connected status and lists its tools, then ask Cursor to call `server/info`.

## Uninstall

```bash
recalldb mcp uninstall --only cursor
```
