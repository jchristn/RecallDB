namespace RecallDb.Core.Models
{
    using System;
    using System.Text.Json.Serialization;
    using RecallDb.Core.Enums;

    /// <summary>
    /// Full-text search query parameters for content relevance scoring.
    /// </summary>
    public class FullTextQuery
    {
        #region Public-Members

        /// <summary>
        /// The search text to match against document content.
        /// Processed by PostgreSQL's text search parser (stemming, stop word removal).
        /// A null or whitespace-only query means "no full-text": in a request that also has a vector it is
        /// ignored, and on its own it is rejected with 400.
        /// </summary>
        public string Query
        {
            get
            {
                return _Query;
            }
            set
            {
                _Query = value;
            }
        }

        /// <summary>
        /// How the query string is turned into a tsquery, which decides which documents match.
        /// Any (default): documents containing any meaningful term, ranked by relevance.
        /// All: every term required (the behavior before MatchMode existed).
        /// Phrase: terms adjacent and in order.
        /// WebSearch: Google-style syntax ("quoted phrase", or, -exclude).
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TextMatchModeEnum MatchMode
        {
            get
            {
                return _MatchMode;
            }
            set
            {
                if (!Enum.IsDefined(typeof(TextMatchModeEnum), value))
                    throw new ArgumentOutOfRangeException(nameof(MatchMode), "MatchMode must be one of Any, All, Phrase, or WebSearch.");
                _MatchMode = value;
            }
        }

        /// <summary>
        /// Text search ranking function to use.
        /// Default: TsRank.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TextSearchTypeEnum SearchType
        {
            get
            {
                return _SearchType;
            }
            set
            {
                _SearchType = value;
            }
        }

        /// <summary>
        /// PostgreSQL text search configuration to use (e.g., "english", "simple", "spanish").
        /// Default: "english". The server validates the value against the configurations installed in the
        /// database (pg_ts_config) and rejects anything else with 400. Only "english" is served by the
        /// stored, indexed content_tsv column; other configurations work but evaluate without an index.
        /// </summary>
        public string Language
        {
            get
            {
                return _Language;
            }
            set
            {
                _Language = value;
            }
        }

        /// <summary>
        /// Normalization option for ts_rank scoring (PostgreSQL normalization bitmask).
        /// 0 = none, 1 = log(length), 2 = length, 32 = self+1 (0-1 range).
        /// Flags can be combined (the value is a bitmask of 1, 2, 4, 8, 16, and 32).
        /// Default: 32 (normalized to 0-1 range). Minimum: 0. Maximum: 63.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is outside 0-63.</exception>
        public int Normalization
        {
            get
            {
                return _Normalization;
            }
            set
            {
                if (value < 0 || value > 63)
                    throw new ArgumentOutOfRangeException(nameof(Normalization), "Normalization must be between 0 and 63 (the ts_rank normalization bitmask).");
                _Normalization = value;
            }
        }

        /// <summary>
        /// Minimum text relevance score (TextScore) threshold. Applied in SQL, so TotalRecords and pagination
        /// reflect it. In full-text-only search, documents below the threshold are excluded. In hybrid Rrf and
        /// Linear search, it gates the text leg: a document below the threshold gets no text rank but can still
        /// be returned through the vector leg. In hybrid Filter search, documents below it are excluded.
        /// Default: null (no threshold).
        /// </summary>
        public double? MinimumScore
        {
            get
            {
                return _MinimumScore;
            }
            set
            {
                _MinimumScore = value;
            }
        }

        /// <summary>
        /// The text leg's share (w) when combining with the vector leg in hybrid search; the vector leg gets
        /// (1.0 - TextWeight). The meaning is the same in every hybrid strategy: 0.0 ranks by the vector leg only,
        /// 1.0 ranks by the text leg only.
        /// Default: 0.5 (equal weighting). Minimum: 0.0. Maximum: 1.0.
        /// Only used when both Vector and FullText queries are present.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is outside 0.0-1.0 or is not a finite number.</exception>
        public double TextWeight
        {
            get
            {
                return _TextWeight;
            }
            set
            {
                if (double.IsNaN(value) || value < 0.0 || value > 1.0)
                    throw new ArgumentOutOfRangeException(nameof(TextWeight), "TextWeight must be between 0.0 and 1.0.");
                _TextWeight = value;
            }
        }

        #endregion

        #region Private-Members

        private string _Query = null;
        private TextMatchModeEnum _MatchMode = TextMatchModeEnum.Any;
        private TextSearchTypeEnum _SearchType = TextSearchTypeEnum.TsRank;
        private string _Language = "english";
        private int _Normalization = 32;
        private double? _MinimumScore = null;
        private double _TextWeight = 0.5;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public FullTextQuery()
        {
        }

        #endregion
    }
}
