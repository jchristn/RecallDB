namespace RecallDb.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using RecallDb.Core.Helpers;

    /// <summary>
    /// Label record associated with a document in a collection.
    /// </summary>
    public class LabelRecord
    {
        #region Public-Members

        /// <summary>
        /// Label ID.
        /// Default: auto-generated K-sortable ID with prefix lbl_.
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
        /// Position (nullable, for chunk-level labels).
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
        /// Label value.
        /// </summary>
        public string Label
        {
            get
            {
                return _Label;
            }
            set
            {
                if (string.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Label));
                _Label = value;
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

        private string _Id = IdGenerator.NewLabelId();
        private string _DocumentKey = null;
        private string _DocumentId = null;
        private int? _Position = null;
        private string _Label = "label";
        private DateTime _CreatedUtc = DateTime.UtcNow;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public LabelRecord()
        {
        }

        /// <summary>
        /// Create from a DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <returns>LabelRecord.</returns>
        public static LabelRecord FromDataRow(DataRow row)
        {
            if (row == null) return null;

            LabelRecord label = new LabelRecord();
            label.Id = DataTableHelper.GetStringValue(row, "id");
            label.DocumentKey = DataTableHelper.GetStringValue(row, "document_key");
            label.DocumentId = DataTableHelper.GetStringValue(row, "document_id");
            label.Position = DataTableHelper.GetNullableIntValue(row, "position");
            label.Label = DataTableHelper.GetStringValue(row, "label");
            label.CreatedUtc = DataTableHelper.GetDateTimeValue(row, "created_utc");
            return label;
        }

        /// <summary>
        /// Create a list from a DataTable.
        /// </summary>
        /// <param name="table">DataTable.</param>
        /// <returns>List of LabelRecord.</returns>
        public static List<LabelRecord> FromDataTable(DataTable table)
        {
            List<LabelRecord> result = new List<LabelRecord>();
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
