# Connecting Claude Code to RecallDB (MCP)

RecallDB exposes an in-process MCP server over **Streamable HTTP** at `http://localhost:8620/mcp`. This guide connects [Claude Code](https://code.claude.com) to it.

## Prerequisites

- A running RecallDB server (`http://localhost:8620/mcp` reachable — confirm with `curl http://localhost:8620/`).
- A bearer token: your **admin API key** (default `recalldbadmin`) or a **credential bearer token**. RecallDB MCP tools authorize per call — see [Authentication](#authentication).

## Automatic setup (recommended)

From anywhere, run the RecallDB CLI:

```bash
recalldb mcp install --only claude
```

(Or `recalldb mcp install` to configure every detected harness at once.)

This:
- adds a `recalldb` server to `~/.claude.json` (`%USERPROFILE%\.claude.json` on Windows), and
- writes a Claude subagent at `~/.claude/agents/recalldb.md` that tells the model how to call the tools (including the `bearerToken`).

Preview without writing: add `--dry-run`. Use a specific token with `--token <token>`. Restart Claude Code afterward.

## Manual setup

### Option A — `claude mcp add`

```bash
claude mcp add --transport http recalldb http://localhost:8620/mcp \
  --header "Authorization: Bearer recalldbadmin"
```

Scope with `--scope local|project|user` (default `local`).

### Option B — edit config directly

Add to `~/.claude.json` (user/local scope) or a project-root `.mcp.json` (shared with your team):

```json
{
  "mcpServers": {
    "recalldb": {
      "type": "http",
      "url": "http://localhost:8620/mcp",
      "headers": {
        "Authorization": "Bearer recalldbadmin"
      }
    }
  }
}
```

The `"type": "http"` field is required — a URL entry with no `type` is treated as stdio and fails.

## Authentication

The `Authorization` header authenticates the transport (an invalid token is rejected with 401). **In addition, every authenticated RecallDB tool call must include a `bearerToken` argument** — the MCP protocol does not forward the header into individual tool calls.

The auto-installed agent (`~/.claude/agents/recalldb.md`) instructs the model to pass `bearerToken = "<your token>"` on every call. If you configure manually, tell Claude to do the same, e.g.:

> Use the recalldb MCP tools. Pass `bearerToken="recalldbadmin"` on every call.

`server/info` and `auth/authenticate` require no token.

## Verify

In Claude Code, run `/mcp` to see `recalldb` listed and connected, then ask Claude to call `server/info` (no token needed). Then try `tenant/enumerate` with your token.

## Uninstall

```bash
recalldb mcp uninstall --only claude
```
