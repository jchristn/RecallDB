namespace RecallDb.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Filter for document content terms.
    /// </summary>
    public class TermsFilter
    {
        #region Public-Members

        /// <summary>
        /// Terms that must be present in the document content (case-insensitive substring match, all must match).
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
        /// Terms that must not be present in the document content (case-insensitive substring match, none may match).
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
        public TermsFilter()
        {
        }

        #endregion
    }
}
