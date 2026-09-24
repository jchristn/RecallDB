namespace RecallDb.Core.Enums
{
    using System.Runtime.Serialization;

    /// <summary>
    /// How a full-text query string is turned into a PostgreSQL tsquery, which decides which documents match.
    /// </summary>
    public enum TextMatchModeEnum
    {
        /// <summary>
        /// Match documents containing ANY of the query's meaningful terms (OR semantics). The query is normalized
        /// with to_tsvector, so stemming and stop-word removal agree exactly with the index, and user-supplied
        /// tsquery operators are neutralized. Ranking rewards documents that match more (and denser) terms.
        /// This is the default and the right choice for natural-language questions.
        /// </summary>
        [EnumMember(Value = "Any")]
        Any,

        /// <summary>
        /// Match only documents containing EVERY term (plainto_tsquery, AND semantics).
        /// This was the only behavior before MatchMode existed.
        /// </summary>
        [EnumMember(Value = "All")]
        All,

        /// <summary>
        /// Match documents containing the terms adjacent and in order (phraseto_tsquery).
        /// </summary>
        [EnumMember(Value = "Phrase")]
        Phrase,

        /// <summary>
        /// Google-style syntax (websearch_to_tsquery): "quoted phrase", or, and -exclude.
        /// Parsed by PostgreSQL's safe parser, so malformed input never raises a syntax error.
        /// </summary>
        [EnumMember(Value = "WebSearch")]
        WebSearch
    }
}
