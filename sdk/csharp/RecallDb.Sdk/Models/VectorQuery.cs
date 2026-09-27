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
        public List<float>? Embeddings { get; set; }

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
        /// Size of the HNSW candidate list (hnsw.ef_search) for this search, which is also the most rows the vector
        /// index can return. Larger values raise recall and allow deeper pages, especially with filters, at some cost
        /// in latency. The server clamps it to 1-1000. Null (the default, omitted on the wire) lets the server choose:
        /// four times the requested page (offset plus MaxResults), at least 100 and at most 1000, for a vector-only
        /// search, and the candidate pool for hybrid and collapsed searches.
        /// Capability: <see cref="Constants.Capabilities.VectorEfSearch"/>.
        /// </summary>
        public int? EfSearch { get; set; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public VectorQuery()
        {
            SearchType = "CosineSimilarity";
        }
    }
}
