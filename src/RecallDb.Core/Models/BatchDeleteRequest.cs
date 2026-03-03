namespace RecallDb.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Request body for batch deleting documents by their document keys.
    /// </summary>
    public class BatchDeleteRequest
    {
        #region Public-Members

        /// <summary>
        /// List of document keys to delete.
        /// </summary>
        public List<string> DocumentKeys
        {
            get
            {
                return _DocumentKeys;
            }
            set
            {
                if (value == null) value = new List<string>();
                _DocumentKeys = value;
            }
        }

        #endregion

        #region Private-Members

        private List<string> _DocumentKeys = new List<string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public BatchDeleteRequest()
        {
        }

        #endregion
    }
}
