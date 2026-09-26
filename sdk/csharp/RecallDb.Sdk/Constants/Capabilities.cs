namespace RecallDb.Sdk.Constants
{
    /// <summary>
    /// Search capability strings reported by the server in GET / (ServerInfo.Capabilities). Check them with RecallDbClient.SupportsAsync before relying on a feature, because servers that report the same version can differ.
    /// </summary>
    public static class Capabilities
    {
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
    }
}
