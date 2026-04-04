namespace RecallDb.Core.Database.Postgresql.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using RecallDb.Core.Database.Interfaces;
    using RecallDb.Core.Models;

    /// <summary>
    /// PostgreSQL implementation of request history data access operations.
    /// </summary>
    public class RequestHistoryMethods : IRequestHistoryMethods
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private readonly PostgresqlDatabaseDriver _Driver;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[RequestHistoryMethods] ";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL database driver.</param>
        /// <param name="logging">Logging module.</param>
        public RequestHistoryMethods(PostgresqlDatabaseDriver driver, LoggingModule logging)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _Logging = logging;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Insert a request history entry.
        /// </summary>
        /// <param name="entry">Request history entry.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task InsertAsync(RequestHistoryEntry entry, CancellationToken token = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            string query =
                "INSERT INTO request_history " +
                "(guid, http_method, request_url, source_ip, status_code, success, duration_ms, " +
                "request_content_type, request_body_length, response_content_type, response_body_length, " +
                "request_body, response_body, created_utc) " +
                "VALUES (" +
                "'" + _Driver.Sanitize(entry.Guid) + "', " +
                "'" + _Driver.Sanitize(entry.HttpMethod) + "', " +
                "'" + _Driver.Sanitize(entry.RequestUrl) + "', " +
                "'" + _Driver.Sanitize(entry.SourceIp) + "', " +
                entry.StatusCode + ", " +
                _Driver.FormatBoolean(entry.Success) + ", " +
                entry.DurationMs + ", " +
                _Driver.FormatNullableString(entry.RequestContentType) + ", " +
                entry.RequestBodyLength + ", " +
                _Driver.FormatNullableString(entry.ResponseContentType) + ", " +
                entry.ResponseBodyLength + ", " +
                _Driver.FormatNullableString(entry.RequestBody) + ", " +
                _Driver.FormatNullableString(entry.ResponseBody) + ", " +
                "'" + _Driver.FormatDateTime(entry.CreatedUtc) + "'" +
                ")";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "inserted request history entry " + entry.Guid);
        }

        /// <summary>
        /// Read a request history entry by GUID.
        /// </summary>
        /// <param name="guid">Entry GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Request history entry, or null if not found.</returns>
        public async Task<RequestHistoryEntry> ReadAsync(string guid, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(guid)) throw new ArgumentNullException(nameof(guid));

            string query =
                "SELECT * FROM request_history WHERE guid = '" + _Driver.Sanitize(guid) + "' LIMIT 1";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;
            return RequestHistoryEntry.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Delete a request history entry by GUID.
        /// </summary>
        /// <param name="guid">Entry GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteAsync(string guid, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(guid)) throw new ArgumentNullException(nameof(guid));

            string query =
                "DELETE FROM request_history WHERE guid = '" + _Driver.Sanitize(guid) + "'";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "deleted request history entry " + guid);
        }

        /// <summary>
        /// Search request history entries with optional filters.
        /// </summary>
        public async Task<List<RequestHistoryEntry>> SearchAsync(
            string httpMethod = null,
            int? statusCode = null,
            string sourceIp = null,
            DateTime? startUtc = null,
            DateTime? endUtc = null,
            int maxResults = 100,
            int offset = 0,
            CancellationToken token = default)
        {
            string whereClause = BuildWhereClause(httpMethod, statusCode, sourceIp, startUtc, endUtc);

            string query =
                "SELECT * FROM request_history" +
                whereClause +
                " ORDER BY created_utc DESC" +
                " LIMIT " + maxResults +
                " OFFSET " + offset;

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            return RequestHistoryEntry.FromDataTable(result);
        }

        /// <summary>
        /// Count request history entries with optional filters.
        /// </summary>
        public async Task<long> CountAsync(
            string httpMethod = null,
            int? statusCode = null,
            string sourceIp = null,
            DateTime? startUtc = null,
            DateTime? endUtc = null,
            CancellationToken token = default)
        {
            string whereClause = BuildWhereClause(httpMethod, statusCode, sourceIp, startUtc, endUtc);

            string query =
                "SELECT COUNT(*) AS count FROM request_history" + whereClause;

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return 0;
            return Convert.ToInt64(result.Rows[0]["count"]);
        }

        /// <summary>
        /// Get a summary of request history bucketed by time interval.
        /// </summary>
        public async Task<RequestHistorySummaryResult> SummaryAsync(
            DateTime startUtc,
            DateTime endUtc,
            string interval,
            CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(interval)) throw new ArgumentNullException(nameof(interval));

            string truncExpr;

            switch (interval.ToLower())
            {
                case "minute":
                    truncExpr = "date_trunc('minute', created_utc)";
                    break;
                case "15minute":
                    truncExpr = "date_trunc('hour', created_utc) + (EXTRACT(MINUTE FROM created_utc)::int / 15) * INTERVAL '15 minutes'";
                    break;
                case "hour":
                    truncExpr = "date_trunc('hour', created_utc)";
                    break;
                case "6hour":
                    truncExpr = "date_trunc('day', created_utc) + (EXTRACT(HOUR FROM created_utc)::int / 6) * INTERVAL '6 hours'";
                    break;
                case "day":
                    truncExpr = "date_trunc('day', created_utc)";
                    break;
                default:
                    truncExpr = "date_trunc('hour', created_utc)";
                    break;
            }

            string query =
                "SELECT " +
                truncExpr + " AS bucket, " +
                "COUNT(*) FILTER (WHERE success = TRUE) AS success_count, " +
                "COUNT(*) FILTER (WHERE success = FALSE) AS failure_count " +
                "FROM request_history " +
                "WHERE created_utc >= '" + _Driver.FormatDateTime(startUtc) + "' " +
                "AND created_utc <= '" + _Driver.FormatDateTime(endUtc) + "' " +
                "GROUP BY bucket " +
                "ORDER BY bucket ASC";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            RequestHistorySummaryResult summary = new RequestHistorySummaryResult();
            summary.StartUtc = startUtc;
            summary.EndUtc = endUtc;
            summary.Interval = interval;
            summary.TotalSuccess = 0;
            summary.TotalFailure = 0;

            if (result != null && result.Rows.Count > 0)
            {
                foreach (DataRow row in result.Rows)
                {
                    RequestHistorySummaryBucket bucket = new RequestHistorySummaryBucket();
                    bucket.TimestampUtc = DateTime.SpecifyKind(Convert.ToDateTime(row["bucket"]), DateTimeKind.Utc);
                    bucket.SuccessCount = Convert.ToInt64(row["success_count"]);
                    bucket.FailureCount = Convert.ToInt64(row["failure_count"]);

                    summary.Buckets.Add(bucket);
                    summary.TotalSuccess += bucket.SuccessCount;
                    summary.TotalFailure += bucket.FailureCount;
                }
            }

            return summary;
        }

        /// <summary>
        /// Prune request history entries older than the specified retention period.
        /// </summary>
        public async Task PruneAsync(int retentionDays, CancellationToken token = default)
        {
            if (retentionDays < 1) throw new ArgumentException("Retention days must be at least 1.", nameof(retentionDays));

            DateTime cutoff = DateTime.UtcNow.AddDays(-1 * retentionDays);

            string query =
                "DELETE FROM request_history WHERE created_utc < '" + _Driver.FormatDateTime(cutoff) + "'";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (_Logging != null) _Logging.Debug(_Header + "pruned request history entries older than " + retentionDays + " days");
        }

        #endregion

        #region Private-Methods

        private string BuildWhereClause(
            string httpMethod,
            int? statusCode,
            string sourceIp,
            DateTime? startUtc,
            DateTime? endUtc)
        {
            List<string> conditions = new List<string>();

            if (!string.IsNullOrEmpty(httpMethod))
                conditions.Add("http_method = '" + _Driver.Sanitize(httpMethod) + "'");

            if (statusCode != null)
                conditions.Add("status_code = " + statusCode.Value);

            if (!string.IsNullOrEmpty(sourceIp))
                conditions.Add("source_ip = '" + _Driver.Sanitize(sourceIp) + "'");

            if (startUtc != null)
                conditions.Add("created_utc >= '" + _Driver.FormatDateTime(startUtc.Value) + "'");

            if (endUtc != null)
                conditions.Add("created_utc <= '" + _Driver.FormatDateTime(endUtc.Value) + "'");

            if (conditions.Count == 0) return "";

            return " WHERE " + string.Join(" AND ", conditions);
        }

        #endregion
    }
}
