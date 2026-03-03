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
    /// PostgreSQL implementation of label CRUD and enumeration operations for dynamic collection tables.
    /// </summary>
    public class LabelMethods : ILabelMethods
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private readonly PostgresqlDatabaseDriver _Driver;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[LabelMethods] ";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL database driver.</param>
        /// <param name="logging">Logging module.</param>
        public LabelMethods(PostgresqlDatabaseDriver driver, LoggingModule logging)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _Logging = logging;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create a new label record within a collection.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="label">Label record to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created label record.</returns>
        public async Task<LabelRecord> CreateAsync(string collectionId, LabelRecord label, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (label == null) throw new ArgumentNullException(nameof(label));

            string tableName = "collection_" + SanitizeTableName(collectionId) + "_labels";

            string query =
                "INSERT INTO " + tableName + " " +
                "(id, document_key, document_id, position, label, created_utc) " +
                "VALUES (" +
                "'" + _Driver.Sanitize(label.Id) + "', " +
                "'" + _Driver.Sanitize(label.DocumentKey) + "', " +
                _Driver.FormatNullableString(label.DocumentId) + ", " +
                FormatNullableInt(label.Position) + ", " +
                "'" + _Driver.Sanitize(label.Label) + "', " +
                "'" + _Driver.FormatDateTime(label.CreatedUtc) + "'" +
                ")";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "created label " + label.Id + " in " + collectionId);
            return label;
        }

        /// <summary>
        /// Read a label record by collection ID and label ID.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="id">Label ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Label record, or null if not found.</returns>
        public async Task<LabelRecord> ReadAsync(string collectionId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string tableName = "collection_" + SanitizeTableName(collectionId) + "_labels";

            string query =
                "SELECT * FROM " + tableName + " " +
                "WHERE id = '" + _Driver.Sanitize(id) + "'";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;
            return LabelRecord.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Delete a label record by collection ID and label ID.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="id">Label ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteAsync(string collectionId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string tableName = "collection_" + SanitizeTableName(collectionId) + "_labels";

            string query =
                "DELETE FROM " + tableName + " " +
                "WHERE id = '" + _Driver.Sanitize(id) + "'";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "deleted label " + id + " from " + collectionId);
        }

        /// <summary>
        /// Enumerate label records within a collection with pagination.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="query">Enumeration query parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Paginated enumeration result of label records.</returns>
        public async Task<EnumerationResult<LabelRecord>> EnumerateAsync(string collectionId, EnumerationQuery query, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (query == null) throw new ArgumentNullException(nameof(query));

            string tableName = "collection_" + SanitizeTableName(collectionId) + "_labels";

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

            List<LabelRecord> labels = LabelRecord.FromDataTable(selectResult);
            long totalCount = 0;

            if (countResult != null && countResult.Rows.Count > 0)
            {
                totalCount = Convert.ToInt64(countResult.Rows[0]["count"]);
            }

            return EnumerationResult<LabelRecord>.Create(query, labels, totalCount);
        }

        /// <summary>
        /// Enumerate label records by document key within a collection with pagination.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentKey">Document key.</param>
        /// <param name="query">Enumeration query parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Paginated enumeration result of label records for the specified document.</returns>
        public async Task<EnumerationResult<LabelRecord>> EnumerateByDocumentKeyAsync(string collectionId, string documentKey, EnumerationQuery query, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (string.IsNullOrEmpty(documentKey)) throw new ArgumentNullException(nameof(documentKey));
            if (query == null) throw new ArgumentNullException(nameof(query));

            string tableName = "collection_" + SanitizeTableName(collectionId) + "_labels";

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

            List<LabelRecord> labels = LabelRecord.FromDataTable(selectResult);
            long totalCount = 0;

            if (countResult != null && countResult.Rows.Count > 0)
            {
                totalCount = Convert.ToInt64(countResult.Rows[0]["count"]);
            }

            return EnumerationResult<LabelRecord>.Create(query, labels, totalCount);
        }

        /// <summary>
        /// Batch-fetch labels for multiple documents in a single query.
        /// </summary>
        public async Task<Dictionary<string, List<string>>> GetByDocumentKeysAsync(string collectionId, List<string> documentKeys, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));

            Dictionary<string, List<string>> result = new Dictionary<string, List<string>>();
            if (documentKeys == null || documentKeys.Count == 0) return result;

            string tableName = "collection_" + SanitizeTableName(collectionId) + "_labels";

            List<string> sanitizedKeys = new List<string>();
            foreach (string key in documentKeys)
            {
                sanitizedKeys.Add("'" + _Driver.Sanitize(key) + "'");
            }

            string query =
                "SELECT document_key, label FROM " + tableName + " " +
                "WHERE document_key IN (" + string.Join(", ", sanitizedKeys) + ")";

            DataTable dt = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (dt != null && dt.Rows.Count > 0)
            {
                foreach (DataRow row in dt.Rows)
                {
                    string docKey = DataTableHelper.GetStringValue(row, "document_key");
                    string label = DataTableHelper.GetStringValue(row, "label");

                    if (!result.ContainsKey(docKey))
                        result[docKey] = new List<string>();

                    result[docKey].Add(label);
                }
            }

            return result;
        }

        /// <summary>
        /// Delete all label records for a given document key.
        /// </summary>
        public async Task DeleteByDocumentKeyAsync(string collectionId, string documentKey, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (string.IsNullOrEmpty(documentKey)) throw new ArgumentNullException(nameof(documentKey));

            string tableName = "collection_" + SanitizeTableName(collectionId) + "_labels";

            string query =
                "DELETE FROM " + tableName + " " +
                "WHERE document_key = '" + _Driver.Sanitize(documentKey) + "'";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "deleted labels for document " + documentKey + " from " + collectionId);
        }

        /// <summary>
        /// Delete all label records for multiple document keys.
        /// </summary>
        public async Task DeleteByDocumentKeysAsync(string collectionId, List<string> documentKeys, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (documentKeys == null) throw new ArgumentNullException(nameof(documentKeys));
            if (documentKeys.Count == 0) return;

            string tableName = "collection_" + SanitizeTableName(collectionId) + "_labels";

            List<string> sanitizedKeys = new List<string>();
            foreach (string key in documentKeys)
            {
                sanitizedKeys.Add("'" + _Driver.Sanitize(key) + "'");
            }

            string query =
                "DELETE FROM " + tableName + " " +
                "WHERE document_key IN (" + string.Join(", ", sanitizedKeys) + ")";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "batch deleted labels for " + documentKeys.Count + " documents from " + collectionId);
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
