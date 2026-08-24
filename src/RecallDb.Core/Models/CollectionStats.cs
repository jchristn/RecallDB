namespace RecallDb.Core.Models
{
    /// <summary>
    /// Aggregate statistics for a collection.
    /// </summary>
    public class CollectionStats
    {
        #region Public-Members

        /// <summary>
        /// Collection identifier.
        /// </summary>
        public string CollectionId { get; set; } = null;

        /// <summary>
        /// Total number of document chunks in the collection.
        /// </summary>
        public long DocumentCount { get; set; } = 0;

        /// <summary>
        /// Number of distinct document IDs (logical documents) in the collection.
        /// </summary>
        public long UniqueDocumentCount { get; set; } = 0;

        /// <summary>
        /// Total content length across all documents, in bytes.
        /// </summary>
        public long TotalContentLength { get; set; } = 0;

        /// <summary>
        /// Total number of label records in the collection.
        /// </summary>
        public long LabelCount { get; set; } = 0;

        /// <summary>
        /// Total number of tag records in the collection.
        /// </summary>
        public long TagCount { get; set; } = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public CollectionStats()
        {
        }

        #endregion
    }
}
