namespace RecallDb.Core.Models
{
    using System;

    /// <summary>
    /// Typed filter for request-history enumeration and summary operations. Populated identically by the REST
    /// query string and by MCP tool arguments.
    /// </summary>
    public class RequestHistoryFilter
    {
        #region Public-Members

        /// <summary>
        /// HTTP method filter. Null for no filter.
        /// </summary>
        public string Method { get; set; } = null;

        /// <summary>
        /// Status code filter. Null for no filter.
        /// </summary>
        public int? StatusCode { get; set; } = null;

        /// <summary>
        /// Source IP filter. Null for no filter.
        /// </summary>
        public string SourceIp { get; set; } = null;

        /// <summary>
        /// Start time filter (UTC). Null for no lower bound.
        /// </summary>
        public DateTime? StartUtc { get; set; } = null;

        /// <summary>
        /// End time filter (UTC). Null for no upper bound.
        /// </summary>
        public DateTime? EndUtc { get; set; } = null;

        /// <summary>
        /// Maximum number of results. Default: 100. Clamped to a maximum of 250 by the service.
        /// </summary>
        public int MaxResults { get; set; } = 100;

        /// <summary>
        /// Offset into the result set. Default: 0.
        /// </summary>
        public int Offset { get; set; } = 0;

        /// <summary>
        /// Bucket interval for summary operations (minute, 15minute, hour, 6hour, day). Default: hour.
        /// </summary>
        public string Interval { get; set; } = "hour";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RequestHistoryFilter()
        {
        }

        #endregion
    }
}
