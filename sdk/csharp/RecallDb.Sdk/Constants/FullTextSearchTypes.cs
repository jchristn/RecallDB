namespace RecallDb.Sdk.Constants
{
    /// <summary>
    /// Values for FullTextQuery.SearchType.
    /// </summary>
    public static class FullTextSearchTypes
    {
        /// <summary>
        /// PostgreSQL ts_rank (default).
        /// </summary>
        public const string TsRank = "TsRank";

        /// <summary>
        /// PostgreSQL ts_rank_cd (cover density; rewards term proximity).
        /// </summary>
        public const string TsRankCd = "TsRankCd";
    }
}
