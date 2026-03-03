namespace RecallDb.Core.Database.Postgresql.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using RecallDb.Core.Database.Interfaces;
    using RecallDb.Core.Enums;
    using RecallDb.Core.Helpers;
    using RecallDb.Core.Models;

    /// <summary>
    /// PostgreSQL implementation of tag CRUD and enumeration operations for dynamic collection tables.
    /// </summary>
    public class TagMethods : ITagMethods
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private readonly PostgresqlDatabaseDriver _Driver;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[TagMethods] ";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL database driver.</param>
        /// <param name="logging">Logging module.</param>
        public TagMethods(PostgresqlDatabaseDriver driver, LoggingModule logging)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _Logging = logging;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create a new tag record within a collection.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="tag">Tag record to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created tag record.</returns>
        public async Task<TagRecord> CreateAsync(string collectionId, TagRecord tag, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (tag == null) throw new ArgumentNullException(nameof(tag));

            string tableName = "collection_" + SanitizeTableName(collectionId) + "_tags";

            string query =
                "INSERT INTO " + tableName + " " +
                "(id, document_key, document_id, position, key, value, created_utc) " +
                "VALUES (" +
                "'" + _Driver.Sanitize(tag.Id) + "', " +
                "'" + _Driver.Sanitize(tag.DocumentKey) + "', " +
                _Driver.FormatNullableString(tag.DocumentId) + ", " +
                FormatNullableInt(tag.Position) + ", " +
                "'" + _Driver.Sanitize(tag.Key) + "', " +
                _Driver.FormatNullableString(tag.Value) + ", " +
                "'" + _Driver.FormatDateTime(tag.CreatedUtc) + "'" +
                ")";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "created tag " + tag.Id + " in " + collectionId);
            return tag;
        }

        /// <summary>
        /// Read a tag record by collection ID and tag ID.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="id">Tag ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tag record, or null if not found.</returns>
        public async Task<TagRecord> ReadAsync(string collectionId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string tableName = "collection_" + SanitizeTableName(collectionId) + "_tags";

            string query =
                "SELECT * FROM " + tableName + " " +
                "WHERE id = '" + _Driver.Sanitize(id) + "'";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;
            return TagRecord.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Delete a tag record by collection ID and tag ID.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="id">Tag ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteAsync(string collectionId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string tableName = "collection_" + SanitizeTableName(collectionId) + "_tags";

            string query =
                "DELETE FROM " + tableName + " " +
                "WHERE id = '" + _Driver.Sanitize(id) + "'";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "deleted tag " + id + " from " + collectionId);
        }

        /// <summary>
        /// Enumerate tag records within a collection with pagination.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="query">Enumeration query parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Paginated enumeration result of tag records.</returns>
        public async Task<EnumerationResult<TagRecord>> EnumerateAsync(string collectionId, EnumerationQuery query, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (query == null) throw new ArgumentNullException(nameof(query));

            string tableName = "collection_" + SanitizeTableName(collectionId) + "_tags";

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
                "SELECT * FROM " + tableName + " " +
                "ORDER BY created_utc " + orderDirection + " " +
                "LIMIT " + query.MaxResults.ToString(CultureInfo.InvariantCulture) + " " +
                "OFFSET " + offset.ToString(CultureInfo.InvariantCulture);

            string countQuery =
                "SELECT COUNT(*) AS count FROM " + tableName;

            DataTable selectResult = await _Driver.ExecuteQueryAsync(selectQuery, false, token).ConfigureAwait(false);
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);

            List<TagRecord> tags = TagRecord.FromDataTable(selectResult);
            long totalCount = 0;

            if (countResult != null && countResult.Rows.Count > 0)
            {
                totalCount = Convert.ToInt64(countResult.Rows[0]["count"]);
            }

            return EnumerationResult<TagRecord>.Create(query, tags, totalCount);
        }

        /// <summary>
        /// Enumerate tag records by document key within a collection with pagination.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentKey">Document key.</param>
        /// <param name="query">Enumeration query parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Paginated enumeration result of tag records for the specified document.</returns>
        public async Task<EnumerationResult<TagRecord>> EnumerateByDocumentKeyAsync(string collectionId, string documentKey, EnumerationQuery query, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (string.IsNullOrEmpty(documentKey)) throw new ArgumentNullException(nameof(documentKey));
            if (query == null) throw new ArgumentNullException(nameof(query));

            string tableName = "collection_" + SanitizeTableName(collectionId) + "_tags";

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
                "SELECT * FROM " + tableName + " " +
                "WHERE document_key = '" + _Driver.Sanitize(documentKey) + "' " +
                "ORDER BY created_utc " + orderDirection + " " +
                "LIMIT " + query.MaxResults.ToString(CultureInfo.InvariantCulture) + " " +
                "OFFSET " + offset.ToString(CultureInfo.InvariantCulture);

            string countQuery =
                "SELECT COUNT(*) AS count FROM " + tableName + " " +
                "WHERE document_key = '" + _Driver.Sanitize(documentKey) + "'";

            DataTable selectResult = await _Driver.ExecuteQueryAsync(selectQuery, false, token).ConfigureAwait(false);
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);

            List<TagRecord> tags = TagRecord.FromDataTable(selectResult);
            long totalCount = 0;

            if (countResult != null && countResult.Rows.Count > 0)
            {
                totalCount = Convert.ToInt64(countResult.Rows[0]["count"]);
            }

            return EnumerationResult<TagRecord>.Create(query, tags, totalCount);
        }

        /// <summary>
        /// Batch-fetch tags for multiple documents in a single query.
        /// </summary>
        public async Task<Dictionary<string, Dictionary<string, string>>> GetByDocumentKeysAsync(string collectionId, List<string> documentKeys, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));

            Dictionary<string, Dictionary<string, string>> result = new Dictionary<string, Dictionary<string, string>>();
            if (documentKeys == null || documentKeys.Count == 0) return result;

            string tableName = "collection_" + SanitizeTableName(collectionId) + "_tags";

            List<string> sanitizedKeys = new List<string>();
            foreach (string key in documentKeys)
            {
                sanitizedKeys.Add("'" + _Driver.Sanitize(key) + "'");
            }

            string query =
                "SELECT document_key, key, value FROM " + tableName + " " +
                "WHERE document_key IN (" + string.Join(", ", sanitizedKeys) + ")";

            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (dt != null && dt.Rows.Count > 0)
            {
                foreach (DataRow row in dt.Rows)
                {
                    string docKey = DataTableHelper.GetStringValue(row, "document_key");
                    string tagKey = DataTableHelper.GetStringValue(row, "key");
                    string tagValue = DataTableHelper.GetStringValue(row, "value");

                    if (!result.ContainsKey(docKey))
                        result[docKey] = new Dictionary<string, string>();

                    result[docKey][tagKey] = tagValue;
                }
            }

            return result;
        }

        /// <summary>
        /// Delete all tag records for a given document key.
        /// </summary>
        public async Task DeleteByDocumentKeyAsync(string collectionId, string documentKey, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (string.IsNullOrEmpty(documentKey)) throw new ArgumentNullException(nameof(documentKey));

            string tableName = "collection_" + SanitizeTableName(collectionId) + "_tags";

            string query =
                "DELETE FROM " + tableName + " " +
                "WHERE document_key = '" + _Driver.Sanitize(documentKey) + "'";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "deleted tags for document " + documentKey + " from " + collectionId);
        }

        /// <summary>
        /// Delete all tag records for multiple document keys.
        /// </summary>
        public async Task DeleteByDocumentKeysAsync(string collectionId, List<string> documentKeys, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (documentKeys == null) throw new ArgumentNullException(nameof(documentKeys));
            if (documentKeys.Count == 0) return;

            string tableName = "collection_" + SanitizeTableName(collectionId) + "_tags";

            List<string> sanitizedKeys = new List<string>();
            foreach (string key in documentKeys)
            {
                sanitizedKeys.Add("'" + _Driver.Sanitize(key) + "'");
            }

            string query =
                "DELETE FROM " + tableName + " " +
                "WHERE document_key IN (" + string.Join(", ", sanitizedKeys) + ")";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "batch deleted tags for " + documentKeys.Count + " documents from " + collectionId);
        }

        #endregion

        #region Private-Methods

        private string SanitizeTableName(string collectionId)
        {
            if (string.IsNullOrEmpty(collectionId)) return "unknown";
            return collectionId.Replace("-", "_").Replace(".", "_");
        }

        private string FormatNullableInt(int? value)
        {
            if (!value.HasValue) return "NULL";
            return value.Value.ToString(CultureInfo.InvariantCulture);
        }

        #endregion
    }
}
