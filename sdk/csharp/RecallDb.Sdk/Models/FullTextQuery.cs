namespace RecallDb.Sdk.Models
{
    /// <summary>
    /// Full-text search query parameters for content relevance scoring.
    /// </summary>
    public class FullTextQuery
    {
        /// <summary>
        /// The search text to match against document content.
        /// Processed by PostgreSQL's text search parser (stemming, stop word removal).
        /// </summary>
        public string Query { get; set; }

        /// <summary>
        /// Text search ranking function to use.
        /// Values: TsRank (term frequency with normalization), TsRankCd (cover density ranking, rewards term proximity).
        /// Default: TsRank.
        /// </summary>
        public string SearchType { get; set; }

        /// <summary>
        /// How the query text is turned into a PostgreSQL tsquery.
        /// Values: Any (a document matches if it contains any of the query's terms),
        /// All (every term is required, the previous behavior, equivalent to plainto_tsquery),
        /// Phrase (terms must appear adjacent and in order, equivalent to phraseto_tsquery),
        /// WebSearch (web-search syntax: "quoted phrase", or, -exclude, equivalent to websearch_to_tsquery).
        /// Default: Any.
        /// </summary>
        public string MatchMode { get; set; }

        /// <summary>
        /// PostgreSQL text search configuration to use (e.g., "english", "simple", "spanish").
        /// Must be a text search configuration installed on the server; unknown values are rejected.
        /// Default: "english".
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// Normalization option for ts_rank scoring (PostgreSQL normalization bitmask).
        /// 0 = none, 1 = log(length), 2 = length, 32 = self+1 (0-1 range).
        /// Default: 32 (normalized to 0-1 range). Minimum: 0. Maximum: 63.
        /// </summary>
        public int Normalization { get; set; }

        /// <summary>
        /// Minimum text relevance score threshold for results.
        /// Documents scoring below this value are excluded.
        /// </summary>
        public double? MinimumScore { get; set; }

        /// <summary>
        /// Weight to apply to the text leg when combining with the vector leg
        /// in hybrid search mode, for every hybrid strategy. Must be between 0.0 and 1.0;
        /// values outside that range are rejected by the server.
        /// The vector weight is computed as (1.0 - TextWeight).
        /// Default: 0.5 (equal weighting).
        /// </summary>
        public double TextWeight { get; set; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public FullTextQuery()
        {
            SearchType = "TsRank";
            MatchMode = "Any";
            Language = "english";
            Normalization = 32;
            TextWeight = 0.5;
        }
    }
}
