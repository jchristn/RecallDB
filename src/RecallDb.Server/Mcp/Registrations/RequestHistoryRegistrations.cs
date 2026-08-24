namespace RecallDb.Server.Mcp.Registrations
{
    using System;
    using System.Threading.Tasks;

    using Voltaic.Core;
    using Voltaic.Mcp;

    using RecallDb.Core.Models;
    using RecallDb.Server.Classes;
    using RecallDb.Server.Services;

    /// <summary>
    /// Registers request-history MCP tools (enumerate, read, summary, delete).
    /// </summary>
    public static class RequestHistoryRegistrations
    {
        #region Public-Methods

        /// <summary>
        /// Register request-history tools on the MCP HTTP server.
        /// </summary>
        /// <param name="server">MCP HTTP server.</param>
        /// <param name="ctx">Tool context.</param>
        public static void Register(McpHttpServer server, McpToolContext ctx)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            server.RegisterTool(
                "requestHistory/enumerate",
                "Enumerate request-history entries matching a filter (admin only). Supply an optional RequestHistoryFilter as a JSON string.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        filter = new { type = "string", description = "RequestHistoryFilter serialized as a JSON string." }
                    },
                    required = new[] { "bearerToken" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("requestHistory/enumerate", args, ctx.Authentication).ConfigureAwait(false);
                    c.Payload = McpHelpers.DeserializeArgOptional<RequestHistoryFilter>(args, "filter");
                    ServiceResult r = await ctx.Services.RequestHistory.EnumerateAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterTool(
                "requestHistory/read",
                "Read a request-history entry by GUID (admin only).",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        guid = new { type = "string", description = "Request-history entry GUID." }
                    },
                    required = new[] { "bearerToken", "guid" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("requestHistory/read", args, ctx.Authentication).ConfigureAwait(false);
                    c.ResourceId = McpHelpers.GetStringRequired(args, "guid");
                    ServiceResult r = await ctx.Services.RequestHistory.ReadAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterTool(
                "requestHistory/summary",
                "Time-bucketed request-history summary (admin only).",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        filter = new { type = "string", description = "RequestHistoryFilter serialized as a JSON string." }
                    },
                    required = new[] { "bearerToken" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("requestHistory/summary", args, ctx.Authentication).ConfigureAwait(false);
                    c.Payload = McpHelpers.DeserializeArgOptional<RequestHistoryFilter>(args, "filter");
                    ServiceResult r = await ctx.Services.RequestHistory.SummaryAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterTool(
                "requestHistory/delete",
                "Delete a request-history entry by GUID (admin only).",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        guid = new { type = "string", description = "Request-history entry GUID." }
                    },
                    required = new[] { "bearerToken", "guid" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("requestHistory/delete", args, ctx.Authentication).ConfigureAwait(false);
                    c.ResourceId = McpHelpers.GetStringRequired(args, "guid");
                    ServiceResult r = await ctx.Services.RequestHistory.DeleteAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });
        }

        #endregion
    }
}
