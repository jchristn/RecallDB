namespace RecallDb.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using RecallDb.Core.Enums;

    /// <summary>
    /// Search query for finding documents.
    /// </summary>
    public class SearchQuery
    {
        #region Public-Members

        /// <summary>
        /// Sort order for search results.
        /// Default: ScoreDescending.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SortOrderEnum SortOrder
        {
            get
            {
                return _SortOrder;
            }
            set
            {
                _SortOrder = value;
            }
        }

        /// <summary>
        /// Filter to documents created before this date and time.
        /// </summary>
        public DateTime? CreatedBefore
        {
            get
            {
                return _CreatedBefore;
            }
            set
            {
                _CreatedBefore = value;
            }
        }

        /// <summary>
        /// Filter to documents created after this date and time.
        /// </summary>
        public DateTime? CreatedAfter
        {
            get
            {
                return _CreatedAfter;
            }
            set
            {
                _CreatedAfter = value;
            }
        }

        /// <summary>
        /// List of document IDs to restrict the search to.
        /// </summary>
        public List<string> DocumentIds
        {
            get
            {
                return _DocumentIds;
            }
            set
            {
                if (value == null) value = new List<string>();
                _DocumentIds = value;
            }
        }

        /// <summary>
        /// Label filter for including or excluding documents by label.
        /// </summary>
        public LabelFilter LabelFilter
        {
            get
            {
                return _LabelFilter;
            }
            set
            {
                _LabelFilter = value;
            }
        }

        /// <summary>
        /// Tag filter set for including or excluding documents by tag conditions.
        /// </summary>
        public TagFilterSet TagFilter
        {
            get
            {
                return _TagFilter;
            }
            set
            {
                _TagFilter = value;
            }
        }

        /// <summary>
        /// Vector query parameters for similarity or distance search.
        /// </summary>
        public VectorQuery Vector
        {
            get
            {
                return _Vector;
            }
            set
            {
                _Vector = value;
            }
        }

        /// <summary>
        /// Full-text search query parameters for content relevance scoring.
        /// When provided without Vector, performs a standalone full-text search.
        /// When provided with Vector, performs a hybrid search combining both scores.
        /// </summary>
        public FullTextQuery FullText
        {
            get
            {
                return _FullText;
            }
            set
            {
                _FullText = value;
            }
        }

        /// <summary>
        /// Terms filter for including or excluding documents by content terms.
        /// </summary>
        public TermsFilter Terms
        {
            get
            {
                return _Terms;
            }
            set
            {
                _Terms = value;
            }
        }

        /// <summary>
        /// Minimum score threshold for results.
        /// </summary>
        public double? MinimumScore
        {
            get
            {
                return _MinimumScore;
            }
            set
            {
                _MinimumScore = value;
            }
        }

        /// <summary>
        /// Maximum score threshold for results.
        /// </summary>
        public double? MaximumScore
        {
            get
            {
                return _MaximumScore;
            }
            set
            {
                _MaximumScore = value;
            }
        }

        /// <summary>
        /// Minimum distance threshold for results.
        /// </summary>
        public double? MinimumDistance
        {
            get
            {
                return _MinimumDistance;
            }
            set
            {
                _MinimumDistance = value;
            }
        }

        /// <summary>
        /// Maximum distance threshold for results.
        /// </summary>
        public double? MaximumDistance
        {
            get
            {
                return _MaximumDistance;
            }
            set
            {
                _MaximumDistance = value;
            }
        }

        /// <summary>
        /// Maximum number of results to return.
        /// Default: 10. Minimum: 1. Maximum: 1000.
        /// Values outside the range are clamped.
        /// </summary>
        public int MaxResults
        {
            get
            {
                return _MaxResults;
            }
            set
            {
                if (value < 1) value = 1;
                if (value > 1000) value = 1000;
                _MaxResults = value;
            }
        }

        /// <summary>
        /// Continuation token for retrieving the next page of results.
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

        #endregion

        #region Private-Members

        private SortOrderEnum _SortOrder = SortOrderEnum.ScoreDescending;
        private DateTime? _CreatedBefore = null;
        private DateTime? _CreatedAfter = null;
        private List<string> _DocumentIds = new List<string>();
        private LabelFilter _LabelFilter = null;
        private TagFilterSet _TagFilter = null;
        private VectorQuery _Vector = null;
        private FullTextQuery _FullText = null;
        private TermsFilter _Terms = null;
        private double? _MinimumScore = null;
        private double? _MaximumScore = null;
        private double? _MinimumDistance = null;
        private double? _MaximumDistance = null;
        private int _MaxResults = 10;
        private string _ContinuationToken = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SearchQuery()
        {
        }

        #endregion
    }
}
