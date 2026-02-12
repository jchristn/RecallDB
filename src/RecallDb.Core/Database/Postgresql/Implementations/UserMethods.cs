namespace RecallDb.Core.Database.Postgresql.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using RecallDb.Core.Database.Interfaces;
    using RecallDb.Core.Enums;
    using RecallDb.Core.Models;

    /// <summary>
    /// PostgreSQL implementation of user CRUD and enumeration operations, scoped by tenant.
    /// </summary>
    public class UserMethods : IUserMethods
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private readonly PostgresqlDatabaseDriver _Driver;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[UserMethods] ";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL database driver.</param>
        /// <param name="logging">Logging module.</param>
        public UserMethods(PostgresqlDatabaseDriver driver, LoggingModule logging)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _Logging = logging;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create a new user within a tenant.
        /// </summary>
        /// <param name="user">User record to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created user record.</returns>
        public async Task<UserMaster> CreateAsync(UserMaster user, CancellationToken token = default)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            string query =
                "INSERT INTO users " +
                "(id, tenant_id, email, password_sha256, first_name, last_name, is_admin, is_tenant_admin, active, created_utc, last_update_utc) " +
                "VALUES (" +
                "'" + _Driver.Sanitize(user.Id) + "', " +
                "'" + _Driver.Sanitize(user.TenantId) + "', " +
                "'" + _Driver.Sanitize(user.Email) + "', " +
                _Driver.FormatNullableString(user.PasswordSha256) + ", " +
                _Driver.FormatNullableString(user.FirstName) + ", " +
                _Driver.FormatNullableString(user.LastName) + ", " +
                _Driver.FormatBoolean(user.IsAdmin) + ", " +
                _Driver.FormatBoolean(user.IsTenantAdmin) + ", " +
                _Driver.FormatBoolean(user.Active) + ", " +
                "'" + _Driver.FormatDateTime(user.CreatedUtc) + "', " +
                "'" + _Driver.FormatDateTime(user.LastUpdateUtc) + "'" +
                ")";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "created user " + user.Id + " in tenant " + user.TenantId);
            return user;
        }

        /// <summary>
        /// Read a user by tenant ID and user ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">User ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>User record, or null if not found.</returns>
        public async Task<UserMaster> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query =
                "SELECT * FROM users " +
                "WHERE id = '" + _Driver.Sanitize(id) + "' " +
                "AND tenant_id = '" + _Driver.Sanitize(tenantId) + "'";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;
            return UserMaster.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Read a user by tenant ID and email address.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="email">Email address.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>User record, or null if not found.</returns>
        public async Task<UserMaster> ReadByEmailAsync(string tenantId, string email, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(email)) throw new ArgumentNullException(nameof(email));

            string query =
                "SELECT * FROM users " +
                "WHERE tenant_id = '" + _Driver.Sanitize(tenantId) + "' " +
                "AND email = '" + _Driver.Sanitize(email) + "'";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;
            return UserMaster.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Update an existing user.
        /// </summary>
        /// <param name="user">User record with updated values.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The updated user record.</returns>
        public async Task<UserMaster> UpdateAsync(UserMaster user, CancellationToken token = default)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            user.LastUpdateUtc = DateTime.UtcNow;

            string query =
                "UPDATE users SET " +
                "email = '" + _Driver.Sanitize(user.Email) + "', " +
                "password_sha256 = " + _Driver.FormatNullableString(user.PasswordSha256) + ", " +
                "first_name = " + _Driver.FormatNullableString(user.FirstName) + ", " +
                "last_name = " + _Driver.FormatNullableString(user.LastName) + ", " +
                "is_admin = " + _Driver.FormatBoolean(user.IsAdmin) + ", " +
                "is_tenant_admin = " + _Driver.FormatBoolean(user.IsTenantAdmin) + ", " +
                "active = " + _Driver.FormatBoolean(user.Active) + ", " +
                "last_update_utc = '" + _Driver.FormatDateTime(user.LastUpdateUtc) + "' " +
                "WHERE id = '" + _Driver.Sanitize(user.Id) + "' " +
                "AND tenant_id = '" + _Driver.Sanitize(user.TenantId) + "'";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "updated user " + user.Id + " in tenant " + user.TenantId);
            return user;
        }

        /// <summary>
        /// Delete a user by tenant ID and user ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">User ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query =
                "DELETE FROM users " +
                "WHERE id = '" + _Driver.Sanitize(id) + "' " +
                "AND tenant_id = '" + _Driver.Sanitize(tenantId) + "'";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "deleted user " + id + " in tenant " + tenantId);
        }

        /// <summary>
        /// Check whether a user exists by tenant ID and user ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">User ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the user exists.</returns>
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query =
                "SELECT COUNT(*) AS count FROM users " +
                "WHERE id = '" + _Driver.Sanitize(id) + "' " +
                "AND tenant_id = '" + _Driver.Sanitize(tenantId) + "'";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return false;
            long count = Convert.ToInt64(result.Rows[0]["count"]);
            return count > 0;
        }

        /// <summary>
        /// Enumerate users within a tenant with pagination.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="query">Enumeration query parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Paginated enumeration result of user records.</returns>
        public async Task<EnumerationResult<UserMaster>> EnumerateAsync(string tenantId, EnumerationQuery query, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (query == null) throw new ArgumentNullException(nameof(query));

            int offset = 0;

            if (!string.IsNullOrEmpty(query.ContinuationToken))
            {
                if (int.TryParse(query.ContinuationToken, out int parsedOffset))
                {
                    offset = parsedOffset;
                }
            }

            string orderDirection = query.Ordering == EnumerationOrderEnum.CreatedAscending ? "ASC" : "DESC";
            string sanitizedTenantId = _Driver.Sanitize(tenantId);

            string selectQuery =
                "SELECT * FROM users " +
                "WHERE tenant_id = '" + sanitizedTenantId + "' " +
                "ORDER BY created_utc " + orderDirection + " " +
                "LIMIT " + query.MaxResults + " " +
                "OFFSET " + offset;

            string countQuery =
                "SELECT COUNT(*) AS count FROM users " +
                "WHERE tenant_id = '" + sanitizedTenantId + "'";

            DataTable selectResult = await _Driver.ExecuteQueryAsync(selectQuery, false, token).ConfigureAwait(false);
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);

            List<UserMaster> users = UserMaster.FromDataTable(selectResult);
            long totalCount = 0;

            if (countResult != null && countResult.Rows.Count > 0)
            {
                totalCount = Convert.ToInt64(countResult.Rows[0]["count"]);
            }

            return EnumerationResult<UserMaster>.Create(query, users, totalCount);
        }

        /// <summary>
        /// Get the total count of users within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Total number of users in the tenant.</returns>
        public async Task<long> GetCountAsync(string tenantId, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            string query =
                "SELECT COUNT(*) AS count FROM users " +
                "WHERE tenant_id = '" + _Driver.Sanitize(tenantId) + "'";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return 0;
            return Convert.ToInt64(result.Rows[0]["count"]);
        }

        /// <summary>
        /// Delete all users within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteByTenantIdAsync(string tenantId, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            string query =
                "DELETE FROM users " +
                "WHERE tenant_id = '" + _Driver.Sanitize(tenantId) + "'";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "deleted all users in tenant " + tenantId);
        }

        #endregion
    }
}
