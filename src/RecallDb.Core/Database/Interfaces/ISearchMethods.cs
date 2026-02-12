namespace RecallDb.Core.Database.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using RecallDb.Core.Models;

    /// <summary>
    /// Interface for vector similarity search operations.
    /// </summary>
    public interface ISearchMethods
    {
        /// <summary>
        /// Perform a vector similarity search within a collection.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="dimensionality">Vector dimensionality of the collection.</param>
        /// <param name="query">Search query parameters including embeddings and filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Search result containing matching documents with scores.</returns>
        Task<SearchResult> SearchAsync(string collectionId, int dimensionality, SearchQuery query, CancellationToken token = default);
    }
}
