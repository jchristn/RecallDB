namespace RecallDb.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using RecallDb.Core.Helpers;

    /// <summary>
    /// Tag record associated with a document in a collection.
    /// </summary>
    public class TagRecord
    {
        #region Public-Members

        /// <summary>
        /// Tag ID.
        /// Default: auto-generated K-sortable ID with prefix tag_.
        /// </summary>
        public string Id
        {
            get
            {
                return _Id;
            }
            set
            {
                if (string.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Id));
                _Id = value;
            }
        }

        /// <summary>
        /// Document key.
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
        /// Document ID (nullable, for grouping chunks).
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
        /// Position (nullable, for chunk-level tags).
        /// </summary>
        public int? Position
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
        /// Tag key.
        /// </summary>
        public string Key
        {
            get
            {
                return _Key;
            }
            set
            {
                if (string.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Key));
                _Key = value;
            }
        }

        /// <summary>
        /// Tag value.
        /// </summary>
        public string Value
        {
            get
            {
                return _Value;
            }
            set
            {
                _Value = value;
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

        #endregion

        #region Private-Members

        private string _Id = IdGenerator.NewTagId();
        private string _DocumentKey = null;
        private string _DocumentId = null;
        private int? _Position = null;
        private string _Key = "key";
        private string _Value = null;
        private DateTime _CreatedUtc = DateTime.UtcNow;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TagRecord()
        {
        }

        /// <summary>
        /// Create from a DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <returns>TagRecord.</returns>
        public static TagRecord FromDataRow(DataRow row)
        {
            if (row == null) return null;

            TagRecord tag = new TagRecord();
            tag.Id = DataTableHelper.GetStringValue(row, "id");
            tag.DocumentKey = DataTableHelper.GetStringValue(row, "document_key");
            tag.DocumentId = DataTableHelper.GetStringValue(row, "document_id");
            tag.Position = DataTableHelper.GetNullableIntValue(row, "position");
            tag.Key = DataTableHelper.GetStringValue(row, "key");
            tag.Value = DataTableHelper.GetStringValue(row, "value");
            tag.CreatedUtc = DataTableHelper.GetDateTimeValue(row, "created_utc");
            return tag;
        }

        /// <summary>
        /// Create a list from a DataTable.
        /// </summary>
        /// <param name="table">DataTable.</param>
        /// <returns>List of TagRecord.</returns>
        public static List<TagRecord> FromDataTable(DataTable table)
        {
            List<TagRecord> result = new List<TagRecord>();
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
