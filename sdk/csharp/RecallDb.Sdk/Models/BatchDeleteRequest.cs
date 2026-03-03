namespace RecallDb.Sdk.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Request body for batch deleting documents by their document keys.
    /// </summary>
    public class BatchDeleteRequest
    {
        /// <summary>
        /// List of document keys to delete.
        /// </summary>
        public List<string> DocumentKeys { get; set; } = new List<string>();

        /// <summary>
        /// Instantiate.
        /// </summary>
        public BatchDeleteRequest()
        {
        }
    }
}
