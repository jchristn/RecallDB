namespace RecallDb.Core.Database.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using RecallDb.Core.Models;

    /// <summary>
    /// Interface for tenant CRUD and enumeration operations.
    /// </summary>
    public interface ITenantMethods
    {
        /// <summary>
        /// Create a new tenant.
        /// </summary>
        /// <param name="tenant">Tenant metadata to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created tenant metadata.</returns>
        Task<TenantMetadata> CreateAsync(TenantMetadata tenant, CancellationToken token = default);

        /// <summary>
        /// Read a tenant by ID.
        /// </summary>
        /// <param name="id">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tenant metadata, or null if not found.</returns>
        Task<TenantMetadata> ReadAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Update an existing tenant.
        /// </summary>
        /// <param name="tenant">Tenant metadata with updated values.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The updated tenant metadata.</returns>
        Task<TenantMetadata> UpdateAsync(TenantMetadata tenant, CancellationToken token = default);

        /// <summary>
        /// Delete a tenant by ID.
        /// </summary>
        /// <param name="id">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Check whether a tenant exists by ID.
        /// </summary>
        /// <param name="id">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the tenant exists.</returns>
        Task<bool> ExistsAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate tenants with pagination.
        /// </summary>
        /// <param name="query">Enumeration query parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Paginated enumeration result of tenant metadata.</returns>
        Task<EnumerationResult<TenantMetadata>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default);

        /// <summary>
        /// Get the total count of tenants.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Total number of tenants.</returns>
        Task<long> GetCountAsync(CancellationToken token = default);
    }
}
