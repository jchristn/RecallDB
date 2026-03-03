namespace RecallDb.Core.Models
{
    /// <summary>
    /// Result of a delete operation indicating the number of documents deleted.
    /// </summary>
    public class DeleteResult
    {
        #region Public-Members

        /// <summary>
        /// Number of documents deleted.
        /// </summary>
        public long DocumentsDeleted
        {
            get
            {
                return _DocumentsDeleted;
            }
            set
            {
                if (value < 0) value = 0;
                _DocumentsDeleted = value;
            }
        }

        #endregion

        #region Private-Members

        private long _DocumentsDeleted = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public DeleteResult()
        {
        }

        #endregion
    }
}
