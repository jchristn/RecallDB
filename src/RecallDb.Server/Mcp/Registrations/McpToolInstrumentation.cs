namespace RecallDb.Server.Mcp.Registrations
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;

    using Voltaic.Core;
    using Voltaic.Mcp;

    using RecallDb.Server.Mcp;
    using RecallDb.Server.Observability;
    using RecallDb.Server.Services;

    /// <summary>
    /// Wraps MCP tool registration with telemetry so every tool invocation is measured and traced uniformly. Each
    /// registration file calls <c>server.RegisterInstrumentedTool(...)</c> in place of <c>server.RegisterTool(...)</c>;
    /// the wrapper opens a server span (which parents any downstream database spans), times the call, classifies the
    /// outcome, and records both the MCP transport metrics and the unified application-operation metrics. The tool
    /// handler bodies are untouched.
    /// </summary>
    public static class McpToolInstrumentation
    {
        #region Public-Methods

        /// <summary>
        /// Register an asynchronous MCP tool with telemetry instrumentation.
        /// </summary>
        /// <param name="server">MCP HTTP server.</param>
        /// <param name="name">Tool name.</param>
        /// <param name="description">Tool description.</param>
        /// <param name="schema">Tool input schema.</param>
        /// <param name="handler">Asynchronous handler.</param>
        public static void RegisterInstrumentedTool(this McpHttpServer server, string name, string description, object schema, Func<RpcParameters, Task<object>> handler)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            server.RegisterTool(name, description, schema, async (RpcParameters args) =>
            {
                using (Activity activity = ServerTelemetry.ActivitySource.StartActivity("mcp " + name, ActivityKind.Server))
                {
                    Prime(activity, name);
                    long start = Stopwatch.GetTimestamp();
                    ServerTelemetry.McpActiveAdd(1, name);
                    int statusCode = 200;
                    string outcome = "success";
                    try
                    {
                        object result = await handler(args).ConfigureAwait(false);
                        activity?.SetStatus(ActivityStatusCode.Ok);
                        return result;
                    }
                    catch (Exception e)
                    {
                        outcome = Classify(e, out statusCode);
                        RecallDb.Core.Observability.RecallDbTelemetry.RecordException(activity, e);
                        throw;
                    }
                    finally
                    {
                        Finish(name, outcome, statusCode, start);
                    }
                }
            });
        }

        /// <summary>
        /// Register a synchronous MCP tool with telemetry instrumentation.
        /// </summary>
        /// <param name="server">MCP HTTP server.</param>
        /// <param name="name">Tool name.</param>
        /// <param name="description">Tool description.</param>
        /// <param name="schema">Tool input schema.</param>
        /// <param name="handler">Synchronous handler.</param>
        public static void RegisterInstrumentedTool(this McpHttpServer server, string name, string description, object schema, Func<RpcParameters, object> handler)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            server.RegisterTool(name, description, schema, (RpcParameters args) =>
            {
                using (Activity activity = ServerTelemetry.ActivitySource.StartActivity("mcp " + name, ActivityKind.Server))
                {
                    Prime(activity, name);
                    long start = Stopwatch.GetTimestamp();
                    ServerTelemetry.McpActiveAdd(1, name);
                    int statusCode = 200;
                    string outcome = "success";
                    try
                    {
                        object result = handler(args);
                        activity?.SetStatus(ActivityStatusCode.Ok);
                        return result;
                    }
                    catch (Exception e)
                    {
                        outcome = Classify(e, out statusCode);
                        RecallDb.Core.Observability.RecallDbTelemetry.RecordException(activity, e);
                        throw;
                    }
                    finally
                    {
                        Finish(name, outcome, statusCode, start);
                    }
                }
            });
        }

        #endregion

        #region Private-Methods

        private static void Prime(Activity activity, string name)
        {
            if (activity == null) return;
            activity.SetTag("rpc.system", "mcp");
            activity.SetTag("rpc.method", name);
            activity.SetTag(ServerTelemetry.TagOrigin, "mcp");
            activity.SetTag(ServerTelemetry.TagMcpTool, name);
        }

        private static string Classify(Exception e, out int statusCode)
        {
            McpToolException toolException = e as McpToolException;
            statusCode = toolException != null ? toolException.StatusCode : 500;
            return ServerTelemetry.ClassifyOutcome(false, statusCode);
        }

        private static void Finish(string name, string outcome, int statusCode, long start)
        {
            double seconds = Stopwatch.GetElapsedTime(start).TotalSeconds;
            OperationScope scope = OperationScopeMap.Resolve(name);
            ServerTelemetry.RecordMcp(name, scope.ResourceType, scope.Operation, outcome, statusCode, seconds);
            ServerTelemetry.McpActiveAdd(-1, name);
        }

        #endregion
    }
}
