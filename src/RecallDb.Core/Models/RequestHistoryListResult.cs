namespace RecallDb.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Offset-paginated result for request-history listing.
    /// </summary>
    public class RequestHistoryListResult
    {
        #region Public-Members

        /// <summary>
        /// Whether the operation succeeded. Default: true.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Total number of records matching the filter.
        /// </summary>
        public long TotalRecords { get; set; } = 0;

        /// <summary>
        /// Maximum number of results requested.
        /// </summary>
        public int MaxResults { get; set; } = 100;

        /// <summary>
        /// Offset into the result set for this page.
        /// </summary>
        public int Offset { get; set; } = 0;

        /// <summary>
        /// Request-history entries in this page.
        /// </summary>
        public List<RequestHistoryEntry> Objects
        {
            get
            {
                return _Objects;
            }
            set
            {
                if (value == null) value = new List<RequestHistoryEntry>();
                _Objects = value;
            }
        }

        #endregion

        #region Private-Members

        private List<RequestHistoryEntry> _Objects = new List<RequestHistoryEntry>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RequestHistoryListResult()
        {
        }

        #endregion
    }
}
