namespace RecallDb.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Filter for document labels.
    /// </summary>
    public class LabelFilter
    {
        #region Public-Members

        /// <summary>
        /// Labels that must be present on the document.
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
        /// Labels that must not be present on the document.
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

        #endregion

        #region Private-Members

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
