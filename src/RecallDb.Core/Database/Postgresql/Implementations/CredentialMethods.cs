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
    /// PostgreSQL implementation of credential CRUD and enumeration operations, scoped by tenant.
    /// Includes cross-tenant bearer token lookup.
    /// </summary>
    public class CredentialMethods : ICredentialMethods
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private readonly PostgresqlDatabaseDriver _Driver;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[CredentialMethods] ";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL database driver.</param>
        /// <param name="logging">Logging module.</param>
        public CredentialMethods(PostgresqlDatabaseDriver driver, LoggingModule logging)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _Logging = logging;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create a new credential within a tenant.
        /// </summary>
        /// <param name="credential">Credential to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created credential.</returns>
        public async Task<Credential> CreateAsync(Credential credential, CancellationToken token = default)
        {
            if (credential == null) throw new ArgumentNullException(nameof(credential));

            string query =
                "INSERT INTO credentials " +
                "(id, tenant_id, user_id, bearer_token, name, active, created_utc, last_update_utc) " +
                "VALUES (" +
                "'" + _Driver.Sanitize(credential.Id) + "', " +
                "'" + _Driver.Sanitize(credential.TenantId) + "', " +
                "'" + _Driver.Sanitize(credential.UserId) + "', " +
                "'" + _Driver.Sanitize(credential.BearerToken) + "', " +
                _Driver.FormatNullableString(credential.Name) + ", " +
                _Driver.FormatBoolean(credential.Active) + ", " +
                "'" + _Driver.FormatDateTime(credential.CreatedUtc) + "', " +
                "'" + _Driver.FormatDateTime(credential.LastUpdateUtc) + "'" +
                ")";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "created credential " + credential.Id + " in tenant " + credential.TenantId);
            return credential;
        }

        /// <summary>
        /// Read a credential by tenant ID and credential ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Credential ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Credential, or null if not found.</returns>
        public async Task<Credential> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query =
                "SELECT * FROM credentials " +
                "WHERE id = '" + _Driver.Sanitize(id) + "' " +
                "AND tenant_id = '" + _Driver.Sanitize(tenantId) + "'";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;
            return Credential.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Read a credential by bearer token across all tenants.
        /// </summary>
        /// <param name="bearerToken">Bearer token.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Credential, or null if not found.</returns>
        public async Task<Credential> ReadByBearerTokenAsync(string bearerToken, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(bearerToken)) throw new ArgumentNullException(nameof(bearerToken));

            string query =
                "SELECT * FROM credentials " +
                "WHERE bearer_token = '" + _Driver.Sanitize(bearerToken) + "'";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;
            return Credential.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Update an existing credential.
        /// </summary>
        /// <param name="credential">Credential with updated values.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The updated credential.</returns>
        public async Task<Credential> UpdateAsync(Credential credential, CancellationToken token = default)
        {
            if (credential == null) throw new ArgumentNullException(nameof(credential));

            credential.LastUpdateUtc = DateTime.UtcNow;

            string query =
                "UPDATE credentials SET " +
                "name = " + _Driver.FormatNullableString(credential.Name) + ", " +
                "active = " + _Driver.FormatBoolean(credential.Active) + ", " +
                "last_update_utc = '" + _Driver.FormatDateTime(credential.LastUpdateUtc) + "' " +
                "WHERE id = '" + _Driver.Sanitize(credential.Id) + "' " +
                "AND tenant_id = '" + _Driver.Sanitize(credential.TenantId) + "'";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "updated credential " + credential.Id + " in tenant " + credential.TenantId);
            return credential;
        }

        /// <summary>
        /// Delete a credential by tenant ID and credential ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Credential ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query =
                "DELETE FROM credentials " +
                "WHERE id = '" + _Driver.Sanitize(id) + "' " +
                "AND tenant_id = '" + _Driver.Sanitize(tenantId) + "'";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "deleted credential " + id + " in tenant " + tenantId);
        }

        /// <summary>
        /// Check whether a credential exists by tenant ID and credential ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Credential ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the credential exists.</returns>
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query =
                "SELECT COUNT(*) AS count FROM credentials " +
                "WHERE id = '" + _Driver.Sanitize(id) + "' " +
                "AND tenant_id = '" + _Driver.Sanitize(tenantId) + "'";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return false;
            long count = Convert.ToInt64(result.Rows[0]["count"]);
            return count > 0;
        }

        /// <summary>
        /// Enumerate credentials within a tenant with pagination.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="query">Enumeration query parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Paginated enumeration result of credentials.</returns>
        public async Task<EnumerationResult<Credential>> EnumerateAsync(string tenantId, EnumerationQuery query, CancellationToken token = default)
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
                "SELECT * FROM credentials " +
                "WHERE tenant_id = '" + sanitizedTenantId + "' " +
                "ORDER BY created_utc " + orderDirection + " " +
                "LIMIT " + query.MaxResults + " " +
                "OFFSET " + offset;

            string countQuery =
                "SELECT COUNT(*) AS count FROM credentials " +
                "WHERE tenant_id = '" + sanitizedTenantId + "'";

            DataTable selectResult = await _Driver.ExecuteQueryAsync(selectQuery, false, token).ConfigureAwait(false);
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);

            List<Credential> credentials = Credential.FromDataTable(selectResult);
            long totalCount = 0;

            if (countResult != null && countResult.Rows.Count > 0)
            {
                totalCount = Convert.ToInt64(countResult.Rows[0]["count"]);
            }

            return EnumerationResult<Credential>.Create(query, credentials, totalCount);
        }

        /// <summary>
        /// Get the total count of credentials within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Total number of credentials in the tenant.</returns>
        public async Task<long> GetCountAsync(string tenantId, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            string query =
                "SELECT COUNT(*) AS count FROM credentials " +
                "WHERE tenant_id = '" + _Driver.Sanitize(tenantId) + "'";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return 0;
            return Convert.ToInt64(result.Rows[0]["count"]);
        }

        /// <summary>
        /// Delete all credentials for a specific user within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="userId">User ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteByUserIdAsync(string tenantId, string userId, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(userId)) throw new ArgumentNullException(nameof(userId));

            string query =
                "DELETE FROM credentials " +
                "WHERE user_id = '" + _Driver.Sanitize(userId) + "' " +
                "AND tenant_id = '" + _Driver.Sanitize(tenantId) + "'";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "deleted credentials for user " + userId + " in tenant " + tenantId);
        }

        /// <summary>
        /// Delete all credentials within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteByTenantIdAsync(string tenantId, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            string query =
                "DELETE FROM credentials " +
                "WHERE tenant_id = '" + _Driver.Sanitize(tenantId) + "'";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "deleted all credentials in tenant " + tenantId);
        }

        #endregion
    }
}
