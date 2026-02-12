namespace RecallDb.Core.Enums
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Vector search type enumeration.
    /// </summary>
    public enum SearchTypeEnum
    {
        /// <summary>
        /// Cosine similarity search.
        /// </summary>
        [EnumMember(Value = "CosineSimilarity")]
        CosineSimilarity,

        /// <summary>
        /// Cosine distance search.
        /// </summary>
        [EnumMember(Value = "CosineDistance")]
        CosineDistance,

        /// <summary>
        /// Euclidean similarity search.
        /// </summary>
        [EnumMember(Value = "EuclideanSimilarity")]
        EuclideanSimilarity,

        /// <summary>
        /// Euclidean distance search.
        /// </summary>
        [EnumMember(Value = "EuclideanDistance")]
        EuclideanDistance,

        /// <summary>
        /// Inner product search.
        /// </summary>
        [EnumMember(Value = "InnerProduct")]
        InnerProduct
    }
}
