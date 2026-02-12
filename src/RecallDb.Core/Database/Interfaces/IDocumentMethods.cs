namespace RecallDb.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using RecallDb.Core.Models;

    /// <summary>
    /// Interface for document CRUD, batch, and enumeration operations, scoped by collection.
    /// </summary>
    public interface IDocumentMethods
    {
        /// <summary>
        /// Create a new document record within a collection.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="document">Document record to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created document record.</returns>
        Task<DocumentRecord> CreateAsync(string collectionId, DocumentRecord document, CancellationToken token = default);

        /// <summary>
        /// Create multiple document records within a collection in a single batch operation.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documents">List of document records to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of created document records.</returns>
        Task<List<DocumentRecord>> CreateBatchAsync(string collectionId, List<DocumentRecord> documents, CancellationToken token = default);

        /// <summary>
        /// Read a document record by collection ID and document key.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentKey">Document key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Document record, or null if not found.</returns>
        Task<DocumentRecord> ReadAsync(string collectionId, string documentKey, CancellationToken token = default);

        /// <summary>
        /// Read all document records matching a document ID within a collection.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of document records matching the document ID.</returns>
        Task<List<DocumentRecord>> ReadByDocumentIdAsync(string collectionId, string documentId, CancellationToken token = default);

        /// <summary>
        /// Read a document record by collection ID, document ID, and position.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="position">Chunk position.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Document record, or null if not found.</returns>
        Task<DocumentRecord> ReadByDocumentIdAndPositionAsync(string collectionId, string documentId, int position, CancellationToken token = default);

        /// <summary>
        /// Update an existing document record within a collection.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="document">Document record with updated values.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The updated document record.</returns>
        Task<DocumentRecord> UpdateAsync(string collectionId, DocumentRecord document, CancellationToken token = default);

        /// <summary>
        /// Delete a document record by collection ID and document key.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentKey">Document key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteAsync(string collectionId, string documentKey, CancellationToken token = default);

        /// <summary>
        /// Delete a document record by collection ID and document key.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentKey">Document key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByDocumentKeyAsync(string collectionId, string documentKey, CancellationToken token = default);

        /// <summary>
        /// Check whether a document record exists by collection ID and document key.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentKey">Document key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the document record exists.</returns>
        Task<bool> ExistsAsync(string collectionId, string documentKey, CancellationToken token = default);

        /// <summary>
        /// Enumerate document records within a collection with pagination.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="query">Enumeration query parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Paginated enumeration result of document records.</returns>
        Task<EnumerationResult<DocumentRecord>> EnumerateAsync(string collectionId, EnumerationQuery query, CancellationToken token = default);
    }
}
