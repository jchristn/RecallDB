namespace RecallDb.Server.Services
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    using SyslogLogging;

    using RecallDb.Core.Database;
    using RecallDb.Core.Models;
    using RecallDb.Server.Classes;

    /// <summary>
    /// Tag operations shared by REST and MCP.
    /// </summary>
    public class TagService : ServiceBase
    {
        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="logging">Logging module.</param>
        public TagService(DatabaseDriverBase database, LoggingModule logging)
            : base(database, logging)
        {
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// List tags in a collection (first page).
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping an EnumerationResult of TagRecord.</returns>
        public async Task<ServiceResult> ListAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            Stopwatch sw = Stopwatch.StartNew();
            EnumerationQuery query = new EnumerationQuery();
            EnumerationResult<TagRecord> result = await _Database.Tags.EnumerateAsync(ctx.CollectionId, query).ConfigureAwait(false);
            result.TotalMs = sw.Elapsed.TotalMilliseconds;
            return ServiceResult.Ok(result);
        }

        /// <summary>
        /// Enumerate tags in a collection with pagination.
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping an EnumerationResult of TagRecord.</returns>
        public async Task<ServiceResult> EnumerateAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            EnumerationQuery query = ctx.Query ?? new EnumerationQuery();

            Stopwatch sw = Stopwatch.StartNew();
            EnumerationResult<TagRecord> result = await _Database.Tags.EnumerateAsync(ctx.CollectionId, query).ConfigureAwait(false);
            result.TotalMs = sw.Elapsed.TotalMilliseconds;
            return ServiceResult.Ok(result);
        }

        /// <summary>
        /// Read a tag by ID.
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping a TagRecord.</returns>
        public async Task<ServiceResult> ReadAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            TagRecord tag = await _Database.Tags.ReadAsync(ctx.CollectionId, ctx.ResourceId).ConfigureAwait(false);
            if (tag == null)
                return ServiceResult.Fail(404, "Not found", "Tag not found.");

            return ServiceResult.Ok(tag);
        }

        /// <summary>
        /// Create a tag.
        /// </summary>
        /// <param name="ctx">Request context. Payload must be a TagRecord.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping the created TagRecord (201).</returns>
        public async Task<ServiceResult> CreateAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            TagRecord tag = ctx.Payload as TagRecord;
            if (tag == null)
                return ServiceResult.Fail(400, "Bad request", "Request body is required.");

            tag = await _Database.Tags.CreateAsync(ctx.CollectionId, tag).ConfigureAwait(false);
            return ServiceResult.Ok(tag, 201);
        }

        /// <summary>
        /// Delete a tag by ID.
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult (204).</returns>
        public async Task<ServiceResult> DeleteAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            await _Database.Tags.DeleteAsync(ctx.CollectionId, ctx.ResourceId).ConfigureAwait(false);
            return ServiceResult.NoContent();
        }

        /// <summary>
        /// Return distinct tag keys in a collection (REST only).
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping a list of distinct tag keys.</returns>
        public async Task<ServiceResult> DistinctAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            List<string> keys = await _Database.Tags.DistinctKeysAsync(ctx.CollectionId).ConfigureAwait(false);
            return ServiceResult.Ok(keys);
        }

        #endregion
    }
}
