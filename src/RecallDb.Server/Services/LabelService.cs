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
    /// Label operations shared by REST and MCP.
    /// </summary>
    public class LabelService : ServiceBase
    {
        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="logging">Logging module.</param>
        public LabelService(DatabaseDriverBase database, LoggingModule logging)
            : base(database, logging)
        {
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// List labels in a collection (first page).
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping an EnumerationResult of LabelRecord.</returns>
        public async Task<ServiceResult> ListAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            Stopwatch sw = Stopwatch.StartNew();
            EnumerationQuery query = new EnumerationQuery();
            EnumerationResult<LabelRecord> result = await _Database.Labels.EnumerateAsync(ctx.CollectionId, query).ConfigureAwait(false);
            result.TotalMs = sw.Elapsed.TotalMilliseconds;
            return ServiceResult.Ok(result);
        }

        /// <summary>
        /// Enumerate labels in a collection with pagination.
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping an EnumerationResult of LabelRecord.</returns>
        public async Task<ServiceResult> EnumerateAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            EnumerationQuery query = ctx.Query ?? new EnumerationQuery();

            Stopwatch sw = Stopwatch.StartNew();
            EnumerationResult<LabelRecord> result = await _Database.Labels.EnumerateAsync(ctx.CollectionId, query).ConfigureAwait(false);
            result.TotalMs = sw.Elapsed.TotalMilliseconds;
            return ServiceResult.Ok(result);
        }

        /// <summary>
        /// Read a label by ID.
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping a LabelRecord.</returns>
        public async Task<ServiceResult> ReadAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            LabelRecord label = await _Database.Labels.ReadAsync(ctx.CollectionId, ctx.ResourceId).ConfigureAwait(false);
            if (label == null)
                return ServiceResult.Fail(404, "Not found", "Label not found.");

            return ServiceResult.Ok(label);
        }

        /// <summary>
        /// Create a label.
        /// </summary>
        /// <param name="ctx">Request context. Payload must be a LabelRecord.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping the created LabelRecord (201).</returns>
        public async Task<ServiceResult> CreateAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            LabelRecord label = ctx.Payload as LabelRecord;
            if (label == null)
                return ServiceResult.Fail(400, "Bad request", "Request body is required.");

            label = await _Database.Labels.CreateAsync(ctx.CollectionId, label).ConfigureAwait(false);
            return ServiceResult.Ok(label, 201);
        }

        /// <summary>
        /// Delete a label by ID.
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult (204).</returns>
        public async Task<ServiceResult> DeleteAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            await _Database.Labels.DeleteAsync(ctx.CollectionId, ctx.ResourceId).ConfigureAwait(false);
            return ServiceResult.NoContent();
        }

        /// <summary>
        /// Return distinct label values in a collection (REST only).
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping a list of distinct label strings.</returns>
        public async Task<ServiceResult> DistinctAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            List<string> labels = await _Database.Labels.DistinctAsync(ctx.CollectionId).ConfigureAwait(false);
            return ServiceResult.Ok(labels);
        }

        #endregion
    }
}
