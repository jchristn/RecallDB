namespace RecallDb.Core.Database.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using RecallDb.Core.Models;

    /// <summary>
    /// Interface for request history data access operations.
    /// </summary>
    public interface IRequestHistoryMethods
    {
        /// <summary>
        /// Insert a request history entry.
        /// </summary>
        /// <param name="entry">Request history entry.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task InsertAsync(RequestHistoryEntry entry, CancellationToken token = default);

        /// <summary>
        /// Read a request history entry by GUID.
        /// </summary>
        /// <param name="guid">Entry GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Request history entry, or null if not found.</returns>
        Task<RequestHistoryEntry> ReadAsync(string guid, CancellationToken token = default);

        /// <summary>
        /// Delete a request history entry by GUID.
        /// </summary>
        /// <param name="guid">Entry GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteAsync(string guid, CancellationToken token = default);

        /// <summary>
        /// Search request history entries with optional filters.
        /// </summary>
        /// <param name="httpMethod">HTTP method filter.</param>
        /// <param name="statusCode">Status code filter.</param>
        /// <param name="sourceIp">Source IP filter.</param>
        /// <param name="startUtc">Start time filter.</param>
        /// <param name="endUtc">End time filter.</param>
        /// <param name="maxResults">Maximum number of results.</param>
        /// <param name="offset">Result offset.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of request history entries.</returns>
        Task<List<RequestHistoryEntry>> SearchAsync(
            string httpMethod = null,
            int? statusCode = null,
            string sourceIp = null,
            DateTime? startUtc = null,
            DateTime? endUtc = null,
            int maxResults = 100,
            int offset = 0,
            CancellationToken token = default);

        /// <summary>
        /// Count request history entries with optional filters.
        /// </summary>
        /// <param name="httpMethod">HTTP method filter.</param>
        /// <param name="statusCode">Status code filter.</param>
        /// <param name="sourceIp">Source IP filter.</param>
        /// <param name="startUtc">Start time filter.</param>
        /// <param name="endUtc">End time filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Count of matching entries.</returns>
        Task<long> CountAsync(
            string httpMethod = null,
            int? statusCode = null,
            string sourceIp = null,
            DateTime? startUtc = null,
            DateTime? endUtc = null,
            CancellationToken token = default);

        /// <summary>
        /// Get a summary of request history bucketed by time interval.
        /// </summary>
        /// <param name="startUtc">Start time.</param>
        /// <param name="endUtc">End time.</param>
        /// <param name="interval">Bucket interval (minute, 15minute, hour, 6hour, day).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Summary result with buckets.</returns>
        Task<RequestHistorySummaryResult> SummaryAsync(
            DateTime startUtc,
            DateTime endUtc,
            string interval,
            CancellationToken token = default);

        /// <summary>
        /// Prune request history entries older than the specified retention period.
        /// </summary>
        /// <param name="retentionDays">Number of days to retain.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task PruneAsync(int retentionDays, CancellationToken token = default);
    }
}
