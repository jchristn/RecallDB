namespace RecallDb.Core.Database.Postgresql.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using RecallDb.Core.Database.Interfaces;
    using RecallDb.Core.Enums;
    using RecallDb.Core.Models;

    /// <summary>
    /// PostgreSQL implementation of document CRUD, batch, and enumeration operations for dynamic collection tables.
    /// </summary>
    public class DocumentMethods : IDocumentMethods
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private readonly PostgresqlDatabaseDriver _Driver;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[DocumentMethods] ";

        private const string _SelectColumns =
            "id, document_key, document_id, content_length, etag, sha256, position, content_type, content, binary_data, embeddings::text as embeddings, created_utc";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL database driver.</param>
        /// <param name="logging">Logging module.</param>
        public DocumentMethods(PostgresqlDatabaseDriver driver, LoggingModule logging)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _Logging = logging;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create a new document record within a collection.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="document">Document record to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created document record.</returns>
        public async Task<DocumentRecord> CreateAsync(string collectionId, DocumentRecord document, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (document == null) throw new ArgumentNullException(nameof(document));

            CalculateContentLength(document);

            string tableName = "collection_" + SanitizeTableName(collectionId);

            string query =
                "INSERT INTO " + tableName + " " +
                "(document_key, document_id, content_length, etag, sha256, position, content_type, content, binary_data, embeddings, created_utc) " +
                "VALUES (" +
                "'" + _Driver.Sanitize(document.DocumentKey) + "', " +
                _Driver.FormatNullableString(document.DocumentId) + ", " +
                document.ContentLength.ToString(CultureInfo.InvariantCulture) + ", " +
                _Driver.FormatNullableString(document.Etag) + ", " +
                _Driver.FormatNullableString(document.Sha256) + ", " +
                document.Position.ToString(CultureInfo.InvariantCulture) + ", " +
                "'" + _Driver.Sanitize(document.ContentType.ToString()) + "', " +
                _Driver.FormatNullableString(document.Content) + ", " +
                FormatBinaryData(document.BinaryData) + ", " +
                FormatEmbeddings(document.Embeddings) + ", " +
                "'" + _Driver.FormatDateTime(document.CreatedUtc) + "'" +
                ")";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "created document " + document.DocumentKey + " in " + collectionId);
            return document;
        }

        /// <summary>
        /// Create multiple document records within a collection in a single batch operation.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documents">List of document records to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of created document records.</returns>
        public async Task<List<DocumentRecord>> CreateBatchAsync(string collectionId, List<DocumentRecord> documents, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (documents == null) throw new ArgumentNullException(nameof(documents));
            if (documents.Count == 0) return documents;

            string tableName = "collection_" + SanitizeTableName(collectionId);

            List<string> queries = new List<string>();

            foreach (DocumentRecord document in documents)
            {
                CalculateContentLength(document);

                string query =
                    "INSERT INTO " + tableName + " " +
                    "(document_key, document_id, content_length, etag, sha256, position, content_type, content, binary_data, embeddings, created_utc) " +
                    "VALUES (" +
                    "'" + _Driver.Sanitize(document.DocumentKey) + "', " +
                    _Driver.FormatNullableString(document.DocumentId) + ", " +
                    document.ContentLength.ToString(CultureInfo.InvariantCulture) + ", " +
                    _Driver.FormatNullableString(document.Etag) + ", " +
                    _Driver.FormatNullableString(document.Sha256) + ", " +
                    document.Position.ToString(CultureInfo.InvariantCulture) + ", " +
                    "'" + _Driver.Sanitize(document.ContentType.ToString()) + "', " +
                    _Driver.FormatNullableString(document.Content) + ", " +
                    FormatBinaryData(document.BinaryData) + ", " +
                    FormatEmbeddings(document.Embeddings) + ", " +
                    "'" + _Driver.FormatDateTime(document.CreatedUtc) + "'" +
                    ")";

                queries.Add(query);
            }

            await _Driver.ExecuteQueriesAsync(queries, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "created batch of " + documents.Count + " documents in " + collectionId);
            return documents;
        }

        /// <summary>
        /// Read a document record by collection ID and document key.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentKey">Document key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Document record, or null if not found.</returns>
        public async Task<DocumentRecord> ReadAsync(string collectionId, string documentKey, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (string.IsNullOrEmpty(documentKey)) throw new ArgumentNullException(nameof(documentKey));

            string tableName = "collection_" + SanitizeTableName(collectionId);

            string query =
                "SELECT " + _SelectColumns + " FROM " + tableName + " " +
                "WHERE document_key = '" + _Driver.Sanitize(documentKey) + "'";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;
            return DocumentRecord.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Read all document records matching a document ID within a collection.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of document records matching the document ID.</returns>
        public async Task<List<DocumentRecord>> ReadByDocumentIdAsync(string collectionId, string documentId, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (string.IsNullOrEmpty(documentId)) throw new ArgumentNullException(nameof(documentId));

            string tableName = "collection_" + SanitizeTableName(collectionId);

            string query =
                "SELECT " + _SelectColumns + " FROM " + tableName + " " +
                "WHERE document_id = '" + _Driver.Sanitize(documentId) + "' " +
                "ORDER BY position ASC";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            return DocumentRecord.FromDataTable(result);
        }

        /// <summary>
        /// Read a document record by collection ID, document ID, and position.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="position">Chunk position.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Document record, or null if not found.</returns>
        public async Task<DocumentRecord> ReadByDocumentIdAndPositionAsync(string collectionId, string documentId, int position, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (string.IsNullOrEmpty(documentId)) throw new ArgumentNullException(nameof(documentId));

            string tableName = "collection_" + SanitizeTableName(collectionId);

            string query =
                "SELECT " + _SelectColumns + " FROM " + tableName + " " +
                "WHERE document_id = '" + _Driver.Sanitize(documentId) + "' " +
                "AND position = " + position.ToString(CultureInfo.InvariantCulture);

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;
            return DocumentRecord.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Read document records by document ID within a position range.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="minPosition">Minimum position (inclusive).</param>
        /// <param name="maxPosition">Maximum position (inclusive).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of document records within the position range, ordered by position ascending.</returns>
        public async Task<List<DocumentRecord>> ReadByDocumentIdAndPositionRangeAsync(string collectionId, string documentId, int minPosition, int maxPosition, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (string.IsNullOrEmpty(documentId)) throw new ArgumentNullException(nameof(documentId));

            string tableName = "collection_" + SanitizeTableName(collectionId);

            string query =
                "SELECT " + _SelectColumns + " FROM " + tableName + " " +
                "WHERE document_id = '" + _Driver.Sanitize(documentId) + "' " +
                "AND position BETWEEN " + minPosition.ToString(CultureInfo.InvariantCulture) + " AND " + maxPosition.ToString(CultureInfo.InvariantCulture) + " " +
                "ORDER BY position ASC";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            return DocumentRecord.FromDataTable(result);
        }

        /// <summary>
        /// Update an existing document record within a collection.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="document">Document record with updated values.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The updated document record.</returns>
        public async Task<DocumentRecord> UpdateAsync(string collectionId, DocumentRecord document, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (document == null) throw new ArgumentNullException(nameof(document));

            string tableName = "collection_" + SanitizeTableName(collectionId);

            string query =
                "UPDATE " + tableName + " SET " +
                "document_id = " + _Driver.FormatNullableString(document.DocumentId) + ", " +
                "content_length = " + document.ContentLength.ToString(CultureInfo.InvariantCulture) + ", " +
                "etag = " + _Driver.FormatNullableString(document.Etag) + ", " +
                "sha256 = " + _Driver.FormatNullableString(document.Sha256) + ", " +
                "position = " + document.Position.ToString(CultureInfo.InvariantCulture) + ", " +
                "content_type = '" + _Driver.Sanitize(document.ContentType.ToString()) + "', " +
                "content = " + _Driver.FormatNullableString(document.Content) + ", " +
                "binary_data = " + FormatBinaryData(document.BinaryData) + ", " +
                "embeddings = " + FormatEmbeddings(document.Embeddings) + " " +
                "WHERE document_key = '" + _Driver.Sanitize(document.DocumentKey) + "'";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "updated document " + document.DocumentKey + " in " + collectionId);
            return document;
        }

        /// <summary>
        /// Delete a document record by collection ID and document key.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentKey">Document key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteAsync(string collectionId, string documentKey, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (string.IsNullOrEmpty(documentKey)) throw new ArgumentNullException(nameof(documentKey));

            string tableName = "collection_" + SanitizeTableName(collectionId);

            string query =
                "DELETE FROM " + tableName + " " +
                "WHERE document_key = '" + _Driver.Sanitize(documentKey) + "'";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "deleted document " + documentKey + " from " + collectionId);
        }

        /// <summary>
        /// Delete a document record by collection ID and document key.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentKey">Document key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteByDocumentKeyAsync(string collectionId, string documentKey, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (string.IsNullOrEmpty(documentKey)) throw new ArgumentNullException(nameof(documentKey));

            string tableName = "collection_" + SanitizeTableName(collectionId);

            string query =
                "DELETE FROM " + tableName + " " +
                "WHERE document_key = '" + _Driver.Sanitize(documentKey) + "'";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "deleted document by key " + documentKey + " from " + collectionId);
        }

        /// <summary>
        /// Delete multiple document records by collection ID and a list of document keys.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentKeys">List of document keys to delete.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteBatchAsync(string collectionId, List<string> documentKeys, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (documentKeys == null) throw new ArgumentNullException(nameof(documentKeys));
            if (documentKeys.Count == 0) return;

            string tableName = "collection_" + SanitizeTableName(collectionId);

            List<string> sanitizedKeys = new List<string>();
            foreach (string key in documentKeys)
            {
                sanitizedKeys.Add("'" + _Driver.Sanitize(key) + "'");
            }

            string query =
                "DELETE FROM " + tableName + " " +
                "WHERE document_key IN (" + string.Join(", ", sanitizedKeys) + ")";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "batch deleted " + documentKeys.Count + " documents from " + collectionId);
        }

        /// <summary>
        /// Check whether a document record exists by collection ID and document key.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentKey">Document key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the document record exists.</returns>
        public async Task<bool> ExistsAsync(string collectionId, string documentKey, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (string.IsNullOrEmpty(documentKey)) throw new ArgumentNullException(nameof(documentKey));

            string tableName = "collection_" + SanitizeTableName(collectionId);

            string query =
                "SELECT COUNT(*) AS count FROM " + tableName + " " +
                "WHERE document_key = '" + _Driver.Sanitize(documentKey) + "'";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return false;
            long count = Convert.ToInt64(result.Rows[0]["count"]);
            return count > 0;
        }

        /// <summary>
        /// Enumerate document records within a collection with pagination and optional filtering.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="query">Enumeration query parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Paginated enumeration result of document records.</returns>
        public async Task<EnumerationResult<DocumentRecord>> EnumerateAsync(string collectionId, EnumerationQuery query, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (query == null) throw new ArgumentNullException(nameof(query));

            string tableName = "collection_" + SanitizeTableName(collectionId);
            string labelsTableName = tableName + "_labels";
            string tagsTableName = tableName + "_tags";

            int offset = 0;

            if (!string.IsNullOrEmpty(query.ContinuationToken))
            {
                if (int.TryParse(query.ContinuationToken, out int parsedOffset))
                {
                    offset = parsedOffset;
                }
            }

            // Build WHERE clause
            List<string> conditions = new List<string>();

            if (query.CreatedBefore.HasValue)
            {
                conditions.Add("created_utc < '" + _Driver.FormatDateTime(query.CreatedBefore.Value) + "'");
            }

            if (query.CreatedAfter.HasValue)
            {
                conditions.Add("created_utc > '" + _Driver.FormatDateTime(query.CreatedAfter.Value) + "'");
            }

            if (query.DocumentIds != null && query.DocumentIds.Count > 0)
            {
                List<string> sanitizedIds = new List<string>();
                foreach (string docId in query.DocumentIds)
                {
                    sanitizedIds.Add("'" + _Driver.Sanitize(docId) + "'");
                }
                conditions.Add("document_id IN (" + string.Join(",", sanitizedIds) + ")");
            }

            if (query.LabelFilter != null)
            {
                if (query.LabelFilter.Required != null && query.LabelFilter.Required.Count > 0)
                {
                    List<string> sanitizedLabels = new List<string>();
                    foreach (string label in query.LabelFilter.Required)
                    {
                        sanitizedLabels.Add("'" + _Driver.Sanitize(label) + "'");
                    }
                    conditions.Add(
                        "document_key IN (" +
                        "SELECT document_key FROM " + labelsTableName + " " +
                        "WHERE label IN (" + string.Join(",", sanitizedLabels) + ")" +
                        ")");
                }

                if (query.LabelFilter.Excluded != null && query.LabelFilter.Excluded.Count > 0)
                {
                    List<string> sanitizedLabels = new List<string>();
                    foreach (string label in query.LabelFilter.Excluded)
                    {
                        sanitizedLabels.Add("'" + _Driver.Sanitize(label) + "'");
                    }
                    conditions.Add(
                        "document_key NOT IN (" +
                        "SELECT document_key FROM " + labelsTableName + " " +
                        "WHERE label IN (" + string.Join(",", sanitizedLabels) + ")" +
                        ")");
                }
            }

            if (query.TagFilter != null)
            {
                if (query.TagFilter.Required != null && query.TagFilter.Required.Count > 0)
                {
                    foreach (TagCondition tagCondition in query.TagFilter.Required)
                    {
                        string tagClause = BuildTagConditionClause(tagsTableName, tagCondition, false);
                        if (!string.IsNullOrEmpty(tagClause))
                        {
                            conditions.Add(tagClause);
                        }
                    }
                }

                if (query.TagFilter.Excluded != null && query.TagFilter.Excluded.Count > 0)
                {
                    foreach (TagCondition tagCondition in query.TagFilter.Excluded)
                    {
                        string tagClause = BuildTagConditionClause(tagsTableName, tagCondition, true);
                        if (!string.IsNullOrEmpty(tagClause))
                        {
                            conditions.Add(tagClause);
                        }
                    }
                }
            }

            if (query.Terms != null)
            {
                if (query.Terms.Required != null && query.Terms.Required.Count > 0)
                {
                    foreach (string term in query.Terms.Required)
                    {
                        string sanitized = _Driver.Sanitize(term).Replace("%", "\\%").Replace("_", "\\_");
                        conditions.Add("content ILIKE '%" + sanitized + "%'");
                    }
                }

                if (query.Terms.Excluded != null && query.Terms.Excluded.Count > 0)
                {
                    foreach (string term in query.Terms.Excluded)
                    {
                        string sanitized = _Driver.Sanitize(term).Replace("%", "\\%").Replace("_", "\\_");
                        conditions.Add("content NOT ILIKE '%" + sanitized + "%'");
                    }
                }
            }

            string whereClause = "";
            if (conditions.Count > 0)
            {
                whereClause = " WHERE " + string.Join(" AND ", conditions);
            }

            string orderDirection = query.Ordering == EnumerationOrderEnum.CreatedAscending ? "ASC" : "DESC";

            string selectQuery =
                "SELECT " + _SelectColumns + " FROM " + tableName +
                whereClause +
                " ORDER BY created_utc " + orderDirection +
                " LIMIT " + query.MaxResults.ToString(CultureInfo.InvariantCulture) +
                " OFFSET " + offset.ToString(CultureInfo.InvariantCulture);

            string countQuery =
                "SELECT COUNT(*) AS count FROM " + tableName + whereClause;

            DataTable selectResult = await _Driver.ExecuteQueryAsync(selectQuery, false, token).ConfigureAwait(false);
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);

            List<DocumentRecord> documents = DocumentRecord.FromDataTable(selectResult);
            long totalCount = 0;

            if (countResult != null && countResult.Rows.Count > 0)
            {
                totalCount = Convert.ToInt64(countResult.Rows[0]["count"]);
            }

            return EnumerationResult<DocumentRecord>.Create(query, documents, totalCount);
        }

        #endregion

        #region Private-Methods

        private void CalculateContentLength(DocumentRecord document)
        {
            if (document.ContentLength > 0) return;

            if (!string.IsNullOrEmpty(document.Content))
                document.ContentLength = System.Text.Encoding.UTF8.GetByteCount(document.Content);
            else if (document.BinaryData != null)
                document.ContentLength = document.BinaryData.Length;
        }

        private string SanitizeTableName(string collectionId)
        {
            if (string.IsNullOrEmpty(collectionId)) return "unknown";
            return collectionId.Replace("-", "_").Replace(".", "_");
        }

        private string BuildTagConditionClause(string tagsTableName, TagCondition tagCondition, bool excluded)
        {
            if (tagCondition == null) return null;
            if (string.IsNullOrEmpty(tagCondition.Key)) return null;

            string sanitizedKey = _Driver.Sanitize(tagCondition.Key);
            string sanitizedValue = tagCondition.Value != null ? _Driver.Sanitize(tagCondition.Value) : "";
            string inOrNotIn = excluded ? "NOT IN" : "IN";

            switch (tagCondition.Condition)
            {
                case TagConditionEnum.Equals:
                    return "document_key " + inOrNotIn + " (" +
                        "SELECT document_key FROM " + tagsTableName + " " +
                        "WHERE key = '" + sanitizedKey + "' AND value = '" + sanitizedValue + "'" +
                        ")";

                case TagConditionEnum.NotEquals:
                    return "document_key " + (excluded ? "IN" : "NOT IN") + " (" +
                        "SELECT document_key FROM " + tagsTableName + " " +
                        "WHERE key = '" + sanitizedKey + "' AND value = '" + sanitizedValue + "'" +
                        ")";

                case TagConditionEnum.GreaterThan:
                    return "document_key " + inOrNotIn + " (" +
                        "SELECT document_key FROM " + tagsTableName + " " +
                        "WHERE key = '" + sanitizedKey + "' AND value > '" + sanitizedValue + "'" +
                        ")";

                case TagConditionEnum.LessThan:
                    return "document_key " + inOrNotIn + " (" +
                        "SELECT document_key FROM " + tagsTableName + " " +
                        "WHERE key = '" + sanitizedKey + "' AND value < '" + sanitizedValue + "'" +
                        ")";

                case TagConditionEnum.Contains:
                    return "document_key " + inOrNotIn + " (" +
                        "SELECT document_key FROM " + tagsTableName + " " +
                        "WHERE key = '" + sanitizedKey + "' AND value LIKE '%" + sanitizedValue + "%'" +
                        ")";

                case TagConditionEnum.ContainsNot:
                    return "document_key " + inOrNotIn + " (" +
                        "SELECT document_key FROM " + tagsTableName + " " +
                        "WHERE key = '" + sanitizedKey + "' AND value NOT LIKE '%" + sanitizedValue + "%'" +
                        ")";

                case TagConditionEnum.StartsWith:
                    return "document_key " + inOrNotIn + " (" +
                        "SELECT document_key FROM " + tagsTableName + " " +
                        "WHERE key = '" + sanitizedKey + "' AND value LIKE '" + sanitizedValue + "%'" +
                        ")";

                case TagConditionEnum.EndsWith:
                    return "document_key " + inOrNotIn + " (" +
                        "SELECT document_key FROM " + tagsTableName + " " +
                        "WHERE key = '" + sanitizedKey + "' AND value LIKE '%" + sanitizedValue + "'" +
                        ")";

                case TagConditionEnum.IsNull:
                    return "document_key " + (excluded ? "IN" : "NOT IN") + " (" +
                        "SELECT document_key FROM " + tagsTableName + " " +
                        "WHERE key = '" + sanitizedKey + "' AND value IS NOT NULL AND value != ''" +
                        ")";

                case TagConditionEnum.IsNotNull:
                    return "document_key " + inOrNotIn + " (" +
                        "SELECT document_key FROM " + tagsTableName + " " +
                        "WHERE key = '" + sanitizedKey + "' AND value IS NOT NULL AND value != ''" +
                        ")";

                default:
                    return null;
            }
        }

        private string FormatEmbeddings(List<float> embeddings)
        {
            if (embeddings == null || embeddings.Count == 0) return "NULL";

            StringBuilder sb = new StringBuilder();
            sb.Append("'[");

            for (int i = 0; i < embeddings.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(embeddings[i].ToString(CultureInfo.InvariantCulture));
            }

            sb.Append("]'::vector");
            return sb.ToString();
        }

        private string FormatBinaryData(byte[] data)
        {
            if (data == null) return "NULL";

            StringBuilder sb = new StringBuilder();
            sb.Append("E'\\\\x");

            for (int i = 0; i < data.Length; i++)
            {
                sb.Append(data[i].ToString("x2"));
            }

            sb.Append("'");
            return sb.ToString();
        }

        #endregion
    }
}
