namespace RecallDb.Sdk.Models
{
    /// <summary>
    /// Enumeration query for paginated listing.
    /// </summary>
    public class EnumerationQuery
    {
        /// <summary>
        /// Maximum number of results to return.
        /// Default: 100. Minimum: 1. Maximum: 1000.
        /// </summary>
        public int MaxResults { get; set; }

        /// <summary>
        /// Continuation token for retrieving the next page of results.
        /// </summary>
        public string ContinuationToken { get; set; }

        /// <summary>
        /// Ordering for enumeration results.
        /// Values: CreatedAscending, CreatedDescending.
        /// Default: CreatedDescending.
        /// </summary>
        public string Ordering { get; set; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public EnumerationQuery()
        {
            MaxResults = 100;
            Ordering = "CreatedDescending";
        }
    }
}
