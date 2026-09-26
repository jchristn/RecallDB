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
                "Execute a vector, full-text, or hybrid search within a collection, with optional neighbor enrichment. Supply the SearchQuery as a JSON string. "
                    + "FullText.MatchMode: Any (default; any meaningful term, ranked by relevance), All (every term), Phrase, or WebSearch. "
                    + "When both Vector and FullText are supplied, Hybrid.Strategy chooses the fusion: Rrf (default; rank fusion over the union of both legs, "
                    + "scores in [0, 1]), Linear (normalized score blend), or Filter (legacy; text match required). Hybrid.RrfK (default 60) and "
                    + "Hybrid.CandidatePool (default max(MaxResults x 4, 100), capped at 1000) tune fusion; FullText.TextWeight is the text leg's share. "
                    + "Results include VectorScore, VectorRank, TextRank, and an optional Notice. "
                    + "Hybrid.RecencyWeight (0.0-1.0, default 0, Rrf only) adds a recency signal (newest CreatedUtc per group); hits carry RecencyRank. "
                    + "Collapse {\"Field\":\"DocumentId\"|\"Tag\",\"TagKey\":\"...\",\"CandidatePool\":n} returns one hit per group (best chunk) with GroupKey and GroupHits; "
                    + "MaxResults and TotalRecords then count groups. FullText.MinimumShouldMatch (1-3, Any only) requires that many distinct terms. "
                    + "Hybrid example: {\"Vector\":{\"SearchType\":\"CosineSimilarity\",\"Embeddings\":[0.1,0.2,0.3]},"
                    + "\"FullText\":{\"Query\":\"run the test suite\",\"TextWeight\":0.5},\"Hybrid\":{\"Strategy\":\"Rrf\",\"RecencyWeight\":0.1},"
                    + "\"Collapse\":{\"Field\":\"Tag\",\"TagKey\":\"parentKey\"},\"MaxResults\":10}",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        collectionId = new { type = "string", description = "Collection ID." },
                        search = new { type = "string", description = "SearchQuery serialized as a JSON string (Vector, FullText with MatchMode/MinimumShouldMatch, Hybrid with Strategy/RrfK/CandidatePool/RecencyWeight, Collapse, filters, MaxResults, ContinuationToken)." }
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
