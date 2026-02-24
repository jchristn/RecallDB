namespace RecallDb.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using RecallDb.Core.Enums;
    using RecallDb.Core.Helpers;

    /// <summary>
    /// Document record stored in a collection.
    /// </summary>
    public class DocumentRecord
    {
        #region Public-Members

        /// <summary>
        /// Auto-increment ID from the database.
        /// </summary>
        public long Id
        {
            get
            {
                return _Id;
            }
            set
            {
                _Id = value;
            }
        }

        /// <summary>
        /// Document key (unique identifier within the collection).
        /// </summary>
        public string DocumentKey
        {
            get
            {
                return _DocumentKey;
            }
            set
            {
                if (string.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(DocumentKey));
                _DocumentKey = value;
            }
        }

        /// <summary>
        /// Document ID (groups chunks of the same document).
        /// </summary>
        public string DocumentId
        {
            get
            {
                return _DocumentId;
            }
            set
            {
                _DocumentId = value;
            }
        }

        /// <summary>
        /// Content length in bytes.
        /// </summary>
        public long ContentLength
        {
            get
            {
                return _ContentLength;
            }
            set
            {
                _ContentLength = value;
            }
        }

        /// <summary>
        /// Entity tag.
        /// </summary>
        public string Etag
        {
            get
            {
                return _Etag;
            }
            set
            {
                _Etag = value;
            }
        }

        /// <summary>
        /// SHA256 hash of the content.
        /// </summary>
        public string Sha256
        {
            get
            {
                return _Sha256;
            }
            set
            {
                _Sha256 = value;
            }
        }

        /// <summary>
        /// Position (chunk index within a document).
        /// </summary>
        public int Position
        {
            get
            {
                return _Position;
            }
            set
            {
                _Position = value;
            }
        }

        /// <summary>
        /// Content type.
        /// Default: Text.
        /// </summary>
        public ContentTypeEnum ContentType
        {
            get
            {
                return _ContentType;
            }
            set
            {
                _ContentType = value;
            }
        }

        /// <summary>
        /// Text content.
        /// </summary>
        public string Content
        {
            get
            {
                return _Content;
            }
            set
            {
                _Content = value;
            }
        }

        /// <summary>
        /// Binary data.
        /// </summary>
        public byte[] BinaryData
        {
            get
            {
                return _BinaryData;
            }
            set
            {
                _BinaryData = value;
            }
        }

        /// <summary>
        /// Vector embeddings.
        /// </summary>
        public List<float> Embeddings
        {
            get
            {
                return _Embeddings;
            }
            set
            {
                _Embeddings = value;
            }
        }

        /// <summary>
        /// Creation timestamp in UTC.
        /// </summary>
        public DateTime CreatedUtc
        {
            get
            {
                return _CreatedUtc;
            }
            set
            {
                _CreatedUtc = value;
            }
        }

        /// <summary>
        /// Vector distance (transient, populated during search).
        /// Only present when a vector query is used; 0.0 for full-text-only searches.
        /// </summary>
        public double Distance
        {
            get
            {
                return _Distance;
            }
            set
            {
                _Distance = value;
            }
        }

        /// <summary>
        /// Score (transient, populated during search).
        /// </summary>
        public double Score
        {
            get
            {
                return _Score;
            }
            set
            {
                _Score = value;
            }
        }

        /// <summary>
        /// Full-text relevance score (transient, populated during search).
        /// Only present when a FullText query is used.
        /// </summary>
        public double? TextScore
        {
            get
            {
                return _TextScore;
            }
            set
            {
                _TextScore = value;
            }
        }

        /// <summary>
        /// Labels associated with this document (populated by the server layer, not stored in the documents table).
        /// </summary>
        public List<string> Labels
        {
            get
            {
                return _Labels;
            }
            set
            {
                _Labels = value;
            }
        }

        /// <summary>
        /// Neighboring chunks surrounding this document in positional order.
        /// Populated when IncludeNeighbors is specified in the search query. Null when not requested.
        /// </summary>
        public List<DocumentRecord> Neighbors
        {
            get
            {
                return _Neighbors;
            }
            set
            {
                _Neighbors = value;
            }
        }

        /// <summary>
        /// Tags associated with this document (populated by the server layer, not stored in the documents table).
        /// </summary>
        public Dictionary<string, string> Tags
        {
            get
            {
                return _Tags;
            }
            set
            {
                _Tags = value;
            }
        }

        #endregion

        #region Private-Members

        private long _Id = 0;
        private string _DocumentKey = IdGenerator.NewDocumentKey();
        private string _DocumentId = null;
        private long _ContentLength = 0;
        private string _Etag = null;
        private string _Sha256 = null;
        private int _Position = 0;
        private ContentTypeEnum _ContentType = ContentTypeEnum.Text;
        private string _Content = null;
        private byte[] _BinaryData = null;
        private List<float> _Embeddings = null;
        private DateTime _CreatedUtc = DateTime.UtcNow;
        private double _Distance = 0;
        private double _Score = 0;
        private double? _TextScore = null;
        private List<DocumentRecord> _Neighbors = null;
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public DocumentRecord()
        {
        }

        /// <summary>
        /// Create from a DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <returns>DocumentRecord.</returns>
        public static DocumentRecord FromDataRow(DataRow row)
        {
            if (row == null) return null;

            DocumentRecord doc = new DocumentRecord();
            doc.Id = DataTableHelper.GetLongValue(row, "id");
            doc.DocumentKey = DataTableHelper.GetStringValue(row, "document_key");
            doc.DocumentId = DataTableHelper.GetStringValue(row, "document_id");
            doc.ContentLength = DataTableHelper.GetLongValue(row, "content_length");
            doc.Etag = DataTableHelper.GetStringValue(row, "etag");
            doc.Sha256 = DataTableHelper.GetStringValue(row, "sha256");
            doc.Position = DataTableHelper.GetIntValue(row, "position");

            string contentTypeStr = DataTableHelper.GetStringValue(row, "content_type");
            if (!string.IsNullOrEmpty(contentTypeStr))
            {
                if (Enum.TryParse<ContentTypeEnum>(contentTypeStr, true, out ContentTypeEnum ct))
                    doc.ContentType = ct;
            }

            doc.Content = DataTableHelper.GetStringValue(row, "content");
            doc.BinaryData = DataTableHelper.GetByteArrayValue(row, "binary_data");
            doc.Embeddings = DataTableHelper.GetFloatArrayValue(row, "embeddings");
            doc.CreatedUtc = DataTableHelper.GetDateTimeValue(row, "created_utc");

            // Distance may or may not be present (only in search results)
            if (row.Table.Columns.Contains("distance"))
            {
                doc.Distance = DataTableHelper.GetDoubleValue(row, "distance");
            }

            // Score may or may not be present (only in search results)
            if (row.Table.Columns.Contains("score"))
            {
                doc.Score = DataTableHelper.GetDoubleValue(row, "score");
            }

            // TextScore may or may not be present (only in full-text or hybrid search results)
            if (row.Table.Columns.Contains("text_score"))
            {
                doc.TextScore = DataTableHelper.GetDoubleValue(row, "text_score");
            }

            return doc;
        }

        /// <summary>
        /// Create a list from a DataTable.
        /// </summary>
        /// <param name="table">DataTable.</param>
        /// <returns>List of DocumentRecord.</returns>
        public static List<DocumentRecord> FromDataTable(DataTable table)
        {
            List<DocumentRecord> result = new List<DocumentRecord>();
            if (table == null || table.Rows.Count == 0) return result;
            foreach (DataRow row in table.Rows)
            {
                result.Add(FromDataRow(row));
            }
            return result;
        }

        #endregion
    }
}
