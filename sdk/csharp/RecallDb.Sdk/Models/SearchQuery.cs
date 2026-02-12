namespace RecallDb.Sdk.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Search query for finding documents.
    /// </summary>
    public class SearchQuery
    {
        /// <summary>
        /// Sort order for search results.
        /// Values: ScoreAscending, ScoreDescending, DistanceAscending, DistanceDescending, CreatedAscending, CreatedDescending.
        /// Default: ScoreDescending.
        /// </summary>
        public string SortOrder { get; set; }

        /// <summary>
        /// Filter to documents created before this date and time.
        /// </summary>
        public DateTime? CreatedBefore { get; set; }

        /// <summary>
        /// Filter to documents created after this date and time.
        /// </summary>
        public DateTime? CreatedAfter { get; set; }

        /// <summary>
        /// List of document IDs to restrict the search to.
        /// </summary>
        public List<string> DocumentIds { get; set; }

        /// <summary>
        /// Label filter for including or excluding documents by label.
        /// </summary>
        public LabelFilter LabelFilter { get; set; }

        /// <summary>
        /// Tag filter set for including or excluding documents by tag conditions.
        /// </summary>
        public TagFilterSet TagFilter { get; set; }

        /// <summary>
        /// Vector query parameters for similarity or distance search.
        /// </summary>
        public VectorQuery Vector { get; set; }

        /// <summary>
        /// Terms filter for including or excluding documents by content terms.
        /// </summary>
        public TermsFilter Terms { get; set; }

        /// <summary>
        /// Maximum number of results to return.
        /// Default: 10. Minimum: 1. Maximum: 1000.
        /// </summary>
        public int MaxResults { get; set; }

        /// <summary>
        /// Continuation token for retrieving the next page of results.
        /// </summary>
        public string ContinuationToken { get; set; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SearchQuery()
        {
            SortOrder = "ScoreDescending";
            DocumentIds = new List<string>();
            MaxResults = 10;
        }
    }
}
