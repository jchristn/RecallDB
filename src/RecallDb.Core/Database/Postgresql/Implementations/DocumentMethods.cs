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
        /// Enumerate document records within a collection with pagination.
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
                "SELECT " + _SelectColumns + " FROM " + tableName + " " +
                "ORDER BY created_utc " + orderDirection + " " +
                "LIMIT " + query.MaxResults.ToString(CultureInfo.InvariantCulture) + " " +
                "OFFSET " + offset.ToString(CultureInfo.InvariantCulture);

            string countQuery =
                "SELECT COUNT(*) AS count FROM " + tableName;

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

        private string SanitizeTableName(string collectionId)
        {
            if (string.IsNullOrEmpty(collectionId)) return "unknown";
            return collectionId.Replace("-", "_").Replace(".", "_");
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
