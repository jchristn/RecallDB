namespace RecallDb.Core.Models
{
    /// <summary>
    /// Aggregate statistics for a single document (grouped by document ID when present, otherwise by document key).
    /// </summary>
    public class DocumentStats
    {
        #region Public-Members

        /// <summary>
        /// Document key the stats were requested for.
        /// </summary>
        public string DocumentKey { get; set; } = null;

        /// <summary>
        /// Document ID grouping the chunks, when present.
        /// </summary>
        public string DocumentId { get; set; } = null;

        /// <summary>
        /// Number of chunks belonging to the document.
        /// </summary>
        public long ChunkCount { get; set; } = 0;

        /// <summary>
        /// Total content length across the document's chunks, in bytes.
        /// </summary>
        public long TotalContentLength { get; set; } = 0;

        /// <summary>
        /// Total number of label records associated with the document.
        /// </summary>
        public long LabelCount { get; set; } = 0;

        /// <summary>
        /// Total number of tag records associated with the document.
        /// </summary>
        public long TagCount { get; set; } = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public DocumentStats()
        {
        }

        #endregion
    }
}
