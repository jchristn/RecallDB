namespace RecallDb.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Set of tag conditions used for filtering documents.
    /// </summary>
    public class TagFilterSet
    {
        #region Public-Members

        /// <summary>
        /// Tag conditions that must all be satisfied.
        /// </summary>
        public List<TagCondition> Required
        {
            get
            {
                return _Required;
            }
            set
            {
                if (value == null) value = new List<TagCondition>();
                _Required = value;
            }
        }

        /// <summary>
        /// Tag conditions that must not be satisfied.
        /// </summary>
        public List<TagCondition> Excluded
        {
            get
            {
                return _Excluded;
            }
            set
            {
                if (value == null) value = new List<TagCondition>();
                _Excluded = value;
            }
        }

        #endregion

        #region Private-Members

        private List<TagCondition> _Required = new List<TagCondition>();
        private List<TagCondition> _Excluded = new List<TagCondition>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TagFilterSet()
        {
        }

        #endregion
    }
}
