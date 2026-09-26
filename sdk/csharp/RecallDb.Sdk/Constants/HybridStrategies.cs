namespace RecallDb.Sdk.Constants
{
    /// <summary>
    /// Values for HybridQuery.Strategy.
    /// </summary>
    public static class HybridStrategies
    {
        /// <summary>
        /// Weighted Reciprocal Rank Fusion over the union of both legs; scores normalized to 0.0-1.0 (default).
        /// </summary>
        public const string Rrf = "Rrf";

        /// <summary>
        /// Normalized linear blend over the union of both legs; scores in 0.0-1.0.
        /// </summary>
        public const string Linear = "Linear";

        /// <summary>
        /// Legacy behavior: the text query is a required filter and scores are the raw blend.
        /// </summary>
        public const string Filter = "Filter";
    }
}
