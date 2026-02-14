namespace RecallDb.Sdk.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Generic enumeration result for paginated listing.
    /// </summary>
    /// <typeparam name="T">Type of objects in the result set.</typeparam>
    public class EnumerationResult<T>
    {
        /// <summary>
        /// Indicates whether the operation was successful.
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
        /// List of objects in this page of results.
        /// </summary>
        public List<T> Objects { get; set; }

        /// <summary>
        /// Total time in milliseconds the query took from start to finish.
        /// </summary>
        public double TotalMs { get; set; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public EnumerationResult()
        {
            Success = true;
            EndOfResults = true;
            Objects = new List<T>();
        }
    }
}
