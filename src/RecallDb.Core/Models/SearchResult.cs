namespace RecallDb.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Result of a document search operation.
    /// </summary>
    public class SearchResult
    {
        #region Public-Members

        /// <summary>
        /// Indicates whether the search was successful.
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
        /// List of matching document records with scores.
        /// </summary>
        public List<DocumentRecord> Documents
        {
            get
            {
                return _Documents;
            }
            set
            {
                if (value == null) value = new List<DocumentRecord>();
                _Documents = value;
            }
        }

        #endregion

        #region Private-Members

        private bool _Success = true;
        private int _MaxResults = 10;
        private string _ContinuationToken = null;
        private bool _EndOfResults = true;
        private long _TotalRecords = 0;
        private long _RecordsRemaining = 0;
        private List<DocumentRecord> _Documents = new List<DocumentRecord>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SearchResult()
        {
        }

        #endregion
    }
}
