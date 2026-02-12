namespace RecallDb.Core.Models
{
    using System;
    using System.Text.Json.Serialization;
    using RecallDb.Core.Enums;

    /// <summary>
    /// Tag filter for search queries.
    /// </summary>
    public class TagFilter
    {
        #region Public-Members

        /// <summary>
        /// Tag key to filter on.
        /// </summary>
        public string Key
        {
            get
            {
                return _Key;
            }
            set
            {
                if (string.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Key));
                _Key = value;
            }
        }

        /// <summary>
        /// Condition to apply.
        /// Default: Equals.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TagConditionEnum Condition
        {
            get
            {
                return _Condition;
            }
            set
            {
                _Condition = value;
            }
        }

        /// <summary>
        /// Value to compare against.
        /// </summary>
        public string Value
        {
            get
            {
                return _Value;
            }
            set
            {
                _Value = value;
            }
        }

        #endregion

        #region Private-Members

        private string _Key = "key";
        private TagConditionEnum _Condition = TagConditionEnum.Equals;
        private string _Value = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TagFilter()
        {
        }

        #endregion
    }
}
