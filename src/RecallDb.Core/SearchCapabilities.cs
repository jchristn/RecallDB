namespace RecallDb.Core
{
    using System.Collections.Generic;

    /// <summary>
    /// Capability strings the server advertises in GET / and the MCP server_info tool, so clients can detect
    /// search features on servers that report the same version.
    /// </summary>
    public static class SearchCapabilities
    {
        #region Public-Members

        /// <summary>
        /// Hybrid search is the rank-fused union of the vector and text legs (not the legacy text filter).
        /// </summary>
        public const string HybridRrf = "search.hybrid.rrf";

        /// <summary>
        /// Hybrid.RecencyWeight is honored.
        /// </summary>
        public const string HybridRecency = "search.hybrid.recency";

        /// <summary>
        /// SearchQuery.Collapse is honored.
        /// </summary>
        public const string Collapse = "search.collapse";

        /// <summary>
        /// SearchQuery.IncludeEmbeddings is honored.
        /// </summary>
        public const string IncludeEmbeddings = "search.include-embeddings";

        /// <summary>
        /// FullText.MinimumShouldMatch is honored.
        /// </summary>
        public const string FullTextMinimumShouldMatch = "search.fulltext.minimum-should-match";

        /// <summary>
        /// VectorQuery.EfSearch is honored, and vector-only searches raise hnsw.ef_search to cover the page (so they
        /// are no longer limited to 40 hits).
        /// </summary>
        public const string VectorEfSearch = "search.vector.ef-search";

        /// <summary>
        /// LabelFilter.RequiredMode is honored (All, the default, or Any).
        /// </summary>
        public const string LabelFilterRequiredMode = "search.label-filter.required-mode";

        /// <summary>
        /// Every capability this server supports, in a stable order. Returns a new list on each call.
        /// </summary>
        public static List<string> All
        {
            get
            {
                return new List<string>
                {
                    HybridRrf,
                    HybridRecency,
                    Collapse,
                    IncludeEmbeddings,
                    FullTextMinimumShouldMatch,
                    VectorEfSearch,
                    LabelFilterRequiredMode
                };
            }
        }

        #endregion
    }
}
