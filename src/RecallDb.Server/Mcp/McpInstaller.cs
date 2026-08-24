namespace RecallDb.Server.Mcp
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Nodes;

    using RecallDb.Core.Settings;

    /// <summary>
    /// Auto-configures the RecallDB MCP server into the config of every detected agent harness — Claude Code,
    /// Cursor, Gemini CLI, OpenAI Codex CLI, and Mux — mirroring the "armada mcp install" experience. Invoked from
    /// the CLI as <c>recalldb mcp install</c> / <c>recalldb mcp uninstall</c> / <c>recalldb mcp print</c>.
    ///
    /// Each harness is configured with the RecallDB Streamable HTTP endpoint and an Authorization bearer header.
    /// Because RecallDB MCP tools authorize per call via a <c>bearerToken</c> argument (the Authorization header
    /// only gates the transport), an instruction file is also written for each harness telling the agent to pass
    /// the token on every RecallDB tool call.
    /// </summary>
    public static class McpInstaller
    {
        #region Public-Members

        /// <summary>
        /// MCP server key/name used across every harness config.
        /// </summary>
        public const string ServerName = "recalldb";

        #endregion

        #region Private-Members

        private const string _ManagedBegin = "<!-- recalldb:mcp:begin -->";
        private const string _ManagedEnd = "<!-- recalldb:mcp:end -->";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Entry point for the <c>mcp</c> CLI verb. Returns a process exit code (0 success, non-zero on usage error).
        /// </summary>
        /// <param name="subArgs">Arguments following <c>mcp</c> (e.g. install, uninstall, print, plus flags).</param>
        /// <param name="settings">MCP settings (for hostname/port).</param>
        /// <param name="adminApiKeys">Admin API keys; the first is embedded as the default token.</param>
        /// <param name="version">Server version.</param>
        /// <returns>Exit code.</returns>
        public static int Run(IReadOnlyList<string> subArgs, McpSettings settings, IReadOnlyList<string> adminApiKeys, string version)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            string command = subArgs != null && subArgs.Count > 0 ? subArgs[0].ToLowerInvariant() : "help";
            bool dryRun = HasFlag(subArgs, "--dry-run");
            string only = GetOption(subArgs, "--only");
            string token = GetOption(subArgs, "--token");
            if (string.IsNullOrEmpty(token))
                token = adminApiKeys != null && adminApiKeys.Count > 0 ? adminApiKeys[0] : "recalldbadmin";

            string host = settings.Hostname;
            if (string.IsNullOrEmpty(host) || host == "*" || host == "+" || host == "0.0.0.0")
                host = "localhost";
            string baseUrl = "http://" + host + ":" + settings.Port;
            string mcpUrl = baseUrl + "/mcp";

            switch (command)
            {
                case "install":
                    Install(mcpUrl, baseUrl, token, version, dryRun, only);
                    return 0;
                case "uninstall":
                case "remove":
                    Uninstall(dryRun, only);
                    return 0;
                case "print":
                case "status":
                    Print(mcpUrl, baseUrl, token);
                    return 0;
                default:
                    PrintUsage();
                    return command == "help" || command == "--help" || command == "-h" ? 0 : 1;
            }
        }

        #endregion

        #region Install

        private static void Install(string mcpUrl, string baseUrl, string token, string version, bool dryRun, string only)
        {
            Console.WriteLine(dryRun ? "[DRY RUN] Previewing RecallDB MCP install..." : "Installing RecallDB MCP configuration...");
            Console.WriteLine("Endpoint: " + mcpUrl);
            Console.WriteLine();

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string cwd = Directory.GetCurrentDirectory();

            if (Included(only, "claude"))
            {
                InstallClaude(home, mcpUrl, token, dryRun);
                InstallClaudeAgent(home, token, dryRun);
            }

            if (Included(only, "cursor"))
                InstallJsonServer(Path.Combine(cwd, ".cursor", "mcp.json"), "Cursor", BuildUrlHeaderEntry(mcpUrl, token, false), dryRun);

            if (Included(only, "gemini"))
                InstallJsonServer(Path.Combine(home, ".gemini", "settings.json"), "Gemini CLI", BuildUrlHeaderEntry(mcpUrl, token, true), dryRun);

            if (Included(only, "codex"))
                InstallCodex(Path.Combine(home, ".codex", "config.toml"), mcpUrl, token, dryRun);

            if (Included(only, "mux"))
                InstallMux(GetMuxServersPath(home), baseUrl, token, dryRun);

            if (Included(only, "agents"))
                UpsertInstructionFile(Path.Combine(cwd, "AGENTS.md"), token, dryRun);

            if (Included(only, "gemini"))
                UpsertInstructionFile(Path.Combine(cwd, "GEMINI.md"), token, dryRun);

            Console.WriteLine();
            if (dryRun)
                Console.WriteLine("[DRY RUN] No files were modified.");
            else
                Console.WriteLine("Done. Restart your agent harness to pick up the new MCP configuration.");
        }

        private static void InstallClaude(string home, string mcpUrl, string token, bool dryRun)
        {
            string path = Path.Combine(home, ".claude.json");
            JsonObject root = ReadJsonObject(path);
            JsonObject servers = GetOrCreateObject(root, "mcpServers");

            JsonObject entry = new JsonObject
            {
                ["type"] = "http",
                ["url"] = mcpUrl,
                ["headers"] = new JsonObject { ["Authorization"] = "Bearer " + token }
            };

            servers[ServerName] = entry;
            WriteJson(path, root, "Claude Code", dryRun);
        }

        private static void InstallClaudeAgent(string home, string token, bool dryRun)
        {
            string dir = Path.Combine(home, ".claude", "agents");
            string path = Path.Combine(dir, "recalldb.md");
            string content = BuildClaudeAgent(token);

            if (dryRun)
            {
                Console.WriteLine("[DRY RUN] Claude Code agent -> " + path);
                return;
            }

            Directory.CreateDirectory(dir);
            File.WriteAllText(path, content);
            Console.WriteLine("Configured Claude Code agent -> " + path);
        }

        private static void InstallJsonServer(string path, string label, JsonObject entry, bool dryRun)
        {
            JsonObject root = ReadJsonObject(path);
            JsonObject servers = GetOrCreateObject(root, "mcpServers");
            servers[ServerName] = entry;
            WriteJson(path, root, label, dryRun);
        }

        private static void InstallMux(string path, string baseUrl, string token, bool dryRun)
        {
            if (!IsMuxAvailable(path))
            {
                Console.WriteLine("Skipped Mux (not detected; no ~/.mux directory and no 'mux' on PATH).");
                return;
            }

            JsonObject root = ReadJsonObject(path);
            JsonArray servers = root["servers"] as JsonArray;
            if (servers == null)
            {
                servers = new JsonArray();
                root["servers"] = servers;
            }

            for (int i = servers.Count - 1; i >= 0; i--)
            {
                JsonObject item = servers[i] as JsonObject;
                if (item != null && item["name"] != null && item["name"].GetValue<string>() == ServerName)
                    servers.RemoveAt(i);
            }

            servers.Add(new JsonObject
            {
                ["name"] = ServerName,
                ["transport"] = "http",
                ["url"] = baseUrl,
                ["mcpPath"] = "/mcp",
                ["auth"] = new JsonObject { ["type"] = "bearer", ["bearerToken"] = token }
            });

            WriteJson(path, root, "Mux", dryRun);
        }

        private static void InstallCodex(string path, string mcpUrl, string token, bool dryRun)
        {
            string existing = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            string stripped = RemoveTomlSection(existing, "[mcp_servers." + ServerName + "]");

            StringBuilder sb = new StringBuilder();
            sb.Append(stripped.TrimEnd());
            if (sb.Length > 0) sb.Append(Environment.NewLine).Append(Environment.NewLine);
            sb.Append("[mcp_servers.").Append(ServerName).Append("]").Append(Environment.NewLine);
            sb.Append("url = \"").Append(mcpUrl).Append("\"").Append(Environment.NewLine);
            sb.Append("http_headers = { \"Authorization\" = \"Bearer ").Append(token).Append("\" }").Append(Environment.NewLine);

            string content = sb.ToString();

            if (dryRun)
            {
                Console.WriteLine("[DRY RUN] Codex CLI -> " + path);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content);
            Console.WriteLine("Configured Codex CLI -> " + path);
        }

        private static void UpsertInstructionFile(string path, string token, bool dryRun)
        {
            string block = BuildInstructionBlock(token);
            string existing = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            string updated = UpsertManagedBlock(existing, block);

            if (dryRun)
            {
                Console.WriteLine("[DRY RUN] Instructions -> " + path);
                return;
            }

            File.WriteAllText(path, updated);
            Console.WriteLine("Configured instructions -> " + path);
        }

        #endregion

        #region Uninstall

        private static void Uninstall(bool dryRun, string only)
        {
            Console.WriteLine(dryRun ? "[DRY RUN] Previewing RecallDB MCP uninstall..." : "Removing RecallDB MCP configuration...");
            Console.WriteLine();

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string cwd = Directory.GetCurrentDirectory();

            if (Included(only, "claude"))
            {
                RemoveJsonServer(Path.Combine(home, ".claude.json"), "Claude Code", dryRun);
                RemoveFile(Path.Combine(home, ".claude", "agents", "recalldb.md"), "Claude Code agent", dryRun);
            }
            if (Included(only, "cursor"))
                RemoveJsonServer(Path.Combine(cwd, ".cursor", "mcp.json"), "Cursor", dryRun);
            if (Included(only, "gemini"))
                RemoveJsonServer(Path.Combine(home, ".gemini", "settings.json"), "Gemini CLI", dryRun);
            if (Included(only, "codex"))
                RemoveCodex(Path.Combine(home, ".codex", "config.toml"), dryRun);
            if (Included(only, "mux"))
                RemoveMux(GetMuxServersPath(home), dryRun);
            if (Included(only, "agents"))
                RemoveManagedBlockFile(Path.Combine(cwd, "AGENTS.md"), dryRun);
            if (Included(only, "gemini"))
                RemoveManagedBlockFile(Path.Combine(cwd, "GEMINI.md"), dryRun);

            Console.WriteLine();
            Console.WriteLine(dryRun ? "[DRY RUN] No files were modified." : "Done.");
        }

        private static void RemoveJsonServer(string path, string label, bool dryRun)
        {
            if (!File.Exists(path)) return;
            JsonObject root = ReadJsonObject(path);
            JsonObject servers = root["mcpServers"] as JsonObject;
            if (servers == null || !servers.ContainsKey(ServerName)) return;

            servers.Remove(ServerName);
            if (servers.Count == 0) root.Remove("mcpServers");
            WriteJson(path, root, label, dryRun);
        }

        private static void RemoveMux(string path, bool dryRun)
        {
            if (!File.Exists(path)) return;
            JsonObject root = ReadJsonObject(path);
            JsonArray servers = root["servers"] as JsonArray;
            if (servers == null) return;

            bool changed = false;
            for (int i = servers.Count - 1; i >= 0; i--)
            {
                JsonObject item = servers[i] as JsonObject;
                if (item != null && item["name"] != null && item["name"].GetValue<string>() == ServerName)
                {
                    servers.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed) WriteJson(path, root, "Mux", dryRun);
        }

        private static void RemoveCodex(string path, bool dryRun)
        {
            if (!File.Exists(path)) return;
            string stripped = RemoveTomlSection(File.ReadAllText(path), "[mcp_servers." + ServerName + "]");
            if (dryRun)
            {
                Console.WriteLine("[DRY RUN] Codex CLI -> " + path);
                return;
            }
            File.WriteAllText(path, stripped.TrimEnd() + Environment.NewLine);
            Console.WriteLine("Removed from Codex CLI -> " + path);
        }

        private static void RemoveManagedBlockFile(string path, bool dryRun)
        {
            if (!File.Exists(path)) return;
            string content = File.ReadAllText(path);
            string updated = RemoveManagedBlock(content);
            if (updated == content) return;

            if (dryRun)
            {
                Console.WriteLine("[DRY RUN] Instructions -> " + path);
                return;
            }

            if (string.IsNullOrWhiteSpace(updated)) File.Delete(path);
            else File.WriteAllText(path, updated);
            Console.WriteLine("Removed instructions -> " + path);
        }

        private static void RemoveFile(string path, string label, bool dryRun)
        {
            if (!File.Exists(path)) return;
            if (dryRun)
            {
                Console.WriteLine("[DRY RUN] " + label + " -> " + path);
                return;
            }
            File.Delete(path);
            Console.WriteLine("Removed " + label + " -> " + path);
        }

        #endregion

        #region Print

        private static void Print(string mcpUrl, string baseUrl, string token)
        {
            Console.WriteLine("RecallDB MCP endpoint: " + mcpUrl);
            Console.WriteLine("Bearer token:          " + token);
            Console.WriteLine();
            Console.WriteLine("Claude Code (~/.claude.json) / Cursor (.cursor/mcp.json):");
            Console.WriteLine("  \"" + ServerName + "\": { \"type\": \"http\", \"url\": \"" + mcpUrl + "\", \"headers\": { \"Authorization\": \"Bearer " + token + "\" } }");
            Console.WriteLine();
            Console.WriteLine("Gemini CLI (~/.gemini/settings.json):");
            Console.WriteLine("  \"" + ServerName + "\": { \"httpUrl\": \"" + mcpUrl + "\", \"headers\": { \"Authorization\": \"Bearer " + token + "\" } }");
            Console.WriteLine();
            Console.WriteLine("Codex CLI (~/.codex/config.toml):");
            Console.WriteLine("  [mcp_servers." + ServerName + "]");
            Console.WriteLine("  url = \"" + mcpUrl + "\"");
            Console.WriteLine("  http_headers = { \"Authorization\" = \"Bearer " + token + "\" }");
            Console.WriteLine();
            Console.WriteLine("Mux (~/.mux/mcp-servers.json, or /mcp add in the TUI):");
            Console.WriteLine("  name=" + ServerName + " transport=http url=" + baseUrl + " mcpPath=/mcp auth=bearer token=" + token);
        }

        private static void PrintUsage()
        {
            Console.WriteLine("RecallDB MCP installer");
            Console.WriteLine();
            Console.WriteLine("Usage: recalldb mcp <command> [options]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  install      Configure Claude Code, Cursor, Gemini, Codex, and Mux (if detected)");
            Console.WriteLine("  uninstall    Remove RecallDB entries from all harnesses");
            Console.WriteLine("  print        Print config snippets for manual setup");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --dry-run           Preview changes without writing files");
            Console.WriteLine("  --token <token>     Bearer token to embed (default: first admin API key)");
            Console.WriteLine("  --only <harness>    Limit to one: claude | cursor | gemini | codex | mux | agents");
        }

        #endregion

        #region Content-Builders

        private static JsonObject BuildUrlHeaderEntry(string mcpUrl, string token, bool gemini)
        {
            JsonObject entry = new JsonObject();
            if (gemini) entry["httpUrl"] = mcpUrl;
            else entry["url"] = mcpUrl;
            entry["headers"] = new JsonObject { ["Authorization"] = "Bearer " + token };
            return entry;
        }

        private static string BuildClaudeAgent(string token)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("---");
            sb.AppendLine("name: recalldb");
            sb.AppendLine("description: RecallDB vector database agent — manage tenants, collections, documents, labels, tags, and run vector/full-text search via the RecallDB MCP tools.");
            sb.AppendLine("allowedTools:");
            sb.AppendLine("  - mcp__recalldb__*");
            sb.AppendLine("---");
            sb.AppendLine();
            sb.Append(BuildAgentBody(token));
            return sb.ToString();
        }

        private static string BuildInstructionBlock(string token)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(_ManagedBegin);
            sb.AppendLine("## RecallDB MCP");
            sb.AppendLine();
            sb.Append(BuildAgentBody(token));
            sb.Append(_ManagedEnd);
            return sb.ToString();
        }

        private static string BuildAgentBody(string token)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("RecallDB is a multi-tenant vector database exposed over MCP. The tools are named");
            sb.AppendLine("`tenant/*`, `user/*`, `credential/*`, `collection/*`, `document/*`, `label/*`, `tag/*`,");
            sb.AppendLine("`search/query`, `requestHistory/*`, plus `auth/authenticate` and `server/info`.");
            sb.AppendLine();
            sb.AppendLine("IMPORTANT: every authenticated RecallDB tool call must include a `bearerToken` argument.");
            sb.AppendLine("Use this token unless told otherwise:");
            sb.AppendLine();
            sb.AppendLine("    bearerToken = \"" + token + "\"");
            sb.AppendLine();
            sb.AppendLine("Guidelines:");
            sb.AppendLine("- Listing is always paginated: use `*/enumerate` with an optional `query` (an EnumerationQuery");
            sb.AppendLine("  JSON string, e.g. `{\"MaxResults\":50}`); page with the returned `ContinuationToken`.");
            sb.AppendLine("- Identifiers are camelCase strings: `tenantId`, `collectionId`, `documentKey`, etc.");
            sb.AppendLine("- Complex bodies are passed as JSON strings: `tenant`, `document`, `search`, `query`, etc.");
            sb.AppendLine("- Use `server/info` to confirm connectivity; it needs no token.");
            sb.AppendLine();
            return sb.ToString();
        }

        #endregion

        #region Helpers

        private static bool Included(string only, string harness)
        {
            return string.IsNullOrEmpty(only) || string.Equals(only, harness, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasFlag(IReadOnlyList<string> args, string flag)
        {
            if (args == null) return false;
            foreach (string a in args)
                if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string GetOption(IReadOnlyList<string> args, string name)
        {
            if (args == null) return null;
            for (int i = 0; i < args.Count - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        private static string GetMuxServersPath(string home)
        {
            string dir = Environment.GetEnvironmentVariable("MUX_CONFIG_DIR");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(home, ".mux");
            return Path.Combine(dir, "mcp-servers.json");
        }

        private static bool IsMuxAvailable(string muxServersPath)
        {
            string dir = Path.GetDirectoryName(muxServersPath);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) return true;

            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string segment in pathEnv.Split(Path.PathSeparator))
            {
                if (string.IsNullOrEmpty(segment)) continue;
                foreach (string candidate in new[] { "mux", "mux.exe", "mux.cmd", "mux.bat" })
                {
                    try
                    {
                        if (File.Exists(Path.Combine(segment, candidate))) return true;
                    }
                    catch { }
                }
            }
            return false;
        }

        private static JsonObject ReadJsonObject(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    string content = File.ReadAllText(path);
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        JsonNode node = JsonNode.Parse(content);
                        if (node is JsonObject obj) return obj;
                    }
                }
                catch { }
            }
            return new JsonObject();
        }

        private static JsonObject GetOrCreateObject(JsonObject root, string key)
        {
            if (root[key] is JsonObject existing) return existing;
            JsonObject created = new JsonObject();
            root[key] = created;
            return created;
        }

        private static void WriteJson(string path, JsonObject root, string label, bool dryRun)
        {
            string output = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            if (dryRun)
            {
                Console.WriteLine("[DRY RUN] " + label + " -> " + path);
                return;
            }

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, output);
            Console.WriteLine("Configured " + label + " -> " + path);
        }

        private static string RemoveTomlSection(string content, string header)
        {
            if (string.IsNullOrEmpty(content)) return string.Empty;

            string[] lines = content.Replace("\r\n", "\n").Split('\n');
            StringBuilder sb = new StringBuilder();
            bool skipping = false;

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed == header)
                {
                    skipping = true;
                    continue;
                }
                if (skipping)
                {
                    // A new top-level table header ends the section we are skipping.
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                        skipping = false;
                    else
                        continue;
                }
                sb.Append(line).Append(Environment.NewLine);
            }

            return sb.ToString();
        }

        private static string UpsertManagedBlock(string content, string block)
        {
            if (string.IsNullOrEmpty(content))
                return block + Environment.NewLine;

            int start = content.IndexOf(_ManagedBegin, StringComparison.Ordinal);
            int end = content.IndexOf(_ManagedEnd, StringComparison.Ordinal);
            if (start >= 0 && end > start)
            {
                string before = content.Substring(0, start);
                string after = content.Substring(end + _ManagedEnd.Length);
                return before + block + after;
            }

            return content.TrimEnd() + Environment.NewLine + Environment.NewLine + block + Environment.NewLine;
        }

        private static string RemoveManagedBlock(string content)
        {
            if (string.IsNullOrEmpty(content)) return content;
            int start = content.IndexOf(_ManagedBegin, StringComparison.Ordinal);
            int end = content.IndexOf(_ManagedEnd, StringComparison.Ordinal);
            if (start >= 0 && end > start)
            {
                string before = content.Substring(0, start).TrimEnd();
                string after = content.Substring(end + _ManagedEnd.Length).TrimStart();
                if (before.Length == 0) return after;
                if (after.Length == 0) return before + Environment.NewLine;
                return before + Environment.NewLine + Environment.NewLine + after;
            }
            return content;
        }

        #endregion
    }
}
