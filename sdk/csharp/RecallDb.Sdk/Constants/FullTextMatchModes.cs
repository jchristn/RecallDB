namespace RecallDb.Sdk.Constants
{
    /// <summary>
    /// Values for FullTextQuery.MatchMode.
    /// </summary>
    public static class FullTextMatchModes
    {
        /// <summary>
        /// Documents containing any meaningful query term, ranked by relevance (default).
        /// </summary>
        public const string Any = "Any";

        /// <summary>
        /// Every query term is required.
        /// </summary>
        public const string All = "All";

        /// <summary>
        /// Terms adjacent and in order.
        /// </summary>
        public const string Phrase = "Phrase";

        /// <summary>
        /// Web-search syntax: &quot;quoted phrase&quot;, or, -exclude.
        /// </summary>
        public const string WebSearch = "WebSearch";
    }
}
