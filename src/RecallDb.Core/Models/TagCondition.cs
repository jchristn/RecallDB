namespace RecallDb.Core.Models
{
    using System;
    using System.Text.Json.Serialization;
    using RecallDb.Core.Enums;

    /// <summary>
    /// A single tag condition used for filtering.
    /// </summary>
    public class TagCondition
    {
        #region Public-Members

        /// <summary>
        /// Tag key.
        /// </summary>
        public string Key
        {
            get
            {
                return _Key;
            }
            set
            {
                _Key = value;
            }
        }

        /// <summary>
        /// Condition to evaluate against the tag value.
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

        private string _Key = null;
        private TagConditionEnum _Condition = TagConditionEnum.Equals;
        private string _Value = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TagCondition()
        {
        }

        #endregion
    }
}
