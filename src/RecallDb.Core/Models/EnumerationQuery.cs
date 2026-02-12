namespace RecallDb.Core.Models
{
    using System;
    using System.Text.Json.Serialization;
    using RecallDb.Core.Enums;

    /// <summary>
    /// Enumeration query for paginated listing.
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

        #endregion

        #region Private-Members

        private int _MaxResults = 100;
        private string _ContinuationToken = null;
        private EnumerationOrderEnum _Ordering = EnumerationOrderEnum.CreatedDescending;

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
