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
    /// Registers search MCP tools (query).
    /// </summary>
    public static class SearchRegistrations
    {
        #region Public-Methods

        /// <summary>
        /// Register search tools on the MCP HTTP server.
        /// </summary>
        /// <param name="server">MCP HTTP server.</param>
        /// <param name="ctx">Tool context.</param>
        public static void Register(McpHttpServer server, McpToolContext ctx)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            server.RegisterInstrumentedTool(
                "search/query",
                "Execute a vector, full-text, or hybrid search within a collection, with optional neighbor enrichment. Supply the SearchQuery as a JSON string.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        collectionId = new { type = "string", description = "Collection ID." },
                        search = new { type = "string", description = "SearchQuery serialized as a JSON string." }
                    },
                    required = new[] { "bearerToken", "tenantId", "collectionId", "search" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("search/query", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.CollectionId = McpHelpers.GetStringRequired(args, "collectionId");
                    c.Search = McpHelpers.DeserializeArgRequired<SearchQuery>(args, "search");
                    ServiceResult r = await ctx.Services.Search.SearchAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });
        }

        #endregion
    }
}
