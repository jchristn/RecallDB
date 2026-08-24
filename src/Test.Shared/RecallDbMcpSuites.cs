namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

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

                // 2. server/info (no auth)
                Case("McpServerInfo", "MCP: server/info", async ct =>
                {
                    JsonElement info = await CallAsync("server/info", new { }).ConfigureAwait(false);
                    AssertEqual("RecallDB", GetString(info, "Name"), "server/info Name");
                    AssertNotNullOrEmpty(GetString(info, "Version"), "server/info Version");
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
                    bool exists = await _Mcp.CallAsync<bool>("tenant/exists", new { bearerToken = ApiKey, tenantId = _McpTenantId }).ConfigureAwait(false);
                    AssertTrue(exists, "tenant/exists should be true for created tenant");
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
                }),

                // 15. NEGATIVE: enumerate (admin-only) with invalid token denied
                Case("McpNonAdminEnumerateDenied", "MCP negative: admin-only enumerate denied for invalid token", async ct =>
                {
                    await AssertMcpDenied(
                        () => CallAsync("tenant/enumerate", new { bearerToken = "not-a-real-token" }),
                        "tenant/enumerate with a non-admin/invalid token should be denied");
                }),

                // 16. NEGATIVE: missing required argument
                Case("McpMissingArg", "MCP negative: missing required argument", async ct =>
                {
                    await AssertMcpThrows(
                        () => CallAsync("tenant/read", new { bearerToken = ApiKey }),
                        "tenant/read without tenantId should fail");
                }),

                // 17. NEGATIVE: unknown tenant returns not-found
                Case("McpUnknownTenant", "MCP negative: unknown tenant not found", async ct =>
                {
                    await AssertMcpThrows(
                        () => CallAsync("tenant/read", new { bearerToken = ApiKey, tenantId = "ten_does_not_exist_xyz" }),
                        "tenant/read for a missing tenant should fail");
                }),

                // 18. NEGATIVE: unknown document returns not-found
                Case("McpUnknownDocument", "MCP negative: unknown document not found", async ct =>
                {
                    if (string.IsNullOrEmpty(_McpCollectionId)) return;
                    await AssertMcpThrows(
                        () => CallAsync("document/read", new { bearerToken = ApiKey, tenantId = "default", collectionId = _McpCollectionId, documentKey = "does-not-exist" }),
                        "document/read for a missing document should fail");
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

        private static async Task<JsonElement> CallAsync(string tool, object args)
        {
            return await _Mcp.CallAsync<JsonElement>(tool, args).ConfigureAwait(false);
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
                string text = ex.ToString();
                AssertTrue(
                    text.Contains("403") || text.Contains("401") || text.Contains("Forbidden") || text.Contains("Denied") || text.Contains("Access denied"),
                    message + " (denial detail: " + Truncate(text) + ")");
                return;
            }
            throw new InvalidOperationException(message + " should have been denied.");
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
