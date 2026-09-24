namespace RecallDb.Core.Models
{
    using System;
    using System.Text.Json.Serialization;
    using RecallDb.Core.Enums;

    /// <summary>
    /// Hybrid search options: how the vector leg and the full-text leg are combined.
    /// Only used when a search supplies both a Vector query (with embeddings) and a FullText query (with a
    /// non-blank Query); otherwise it is ignored and the response carries a Notice saying so.
    /// The text leg's share is FullText.TextWeight in every strategy.
    /// </summary>
    public class HybridQuery
    {
        #region Public-Members

        /// <summary>
        /// Fusion strategy.
        /// Rrf (default): weighted Reciprocal Rank Fusion over the union of both legs; scores normalized to [0, 1].
        /// Linear: normalized score blend over the union of both legs; scores in [0, 1].
        /// Filter: legacy behavior; the text query is a required filter and scores are the raw blend.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public HybridStrategyEnum Strategy
        {
            get
            {
                return _Strategy;
            }
            set
            {
                if (!Enum.IsDefined(typeof(HybridStrategyEnum), value))
                    throw new ArgumentOutOfRangeException(nameof(Strategy), "Strategy must be one of Rrf, Linear, or Filter.");
                _Strategy = value;
            }
        }

        /// <summary>
        /// The RRF constant k. Larger values flatten the difference between adjacent ranks; smaller values
        /// reward top-ranked documents more strongly. Only used by the Rrf strategy.
        /// Default: 60. Minimum: 1. Maximum: 100000.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is outside 1-100000.</exception>
        public int RrfK
        {
            get
            {
                return _RrfK;
            }
            set
            {
                if (value < 1 || value > 100000)
                    throw new ArgumentOutOfRangeException(nameof(RrfK), "RrfK must be between 1 and 100000.");
                _RrfK = value;
            }
        }

        /// <summary>
        /// Number of candidates each leg retrieves before fusion (Rrf and Linear strategies).
        /// Default: null, which means max(MaxResults x 4, 100) capped at 1000. Minimum: 1. Maximum: 10000.
        /// TotalRecords for a fused search is the size of the fused candidate set, so it is at most
        /// 2 x CandidatePool, and continuation tokens page within that set.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is outside 1-10000.</exception>
        public int? CandidatePool
        {
            get
            {
                return _CandidatePool;
            }
            set
            {
                if (value.HasValue && (value.Value < 1 || value.Value > 10000))
                    throw new ArgumentOutOfRangeException(nameof(CandidatePool), "CandidatePool must be between 1 and 10000.");
                _CandidatePool = value;
            }
        }

        #endregion

        #region Private-Members

        private HybridStrategyEnum _Strategy = HybridStrategyEnum.Rrf;
        private int _RrfK = 60;
        private int? _CandidatePool = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public HybridQuery()
        {
        }

        #endregion
    }
}
