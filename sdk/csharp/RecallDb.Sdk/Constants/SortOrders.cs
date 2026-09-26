namespace RecallDb.Sdk.Constants
{
    /// <summary>
    /// Values for SearchQuery.SortOrder.
    /// </summary>
    public static class SortOrders
    {
        /// <summary>
        /// Lowest score first.
        /// </summary>
        public const string ScoreAscending = "ScoreAscending";

        /// <summary>
        /// Highest score first (default).
        /// </summary>
        public const string ScoreDescending = "ScoreDescending";

        /// <summary>
        /// Smallest distance first.
        /// </summary>
        public const string DistanceAscending = "DistanceAscending";

        /// <summary>
        /// Largest distance first.
        /// </summary>
        public const string DistanceDescending = "DistanceDescending";

        /// <summary>
        /// Oldest first.
        /// </summary>
        public const string CreatedAscending = "CreatedAscending";

        /// <summary>
        /// Newest first.
        /// </summary>
        public const string CreatedDescending = "CreatedDescending";

        /// <summary>
        /// Lowest text score first.
        /// </summary>
        public const string TextScoreAscending = "TextScoreAscending";

        /// <summary>
        /// Highest text score first.
        /// </summary>
        public const string TextScoreDescending = "TextScoreDescending";
    }
}
