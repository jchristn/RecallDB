# Connecting Gemini CLI to RecallDB (MCP)

RecallDB exposes an in-process MCP server over **Streamable HTTP** at `http://localhost:8620/mcp`. This guide connects the [Gemini CLI](https://github.com/google-gemini/gemini-cli) to it.

## Prerequisites

- A running RecallDB server (`curl http://localhost:8620/` returns 200).
- A bearer token: your admin API key (default `recalldbadmin`) or a credential bearer token.

## Automatic setup (recommended)

```bash
recalldb mcp install --only gemini
```

This writes the `recalldb` server into `~/.gemini/settings.json` (`%USERPROFILE%\.gemini\settings.json` on Windows) and adds a managed RecallDB block to `GEMINI.md` in your working directory. Preview with `--dry-run`; override the token with `--token <token>`. Restart Gemini afterward.

## Manual setup

Edit `~/.gemini/settings.json` (user scope) or `.gemini/settings.json` in your project. Note the key is **`httpUrl`** for Streamable HTTP (Gemini treats plain `url` as an SSE endpoint):

```json
{
  "mcpServers": {
    "recalldb": {
      "httpUrl": "http://localhost:8620/mcp",
      "headers": {
        "Authorization": "Bearer recalldbadmin"
      },
      "timeout": 30000
    }
  }
}
```

You can reference an environment variable in the header value, e.g. `"Authorization": "Bearer $RECALLDB_TOKEN"`.

## Authentication

The `headers` value authenticates the transport. **Every authenticated RecallDB tool call must also include a `bearerToken` argument.** `recalldb mcp install` adds a managed block to `GEMINI.md` telling the model to pass `bearerToken="<token>"` on every RecallDB call. If configuring manually, add the same guidance to your project's `GEMINI.md`:

```markdown
## RecallDB MCP
Use the recalldb MCP tools. Pass `bearerToken="recalldbadmin"` on every authenticated call.
Listing is paginated via `*/enumerate` (optional `query` JSON string). `server/info` needs no token.
```

## Verify

Start Gemini and run `/mcp` (or `/mcp list`) to confirm `recalldb` is connected, then ask it to call `server/info`.

## Uninstall

```bash
recalldb mcp uninstall --only gemini
```
