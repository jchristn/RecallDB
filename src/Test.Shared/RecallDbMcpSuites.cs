namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    using Voltaic.Core;
    using Voltaic.Mcp;

    using static Test.Shared.TestHelpers;

    /// <summary>
    /// Integration tests for the MCP (Model Context Protocol) server. Exercises the Streamable HTTP transport with
    /// a Voltaic client, covering positive operations and negative (auth/validation/not-found) paths. The MCP
    /// endpoint defaults to http://127.0.0.1:8620 and can be overridden with RECALLDB_MCP_ENDPOINT.
    /// </summary>
    public static class RecallDbMcpSuites
    {
        #region Shared-State

        private static McpHttpClient _Mcp = null;
        private static string _McpCollectionId = null;
        private static string _McpDocumentKey = null;
        private static string _McpTenantId = null;

        private static string McpEndpoint
        {
            get
            {
                string env = Environment.GetEnvironmentVariable("RECALLDB_MCP_ENDPOINT");
                if (!string.IsNullOrWhiteSpace(env)) return env.TrimEnd('/');
                return "http://127.0.0.1:8620";
            }
        }

        #endregion

        #region Suite

        /// <summary>
        /// The MCP integration test suite.
        /// </summary>
        public static TestSuiteDescriptor Suite { get; } = Build();

        private static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "RecallDbMcp",
                displayName: "RecallDB MCP Integration Tests",
                beforeSuiteAsync: async ct =>
                {
                    _Mcp = new McpHttpClient();
                    await _Mcp.ConnectStreamableAsync(McpEndpoint, "/mcp", ct).ConfigureAwait(false);
                    await _Mcp.CallAsync<JsonElement>("initialize", new
                    {
                        protocolVersion = "2025-11-25",
                        capabilities = new { },
                        clientInfo = new { name = "recalldb-tests", version = "0.2.0" }
                    }).ConfigureAwait(false);
                    try { await _Mcp.NotifyAsync("notifications/initialized", null).ConfigureAwait(false); }
                    catch { }
                },
                afterSuiteAsync: async ct =>
                {
                    // Best-effort cleanup of MCP-created resources.
                    if (_Mcp != null)
                    {
                        if (!string.IsNullOrEmpty(_McpCollectionId))
                        {
                            try
                            {
                                await CallAsync("collection/delete", new { bearerToken = ApiKey, tenantId = "default", collectionId = _McpCollectionId }).ConfigureAwait(false);
                            }
                            catch { }
                        }

                        if (!string.IsNullOrEmpty(_McpTenantId))
                        {
                            try
                            {
                                await CallAsync("tenant/delete", new { bearerToken = ApiKey, tenantId = _McpTenantId }).ConfigureAwait(false);
                            }
                            catch { }
                        }

                        _Mcp.Dispose();
                        _Mcp = null;
                    }
                },
                cases: BuildCases());
        }

        #endregion

        #region Cases

        private static List<TestCaseDescriptor> BuildCases()
        {
            return new List<TestCaseDescriptor>
            {
                // 1. tools/list exposes tools and NO "get all" tool
                Case("McpToolsList", "MCP: tools/list has no GET-ALL tools", async ct =>
                {
                    JsonElement result = await _Mcp.CallAsync<JsonElement>("tools/list", null).ConfigureAwait(false);
                    AssertTrue(result.TryGetProperty("tools", out JsonElement tools), "Response should contain tools");
                    AssertTrue(tools.GetArrayLength() >= 40, "Expected at least 40 MCP tools");

                    int count = 0;
                    foreach (JsonElement tool in tools.EnumerateArray())
                    {
                        string name = GetString(tool, "name");
                        AssertTrue(name != null, "Tool should have a name");
                        AssertFalse(name.EndsWith("/all") || name.EndsWith("/list"), "No GET-ALL tool should be exposed: " + name);
                        count++;
                    }
                    AssertTrue(count > 0, "Expected tools in catalog");
                }),

                // 1b. tools/list publishes only RecallDB's tools: Voltaic 2.x no longer adds demo tools
                Case("McpToolsListOnlyApplicationTools", "MCP: tools/list has no Voltaic demo tools", async ct =>
                {
                    JsonElement result = await _Mcp.CallAsync<JsonElement>("tools/list", null).ConfigureAwait(false);
                    HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
                    foreach (JsonElement tool in GetProperty(result, "tools").EnumerateArray())
                        names.Add(GetString(tool, "name"));

                    foreach (string demo in DemoToolNames)
                        AssertFalse(names.Contains(demo), "Voltaic demo tool should not be published: " + demo);

                    foreach (string name in names)
                        AssertTrue(name.Contains('/'), "Every RecallDB tool is namespaced with '/': " + name);

                    AssertTrue(names.Contains("server/info"), "server/info should be published");
                    AssertTrue(names.Contains("search/query"), "search/query should be published");
                }),

                // 1c. protocol ping returns an empty object, not "pong"
                Case("McpPing", "MCP: ping returns {}", async ct =>
                {
                    await _Mcp.PingAsync(0, ct).ConfigureAwait(false);
                    JsonRpcResponse response = await _Mcp.CallAsync("ping", null, 0, ct).ConfigureAwait(false);
                    AssertTrue(response.Error == null, "ping should not return an error");
                    JsonElement result = ToElement(response.Result);
                    AssertTrue(result.ValueKind == JsonValueKind.Object, "ping result should be an object, got " + result.ValueKind);
                    AssertFalse(result.EnumerateObject().Any(), "ping result should be empty, got " + result.GetRawText());
                }),

                // 1d. tools/call wraps the tool payload in an MCP tool result
                Case("McpToolCallEnvelope", "MCP: tools/call returns a text content block", async ct =>
                {
                    JsonRpcResponse response = await CallToolRawAsync("server/info", new { }).ConfigureAwait(false);
                    AssertTrue(response.Error == null, "server/info should succeed");
                    JsonElement result = ToElement(response.Result);
                    JsonElement content = GetProperty(result, "content");
                    AssertTrue(content.ValueKind == JsonValueKind.Array && content.GetArrayLength() == 1, "content should hold one block");
                    AssertEqual("text", GetString(content[0], "type"), "content[0].type");
                    AssertFalse(GetBool(result, "isError"), "isError should not be set on success");
                    JsonElement info = JsonDocument.Parse(GetString(content[0], "text")).RootElement;
                    AssertEqual("RecallDB", GetString(info, "Name"), "server/info Name inside text block");
                }),

                // 2. server/info (no auth)
                Case("McpServerInfo", "MCP: server/info", async ct =>
                {
                    JsonElement info = await CallAsync("server/info", new { }).ConfigureAwait(false);
                    AssertEqual("RecallDB", GetString(info, "Name"), "server/info Name");
                    AssertNotNullOrEmpty(GetString(info, "Version"), "server/info Version");
                }),

                // 2b. server/info reports search capabilities
                Case("McpServerInfoCapabilities", "MCP: server/info reports search capabilities", async ct =>
                {
                    JsonElement info = await CallAsync("server/info", new { }).ConfigureAwait(false);
                    JsonElement caps = GetProperty(info, "Capabilities");
                    AssertTrue(caps.ValueKind == JsonValueKind.Array, "server/info Capabilities should be an array");
                    List<string> names = caps.EnumerateArray().Select(c => c.GetString()).ToList();
                    foreach (string expected in RecallDb.Core.SearchCapabilities.All)
                        AssertTrue(names.Contains(expected), "server/info Capabilities should contain " + expected);
                }),

                // 3. auth/authenticate with a valid bearer token
                Case("McpAuthenticate", "MCP: auth/authenticate", async ct =>
                {
                    JsonElement resp = await CallAsync("auth/authenticate", new { bearerToken = ApiKey }).ConfigureAwait(false);
                    AssertTrue(GetBool(resp, "Success"), "auth/authenticate should succeed with the admin key");
                }),

                // 4. tenant/create
                Case("McpTenantCreate", "MCP: tenant/create", async ct =>
                {
                    string tenantJson = JsonSerializer.Serialize(new { Name = "MCP Test Tenant" }, JsonOptions);
                    JsonElement tenant = await CallAsync("tenant/create", new { bearerToken = ApiKey, tenant = tenantJson }).ConfigureAwait(false);
                    _McpTenantId = GetString(tenant, "Id");
                    AssertNotNullOrEmpty(_McpTenantId, "Created tenant Id");
                    AssertEqual("MCP Test Tenant", GetString(tenant, "Name"), "Created tenant Name");
                }),

                // 5. tenant/read
                Case("McpTenantRead", "MCP: tenant/read", async ct =>
                {
                    if (string.IsNullOrEmpty(_McpTenantId)) return;
                    JsonElement tenant = await CallAsync("tenant/read", new { bearerToken = ApiKey, tenantId = _McpTenantId }).ConfigureAwait(false);
                    AssertEqual("MCP Test Tenant", GetString(tenant, "Name"), "Read tenant Name");
                }),

                // 6. tenant/exists
                Case("McpTenantExists", "MCP: tenant/exists", async ct =>
                {
                    if (string.IsNullOrEmpty(_McpTenantId)) return;
                    JsonElement exists = await CallAsync("tenant/exists", new { bearerToken = ApiKey, tenantId = _McpTenantId }).ConfigureAwait(false);
                    AssertTrue(exists.ValueKind == JsonValueKind.True, "tenant/exists should be true for created tenant");
                }),

                // 6b. tenant/exists for a missing tenant is false, not an error
                Case("McpTenantExistsFalse", "MCP: tenant/exists false for missing tenant", async ct =>
                {
                    JsonElement exists = await CallAsync("tenant/exists", new { bearerToken = ApiKey, tenantId = "ten_does_not_exist_xyz" }).ConfigureAwait(false);
                    AssertTrue(exists.ValueKind == JsonValueKind.False, "tenant/exists should be false for a missing tenant");
                }),

                // 7. tenant/enumerate returns pagination shape
                Case("McpTenantEnumerate", "MCP: tenant/enumerate", async ct =>
                {
                    JsonElement result = await CallAsync("tenant/enumerate", new { bearerToken = ApiKey, query = "{\"MaxResults\":100}" }).ConfigureAwait(false);
                    AssertTrue(GetBool(result, "Success"), "enumerate Success");
                    AssertTrue(result.TryGetProperty("Objects", out JsonElement objs), "enumerate should contain Objects");
                    AssertTrue(objs.ValueKind == JsonValueKind.Array, "Objects should be an array");
                    AssertTrue(result.TryGetProperty("EndOfResults", out _), "enumerate should contain EndOfResults");
                }),

                // 8. collection/create under default tenant
                Case("McpCollectionCreate", "MCP: collection/create", async ct =>
                {
                    string collectionJson = JsonSerializer.Serialize(new { Name = "MCP Test Collection", Dimensionality = 3 }, JsonOptions);
                    JsonElement col = await CallAsync("collection/create", new { bearerToken = ApiKey, tenantId = "default", collection = collectionJson }).ConfigureAwait(false);
                    _McpCollectionId = GetString(col, "Id");
                    AssertNotNullOrEmpty(_McpCollectionId, "Created collection Id");
                }),

                // 9. document/create
                Case("McpDocumentCreate", "MCP: document/create", async ct =>
                {
                    if (string.IsNullOrEmpty(_McpCollectionId)) return;
                    string docJson = JsonSerializer.Serialize(new
                    {
                        DocumentKey = "mcp-doc-1",
                        Content = "hello from mcp",
                        Embeddings = new List<float> { 0.1f, 0.2f, 0.3f }
                    }, JsonOptions);
                    JsonElement doc = await CallAsync("document/create", new { bearerToken = ApiKey, tenantId = "default", collectionId = _McpCollectionId, document = docJson }).ConfigureAwait(false);
                    _McpDocumentKey = GetString(doc, "DocumentKey");
                    AssertEqual("mcp-doc-1", _McpDocumentKey, "Created document key");
                }),

                // 10. document/enumerate
                Case("McpDocumentEnumerate", "MCP: document/enumerate", async ct =>
                {
                    if (string.IsNullOrEmpty(_McpCollectionId)) return;
                    JsonElement result = await CallAsync("document/enumerate", new { bearerToken = ApiKey, tenantId = "default", collectionId = _McpCollectionId, query = "{\"MaxResults\":10}" }).ConfigureAwait(false);
                    AssertTrue(result.TryGetProperty("Objects", out JsonElement objs), "document enumerate Objects");
                    AssertTrue(objs.GetArrayLength() >= 1, "Expected at least one document");
                }),

                // 11. document/read
                Case("McpDocumentRead", "MCP: document/read", async ct =>
                {
                    if (string.IsNullOrEmpty(_McpCollectionId) || string.IsNullOrEmpty(_McpDocumentKey)) return;
                    JsonElement doc = await CallAsync("document/read", new { bearerToken = ApiKey, tenantId = "default", collectionId = _McpCollectionId, documentKey = _McpDocumentKey }).ConfigureAwait(false);
                    AssertEqual("mcp-doc-1", GetString(doc, "DocumentKey"), "Read document key");
                }),

                // 12. search/query
                Case("McpSearch", "MCP: search/query", async ct =>
                {
                    if (string.IsNullOrEmpty(_McpCollectionId)) return;
                    string searchJson = JsonSerializer.Serialize(new
                    {
                        SearchType = "Vector",
                        Vector = new { Embeddings = new List<float> { 0.1f, 0.2f, 0.3f } },
                        MaxResults = 5
                    }, JsonOptions);
                    JsonElement result = await CallAsync("search/query", new { bearerToken = ApiKey, tenantId = "default", collectionId = _McpCollectionId, search = searchJson }).ConfigureAwait(false);
                    AssertTrue(result.ValueKind == JsonValueKind.Object, "search should return an object");
                }),

                // 12a. search/query hybrid with collapse and recency: hits carry GroupKey, GroupHits, and RecencyRank
                Case("McpSearchHybridCollapseRecency", "MCP: search/query hybrid with collapse and recency", async ct =>
                {
                    if (string.IsNullOrEmpty(_McpCollectionId)) return;
                    string searchJson = JsonSerializer.Serialize(new
                    {
                        Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.1f, 0.2f, 0.3f } },
                        FullText = new { Query = "hello", MatchMode = "Any" },
                        Hybrid = new { Strategy = "Rrf", RecencyWeight = 0.1 },
                        Collapse = new { Field = "DocumentId" },
                        MaxResults = 5
                    }, JsonOptions);
                    JsonElement result = await CallAsync("search/query", new { bearerToken = ApiKey, tenantId = "default", collectionId = _McpCollectionId, search = searchJson }).ConfigureAwait(false);
                    JsonElement docs = GetProperty(result, "Documents");
                    AssertTrue(docs.ValueKind == JsonValueKind.Array && docs.GetArrayLength() > 0, "Collapsed hybrid search should return the MCP document");
                    JsonElement top = docs[0];
                    AssertNotNullOrEmpty(GetProperty(top, "GroupKey").GetString(), "GroupKey");
                    AssertTrue(GetProperty(top, "GroupHits").GetInt32() >= 1, "GroupHits");
                    AssertEqual(1, GetProperty(top, "RecencyRank").GetInt32(), "RecencyRank");
                }),

                // 12b. search/query hybrid (Rrf): fused results carry ranks and a normalized score
                Case("McpSearchHybridRrf", "MCP: search/query hybrid Rrf", async ct =>
                {
                    if (string.IsNullOrEmpty(_McpCollectionId)) return;
                    string searchJson = JsonSerializer.Serialize(new
                    {
                        Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.1f, 0.2f, 0.3f } },
                        FullText = new { Query = "hello", MatchMode = "Any" },
                        Hybrid = new { Strategy = "Rrf", RrfK = 60 },
                        MaxResults = 5
                    }, JsonOptions);
                    JsonElement result = await CallAsync("search/query", new { bearerToken = ApiKey, tenantId = "default", collectionId = _McpCollectionId, search = searchJson }).ConfigureAwait(false);
                    AssertTrue(result.ValueKind == JsonValueKind.Object, "search should return an object");
                    JsonElement docs = GetProperty(result, "Documents");
                    AssertTrue(docs.ValueKind == JsonValueKind.Array && docs.GetArrayLength() > 0, "Hybrid search should return the MCP document");
                    JsonElement top = docs[0];
                    AssertEqual(1, GetProperty(top, "VectorRank").GetInt32(), "VectorRank");
                    AssertEqual(1, GetProperty(top, "TextRank").GetInt32(), "TextRank");
                    AssertTrue(Math.Abs(GetProperty(top, "Score").GetDouble() - 1.0) < 1e-9, "First in both legs scores 1.0");
                }),

                // 13. requestHistory/enumerate (admin)
                Case("McpRequestHistoryEnumerate", "MCP: requestHistory/enumerate", async ct =>
                {
                    JsonElement result = await CallAsync("requestHistory/enumerate", new { bearerToken = ApiKey }).ConfigureAwait(false);
                    AssertTrue(GetBool(result, "Success"), "requestHistory enumerate Success");
                }),

                // 14. NEGATIVE: invalid bearer token is denied
                Case("McpInvalidBearerDenied", "MCP negative: invalid bearer token denied", async ct =>
                {
                    await AssertMcpDenied(
                        () => CallAsync("tenant/read", new { bearerToken = "not-a-real-token", tenantId = "default" }),
                        "tenant/read with an invalid token should be denied");

                    JsonRpcResponse response = await CallToolRawAsync("tenant/read", new { bearerToken = "not-a-real-token", tenantId = "default" }).ConfigureAwait(false);
                    AssertToolFailure(response, 403, "tenant/read with an invalid token");
                }),

                // 15. NEGATIVE: enumerate (admin-only) with invalid token denied
                Case("McpNonAdminEnumerateDenied", "MCP negative: admin-only enumerate denied for invalid token", async ct =>
                {
                    await AssertMcpDenied(
                        () => CallAsync("tenant/enumerate", new { bearerToken = "not-a-real-token" }),
                        "tenant/enumerate with a non-admin/invalid token should be denied");
                }),

                // 16. NEGATIVE: missing required argument is rejected by input-schema validation
                Case("McpMissingArg", "MCP negative: missing required argument", async ct =>
                {
                    JsonRpcResponse response = await CallToolRawAsync("tenant/read", new { bearerToken = ApiKey }).ConfigureAwait(false);
                    AssertRpcError(response, -32602, "tenantId", "tenant/read without tenantId should fail validation");
                }),

                // 16b. NEGATIVE: a tool can no longer be invoked as a bare JSON-RPC method
                Case("McpBareToolCallRejected", "MCP negative: bare tool method returns -32601", async ct =>
                {
                    JsonRpcResponse bare = await _Mcp.CallAsync("server/info", new { }, 0, ct).ConfigureAwait(false);
                    AssertRpcError(bare, -32601, null, "server/info called as a bare method should be method-not-found");

                    JsonRpcResponse bareAuth = await _Mcp.CallAsync("tenant/read", new { bearerToken = ApiKey, tenantId = "default" }, 0, ct).ConfigureAwait(false);
                    AssertRpcError(bareAuth, -32601, null, "tenant/read called as a bare method should be method-not-found");
                }),

                // 16c. NEGATIVE: Voltaic's former demo tools are not callable
                Case("McpDemoToolsNotCallable", "MCP negative: demo tools are not callable", async ct =>
                {
                    foreach (string demo in DemoToolNames)
                    {
                        JsonRpcResponse viaTools = await CallToolRawAsync(demo, new { }).ConfigureAwait(false);
                        AssertRpcError(viaTools, -32602, "not found", "tools/call " + demo + " should report the tool as not found");
                    }

                    foreach (string method in new[] { "getSessions", "getClients", "echo", "getTime" })
                    {
                        JsonRpcResponse bare = await _Mcp.CallAsync(method, new { }, 0, ct).ConfigureAwait(false);
                        AssertRpcError(bare, -32601, null, "bare " + method + " should be method-not-found");
                    }
                }),

                // 16d. NEGATIVE: unknown tool and missing tool name
                Case("McpUnknownTool", "MCP negative: unknown tool and missing name", async ct =>
                {
                    JsonRpcResponse unknown = await CallToolRawAsync("tenant/nope", new { bearerToken = ApiKey }).ConfigureAwait(false);
                    AssertRpcError(unknown, -32602, "not found", "an unknown tool should be rejected");

                    JsonRpcResponse noName = await _Mcp.CallAsync("tools/call", new { arguments = new { } }, 0, ct).ConfigureAwait(false);
                    AssertRpcError(noName, -32602, "name", "tools/call without a name should be rejected");
                }),

                // 16e. NEGATIVE: wrong argument type is rejected by input-schema validation
                Case("McpWrongArgType", "MCP negative: wrong argument type", async ct =>
                {
                    JsonRpcResponse response = await CallToolRawAsync("tenant/read", new { bearerToken = ApiKey, tenantId = 12345 }).ConfigureAwait(false);
                    AssertRpcError(response, -32602, "tenantId", "a numeric tenantId should fail validation");
                }),

                // 16f. Transport auth: an invalid Authorization header is rejected with 401, a valid one is accepted,
                // and the protocol ping still bypasses authentication.
                Case("McpTransportAuthHeader", "MCP: Authorization header gates tools/call, not ping", async ct =>
                {
                    using (McpHttpClient bad = new McpHttpClient())
                    {
                        bad.SetRequestHeader("Authorization", "Bearer not-a-real-token");
                        bool connected = await bad.ConnectStreamableAsync(McpEndpoint, "/mcp", ct).ConfigureAwait(false);
                        AssertTrue(connected, "ping-based connect should bypass authentication");
                        await bad.PingAsync(0, ct).ConfigureAwait(false);

                        bool rejected = false;
                        try
                        {
                            await bad.CallAsync("tools/call", new { name = "server/info", arguments = new { } }, 0, ct).ConfigureAwait(false);
                        }
                        catch (HttpRequestException e)
                        {
                            rejected = e.StatusCode == HttpStatusCode.Unauthorized;
                        }
                        AssertTrue(rejected, "tools/call with an invalid Authorization header should be rejected with 401");
                    }

                    using (McpHttpClient good = new McpHttpClient())
                    {
                        good.SetRequestHeader("Authorization", "Bearer " + ApiKey);
                        await good.ConnectStreamableAsync(McpEndpoint, "/mcp", ct).ConfigureAwait(false);
                        JsonRpcResponse response = await good.CallAsync("tools/call", new { name = "tenant/read", arguments = new { bearerToken = ApiKey, tenantId = "default" } }, 0, ct).ConfigureAwait(false);
                        AssertTrue(response.Error == null, "tools/call with a valid Authorization header should succeed");
                    }
                }),

                // 17. NEGATIVE: unknown tenant returns not-found
                Case("McpUnknownTenant", "MCP negative: unknown tenant not found", async ct =>
                {
                    await AssertMcpThrows(
                        () => CallAsync("tenant/read", new { bearerToken = ApiKey, tenantId = "ten_does_not_exist_xyz" }),
                        "tenant/read for a missing tenant should fail");

                    JsonRpcResponse response = await CallToolRawAsync("tenant/read", new { bearerToken = ApiKey, tenantId = "ten_does_not_exist_xyz" }).ConfigureAwait(false);
                    AssertToolFailure(response, 404, "tenant/read for a missing tenant");
                }),

                // 18. NEGATIVE: unknown document returns not-found
                Case("McpUnknownDocument", "MCP negative: unknown document not found", async ct =>
                {
                    if (string.IsNullOrEmpty(_McpCollectionId)) return;
                    JsonRpcResponse response = await CallToolRawAsync("document/read", new { bearerToken = ApiKey, tenantId = "default", collectionId = _McpCollectionId, documentKey = "does-not-exist" }).ConfigureAwait(false);
                    AssertToolFailure(response, 404, "document/read for a missing document");
                }),

                // 19. document cleanup via MCP delete
                Case("McpDocumentDelete", "MCP: document/delete", async ct =>
                {
                    if (string.IsNullOrEmpty(_McpCollectionId) || string.IsNullOrEmpty(_McpDocumentKey)) return;
                    JsonElement result = await CallAsync("document/delete", new { bearerToken = ApiKey, tenantId = "default", collectionId = _McpCollectionId, documentKey = _McpDocumentKey }).ConfigureAwait(false);
                    AssertTrue(GetBool(result, "Success"), "document/delete should report success");
                })
            };
        }

        #endregion

        #region Helpers

        private static readonly string[] DemoToolNames = new[] { "ping", "echo", "getTime", "getSessions", "getClients" };

        /// <summary>
        /// Invoke a tool through tools/call and return the raw JSON-RPC response without throwing on an RPC error.
        /// </summary>
        private static async Task<JsonRpcResponse> CallToolRawAsync(string tool, object args)
        {
            return await _Mcp.CallAsync("tools/call", new { name = tool, arguments = args }).ConfigureAwait(false);
        }

        /// <summary>
        /// Invoke a tool through tools/call and return its payload, parsed from the single text content block.
        /// Throws when the server answers with a JSON-RPC error or a tool result flagged isError.
        /// </summary>
        private static async Task<JsonElement> CallAsync(string tool, object args)
        {
            JsonRpcResponse response = await CallToolRawAsync(tool, args).ConfigureAwait(false);
            if (response.Error != null)
                throw new InvalidOperationException("RPC Error " + response.Error.Code + ": " + ErrorText(response.Error));

            JsonElement result = ToElement(response.Result);
            JsonElement content = GetProperty(result, "content");
            if (content.ValueKind != JsonValueKind.Array || content.GetArrayLength() < 1)
                throw new InvalidOperationException(tool + " returned no content: " + Truncate(result.GetRawText()));

            string text = GetString(content[0], "text");
            if (GetBool(result, "isError"))
                throw new InvalidOperationException(tool + " returned a tool error: " + text);

            return JsonDocument.Parse(text).RootElement.Clone();
        }

        private static async Task AssertMcpThrows(Func<Task> action, string message)
        {
            bool threw = false;
            try { await action().ConfigureAwait(false); }
            catch { threw = true; }
            AssertTrue(threw, message);
        }

        private static async Task AssertMcpDenied(Func<Task> action, string message)
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                string text = ex.Message;
                AssertTrue(
                    text.Contains("403") || text.Contains("401") || text.Contains("Forbidden") || text.Contains("Denied") || text.Contains("Access denied"),
                    message + " (denial detail: " + Truncate(text) + ")");
                return;
            }
            throw new InvalidOperationException(message + " should have been denied.");
        }

        private static void AssertRpcError(JsonRpcResponse response, int expectedCode, string expectedText, string message)
        {
            AssertTrue(response.Error != null, message + " (expected RPC error " + expectedCode + ", got a result)");
            AssertEqual(expectedCode, response.Error.Code, message + " (error code)");
            if (!string.IsNullOrEmpty(expectedText))
            {
                string text = ErrorText(response.Error);
                AssertTrue(text.Contains(expectedText, StringComparison.OrdinalIgnoreCase), message + " (error text: " + Truncate(text) + ")");
            }
        }

        /// <summary>
        /// Assert a tool failure is a -32603 JSON-RPC error whose message starts with the HTTP-equivalent status
        /// (the text MCP clients show to the model) and whose data carries the same statusCode.
        /// </summary>
        private static void AssertToolFailure(JsonRpcResponse response, int expectedStatus, string message)
        {
            AssertTrue(response.Error != null, message + " should return an RPC error");
            AssertEqual(-32603, response.Error.Code, message + " (error code)");
            AssertTrue(
                response.Error.Message != null && response.Error.Message.StartsWith(expectedStatus + " ", StringComparison.Ordinal),
                message + " should put the status in the error message, got: " + Truncate(response.Error.Message));
            JsonElement data = ToElement(response.Error.Data);
            JsonElement status = GetProperty(data, "statusCode");
            AssertTrue(status.ValueKind == JsonValueKind.Number && status.GetInt32() == expectedStatus, message + " should carry data.statusCode " + expectedStatus + ", got: " + Truncate(data.GetRawText()));
        }

        private static string ErrorText(JsonRpcError error)
        {
            if (error == null) return null;
            string text = error.Message ?? string.Empty;
            if (error.Data != null) text += " | " + JsonSerializer.Serialize(error.Data);
            return text;
        }

        private static JsonElement ToElement(object value)
        {
            if (value is JsonElement element) return element;
            return JsonDocument.Parse(JsonSerializer.Serialize(value)).RootElement.Clone();
        }

        private static JsonElement GetProperty(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object) return default;
            if (element.TryGetProperty(name, out JsonElement value)) return value;
            foreach (JsonProperty prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase)) return prop.Value;
            }
            return default;
        }

        private static string GetString(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object) return null;
            if (element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
            foreach (JsonProperty prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase) && prop.Value.ValueKind == JsonValueKind.String)
                    return prop.Value.GetString();
            }
            return null;
        }

        private static bool GetBool(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object) return false;
            if (element.TryGetProperty(name, out JsonElement value) &&
                (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False))
                return value.GetBoolean();
            foreach (JsonProperty prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase) &&
                    (prop.Value.ValueKind == JsonValueKind.True || prop.Value.ValueKind == JsonValueKind.False))
                    return prop.Value.GetBoolean();
            }
            return false;
        }

        private static void AssertFalse(bool condition, string message)
        {
            AssertTrue(!condition, message);
        }

        private static string Truncate(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= 300 ? value : value.Substring(0, 300);
        }

        private static TestCaseDescriptor Case(string caseId, string displayName, Func<CancellationToken, Task> execute)
        {
            return new TestCaseDescriptor(
                suiteId: "RecallDbMcp",
                caseId: caseId,
                displayName: displayName,
                executeAsync: execute);
        }

        #endregion
    }
}
