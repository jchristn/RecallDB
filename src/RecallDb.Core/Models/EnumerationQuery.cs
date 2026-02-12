namespace RecallDb.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using RecallDb.Core.Enums;

    /// <summary>
    /// Enumeration query for paginated listing with optional filtering.
    /// Supports the same filters as search queries, except for vector search.
    /// </summary>
    public class EnumerationQuery
    {
        #region Public-Members

        /// <summary>
        /// Maximum number of results to return.
        /// Default: 100. Minimum: 1. Maximum: 1000.
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

        /// <summary>
        /// Ordering for enumeration results.
        /// Default: CreatedDescending.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public EnumerationOrderEnum Ordering
        {
            get
            {
                return _Ordering;
            }
            set
            {
                _Ordering = value;
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
        /// List of document IDs to restrict results to.
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

        #endregion

        #region Private-Members

        private int _MaxResults = 100;
        private string _ContinuationToken = null;
        private EnumerationOrderEnum _Ordering = EnumerationOrderEnum.CreatedDescending;
        private DateTime? _CreatedBefore = null;
        private DateTime? _CreatedAfter = null;
        private List<string> _DocumentIds = new List<string>();
        private LabelFilter _LabelFilter = null;
        private TagFilterSet _TagFilter = null;
        private TermsFilter _Terms = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public EnumerationQuery()
        {
        }

        #endregion
    }
}
