namespace RecallDb.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using RecallDb.Core.Helpers;

    /// <summary>
    /// Collection metadata.
    /// </summary>
    public class CollectionMetadata
    {
        #region Public-Members

        /// <summary>
        /// Collection ID.
        /// Default: auto-generated K-sortable ID with prefix col_.
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
        /// Tenant ID.
        /// </summary>
        public string TenantId
        {
            get
            {
                return _TenantId;
            }
            set
            {
                if (string.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(TenantId));
                _TenantId = value;
            }
        }

        /// <summary>
        /// Collection name.
        /// </summary>
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                if (string.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Name));
                _Name = value;
            }
        }

        /// <summary>
        /// Description of the collection.
        /// </summary>
        public string Description
        {
            get
            {
                return _Description;
            }
            set
            {
                _Description = value;
            }
        }

        /// <summary>
        /// Vector dimensionality.
        /// Must be greater than 0.
        /// </summary>
        public int Dimensionality
        {
            get
            {
                return _Dimensionality;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(Dimensionality));
                _Dimensionality = value;
            }
        }

        /// <summary>
        /// Whether the collection is active.
        /// Default: true.
        /// </summary>
        public bool Active
        {
            get
            {
                return _Active;
            }
            set
            {
                _Active = value;
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
        /// Last update timestamp in UTC.
        /// </summary>
        public DateTime LastUpdateUtc
        {
            get
            {
                return _LastUpdateUtc;
            }
            set
            {
                _LastUpdateUtc = value;
            }
        }

        #endregion

        #region Private-Members

        private string _Id = IdGenerator.NewCollectionId();
        private string _TenantId = "default";
        private string _Name = "My collection";
        private string _Description = null;
        private int _Dimensionality = 384;
        private bool _Active = true;
        private DateTime _CreatedUtc = DateTime.UtcNow;
        private DateTime _LastUpdateUtc = DateTime.UtcNow;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public CollectionMetadata()
        {
        }

        /// <summary>
        /// Create from a DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <returns>CollectionMetadata.</returns>
        public static CollectionMetadata FromDataRow(DataRow row)
        {
            if (row == null) return null;

            CollectionMetadata col = new CollectionMetadata();
            col.Id = DataTableHelper.GetStringValue(row, "id");
            col.TenantId = DataTableHelper.GetStringValue(row, "tenant_id");
            col.Name = DataTableHelper.GetStringValue(row, "name");
            col.Description = DataTableHelper.GetStringValue(row, "description");
            col.Dimensionality = DataTableHelper.GetIntValue(row, "dimensionality");
            col.Active = DataTableHelper.GetBooleanValue(row, "active");
            col.CreatedUtc = DataTableHelper.GetDateTimeValue(row, "created_utc");
            col.LastUpdateUtc = DataTableHelper.GetDateTimeValue(row, "last_update_utc");
            return col;
        }

        /// <summary>
        /// Create a list from a DataTable.
        /// </summary>
        /// <param name="table">DataTable.</param>
        /// <returns>List of CollectionMetadata.</returns>
        public static List<CollectionMetadata> FromDataTable(DataTable table)
        {
            List<CollectionMetadata> result = new List<CollectionMetadata>();
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
