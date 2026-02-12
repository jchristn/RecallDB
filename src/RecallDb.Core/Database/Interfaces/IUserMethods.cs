namespace RecallDb.Core.Database.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using RecallDb.Core.Models;

    /// <summary>
    /// Interface for user CRUD and enumeration operations, scoped by tenant.
    /// </summary>
    public interface IUserMethods
    {
        /// <summary>
        /// Create a new user within a tenant.
        /// </summary>
        /// <param name="user">User record to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created user record.</returns>
        Task<UserMaster> CreateAsync(UserMaster user, CancellationToken token = default);

        /// <summary>
        /// Read a user by tenant ID and user ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">User ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>User record, or null if not found.</returns>
        Task<UserMaster> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a user by tenant ID and email address.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="email">Email address.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>User record, or null if not found.</returns>
        Task<UserMaster> ReadByEmailAsync(string tenantId, string email, CancellationToken token = default);

        /// <summary>
        /// Update an existing user.
        /// </summary>
        /// <param name="user">User record with updated values.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The updated user record.</returns>
        Task<UserMaster> UpdateAsync(UserMaster user, CancellationToken token = default);

        /// <summary>
        /// Delete a user by tenant ID and user ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">User ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Check whether a user exists by tenant ID and user ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">User ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the user exists.</returns>
        Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate users within a tenant with pagination.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="query">Enumeration query parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Paginated enumeration result of user records.</returns>
        Task<EnumerationResult<UserMaster>> EnumerateAsync(string tenantId, EnumerationQuery query, CancellationToken token = default);

        /// <summary>
        /// Get the total count of users within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Total number of users in the tenant.</returns>
        Task<long> GetCountAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Delete all users within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByTenantIdAsync(string tenantId, CancellationToken token = default);
    }
}
