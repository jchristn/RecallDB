namespace RecallDb.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using RecallDb.Core.Enums;

    /// <summary>
    /// Filter for document labels.
    /// </summary>
    public class LabelFilter
    {
        #region Public-Members

        /// <summary>
        /// Labels the document must carry: every one of them when RequiredMode is All (the default), or at least one
        /// when it is Any. An empty list does not filter.
        /// </summary>
        public List<string> Required
        {
            get
            {
                return _Required;
            }
            set
            {
                if (value == null) value = new List<string>();
                _Required = value;
            }
        }

        /// <summary>
        /// Labels of which the document must carry none. An empty list does not filter.
        /// </summary>
        public List<string> Excluded
        {
            get
            {
                return _Excluded;
            }
            set
            {
                if (value == null) value = new List<string>();
                _Excluded = value;
            }
        }

        /// <summary>
        /// How Required combines its labels.
        /// All (default): the document must carry every required label. Any: at least one of them.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not a defined LabelMatchModeEnum.</exception>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public LabelMatchModeEnum RequiredMode
        {
            get
            {
                return _RequiredMode;
            }
            set
            {
                if (!Enum.IsDefined(typeof(LabelMatchModeEnum), value))
                    throw new ArgumentOutOfRangeException(nameof(RequiredMode), "LabelFilter.RequiredMode must be one of All or Any.");
                _RequiredMode = value;
            }
        }

        #endregion

        #region Private-Members

        private LabelMatchModeEnum _RequiredMode = LabelMatchModeEnum.All;
        private List<string> _Required = new List<string>();
        private List<string> _Excluded = new List<string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public LabelFilter()
        {
        }

        #endregion
    }
}
