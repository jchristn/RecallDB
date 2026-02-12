namespace RecallDb.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Generic enumeration result for paginated listing.
    /// </summary>
    /// <typeparam name="T">Type of objects in the result set.</typeparam>
    public class EnumerationResult<T>
    {
        #region Public-Members

        /// <summary>
        /// Indicates whether the operation was successful.
        /// Default: true.
        /// </summary>
        public bool Success
        {
            get
            {
                return _Success;
            }
            set
            {
                _Success = value;
            }
        }

        /// <summary>
        /// Maximum number of results requested.
        /// </summary>
        public int MaxResults
        {
            get
            {
                return _MaxResults;
            }
            set
            {
                _MaxResults = value;
            }
        }

        /// <summary>
        /// Continuation token for retrieving the next page of results.
        /// Null when no more results are available.
        /// </summary>
        public string ContinuationToken
        {
            get
            {
                return _ContinuationToken;
            }
            set
            {
                _ContinuationToken = value;
            }
        }

        /// <summary>
        /// Indicates whether the end of results has been reached.
        /// </summary>
        public bool EndOfResults
        {
            get
            {
                return _EndOfResults;
            }
            set
            {
                _EndOfResults = value;
            }
        }

        /// <summary>
        /// Total number of records available.
        /// </summary>
        public long TotalRecords
        {
            get
            {
                return _TotalRecords;
            }
            set
            {
                _TotalRecords = value;
            }
        }

        /// <summary>
        /// Number of records remaining after this page.
        /// </summary>
        public long RecordsRemaining
        {
            get
            {
                return _RecordsRemaining;
            }
            set
            {
                _RecordsRemaining = value;
            }
        }

        /// <summary>
        /// List of objects in this page of results.
        /// </summary>
        public List<T> Objects
        {
            get
            {
                return _Objects;
            }
            set
            {
                if (value == null) value = new List<T>();
                _Objects = value;
            }
        }

        #endregion

        #region Private-Members

        private bool _Success = true;
        private int _MaxResults = 100;
        private string _ContinuationToken = null;
        private bool _EndOfResults = true;
        private long _TotalRecords = 0;
        private long _RecordsRemaining = 0;
        private List<T> _Objects = new List<T>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public EnumerationResult()
        {
        }

        /// <summary>
        /// Create an EnumerationResult from a query, data list, and total count.
        /// Automatically calculates ContinuationToken, EndOfResults, and RecordsRemaining.
        /// </summary>
        /// <param name="query">The enumeration query.</param>
        /// <param name="data">The list of objects for this page.</param>
        /// <param name="totalCount">The total number of records available.</param>
        /// <returns>EnumerationResult.</returns>
        public static EnumerationResult<T> Create(EnumerationQuery query, List<T> data, long totalCount)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (data == null) data = new List<T>();

            EnumerationResult<T> result = new EnumerationResult<T>();
            result.Success = true;
            result.MaxResults = query.MaxResults;
            result.TotalRecords = totalCount;
            result.Objects = data;

            long offset = 0;

            if (!string.IsNullOrEmpty(query.ContinuationToken))
            {
                if (long.TryParse(query.ContinuationToken, out long parsedOffset))
                {
                    offset = parsedOffset;
                }
            }

            long nextOffset = offset + data.Count;

            if (nextOffset >= totalCount || data.Count < query.MaxResults)
            {
                result.EndOfResults = true;
                result.ContinuationToken = null;
                result.RecordsRemaining = 0;
            }
            else
            {
                result.EndOfResults = false;
                result.ContinuationToken = nextOffset.ToString();
                result.RecordsRemaining = totalCount - nextOffset;
            }

            return result;
        }

        #endregion
    }
}
