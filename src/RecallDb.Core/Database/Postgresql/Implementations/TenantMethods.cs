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
    /// PostgreSQL implementation of tenant CRUD and enumeration operations.
    /// </summary>
    public class TenantMethods : ITenantMethods
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private readonly PostgresqlDatabaseDriver _Driver;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[TenantMethods] ";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL database driver.</param>
        /// <param name="logging">Logging module.</param>
        public TenantMethods(PostgresqlDatabaseDriver driver, LoggingModule logging)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _Logging = logging;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create a new tenant.
        /// </summary>
        /// <param name="tenant">Tenant metadata to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created tenant metadata.</returns>
        public async Task<TenantMetadata> CreateAsync(TenantMetadata tenant, CancellationToken token = default)
        {
            if (tenant == null) throw new ArgumentNullException(nameof(tenant));

            string query =
                "INSERT INTO tenants " +
                "(id, name, active, labels_json, tags_json, created_utc, last_update_utc) " +
                "VALUES (" +
                "'" + _Driver.Sanitize(tenant.Id) + "', " +
                "'" + _Driver.Sanitize(tenant.Name) + "', " +
                _Driver.FormatBoolean(tenant.Active) + ", " +
                _Driver.FormatNullableString(tenant.LabelsJson) + ", " +
                _Driver.FormatNullableString(tenant.TagsJson) + ", " +
                "'" + _Driver.FormatDateTime(tenant.CreatedUtc) + "', " +
                "'" + _Driver.FormatDateTime(tenant.LastUpdateUtc) + "'" +
                ")";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "created tenant " + tenant.Id);
            return tenant;
        }

        /// <summary>
        /// Read a tenant by ID.
        /// </summary>
        /// <param name="id">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tenant metadata, or null if not found.</returns>
        public async Task<TenantMetadata> ReadAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query =
                "SELECT * FROM tenants WHERE id = '" + _Driver.Sanitize(id) + "'";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;
            return TenantMetadata.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Update an existing tenant.
        /// </summary>
        /// <param name="tenant">Tenant metadata with updated values.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The updated tenant metadata.</returns>
        public async Task<TenantMetadata> UpdateAsync(TenantMetadata tenant, CancellationToken token = default)
        {
            if (tenant == null) throw new ArgumentNullException(nameof(tenant));

            tenant.LastUpdateUtc = DateTime.UtcNow;

            string query =
                "UPDATE tenants SET " +
                "name = '" + _Driver.Sanitize(tenant.Name) + "', " +
                "active = " + _Driver.FormatBoolean(tenant.Active) + ", " +
                "labels_json = " + _Driver.FormatNullableString(tenant.LabelsJson) + ", " +
                "tags_json = " + _Driver.FormatNullableString(tenant.TagsJson) + ", " +
                "last_update_utc = '" + _Driver.FormatDateTime(tenant.LastUpdateUtc) + "' " +
                "WHERE id = '" + _Driver.Sanitize(tenant.Id) + "'";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "updated tenant " + tenant.Id);
            return tenant;
        }

        /// <summary>
        /// Delete a tenant by ID.
        /// </summary>
        /// <param name="id">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query =
                "DELETE FROM tenants WHERE id = '" + _Driver.Sanitize(id) + "'";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "deleted tenant " + id);
        }

        /// <summary>
        /// Check whether a tenant exists by ID.
        /// </summary>
        /// <param name="id">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the tenant exists.</returns>
        public async Task<bool> ExistsAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query =
                "SELECT COUNT(*) AS count FROM tenants WHERE id = '" + _Driver.Sanitize(id) + "'";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return false;
            long count = Convert.ToInt64(result.Rows[0]["count"]);
            return count > 0;
        }

        /// <summary>
        /// Enumerate tenants with pagination.
        /// </summary>
        /// <param name="query">Enumeration query parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Paginated enumeration result of tenant metadata.</returns>
        public async Task<EnumerationResult<TenantMetadata>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default)
        {
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

            string selectQuery =
                "SELECT * FROM tenants " +
                "ORDER BY created_utc " + orderDirection + " " +
                "LIMIT " + query.MaxResults + " " +
                "OFFSET " + offset;

            string countQuery = "SELECT COUNT(*) AS count FROM tenants";

            DataTable selectResult = await _Driver.ExecuteQueryAsync(selectQuery, false, token).ConfigureAwait(false);
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);

            List<TenantMetadata> tenants = TenantMetadata.FromDataTable(selectResult);
            long totalCount = 0;

            if (countResult != null && countResult.Rows.Count > 0)
            {
                totalCount = Convert.ToInt64(countResult.Rows[0]["count"]);
            }

            return EnumerationResult<TenantMetadata>.Create(query, tenants, totalCount);
        }

        /// <summary>
        /// Get the total count of tenants.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Total number of tenants.</returns>
        public async Task<long> GetCountAsync(CancellationToken token = default)
        {
            string query = "SELECT COUNT(*) AS count FROM tenants";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return 0;
            return Convert.ToInt64(result.Rows[0]["count"]);
        }

        #endregion
    }
}
