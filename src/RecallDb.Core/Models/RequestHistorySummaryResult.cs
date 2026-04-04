namespace RecallDb.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Result of a request history summary query.
    /// </summary>
    public class RequestHistorySummaryResult
    {
        #region Public-Members

        /// <summary>
        /// Time buckets.
        /// </summary>
        public List<RequestHistorySummaryBucket> Buckets { get; set; } = new List<RequestHistorySummaryBucket>();

        /// <summary>
        /// Start of the summary period in UTC.
        /// </summary>
        public DateTime StartUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// End of the summary period in UTC.
        /// </summary>
        public DateTime EndUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Interval used for bucketing.
        /// </summary>
        public string Interval { get; set; } = null;

        /// <summary>
        /// Total number of successful requests.
        /// </summary>
        public long TotalSuccess { get; set; } = 0;

        /// <summary>
        /// Total number of failed requests.
        /// </summary>
        public long TotalFailure { get; set; } = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RequestHistorySummaryResult()
        {
        }

        #endregion
    }
}
