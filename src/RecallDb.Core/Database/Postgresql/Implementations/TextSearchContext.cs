namespace RecallDb.Core.Database.Postgresql.Implementations
{
    /// <summary>
    /// The SQL fragments for one full-text query, resolved once per search and shared by every SQL builder in
    /// SearchMethods (full-text-only, and both legs of hybrid search). Not thread-safe; create one per search.
    /// </summary>
    internal class TextSearchContext
    {
        #region Public-Members

        /// <summary>
        /// Normalized (lower-case) text search configuration name, for example "english".
        /// </summary>
        public string Language { get; set; } = SearchMethods.IndexedLanguage;

        /// <summary>
        /// True when the text match can use a GIN index: the stored content_tsv column, or (for english
        /// collections not yet migrated) the legacy expression index.
        /// </summary>
        public bool UsesIndex { get; set; } = false;

        /// <summary>
        /// SQL expression producing the tsquery for the configured match mode.
        /// </summary>
        public string TsQueryExpression { get; set; } = null;

        /// <summary>
        /// SQL boolean condition matching documents against the tsquery ("tsvector @@ tsquery").
        /// </summary>
        public string MatchCondition { get; set; } = null;

        /// <summary>
        /// SQL expression computing the text relevance score (ts_rank or ts_rank_cd).
        /// </summary>
        public string RankExpression { get; set; } = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TextSearchContext()
        {
        }

        #endregion
    }
}
