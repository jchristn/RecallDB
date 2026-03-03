namespace RecallDb.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using RecallDb.Core.Models;

    /// <summary>
    /// Interface for label CRUD and enumeration operations, scoped by collection.
    /// </summary>
    public interface ILabelMethods
    {
        /// <summary>
        /// Create a new label record within a collection.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="label">Label record to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created label record.</returns>
        Task<LabelRecord> CreateAsync(string collectionId, LabelRecord label, CancellationToken token = default);

        /// <summary>
        /// Read a label record by collection ID and label ID.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="id">Label ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Label record, or null if not found.</returns>
        Task<LabelRecord> ReadAsync(string collectionId, string id, CancellationToken token = default);

        /// <summary>
        /// Delete a label record by collection ID and label ID.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="id">Label ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteAsync(string collectionId, string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate label records within a collection with pagination.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="query">Enumeration query parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Paginated enumeration result of label records.</returns>
        Task<EnumerationResult<LabelRecord>> EnumerateAsync(string collectionId, EnumerationQuery query, CancellationToken token = default);

        /// <summary>
        /// Enumerate label records by document key within a collection with pagination.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentKey">Document key.</param>
        /// <param name="query">Enumeration query parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Paginated enumeration result of label records for the specified document.</returns>
        Task<EnumerationResult<LabelRecord>> EnumerateByDocumentKeyAsync(string collectionId, string documentKey, EnumerationQuery query, CancellationToken token = default);

        /// <summary>
        /// Batch-fetch labels for multiple documents in a single query.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentKeys">List of document keys.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary mapping document keys to their list of label strings.</returns>
        Task<Dictionary<string, List<string>>> GetByDocumentKeysAsync(string collectionId, List<string> documentKeys, CancellationToken token = default);

        /// <summary>
        /// Delete all label records for a given document key.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentKey">Document key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByDocumentKeyAsync(string collectionId, string documentKey, CancellationToken token = default);

        /// <summary>
        /// Delete all label records for multiple document keys.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentKeys">List of document keys.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByDocumentKeysAsync(string collectionId, List<string> documentKeys, CancellationToken token = default);
    }
}
