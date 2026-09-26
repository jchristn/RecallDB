namespace RecallDb.Sdk.Constants
{
    /// <summary>
    /// Values for VectorQuery.SearchType.
    /// </summary>
    public static class VectorSearchTypes
    {
        /// <summary>
        /// Cosine similarity; higher is better (default).
        /// </summary>
        public const string CosineSimilarity = "CosineSimilarity";

        /// <summary>
        /// Cosine distance; lower is better.
        /// </summary>
        public const string CosineDistance = "CosineDistance";

        /// <summary>
        /// Euclidean similarity; higher is better.
        /// </summary>
        public const string EuclideanSimilarity = "EuclideanSimilarity";

        /// <summary>
        /// Euclidean (L2) distance; lower is better.
        /// </summary>
        public const string EuclideanDistance = "EuclideanDistance";

        /// <summary>
        /// Inner product; higher is better.
        /// </summary>
        public const string InnerProduct = "InnerProduct";
    }
}
