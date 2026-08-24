namespace RecallDb.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using SyslogLogging;

    using RecallDb.Core.Database;
    using RecallDb.Core.Models;
    using RecallDb.Server.Classes;

    /// <summary>
    /// Request-history operations shared by REST and MCP. All operations require administrator access.
    /// </summary>
    public class RequestHistoryService : ServiceBase
    {
        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="logging">Logging module.</param>
        public RequestHistoryService(DatabaseDriverBase database, LoggingModule logging)
            : base(database, logging)
        {
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Enumerate request-history entries matching a filter (offset-based paging). Admin only.
        /// </summary>
        /// <param name="ctx">Request context. Payload may be a RequestHistoryFilter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping a RequestHistoryListResult.</returns>
        public async Task<ServiceResult> EnumerateAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ctx.Auth.IsAdmin)
                return ServiceResult.Fail(403, "Forbidden", "Admin access required.");

            RequestHistoryFilter filter = ctx.Payload as RequestHistoryFilter ?? new RequestHistoryFilter();
            int maxResults = filter.MaxResults;
            if (maxResults > 250) maxResults = 250;
            if (maxResults < 1) maxResults = 1;

            List<RequestHistoryEntry> entries = await _Database.RequestHistory.SearchAsync(
                filter.Method, filter.StatusCode, filter.SourceIp, filter.StartUtc, filter.EndUtc, maxResults, filter.Offset).ConfigureAwait(false);

            long totalCount = await _Database.RequestHistory.CountAsync(
                filter.Method, filter.StatusCode, filter.SourceIp, filter.StartUtc, filter.EndUtc).ConfigureAwait(false);

            RequestHistoryListResult result = new RequestHistoryListResult();
            result.Success = true;
            result.TotalRecords = totalCount;
            result.MaxResults = maxResults;
            result.Offset = filter.Offset;
            result.Objects = entries;
            return ServiceResult.Ok(result);
        }

        /// <summary>
        /// Time-bucketed summary of request history. Admin only.
        /// </summary>
        /// <param name="ctx">Request context. Payload may be a RequestHistoryFilter (StartUtc/EndUtc/Interval).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping a RequestHistorySummaryResult.</returns>
        public async Task<ServiceResult> SummaryAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ctx.Auth.IsAdmin)
                return ServiceResult.Fail(403, "Forbidden", "Admin access required.");

            RequestHistoryFilter filter = ctx.Payload as RequestHistoryFilter ?? new RequestHistoryFilter();

            DateTime startUtc = filter.StartUtc ?? DateTime.UtcNow.AddHours(-24);
            DateTime endUtc = filter.EndUtc ?? DateTime.UtcNow;
            string interval = string.IsNullOrEmpty(filter.Interval) ? "hour" : filter.Interval;

            RequestHistorySummaryResult summary = await _Database.RequestHistory.SummaryAsync(startUtc, endUtc, interval).ConfigureAwait(false);
            return ServiceResult.Ok(summary);
        }

        /// <summary>
        /// Read a request-history entry by GUID. Admin only.
        /// </summary>
        /// <param name="ctx">Request context. ResourceId carries the GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping a RequestHistoryEntry.</returns>
        public async Task<ServiceResult> ReadAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ctx.Auth.IsAdmin)
                return ServiceResult.Fail(403, "Forbidden", "Admin access required.");

            RequestHistoryEntry entry = await _Database.RequestHistory.ReadAsync(ctx.ResourceId).ConfigureAwait(false);
            if (entry == null)
                return ServiceResult.Fail(404, "Not found", "Request history entry not found.");

            return ServiceResult.Ok(entry);
        }

        /// <summary>
        /// Delete a request-history entry by GUID. Admin only.
        /// </summary>
        /// <param name="ctx">Request context. ResourceId carries the GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult (204).</returns>
        public async Task<ServiceResult> DeleteAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ctx.Auth.IsAdmin)
                return ServiceResult.Fail(403, "Forbidden", "Admin access required.");

            RequestHistoryEntry entry = await _Database.RequestHistory.ReadAsync(ctx.ResourceId).ConfigureAwait(false);
            if (entry == null)
                return ServiceResult.Fail(404, "Not found", "Request history entry not found.");

            await _Database.RequestHistory.DeleteAsync(ctx.ResourceId).ConfigureAwait(false);
            return ServiceResult.NoContent();
        }

        #endregion
    }
}
