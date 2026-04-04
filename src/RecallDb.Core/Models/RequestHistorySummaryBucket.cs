namespace RecallDb.Core.Models
{
    using System;

    /// <summary>
    /// A single time bucket in a request history summary.
    /// </summary>
    public class RequestHistorySummaryBucket
    {
        #region Public-Members

        /// <summary>
        /// Bucket timestamp in UTC.
        /// </summary>
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Number of successful requests in this bucket.
        /// </summary>
        public long SuccessCount { get; set; } = 0;

        /// <summary>
        /// Number of failed requests in this bucket.
        /// </summary>
        public long FailureCount { get; set; } = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RequestHistorySummaryBucket()
        {
        }

        #endregion
    }
}
