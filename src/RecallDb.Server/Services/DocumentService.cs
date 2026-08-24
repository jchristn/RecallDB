namespace RecallDb.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using SyslogLogging;

    using RecallDb.Core.Database;
    using RecallDb.Core.Models;
    using RecallDb.Server.Classes;

    /// <summary>
    /// Document operations shared by REST and MCP, including label/tag stitching, batch operations,
    /// filter-based deletion, and per-document statistics.
    /// </summary>
    public class DocumentService : ServiceBase
    {
        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="logging">Logging module.</param>
        public DocumentService(DatabaseDriverBase database, LoggingModule logging)
            : base(database, logging)
        {
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// List documents in a collection (first page), with labels and tags attached.
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping an EnumerationResult of DocumentRecord.</returns>
        public async Task<ServiceResult> ListAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            Stopwatch sw = Stopwatch.StartNew();
            EnumerationQuery query = new EnumerationQuery();
            EnumerationResult<DocumentRecord> result = await _Database.Documents.EnumerateAsync(ctx.CollectionId, query).ConfigureAwait(false);
            await AttachLabelsAndTagsAsync(ctx.CollectionId, result.Objects).ConfigureAwait(false);
            result.TotalMs = sw.Elapsed.TotalMilliseconds;
            return ServiceResult.Ok(result);
        }

        /// <summary>
        /// Enumerate documents in a collection with pagination, with labels and tags attached.
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping an EnumerationResult of DocumentRecord.</returns>
        public async Task<ServiceResult> EnumerateAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            EnumerationQuery query = ctx.Query ?? new EnumerationQuery();

            Stopwatch sw = Stopwatch.StartNew();
            EnumerationResult<DocumentRecord> result = await _Database.Documents.EnumerateAsync(ctx.CollectionId, query).ConfigureAwait(false);
            await AttachLabelsAndTagsAsync(ctx.CollectionId, result.Objects).ConfigureAwait(false);
            result.TotalMs = sw.Elapsed.TotalMilliseconds;
            return ServiceResult.Ok(result);
        }

        /// <summary>
        /// Read a document by key, with labels and tags attached.
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping a DocumentRecord.</returns>
        public async Task<ServiceResult> ReadAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            DocumentRecord doc = await _Database.Documents.ReadAsync(ctx.CollectionId, ctx.DocumentKey).ConfigureAwait(false);
            if (doc == null)
                return ServiceResult.Fail(404, "Not found", "Document not found.");

            await AttachLabelsAndTagsAsync(ctx.CollectionId, doc).ConfigureAwait(false);
            return ServiceResult.Ok(doc);
        }

        /// <summary>
        /// Read a document chunk by document ID and position, with labels and tags attached.
        /// </summary>
        /// <param name="ctx">Request context. Position must be set.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping a DocumentRecord.</returns>
        public async Task<ServiceResult> ReadByPositionAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            if (!ctx.Position.HasValue)
                return ServiceResult.Fail(400, "Bad request", "Position must be an integer.");

            DocumentRecord doc = await _Database.Documents.ReadByDocumentIdAndPositionAsync(ctx.CollectionId, ctx.DocumentId, ctx.Position.Value).ConfigureAwait(false);
            if (doc == null)
                return ServiceResult.Fail(404, "Not found", "Document chunk not found.");

            await AttachLabelsAndTagsAsync(ctx.CollectionId, doc).ConfigureAwait(false);
            return ServiceResult.Ok(doc);
        }

        /// <summary>
        /// Test document existence.
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult.</returns>
        public async Task<ServiceResult> ExistsAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            bool exists = await _Database.Documents.ExistsAsync(ctx.CollectionId, ctx.DocumentKey).ConfigureAwait(false);
            return ServiceResult.Ok(exists, exists ? 200 : 404);
        }

        /// <summary>
        /// Create a document, validating embedding dimensionality and persisting labels and tags.
        /// </summary>
        /// <param name="ctx">Request context. Payload must be a DocumentRecord.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping the created DocumentRecord (201).</returns>
        public async Task<ServiceResult> CreateAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            DocumentRecord doc = ctx.Payload as DocumentRecord;
            if (doc == null)
                return ServiceResult.Fail(400, "Bad request", "Request body is required.");

            CollectionMetadata col = await _Database.Collections.ReadAsync(ctx.TenantId, ctx.CollectionId).ConfigureAwait(false);
            if (col == null)
                return ServiceResult.Fail(404, "Not found", "Collection not found.");

            if (doc.Embeddings != null && doc.Embeddings.Count != col.Dimensionality)
                return ServiceResult.Fail(400, "Bad request", "Embeddings dimensionality mismatch. Expected " + col.Dimensionality + " dimensions, but received " + doc.Embeddings.Count + ".");

            List<string> reqLabels = doc.Labels;
            Dictionary<string, string> reqTags = doc.Tags;

            doc = await _Database.Documents.CreateAsync(ctx.CollectionId, doc).ConfigureAwait(false);

            doc.Labels = reqLabels;
            doc.Tags = reqTags;
            await PersistLabelsAndTagsAsync(ctx.CollectionId, doc).ConfigureAwait(false);
            await AttachLabelsAndTagsAsync(ctx.CollectionId, doc).ConfigureAwait(false);

            return ServiceResult.Ok(doc, 201);
        }

        /// <summary>
        /// Update a document, replacing labels and tags when provided.
        /// </summary>
        /// <param name="ctx">Request context. Payload must be a DocumentRecord.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping the updated DocumentRecord.</returns>
        public async Task<ServiceResult> UpdateAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            DocumentRecord doc = ctx.Payload as DocumentRecord;
            if (doc == null)
                return ServiceResult.Fail(400, "Bad request", "Request body is required.");

            List<string> reqLabels = doc.Labels;
            Dictionary<string, string> reqTags = doc.Tags;

            doc.DocumentKey = ctx.DocumentKey;
            doc = await _Database.Documents.UpdateAsync(ctx.CollectionId, doc).ConfigureAwait(false);
            if (doc == null)
                return ServiceResult.Fail(404, "Not found", "Document not found.");

            if (reqLabels != null)
            {
                await _Database.Labels.DeleteByDocumentKeyAsync(ctx.CollectionId, ctx.DocumentKey).ConfigureAwait(false);
                doc.Labels = reqLabels;
            }

            if (reqTags != null)
            {
                await _Database.Tags.DeleteByDocumentKeyAsync(ctx.CollectionId, ctx.DocumentKey).ConfigureAwait(false);
                doc.Tags = reqTags;
            }

            await PersistLabelsAndTagsAsync(ctx.CollectionId, doc).ConfigureAwait(false);
            await AttachLabelsAndTagsAsync(ctx.CollectionId, doc).ConfigureAwait(false);

            return ServiceResult.Ok(doc);
        }

        /// <summary>
        /// Delete a document and its labels and tags.
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult (204).</returns>
        public async Task<ServiceResult> DeleteAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            await _Database.Labels.DeleteByDocumentKeyAsync(ctx.CollectionId, ctx.DocumentKey).ConfigureAwait(false);
            await _Database.Tags.DeleteByDocumentKeyAsync(ctx.CollectionId, ctx.DocumentKey).ConfigureAwait(false);
            await _Database.Documents.DeleteAsync(ctx.CollectionId, ctx.DocumentKey).ConfigureAwait(false);
            return ServiceResult.NoContent();
        }

        /// <summary>
        /// Create a batch of documents transactionally, validating dimensionality and persisting labels and tags.
        /// </summary>
        /// <param name="ctx">Request context. Payload must be a List of DocumentRecord.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping the created documents (201).</returns>
        public async Task<ServiceResult> BatchCreateAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            List<DocumentRecord> docs = ctx.Payload as List<DocumentRecord>;
            if (docs == null || docs.Count == 0)
                return ServiceResult.Fail(400, "Bad request", "Request body must contain a list of documents.");

            CollectionMetadata col = await _Database.Collections.ReadAsync(ctx.TenantId, ctx.CollectionId).ConfigureAwait(false);
            if (col == null)
                return ServiceResult.Fail(404, "Not found", "Collection not found.");

            foreach (DocumentRecord d in docs)
            {
                if (d.Embeddings != null && d.Embeddings.Count != col.Dimensionality)
                    return ServiceResult.Fail(400, "Bad request", "Embeddings dimensionality mismatch for document '" + d.DocumentKey + "'. Expected " + col.Dimensionality + " dimensions, but received " + d.Embeddings.Count + ".");
            }

            Dictionary<string, List<string>> reqLabelsMap = new Dictionary<string, List<string>>();
            Dictionary<string, Dictionary<string, string>> reqTagsMap = new Dictionary<string, Dictionary<string, string>>();
            foreach (DocumentRecord d in docs)
            {
                if (d.Labels != null) reqLabelsMap[d.DocumentKey] = d.Labels;
                if (d.Tags != null) reqTagsMap[d.DocumentKey] = d.Tags;
            }

            List<DocumentRecord> created = await _Database.Documents.CreateBatchAsync(ctx.CollectionId, docs).ConfigureAwait(false);

            foreach (DocumentRecord d in created)
            {
                if (reqLabelsMap.ContainsKey(d.DocumentKey)) d.Labels = reqLabelsMap[d.DocumentKey];
                if (reqTagsMap.ContainsKey(d.DocumentKey)) d.Tags = reqTagsMap[d.DocumentKey];
                await PersistLabelsAndTagsAsync(ctx.CollectionId, d).ConfigureAwait(false);
            }

            await AttachLabelsAndTagsAsync(ctx.CollectionId, created).ConfigureAwait(false);

            return ServiceResult.Ok(created, 201);
        }

        /// <summary>
        /// Delete a batch of documents by key, along with their labels and tags.
        /// </summary>
        /// <param name="ctx">Request context. Payload must be a BatchDeleteRequest.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult (204).</returns>
        public async Task<ServiceResult> BatchDeleteAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            BatchDeleteRequest batchReq = ctx.Payload as BatchDeleteRequest;
            if (batchReq == null || batchReq.DocumentKeys == null || batchReq.DocumentKeys.Count == 0)
                return ServiceResult.Fail(400, "Bad request", "Request body must contain a non-empty list of document keys.");

            await _Database.Labels.DeleteByDocumentKeysAsync(ctx.CollectionId, batchReq.DocumentKeys).ConfigureAwait(false);
            await _Database.Tags.DeleteByDocumentKeysAsync(ctx.CollectionId, batchReq.DocumentKeys).ConfigureAwait(false);
            await _Database.Documents.DeleteBatchAsync(ctx.CollectionId, batchReq.DocumentKeys).ConfigureAwait(false);
            return ServiceResult.NoContent();
        }

        /// <summary>
        /// Delete all documents matching an enumeration filter, along with their labels and tags.
        /// </summary>
        /// <param name="ctx">Request context. Query carries the filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping a DeleteResult.</returns>
        public async Task<ServiceResult> DeleteByFilterAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            EnumerationQuery query = ctx.Query ?? new EnumerationQuery();

            List<string> allKeys = new List<string>();
            query.MaxResults = 1000;
            query.ContinuationToken = null;

            while (true)
            {
                EnumerationResult<DocumentRecord> result = await _Database.Documents.EnumerateAsync(ctx.CollectionId, query).ConfigureAwait(false);

                if (result.Objects != null)
                {
                    foreach (DocumentRecord doc in result.Objects)
                    {
                        allKeys.Add(doc.DocumentKey);
                    }
                }

                if (result.EndOfResults || result.ContinuationToken == null)
                    break;

                query.ContinuationToken = result.ContinuationToken;
            }

            if (allKeys.Count > 0)
            {
                await _Database.Labels.DeleteByDocumentKeysAsync(ctx.CollectionId, allKeys).ConfigureAwait(false);
                await _Database.Tags.DeleteByDocumentKeysAsync(ctx.CollectionId, allKeys).ConfigureAwait(false);
                await _Database.Documents.DeleteBatchAsync(ctx.CollectionId, allKeys).ConfigureAwait(false);
            }

            DeleteResult deleteResult = new DeleteResult();
            deleteResult.DocumentsDeleted = allKeys.Count;
            return ServiceResult.Ok(deleteResult);
        }

        /// <summary>
        /// Compute aggregate statistics for a single document.
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping a DocumentStats.</returns>
        public async Task<ServiceResult> StatsAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            CollectionMetadata col = await _Database.Collections.ReadAsync(ctx.TenantId, ctx.CollectionId).ConfigureAwait(false);
            if (col == null)
                return ServiceResult.Fail(404, "Not found", "Collection not found.");

            DocumentRecord doc = await _Database.Documents.ReadAsync(ctx.CollectionId, ctx.DocumentKey).ConfigureAwait(false);
            if (doc == null)
                return ServiceResult.Fail(404, "Not found", "Document not found.");

            string tableName = ctx.CollectionId.Replace("-", "_").Replace(".", "_");
            string docsTable = "collection_" + tableName;
            string labelsTable = "collection_" + tableName + "_labels";
            string tagsTable = "collection_" + tableName + "_tags";

            string docFilter;
            string documentId;

            if (!string.IsNullOrEmpty(doc.DocumentId))
            {
                documentId = doc.DocumentId;
                string safeDocId = _Database.Sanitize(doc.DocumentId);
                docFilter = "document_id = '" + safeDocId + "'";
            }
            else
            {
                documentId = null;
                string safeDocKey = _Database.Sanitize(ctx.DocumentKey);
                docFilter = "document_key = '" + safeDocKey + "'";
            }

            string query =
                "SELECT " +
                "(SELECT COUNT(*) FROM " + docsTable + " WHERE " + docFilter + ") AS chunk_count, " +
                "(SELECT COALESCE(SUM(content_length), 0) FROM " + docsTable + " WHERE " + docFilter + ") AS total_content_length, " +
                "(SELECT COUNT(*) FROM " + labelsTable + " WHERE " + docFilter + ") AS label_count, " +
                "(SELECT COUNT(*) FROM " + tagsTable + " WHERE " + docFilter + ") AS tag_count;";

            System.Data.DataTable dt = await _Database.ExecuteQueryAsync(query).ConfigureAwait(false);

            DocumentStats stats = new DocumentStats();
            stats.DocumentKey = ctx.DocumentKey;
            stats.DocumentId = documentId;

            if (dt != null && dt.Rows.Count > 0)
            {
                System.Data.DataRow row = dt.Rows[0];
                stats.ChunkCount = Convert.ToInt64(row["chunk_count"]);
                stats.TotalContentLength = Convert.ToInt64(row["total_content_length"]);
                stats.LabelCount = Convert.ToInt64(row["label_count"]);
                stats.TagCount = Convert.ToInt64(row["tag_count"]);
            }

            return ServiceResult.Ok(stats);
        }

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Attach labels and tags to a list of documents. Exposed to the search service for match/neighbor enrichment.
        /// </summary>
        /// <param name="collectionId">Collection identifier.</param>
        /// <param name="docs">Documents to enrich.</param>
        /// <returns>Task.</returns>
        internal async Task AttachLabelsAndTagsAsync(string collectionId, List<DocumentRecord> docs)
        {
            if (docs == null || docs.Count == 0) return;

            List<string> documentKeys = docs.Select(d => d.DocumentKey).ToList();

            Dictionary<string, List<string>> labelsMap = await _Database.Labels.GetByDocumentKeysAsync(collectionId, documentKeys).ConfigureAwait(false);
            Dictionary<string, Dictionary<string, string>> tagsMap = await _Database.Tags.GetByDocumentKeysAsync(collectionId, documentKeys).ConfigureAwait(false);

            foreach (DocumentRecord doc in docs)
            {
                doc.Labels = labelsMap.ContainsKey(doc.DocumentKey) ? labelsMap[doc.DocumentKey] : new List<string>();
                doc.Tags = tagsMap.ContainsKey(doc.DocumentKey) ? tagsMap[doc.DocumentKey] : new Dictionary<string, string>();
            }
        }

        #endregion

        #region Private-Methods

        private async Task AttachLabelsAndTagsAsync(string collectionId, DocumentRecord doc)
        {
            if (doc == null) return;
            await AttachLabelsAndTagsAsync(collectionId, new List<DocumentRecord> { doc }).ConfigureAwait(false);
        }

        private async Task PersistLabelsAndTagsAsync(string collectionId, DocumentRecord doc)
        {
            if (doc.Labels != null && doc.Labels.Count > 0)
            {
                foreach (string label in doc.Labels)
                {
                    LabelRecord lr = new LabelRecord();
                    lr.DocumentKey = doc.DocumentKey;
                    lr.DocumentId = doc.DocumentId;
                    lr.Position = doc.Position;
                    lr.Label = label;
                    await _Database.Labels.CreateAsync(collectionId, lr).ConfigureAwait(false);
                }
            }

            if (doc.Tags != null && doc.Tags.Count > 0)
            {
                foreach (KeyValuePair<string, string> tag in doc.Tags)
                {
                    TagRecord tr = new TagRecord();
                    tr.DocumentKey = doc.DocumentKey;
                    tr.DocumentId = doc.DocumentId;
                    tr.Position = doc.Position;
                    tr.Key = tag.Key;
                    tr.Value = tag.Value;
                    await _Database.Tags.CreateAsync(collectionId, tr).ConfigureAwait(false);
                }
            }
        }

        #endregion
    }
}
