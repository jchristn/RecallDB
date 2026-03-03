namespace RecallDb.Sdk.Models
{
    /// <summary>
    /// Result of a delete operation indicating the number of documents deleted.
    /// </summary>
    public class DeleteResult
    {
        /// <summary>
        /// Number of documents deleted.
        /// </summary>
        public long DocumentsDeleted { get; set; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public DeleteResult()
        {
        }
    }
}
