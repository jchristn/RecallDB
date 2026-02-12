namespace RecallDb.Sdk.Models
{
    using System;

    /// <summary>
    /// Tag record associated with a document in a collection.
    /// </summary>
    public class TagRecord
    {
        /// <summary>
        /// Tag ID.
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
        /// Position (nullable, for chunk-level tags).
        /// </summary>
        public int? Position { get; set; }

        /// <summary>
        /// Tag key.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Tag value.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Creation timestamp in UTC.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TagRecord()
        {
            CreatedUtc = DateTime.UtcNow;
        }
    }
}
