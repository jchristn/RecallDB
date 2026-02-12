namespace RecallDb.Sdk.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Vector search query parameters.
    /// </summary>
    public class VectorQuery
    {
        /// <summary>
        /// Type of vector search to perform.
        /// Values: CosineSimilarity, CosineDistance, EuclideanSimilarity, EuclideanDistance, InnerProduct.
        /// Default: CosineSimilarity.
        /// </summary>
        public string SearchType { get; set; }

        /// <summary>
        /// Vector embeddings to search with.
        /// </summary>
        public List<float> Embeddings { get; set; }

        /// <summary>
        /// Minimum score threshold for results.
        /// </summary>
        public double? MinimumScore { get; set; }

        /// <summary>
        /// Maximum score threshold for results.
        /// </summary>
        public double? MaximumScore { get; set; }

        /// <summary>
        /// Minimum distance threshold for results.
        /// </summary>
        public double? MinimumDistance { get; set; }

        /// <summary>
        /// Maximum distance threshold for results.
        /// </summary>
        public double? MaximumDistance { get; set; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public VectorQuery()
        {
            SearchType = "CosineSimilarity";
        }
    }
}
