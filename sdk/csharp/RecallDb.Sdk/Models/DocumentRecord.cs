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
        public string? DocumentKey { get; set; }

        /// <summary>
        /// Document ID (groups chunks of the same document).
        /// </summary>
        public string? DocumentId { get; set; }

        /// <summary>
        /// Content length in bytes.
        /// </summary>
        public long ContentLength { get; set; }

        /// <summary>
        /// Entity tag.
        /// </summary>
        public string? Etag { get; set; }

        /// <summary>
        /// SHA256 hash of the content.
        /// </summary>
        public string? Sha256 { get; set; }

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
        public string? Content { get; set; }

        /// <summary>
        /// Binary data.
        /// </summary>
        public byte[]? BinaryData { get; set; }

        /// <summary>
        /// Vector embeddings.
        /// </summary>
        public List<float>? Embeddings { get; set; }

        /// <summary>
        /// Creation timestamp in UTC.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Vector distance (populated during search).
        /// </summary>
        public double Distance { get; set; }

        /// <summary>
        /// Score (populated during search). In hybrid Rrf and Linear searches this is the fused score in the range 0.0 to 1.0.
        /// </summary>
        public double Score { get; set; }

        /// <summary>
        /// Raw vector similarity in the vector search type's units (populated during vector-only and hybrid search).
        /// Null when not applicable.
        /// </summary>
        public double? VectorScore { get; set; }

        /// <summary>
        /// Full-text relevance score, the raw ts_rank value (populated during full-text or hybrid search).
        /// In hybrid Rrf and Linear searches this is null for documents that did not match the text query.
        /// </summary>
        public double? TextScore { get; set; }

        /// <summary>
        /// 1-based rank of this document in the hybrid vector leg (Rrf and Linear strategies only).
        /// Null when the document was outside the vector leg's candidates or the search was not hybrid.
        /// </summary>
        public int? VectorRank { get; set; }

        /// <summary>
        /// 1-based rank of this document in the hybrid text leg (Rrf and Linear strategies only).
        /// Null when the document did not match the text query or the search was not hybrid.
        /// </summary>
        public int? TextRank { get; set; }

        /// <summary>
        /// 1-based rank of this document's recency key among the candidates, 1 = newest (hybrid Rrf search with
        /// Hybrid.RecencyWeight above 0). Every candidate of one collapse group shares a rank. Null otherwise.
        /// </summary>
        public int? RecencyRank { get; set; }

        /// <summary>
        /// The group this hit represents when the search is collapsed: the DocumentId or tag value it was grouped
        /// by, or its DocumentKey when it has none. Null when the search is not collapsed.
        /// </summary>
        public string? GroupKey { get; set; }

        /// <summary>
        /// Number of candidates in this hit's group, including this hit, when the search is collapsed. Null otherwise.
        /// </summary>
        public int? GroupHits { get; set; }

        /// <summary>
        /// Neighboring chunks surrounding this document in positional order.
        /// Populated when IncludeNeighbors is specified in the search query. Null when not requested.
        /// </summary>
        public List<DocumentRecord>? Neighbors { get; set; }

        /// <summary>
        /// Labels associated with this document.
        /// </summary>
        public List<string>? Labels { get; set; }

        /// <summary>
        /// Tags associated with this document.
        /// </summary>
        public Dictionary<string, string>? Tags { get; set; }

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
