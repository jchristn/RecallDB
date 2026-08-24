namespace RecallDb.Server.Services
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    using SyslogLogging;

    using RecallDb.Core.Database;
    using RecallDb.Core.Models;
    using RecallDb.Server.Classes;

    /// <summary>
    /// Collection operations shared by REST and MCP, including collection statistics.
    /// </summary>
    public class CollectionService : ServiceBase
    {
        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="logging">Logging module.</param>
        public CollectionService(DatabaseDriverBase database, LoggingModule logging)
            : base(database, logging)
        {
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// List collections in a tenant (first page).
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping an EnumerationResult of CollectionMetadata.</returns>
        public async Task<ServiceResult> ListAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            Stopwatch sw = Stopwatch.StartNew();
            EnumerationQuery query = new EnumerationQuery();
            EnumerationResult<CollectionMetadata> result = await _Database.Collections.EnumerateAsync(ctx.TenantId, query).ConfigureAwait(false);
            result.TotalMs = sw.Elapsed.TotalMilliseconds;
            return ServiceResult.Ok(result);
        }

        /// <summary>
        /// Enumerate collections in a tenant with pagination.
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping an EnumerationResult of CollectionMetadata.</returns>
        public async Task<ServiceResult> EnumerateAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            EnumerationQuery query = ctx.Query ?? new EnumerationQuery();

            Stopwatch sw = Stopwatch.StartNew();
            EnumerationResult<CollectionMetadata> result = await _Database.Collections.EnumerateAsync(ctx.TenantId, query).ConfigureAwait(false);
            result.TotalMs = sw.Elapsed.TotalMilliseconds;
            return ServiceResult.Ok(result);
        }

        /// <summary>
        /// Read a collection by ID.
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping a CollectionMetadata.</returns>
        public async Task<ServiceResult> ReadAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            CollectionMetadata col = await _Database.Collections.ReadAsync(ctx.TenantId, ctx.CollectionId).ConfigureAwait(false);
            if (col == null)
                return ServiceResult.Fail(404, "Not found", "Collection not found.");

            return ServiceResult.Ok(col);
        }

        /// <summary>
        /// Test collection existence.
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult.</returns>
        public async Task<ServiceResult> ExistsAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            bool exists = await _Database.Collections.ExistsAsync(ctx.TenantId, ctx.CollectionId).ConfigureAwait(false);
            return ServiceResult.Ok(exists, exists ? 200 : 404);
        }

        /// <summary>
        /// Create a collection and its backing tables (admin or tenant admin).
        /// </summary>
        /// <param name="ctx">Request context. Payload must be a CollectionMetadata.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping the created CollectionMetadata (201).</returns>
        public async Task<ServiceResult> CreateAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ctx.Auth.IsAdmin && !ctx.Auth.IsTenantAdmin)
                return ServiceResult.Fail(403, "Forbidden", "Admin or tenant admin required.");

            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            CollectionMetadata col = ctx.Payload as CollectionMetadata;
            if (col == null)
                return ServiceResult.Fail(400, "Bad request", "Request body is required.");

            col.TenantId = ctx.TenantId;
            col = await _Database.Collections.CreateAsync(col).ConfigureAwait(false);
            return ServiceResult.Ok(col, 201);
        }

        /// <summary>
        /// Update a collection. Requires tenant access (no elevated admin gate, preserving REST behavior).
        /// </summary>
        /// <param name="ctx">Request context. Payload must be a CollectionMetadata.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping the updated CollectionMetadata.</returns>
        public async Task<ServiceResult> UpdateAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            CollectionMetadata col = ctx.Payload as CollectionMetadata;
            if (col == null)
                return ServiceResult.Fail(400, "Bad request", "Request body is required.");

            col.Id = ctx.CollectionId;
            col.TenantId = ctx.TenantId;
            col = await _Database.Collections.UpdateAsync(col).ConfigureAwait(false);
            if (col == null)
                return ServiceResult.Fail(404, "Not found", "Collection not found.");

            return ServiceResult.Ok(col);
        }

        /// <summary>
        /// Delete a collection and drop its backing tables (admin or tenant admin).
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult (204).</returns>
        public async Task<ServiceResult> DeleteAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ctx.Auth.IsAdmin && !ctx.Auth.IsTenantAdmin)
                return ServiceResult.Fail(403, "Forbidden", "Admin or tenant admin required.");

            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            await _Database.Collections.DeleteAsync(ctx.TenantId, ctx.CollectionId).ConfigureAwait(false);
            return ServiceResult.NoContent();
        }

        /// <summary>
        /// Compute aggregate statistics for a collection.
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping a CollectionStats.</returns>
        public async Task<ServiceResult> StatsAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            CollectionMetadata col = await _Database.Collections.ReadAsync(ctx.TenantId, ctx.CollectionId).ConfigureAwait(false);
            if (col == null)
                return ServiceResult.Fail(404, "Not found", "Collection not found.");

            string tableName = ctx.CollectionId.Replace("-", "_").Replace(".", "_");
            string docsTable = "collection_" + tableName;
            string labelsTable = "collection_" + tableName + "_labels";
            string tagsTable = "collection_" + tableName + "_tags";

            string query =
                "SELECT " +
                "(SELECT COUNT(*) FROM " + docsTable + ") AS document_count, " +
                "(SELECT COUNT(DISTINCT document_id) FROM " + docsTable + " WHERE document_id IS NOT NULL) AS unique_document_count, " +
                "(SELECT COALESCE(SUM(content_length), 0) FROM " + docsTable + ") AS total_content_length, " +
                "(SELECT COUNT(*) FROM " + labelsTable + ") AS label_count, " +
                "(SELECT COUNT(*) FROM " + tagsTable + ") AS tag_count;";

            System.Data.DataTable dt = await _Database.ExecuteQueryAsync(query).ConfigureAwait(false);

            CollectionStats stats = new CollectionStats();
            stats.CollectionId = ctx.CollectionId;

            if (dt != null && dt.Rows.Count > 0)
            {
                System.Data.DataRow row = dt.Rows[0];
                stats.DocumentCount = Convert.ToInt64(row["document_count"]);
                stats.UniqueDocumentCount = Convert.ToInt64(row["unique_document_count"]);
                stats.TotalContentLength = Convert.ToInt64(row["total_content_length"]);
                stats.LabelCount = Convert.ToInt64(row["label_count"]);
                stats.TagCount = Convert.ToInt64(row["tag_count"]);
            }

            return ServiceResult.Ok(stats);
        }

        #endregion
    }
}
