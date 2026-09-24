namespace RecallDb.Sdk.Models
{
    /// <summary>
    /// Hybrid search options controlling how the vector and full-text legs of a search are combined.
    /// Only used when a search includes both a vector query with embeddings and a full-text query with a non-blank Query.
    /// </summary>
    public class HybridQuery
    {
        /// <summary>
        /// Fusion strategy.
        /// Values: Rrf (reciprocal rank fusion of independently retrieved vector and text candidates; a text match is not required;
        /// scores are normalized to 0.0-1.0), Linear (weighted sum of normalized vector and text scores over the same union of candidates;
        /// scores are 0.0-1.0), Filter (legacy behavior: the text query is a required filter and scores are a weighted sum of raw scores).
        /// Default: Rrf.
        /// </summary>
        public string Strategy { get; set; }

        /// <summary>
        /// Reciprocal rank fusion constant k. Larger values flatten the difference between top and lower ranks.
        /// Used only by the Rrf strategy.
        /// Default: 60. Minimum: 1. Maximum: 100000.
        /// </summary>
        public int RrfK { get; set; }

        /// <summary>
        /// Number of candidates each leg (vector and text) retrieves before fusion. Used by the Rrf and Linear strategies.
        /// Default: null (the server uses the larger of MaxResults * 4 and 100, capped at 1000). Minimum: 1. Maximum: 10000.
        /// </summary>
        public int? CandidatePool { get; set; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public HybridQuery()
        {
            Strategy = "Rrf";
            RrfK = 60;
            CandidatePool = null;
        }
    }
}
