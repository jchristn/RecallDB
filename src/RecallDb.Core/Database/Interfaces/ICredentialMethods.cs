namespace RecallDb.Core.Database.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using RecallDb.Core.Models;

    /// <summary>
    /// Interface for credential CRUD and enumeration operations, scoped by tenant.
    /// Includes cross-tenant bearer token lookup.
    /// </summary>
    public interface ICredentialMethods
    {
        /// <summary>
        /// Create a new credential within a tenant.
        /// </summary>
        /// <param name="credential">Credential to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created credential.</returns>
        Task<Credential> CreateAsync(Credential credential, CancellationToken token = default);

        /// <summary>
        /// Read a credential by tenant ID and credential ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Credential ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Credential, or null if not found.</returns>
        Task<Credential> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a credential by bearer token across all tenants.
        /// </summary>
        /// <param name="bearerToken">Bearer token.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Credential, or null if not found.</returns>
        Task<Credential> ReadByBearerTokenAsync(string bearerToken, CancellationToken token = default);

        /// <summary>
        /// Update an existing credential.
        /// </summary>
        /// <param name="credential">Credential with updated values.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The updated credential.</returns>
        Task<Credential> UpdateAsync(Credential credential, CancellationToken token = default);

        /// <summary>
        /// Delete a credential by tenant ID and credential ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Credential ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Check whether a credential exists by tenant ID and credential ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Credential ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the credential exists.</returns>
        Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate credentials within a tenant with pagination.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="query">Enumeration query parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Paginated enumeration result of credentials.</returns>
        Task<EnumerationResult<Credential>> EnumerateAsync(string tenantId, EnumerationQuery query, CancellationToken token = default);

        /// <summary>
        /// Get the total count of credentials within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Total number of credentials in the tenant.</returns>
        Task<long> GetCountAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Delete all credentials for a specific user within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="userId">User ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByUserIdAsync(string tenantId, string userId, CancellationToken token = default);

        /// <summary>
        /// Delete all credentials within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByTenantIdAsync(string tenantId, CancellationToken token = default);
    }
}
