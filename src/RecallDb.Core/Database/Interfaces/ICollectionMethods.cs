namespace RecallDb.Core.Database.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using RecallDb.Core.Models;

    /// <summary>
    /// Interface for collection CRUD and enumeration operations, scoped by tenant.
    /// </summary>
    public interface ICollectionMethods
    {
        /// <summary>
        /// Create a new collection within a tenant.
        /// </summary>
        /// <param name="collection">Collection metadata to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created collection metadata.</returns>
        Task<CollectionMetadata> CreateAsync(CollectionMetadata collection, CancellationToken token = default);

        /// <summary>
        /// Read a collection by tenant ID and collection ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Collection ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Collection metadata, or null if not found.</returns>
        Task<CollectionMetadata> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a collection by tenant ID and collection name.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="name">Collection name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Collection metadata, or null if not found.</returns>
        Task<CollectionMetadata> ReadByNameAsync(string tenantId, string name, CancellationToken token = default);

        /// <summary>
        /// Update an existing collection.
        /// </summary>
        /// <param name="collection">Collection metadata with updated values.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The updated collection metadata.</returns>
        Task<CollectionMetadata> UpdateAsync(CollectionMetadata collection, CancellationToken token = default);

        /// <summary>
        /// Delete a collection by tenant ID and collection ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Collection ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Check whether a collection exists by tenant ID and collection ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Collection ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the collection exists.</returns>
        Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate collections within a tenant with pagination.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="query">Enumeration query parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Paginated enumeration result of collection metadata.</returns>
        Task<EnumerationResult<CollectionMetadata>> EnumerateAsync(string tenantId, EnumerationQuery query, CancellationToken token = default);

        /// <summary>
        /// Get the total count of collections within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Total number of collections in the tenant.</returns>
        Task<long> GetCountAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Delete all collections within a tenant, including their dynamic tables.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByTenantIdAsync(string tenantId, CancellationToken token = default);
    }
}
