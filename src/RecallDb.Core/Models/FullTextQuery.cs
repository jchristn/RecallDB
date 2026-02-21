namespace RecallDb.Core.Models
{
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
        /// Default: "english".
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
        /// Default: 32 (normalized to 0-1 range).
        /// </summary>
        public int Normalization
        {
            get
            {
                return _Normalization;
            }
            set
            {
                _Normalization = value;
            }
        }

        /// <summary>
        /// Minimum text relevance score threshold for results.
        /// Documents scoring below this value are excluded.
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
        /// Weight to apply to the text score when combining with vector score
        /// in hybrid search mode. Must be between 0.0 and 1.0.
        /// The vector weight is computed as (1.0 - TextWeight).
        /// Default: 0.5 (equal weighting).
        /// Only used when both Vector and FullText queries are present.
        /// </summary>
        public double TextWeight
        {
            get
            {
                return _TextWeight;
            }
            set
            {
                _TextWeight = value;
            }
        }

        #endregion

        #region Private-Members

        private string _Query = null;
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
