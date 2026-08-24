namespace RecallDb.Server.Observability
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Threading;

    using RecallDb.Core.Enums;
    using RecallDb.Core.Observability;
    using RecallDb.Server.Classes;

    /// <summary>
    /// Server telemetry contract: the meter and activity source the transport and application layers emit into.
    /// The names ("RecallDb.Server") are a stable contract the observability host subscribes to by name. As with
    /// the core layer, instrumented code uses only the .NET base class library — the host owns all OpenTelemetry
    /// wiring, so these instruments are no-ops until it starts.
    ///
    /// Coverage is organized into families that map to the Grafana visualization sections:
    /// <list type="bullet">
    /// <item><description><c>recalldb.http.*</c> — REST transport (every inbound HTTP request).</description></item>
    /// <item><description><c>recalldb.mcp.*</c> — MCP transport (every tool invocation).</description></item>
    /// <item><description><c>recalldb.operation.*</c> — unified application layer across both transports.</description></item>
    /// <item><description><c>recalldb.search.*</c> — vector/full-text/hybrid search.</description></item>
    /// </list>
    /// </summary>
    public static class ServerTelemetry
    {
        #region Public-Members

        /// <summary>
        /// Meter name for the server (transport and application) layer.
        /// </summary>
        public const string MeterName = "RecallDb.Server";

        /// <summary>
        /// Activity source name for the server (transport and application) layer.
        /// </summary>
        public const string ActivitySourceName = "RecallDb.Server";

        /// <summary>
        /// Server layer meter.
        /// </summary>
        public static readonly Meter Meter = new Meter(MeterName);

        /// <summary>
        /// Server layer activity source.
        /// </summary>
        public static readonly ActivitySource ActivitySource = new ActivitySource(ActivitySourceName);

        // ----- Tag keys (dotted, lowercase; low cardinality only) -----

        /// <summary>Tag key: originating transport (rest or mcp).</summary>
        public const string TagOrigin = "recalldb.origin";
        /// <summary>Tag key: resource type.</summary>
        public const string TagResource = "recalldb.resource";
        /// <summary>Tag key: operation type.</summary>
        public const string TagOperation = "recalldb.operation";
        /// <summary>Tag key: request type / tool name (e.g. tenant/read).</summary>
        public const string TagRequestType = "recalldb.request_type";
        /// <summary>Tag key: outcome classification (success, client_error, error).</summary>
        public const string TagOutcome = "recalldb.outcome";
        /// <summary>Tag key: HTTP-equivalent status code.</summary>
        public const string TagStatusCode = "recalldb.status_code";
        /// <summary>Tag key: HTTP request method.</summary>
        public const string TagHttpMethod = "http.request.method";
        /// <summary>Tag key: HTTP response status code.</summary>
        public const string TagHttpStatus = "http.response.status_code";
        /// <summary>Tag key: HTTP status class (2xx, 4xx, 5xx).</summary>
        public const string TagHttpStatusClass = "http.response.status_class";
        /// <summary>Tag key: MCP tool name.</summary>
        public const string TagMcpTool = "mcp.tool";
        /// <summary>Tag key: search mode.</summary>
        public const string TagSearchMode = "recalldb.search.mode";

        // ----- HTTP transport instruments -----

        /// <summary>Duration of an inbound HTTP request, in seconds.</summary>
        public static readonly Histogram<double> HttpRequestDuration = Meter.CreateHistogram<double>(
            "recalldb.http.server.request.duration", "s", "Duration of inbound HTTP requests.");

        /// <summary>Count of inbound HTTP requests.</summary>
        public static readonly Counter<long> HttpRequests = Meter.CreateCounter<long>(
            "recalldb.http.server.requests", "{request}", "Count of inbound HTTP requests.");

        /// <summary>Inbound HTTP requests currently in flight.</summary>
        public static readonly UpDownCounter<long> HttpActiveRequests = Meter.CreateUpDownCounter<long>(
            "recalldb.http.server.active_requests", "{request}", "Inbound HTTP requests currently in flight.");

        // ----- Unified application operation instruments (both transports) -----

        /// <summary>Duration of an application-layer operation, in seconds.</summary>
        public static readonly Histogram<double> OperationDuration = Meter.CreateHistogram<double>(
            "recalldb.operation.duration", "s", "Duration of an application-layer operation.");

        /// <summary>Count of application-layer operations.</summary>
        public static readonly Counter<long> Operations = Meter.CreateCounter<long>(
            "recalldb.operation.count", "{operation}", "Count of application-layer operations.");

        /// <summary>Application-layer operations currently in flight.</summary>
        public static readonly UpDownCounter<long> ActiveOperations = Meter.CreateUpDownCounter<long>(
            "recalldb.operation.active", "{operation}", "Application-layer operations currently in flight.");

        // ----- MCP transport instruments -----

        /// <summary>Duration of an MCP tool invocation, in seconds.</summary>
        public static readonly Histogram<double> McpToolDuration = Meter.CreateHistogram<double>(
            "recalldb.mcp.tool.duration", "s", "Duration of an MCP tool invocation.");

        /// <summary>Count of MCP tool invocations.</summary>
        public static readonly Counter<long> McpInvocations = Meter.CreateCounter<long>(
            "recalldb.mcp.tool.invocations", "{invocation}", "Count of MCP tool invocations.");

        /// <summary>MCP tool invocations currently in flight.</summary>
        public static readonly UpDownCounter<long> McpActiveInvocations = Meter.CreateUpDownCounter<long>(
            "recalldb.mcp.tool.active", "{invocation}", "MCP tool invocations currently in flight.");

        // ----- Search instruments -----

        /// <summary>Duration of a search operation, in seconds.</summary>
        public static readonly Histogram<double> SearchDuration = Meter.CreateHistogram<double>(
            "recalldb.search.duration", "s", "Duration of a search operation.");

        /// <summary>Count of search operations.</summary>
        public static readonly Counter<long> SearchQueries = Meter.CreateCounter<long>(
            "recalldb.search.queries", "{query}", "Count of search operations.");

        /// <summary>Number of documents returned by a search operation.</summary>
        public static readonly Histogram<long> SearchResults = Meter.CreateHistogram<long>(
            "recalldb.search.results", "{document}", "Documents returned by a search operation.");

        #endregion

        #region Private-Members

        private static readonly AsyncLocal<RestScope> _RestScope = new AsyncLocal<RestScope>();

        #endregion

        #region Public-Methods

        /// <summary>
        /// Begin the application-operation scope for a REST request. Starts a server span (when sampled) and records
        /// the in-flight operation. The scope flows through the handler's async context and is completed by
        /// <see cref="CompleteRest"/> from the result-mapping helpers. Metrics are recorded regardless of whether the
        /// trace is sampled.
        /// </summary>
        /// <param name="ctx">Request context (already classified into resource/operation).</param>
        /// <param name="requestType">Request type key (e.g. tenant/read).</param>
        public static void BeginRest(RequestContext ctx, string requestType)
        {
            if (ctx == null) return;

            RestScope scope = new RestScope();
            scope.RequestType = requestType;
            scope.Resource = ctx.ResourceType.ToString();
            scope.Operation = ctx.Operation.ToString();
            scope.StartTimestamp = Stopwatch.GetTimestamp();

            Activity activity = ActivitySource.StartActivity(requestType ?? "rest", ActivityKind.Server);
            if (activity != null)
            {
                activity.SetTag(TagOrigin, "rest");
                activity.SetTag(TagResource, scope.Resource);
                activity.SetTag(TagOperation, scope.Operation);
                activity.SetTag(TagRequestType, requestType);
                scope.Activity = activity;
            }

            ActiveOperations.Add(1, new TagList { { TagOrigin, "rest" }, { TagResource, scope.Resource } });
            _RestScope.Value = scope;
        }

        /// <summary>
        /// Complete the REST application-operation scope opened by <see cref="BeginRest"/>: emit the operation
        /// metrics, close the span, and clear the scope. Safe to call when no scope is active (does nothing).
        /// </summary>
        /// <param name="statusCode">HTTP-equivalent status code of the result.</param>
        /// <param name="success">Whether the operation succeeded.</param>
        public static void CompleteRest(int statusCode, bool success)
        {
            RestScope scope = _RestScope.Value;
            if (scope == null) return;
            _RestScope.Value = null;

            double seconds = Stopwatch.GetElapsedTime(scope.StartTimestamp).TotalSeconds;
            string outcome = ClassifyOutcome(success, statusCode);

            TagList tags = new TagList
            {
                { TagOrigin, "rest" },
                { TagResource, scope.Resource },
                { TagOperation, scope.Operation },
                { TagRequestType, scope.RequestType },
                { TagOutcome, outcome },
                { TagStatusCode, statusCode }
            };

            OperationDuration.Record(seconds, tags);
            Operations.Add(1, tags);
            ActiveOperations.Add(-1, new TagList { { TagOrigin, "rest" }, { TagResource, scope.Resource } });

            if (scope.Activity != null)
            {
                scope.Activity.SetTag(TagStatusCode, statusCode);
                scope.Activity.SetTag(TagOutcome, outcome);
                scope.Activity.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
                scope.Activity.Dispose();
            }
        }

        /// <summary>
        /// Record an inbound HTTP request at the transport layer. Called for every request (including those that do
        /// not reach the application layer, such as health checks).
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="statusCode">HTTP response status code.</param>
        /// <param name="seconds">Request duration in seconds.</param>
        public static void RecordHttp(string method, int statusCode, double seconds)
        {
            TagList tags = new TagList
            {
                { TagHttpMethod, method ?? "UNKNOWN" },
                { TagHttpStatus, statusCode },
                { TagHttpStatusClass, StatusClass(statusCode) }
            };
            HttpRequestDuration.Record(seconds, tags);
            HttpRequests.Add(1, tags);
        }

        /// <summary>
        /// Adjust the in-flight HTTP request gauge.
        /// </summary>
        /// <param name="delta">+1 on entry, -1 on completion.</param>
        /// <param name="method">HTTP method.</param>
        public static void HttpActiveAdd(int delta, string method)
        {
            HttpActiveRequests.Add(delta, new TagList { { TagHttpMethod, method ?? "UNKNOWN" } });
        }

        /// <summary>
        /// Record an MCP tool invocation at both the MCP transport family and the unified application-operation
        /// family (origin=mcp), so MCP is observable both on its own and alongside REST.
        /// </summary>
        /// <param name="tool">MCP tool name.</param>
        /// <param name="resource">Resource type resolved from the tool name.</param>
        /// <param name="operation">Operation type resolved from the tool name.</param>
        /// <param name="outcome">Outcome classification (success, client_error, error).</param>
        /// <param name="statusCode">HTTP-equivalent status code.</param>
        /// <param name="seconds">Invocation duration in seconds.</param>
        public static void RecordMcp(string tool, ResourceTypeEnum resource, OperationTypeEnum operation, string outcome, int statusCode, double seconds)
        {
            TagList mcpTags = new TagList
            {
                { TagMcpTool, tool },
                { TagOutcome, outcome },
                { TagStatusCode, statusCode }
            };
            McpToolDuration.Record(seconds, mcpTags);
            McpInvocations.Add(1, mcpTags);

            TagList opTags = new TagList
            {
                { TagOrigin, "mcp" },
                { TagResource, resource.ToString() },
                { TagOperation, operation.ToString() },
                { TagRequestType, tool },
                { TagOutcome, outcome },
                { TagStatusCode, statusCode }
            };
            OperationDuration.Record(seconds, opTags);
            Operations.Add(1, opTags);
        }

        /// <summary>
        /// Adjust the in-flight MCP invocation gauge.
        /// </summary>
        /// <param name="delta">+1 on entry, -1 on completion.</param>
        /// <param name="tool">MCP tool name.</param>
        public static void McpActiveAdd(int delta, string tool)
        {
            McpActiveInvocations.Add(delta, new TagList { { TagMcpTool, tool } });
        }

        /// <summary>
        /// Record a search operation.
        /// </summary>
        /// <param name="origin">Originating transport (rest or mcp).</param>
        /// <param name="mode">Search mode (vector, fulltext, hybrid, or unknown).</param>
        /// <param name="success">Whether the search succeeded.</param>
        /// <param name="statusCode">HTTP-equivalent status code.</param>
        /// <param name="seconds">Search duration in seconds.</param>
        /// <param name="resultCount">Number of documents returned.</param>
        public static void RecordSearch(string origin, string mode, bool success, int statusCode, double seconds, int resultCount)
        {
            string outcome = ClassifyOutcome(success, statusCode);
            TagList tags = new TagList
            {
                { TagOrigin, origin },
                { TagSearchMode, mode ?? "unknown" },
                { TagOutcome, outcome }
            };
            SearchDuration.Record(seconds, tags);
            SearchQueries.Add(1, tags);
            if (success && resultCount >= 0)
                SearchResults.Record(resultCount, new TagList { { TagOrigin, origin }, { TagSearchMode, mode ?? "unknown" } });
        }

        /// <summary>
        /// Classify an outcome into success, client_error (4xx), or error (5xx or exception).
        /// </summary>
        /// <param name="success">Whether the operation reported success.</param>
        /// <param name="statusCode">HTTP-equivalent status code.</param>
        /// <returns>Outcome label.</returns>
        public static string ClassifyOutcome(bool success, int statusCode)
        {
            if (success) return "success";
            if (statusCode >= 500) return "error";
            return "client_error";
        }

        #endregion

        #region Private-Methods

        private static string StatusClass(int statusCode)
        {
            if (statusCode >= 500) return "5xx";
            if (statusCode >= 400) return "4xx";
            if (statusCode >= 300) return "3xx";
            if (statusCode >= 200) return "2xx";
            return "1xx";
        }

        #endregion

        #region Private-Types

        private sealed class RestScope
        {
            public string RequestType;
            public string Resource;
            public string Operation;
            public long StartTimestamp;
            public Activity Activity;
        }

        #endregion
    }
}
