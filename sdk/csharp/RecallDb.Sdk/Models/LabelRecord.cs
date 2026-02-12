namespace RecallDb.Sdk.Models
{
    using System;

    /// <summary>
    /// Label record associated with a document in a collection.
    /// </summary>
    public class LabelRecord
    {
        /// <summary>
        /// Label ID.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Document key.
        /// </summary>
        public string DocumentKey { get; set; }

        /// <summary>
        /// Document ID (nullable, for grouping chunks).
        /// </summary>
        public string DocumentId { get; set; }

        /// <summary>
        /// Position (nullable, for chunk-level labels).
        /// </summary>
        public int? Position { get; set; }

        /// <summary>
        /// Label value.
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Creation timestamp in UTC.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public LabelRecord()
        {
            CreatedUtc = DateTime.UtcNow;
        }
    }
}
