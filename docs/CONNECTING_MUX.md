# Connecting Mux to RecallDB (MCP)

RecallDB exposes an in-process MCP server over **Streamable HTTP** at `http://localhost:8620/mcp`. This guide connects [Mux](https://github.com/joelchristner/mux) to it.

Mux splits the endpoint into a base `url` plus an `mcpPath`, so the values you enter are:

| Field | Value |
|-------|-------|
| Name | `recalldb` |
| Transport | `http` |
| URL (base) | `http://localhost:8620` |
| MCP path | `/mcp` |
| Auth type | `bearer` |
| Bearer token | your admin API key (default `recalldbadmin`) or a credential bearer token |

## Option 1 — Interactive TUI (`/mcp add`)

Inside the Mux REPL, run the `/mcp` slash command to manage MCP servers:

1. Start Mux (just run `mux`).
2. Type **`/mcp add`** and press Enter to open the add-server wizard.
3. **Name**: enter `recalldb`.
4. **Transport**: choose **`http`**.
5. **URL**: enter `http://localhost:8620`.
6. **MCP path**: enter `/mcp` (this is usually the default — accept it).
7. **Auth**: choose **`bearer`**, then enter the token (e.g. `recalldbadmin`). The token field is masked.
8. Confirm. Mux saves the server to `~/.mux/mcp-servers.json` and connects it for the current session.

Other slash commands: `/mcp list` (or `/mcp ls`) to see configured servers, and `/mcp remove recalldb` (aliases `/mcp delete`, `/mcp rm`) to remove it.

## Option 2 — Automatic (`recalldb mcp install`)

```bash
recalldb mcp install --only mux
```

This writes the `recalldb` entry into `~/.mux/mcp-servers.json` (honoring `MUX_CONFIG_DIR` if set). It only runs when Mux is detected (a `~/.mux` directory exists or `mux` is on your `PATH`). Preview with `--dry-run`; override the token with `--token <token>`.

## Option 3 — Edit the config file

`~/.mux/mcp-servers.json` (or `$MUX_CONFIG_DIR/mcp-servers.json`) holds a top-level `servers` array. Add:

```json
{
  "servers": [
    {
      "name": "recalldb",
      "transport": "http",
      "url": "http://localhost:8620",
      "mcpPath": "/mcp",
      "auth": {
        "type": "bearer",
        "bearerToken": "${RECALLDB_TOKEN}"
      }
    }
  ]
}
```

`bearerToken` supports `${VAR}` environment-variable expansion — prefer an env reference over an inline literal. You can also pass a config ad hoc to `mux print` with `--mcp-config ./mcp-servers.json`.

## Authentication

The `auth.bearerToken` authenticates the transport. **Every authenticated RecallDB tool call must also include a `bearerToken` argument.** Instruct Mux's model to pass `bearerToken="<your token>"` on every RecallDB call (for example in your project instructions). `server/info` and `auth/authenticate` need no token.

## Verify

Run `/mcp list` to confirm `recalldb` is connected, then ask Mux to call `server/info`.
