namespace RecallDb.Sdk.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Result of a document search operation.
    /// </summary>
    public class SearchResult
    {
        /// <summary>
        /// Indicates whether the search was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Maximum number of results requested.
        /// </summary>
        public int MaxResults { get; set; }

        /// <summary>
        /// Continuation token for retrieving the next page of results.
        /// Null when no more results are available.
        /// </summary>
        public string ContinuationToken { get; set; }

        /// <summary>
        /// Indicates whether the end of results has been reached.
        /// </summary>
        public bool EndOfResults { get; set; }

        /// <summary>
        /// Total number of records available.
        /// </summary>
        public long TotalRecords { get; set; }

        /// <summary>
        /// Number of records remaining after this page.
        /// </summary>
        public long RecordsRemaining { get; set; }

        /// <summary>
        /// List of matching document records with scores.
        /// </summary>
        public List<DocumentRecord> Documents { get; set; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SearchResult()
        {
            Success = true;
            EndOfResults = true;
            Documents = new List<DocumentRecord>();
        }
    }
}
