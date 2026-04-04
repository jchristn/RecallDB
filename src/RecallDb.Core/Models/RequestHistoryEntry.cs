namespace RecallDb.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using RecallDb.Core.Helpers;

    /// <summary>
    /// Request history entry.
    /// </summary>
    public class RequestHistoryEntry
    {
        #region Public-Members

        /// <summary>
        /// Auto-increment primary key.
        /// </summary>
        public long Id { get; set; } = 0;

        /// <summary>
        /// Unique identifier.
        /// </summary>
        public string Guid { get; set; } = System.Guid.NewGuid().ToString();

        /// <summary>
        /// HTTP method.
        /// </summary>
        public string HttpMethod { get; set; } = null;

        /// <summary>
        /// Request URL.
        /// </summary>
        public string RequestUrl { get; set; } = null;

        /// <summary>
        /// Source IP address.
        /// </summary>
        public string SourceIp { get; set; } = null;

        /// <summary>
        /// HTTP status code.
        /// </summary>
        public int StatusCode { get; set; } = 0;

        /// <summary>
        /// Whether the request was successful.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Duration in milliseconds.
        /// </summary>
        public long DurationMs { get; set; } = 0;

        /// <summary>
        /// Request content type.
        /// </summary>
        public string RequestContentType { get; set; } = null;

        /// <summary>
        /// Request body length in bytes.
        /// </summary>
        public long RequestBodyLength { get; set; } = 0;

        /// <summary>
        /// Response content type.
        /// </summary>
        public string ResponseContentType { get; set; } = null;

        /// <summary>
        /// Response body length in bytes.
        /// </summary>
        public long ResponseBodyLength { get; set; } = 0;

        /// <summary>
        /// Request body (truncated).
        /// </summary>
        public string RequestBody { get; set; } = null;

        /// <summary>
        /// Response body (truncated).
        /// </summary>
        public string ResponseBody { get; set; } = null;

        /// <summary>
        /// Creation timestamp in UTC.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RequestHistoryEntry()
        {
        }

        /// <summary>
        /// Create from a DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <returns>RequestHistoryEntry.</returns>
        public static RequestHistoryEntry FromDataRow(DataRow row)
        {
            if (row == null) return null;

            RequestHistoryEntry entry = new RequestHistoryEntry();
            entry.Id = DataTableHelper.GetLongValue(row, "id");
            entry.Guid = DataTableHelper.GetStringValue(row, "guid");
            entry.HttpMethod = DataTableHelper.GetStringValue(row, "http_method");
            entry.RequestUrl = DataTableHelper.GetStringValue(row, "request_url");
            entry.SourceIp = DataTableHelper.GetStringValue(row, "source_ip");
            entry.StatusCode = DataTableHelper.GetIntValue(row, "status_code");
            entry.Success = DataTableHelper.GetBooleanValue(row, "success");
            entry.DurationMs = DataTableHelper.GetLongValue(row, "duration_ms");
            entry.RequestContentType = DataTableHelper.GetStringValue(row, "request_content_type");
            entry.RequestBodyLength = DataTableHelper.GetLongValue(row, "request_body_length");
            entry.ResponseContentType = DataTableHelper.GetStringValue(row, "response_content_type");
            entry.ResponseBodyLength = DataTableHelper.GetLongValue(row, "response_body_length");
            entry.RequestBody = DataTableHelper.GetStringValue(row, "request_body");
            entry.ResponseBody = DataTableHelper.GetStringValue(row, "response_body");
            entry.CreatedUtc = DataTableHelper.GetDateTimeValue(row, "created_utc");
            return entry;
        }

        /// <summary>
        /// Create a list from a DataTable.
        /// </summary>
        /// <param name="table">DataTable.</param>
        /// <returns>List of RequestHistoryEntry.</returns>
        public static List<RequestHistoryEntry> FromDataTable(DataTable table)
        {
            List<RequestHistoryEntry> result = new List<RequestHistoryEntry>();
            if (table == null || table.Rows.Count == 0) return result;
            foreach (DataRow row in table.Rows)
            {
                result.Add(FromDataRow(row));
            }
            return result;
        }

        #endregion
    }
}
