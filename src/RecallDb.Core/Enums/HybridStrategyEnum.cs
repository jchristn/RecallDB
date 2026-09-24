namespace RecallDb.Core.Enums
{
    using System.Runtime.Serialization;

    /// <summary>
    /// How a hybrid search combines its vector leg and its full-text leg.
    /// </summary>
    public enum HybridStrategyEnum
    {
        /// <summary>
        /// Weighted Reciprocal Rank Fusion over the union of both legs (default). Each leg retrieves its top
        /// candidates independently (using its own index), then each document scores
        /// ((1 - w) / (k + vectorRank) + w / (k + textRank)) * (k + 1), where a missing rank contributes 0.
        /// Scores are normalized to [0, 1]; a document ranked first in both legs scores 1.0.
        /// A text match is not required.
        /// </summary>
        [EnumMember(Value = "Rrf")]
        Rrf,

        /// <summary>
        /// Normalized linear blend over the union of both legs: (1 - w) * vectorNorm + w * textNorm, where
        /// vectorNorm is the cosine similarity (or a min-max normalization of distance for other metrics) and
        /// textNorm is the text score divided by the best text score among the candidates (0 when not a text match).
        /// Scores are in [0, 1]. A text match is not required.
        /// </summary>
        [EnumMember(Value = "Linear")]
        Linear,

        /// <summary>
        /// Legacy behavior: the text query is a required filter, and documents are ranked by the raw blend
        /// (1 - w) * vectorScore + w * textScore. Combined with MatchMode All, this reproduces the results
        /// RecallDB returned before hybrid strategies existed ("vector ranking within keyword matches").
        /// </summary>
        [EnumMember(Value = "Filter")]
        Filter
    }
}
