namespace RecallDb.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using RecallDb.Core.Models;

    /// <summary>
    /// Interface for tag CRUD and enumeration operations, scoped by collection.
    /// </summary>
    public interface ITagMethods
    {
        /// <summary>
        /// Create a new tag record within a collection.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="tag">Tag record to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created tag record.</returns>
        Task<TagRecord> CreateAsync(string collectionId, TagRecord tag, CancellationToken token = default);

        /// <summary>
        /// Read a tag record by collection ID and tag ID.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="id">Tag ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tag record, or null if not found.</returns>
        Task<TagRecord> ReadAsync(string collectionId, string id, CancellationToken token = default);

        /// <summary>
        /// Delete a tag record by collection ID and tag ID.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="id">Tag ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteAsync(string collectionId, string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate tag records within a collection with pagination.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="query">Enumeration query parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Paginated enumeration result of tag records.</returns>
        Task<EnumerationResult<TagRecord>> EnumerateAsync(string collectionId, EnumerationQuery query, CancellationToken token = default);

        /// <summary>
        /// Enumerate tag records by document key within a collection with pagination.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentKey">Document key.</param>
        /// <param name="query">Enumeration query parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Paginated enumeration result of tag records for the specified document.</returns>
        Task<EnumerationResult<TagRecord>> EnumerateByDocumentKeyAsync(string collectionId, string documentKey, EnumerationQuery query, CancellationToken token = default);

        /// <summary>
        /// Batch-fetch tags for multiple documents in a single query.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentKeys">List of document keys.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary mapping document keys to their dictionary of tag key-value pairs.</returns>
        Task<Dictionary<string, Dictionary<string, string>>> GetByDocumentKeysAsync(string collectionId, List<string> documentKeys, CancellationToken token = default);

        /// <summary>
        /// Delete all tag records for a given document key.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentKey">Document key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByDocumentKeyAsync(string collectionId, string documentKey, CancellationToken token = default);

        /// <summary>
        /// Delete all tag records for multiple document keys.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentKeys">List of document keys.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByDocumentKeysAsync(string collectionId, List<string> documentKeys, CancellationToken token = default);
    }
}
