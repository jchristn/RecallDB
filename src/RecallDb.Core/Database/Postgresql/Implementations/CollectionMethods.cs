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
    /// PostgreSQL implementation of collection CRUD and enumeration operations, scoped by tenant.
    /// </summary>
    public class CollectionMethods : ICollectionMethods
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private readonly PostgresqlDatabaseDriver _Driver;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[CollectionMethods] ";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL database driver.</param>
        /// <param name="logging">Logging module.</param>
        public CollectionMethods(PostgresqlDatabaseDriver driver, LoggingModule logging)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _Logging = logging;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create a new collection within a tenant, including dynamic collection tables.
        /// </summary>
        /// <param name="collection">Collection metadata to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created collection metadata.</returns>
        public async Task<CollectionMetadata> CreateAsync(CollectionMetadata collection, CancellationToken token = default)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));

            string query =
                "INSERT INTO collections " +
                "(id, tenant_id, name, description, dimensionality, active, created_utc, last_update_utc) " +
                "VALUES (" +
                "'" + _Driver.Sanitize(collection.Id) + "', " +
                "'" + _Driver.Sanitize(collection.TenantId) + "', " +
                "'" + _Driver.Sanitize(collection.Name) + "', " +
                _Driver.FormatNullableString(collection.Description) + ", " +
                collection.Dimensionality + ", " +
                _Driver.FormatBoolean(collection.Active) + ", " +
                "'" + _Driver.FormatDateTime(collection.CreatedUtc) + "', " +
                "'" + _Driver.FormatDateTime(collection.LastUpdateUtc) + "'" +
                ")";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            await _Driver.CreateCollectionTablesAsync(collection.Id, collection.Dimensionality, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "created collection " + collection.Id + " in tenant " + collection.TenantId);
            return collection;
        }

        /// <summary>
        /// Read a collection by tenant ID and collection ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Collection ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Collection metadata, or null if not found.</returns>
        public async Task<CollectionMetadata> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query =
                "SELECT * FROM collections " +
                "WHERE id = '" + _Driver.Sanitize(id) + "' " +
                "AND tenant_id = '" + _Driver.Sanitize(tenantId) + "'";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;
            return CollectionMetadata.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Read a collection by tenant ID and collection name.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="name">Collection name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Collection metadata, or null if not found.</returns>
        public async Task<CollectionMetadata> ReadByNameAsync(string tenantId, string name, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            string query =
                "SELECT * FROM collections " +
                "WHERE tenant_id = '" + _Driver.Sanitize(tenantId) + "' " +
                "AND name = '" + _Driver.Sanitize(name) + "'";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;
            return CollectionMetadata.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Update an existing collection.
        /// </summary>
        /// <param name="collection">Collection metadata with updated values.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The updated collection metadata.</returns>
        public async Task<CollectionMetadata> UpdateAsync(CollectionMetadata collection, CancellationToken token = default)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));

            collection.LastUpdateUtc = DateTime.UtcNow;

            string query =
                "UPDATE collections SET " +
                "name = '" + _Driver.Sanitize(collection.Name) + "', " +
                "description = " + _Driver.FormatNullableString(collection.Description) + ", " +
                "active = " + _Driver.FormatBoolean(collection.Active) + ", " +
                "last_update_utc = '" + _Driver.FormatDateTime(collection.LastUpdateUtc) + "' " +
                "WHERE id = '" + _Driver.Sanitize(collection.Id) + "' " +
                "AND tenant_id = '" + _Driver.Sanitize(collection.TenantId) + "'";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "updated collection " + collection.Id + " in tenant " + collection.TenantId);
            return collection;
        }

        /// <summary>
        /// Delete a collection by tenant ID and collection ID, including dynamic collection tables.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Collection ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            await _Driver.DropCollectionTablesAsync(id, token).ConfigureAwait(false);

            string query =
                "DELETE FROM collections " +
                "WHERE id = '" + _Driver.Sanitize(id) + "' " +
                "AND tenant_id = '" + _Driver.Sanitize(tenantId) + "'";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "deleted collection " + id + " in tenant " + tenantId);
        }

        /// <summary>
        /// Check whether a collection exists by tenant ID and collection ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Collection ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the collection exists.</returns>
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query =
                "SELECT COUNT(*) AS count FROM collections " +
                "WHERE id = '" + _Driver.Sanitize(id) + "' " +
                "AND tenant_id = '" + _Driver.Sanitize(tenantId) + "'";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return false;
            long count = Convert.ToInt64(result.Rows[0]["count"]);
            return count > 0;
        }

        /// <summary>
        /// Enumerate collections within a tenant with pagination.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="query">Enumeration query parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Paginated enumeration result of collection metadata.</returns>
        public async Task<EnumerationResult<CollectionMetadata>> EnumerateAsync(string tenantId, EnumerationQuery query, CancellationToken token = default)
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
                "SELECT * FROM collections " +
                "WHERE tenant_id = '" + sanitizedTenantId + "' " +
                "ORDER BY created_utc " + orderDirection + " " +
                "LIMIT " + query.MaxResults + " " +
                "OFFSET " + offset;

            string countQuery =
                "SELECT COUNT(*) AS count FROM collections " +
                "WHERE tenant_id = '" + sanitizedTenantId + "'";

            DataTable selectResult = await _Driver.ExecuteQueryAsync(selectQuery, false, token).ConfigureAwait(false);
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);

            List<CollectionMetadata> collections = CollectionMetadata.FromDataTable(selectResult);
            long totalCount = 0;

            if (countResult != null && countResult.Rows.Count > 0)
            {
                totalCount = Convert.ToInt64(countResult.Rows[0]["count"]);
            }

            return EnumerationResult<CollectionMetadata>.Create(query, collections, totalCount);
        }

        /// <summary>
        /// Get the total count of collections within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Total number of collections in the tenant.</returns>
        public async Task<long> GetCountAsync(string tenantId, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            string query =
                "SELECT COUNT(*) AS count FROM collections " +
                "WHERE tenant_id = '" + _Driver.Sanitize(tenantId) + "'";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return 0;
            return Convert.ToInt64(result.Rows[0]["count"]);
        }

        /// <summary>
        /// Delete all collections within a tenant, including their dynamic tables.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteByTenantIdAsync(string tenantId, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            EnumerationQuery query = new EnumerationQuery();
            query.MaxResults = 1000;
            EnumerationResult<CollectionMetadata> collections = await EnumerateAsync(tenantId, query, token).ConfigureAwait(false);

            if (collections != null && collections.Objects != null)
            {
                foreach (CollectionMetadata col in collections.Objects)
                {
                    await _Driver.DropCollectionTablesAsync(col.Id, token).ConfigureAwait(false);
                }
            }

            string deleteQuery =
                "DELETE FROM collections " +
                "WHERE tenant_id = '" + _Driver.Sanitize(tenantId) + "'";

            await _Driver.ExecuteQueryAsync(deleteQuery, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "deleted all collections in tenant " + tenantId);
        }

        #endregion
    }
}
