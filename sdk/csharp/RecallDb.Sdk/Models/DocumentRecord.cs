namespace RecallDb.Sdk.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Document record stored in a collection.
    /// </summary>
    public class DocumentRecord
    {
        /// <summary>
        /// Auto-increment ID from the database.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Document key (unique identifier within the collection).
        /// </summary>
        public string DocumentKey { get; set; }

        /// <summary>
        /// Document ID (groups chunks of the same document).
        /// </summary>
        public string DocumentId { get; set; }

        /// <summary>
        /// Content length in bytes.
        /// </summary>
        public long ContentLength { get; set; }

        /// <summary>
        /// Entity tag.
        /// </summary>
        public string Etag { get; set; }

        /// <summary>
        /// SHA256 hash of the content.
        /// </summary>
        public string Sha256 { get; set; }

        /// <summary>
        /// Position (chunk index within a document).
        /// </summary>
        public int Position { get; set; }

        /// <summary>
        /// Content type.
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Text content.
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Binary data.
        /// </summary>
        public byte[] BinaryData { get; set; }

        /// <summary>
        /// Vector embeddings.
        /// </summary>
        public List<float> Embeddings { get; set; }

        /// <summary>
        /// Creation timestamp in UTC.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Vector distance (populated during search).
        /// </summary>
        public double Distance { get; set; }

        /// <summary>
        /// Score (populated during search).
        /// </summary>
        public double Score { get; set; }

        /// <summary>
        /// Full-text relevance score (populated during full-text or hybrid search).
        /// </summary>
        public double? TextScore { get; set; }

        /// <summary>
        /// Labels associated with this document.
        /// </summary>
        public List<string> Labels { get; set; }

        /// <summary>
        /// Tags associated with this document.
        /// </summary>
        public Dictionary<string, string> Tags { get; set; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public DocumentRecord()
        {
            ContentType = "Text";
            CreatedUtc = DateTime.UtcNow;
        }
    }
}
