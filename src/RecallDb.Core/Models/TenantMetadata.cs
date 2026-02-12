namespace RecallDb.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using RecallDb.Core.Helpers;

    /// <summary>
    /// Tenant metadata.
    /// </summary>
    public class TenantMetadata
    {
        #region Public-Members

        /// <summary>
        /// Tenant ID.
        /// Default: auto-generated K-sortable ID with prefix ten_.
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
        /// Tenant name.
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
        /// Whether the tenant is active.
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
        /// Labels.
        /// </summary>
        public List<string> Labels
        {
            get
            {
                return _Labels;
            }
            set
            {
                if (value == null) value = new List<string>();
                _Labels = value;
            }
        }

        /// <summary>
        /// Tags.
        /// </summary>
        public Dictionary<string, string> Tags
        {
            get
            {
                return _Tags;
            }
            set
            {
                if (value == null) value = new Dictionary<string, string>();
                _Tags = value;
            }
        }

        /// <summary>
        /// JSON representation of labels for database storage.
        /// </summary>
        public string LabelsJson
        {
            get
            {
                if (_Labels == null || _Labels.Count == 0) return null;
                return Serializer.SerializeJson(_Labels);
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _Labels = new List<string>();
                }
                else
                {
                    _Labels = Serializer.DeserializeJson<List<string>>(value);
                    if (_Labels == null) _Labels = new List<string>();
                }
            }
        }

        /// <summary>
        /// JSON representation of tags for database storage.
        /// </summary>
        public string TagsJson
        {
            get
            {
                if (_Tags == null || _Tags.Count == 0) return null;
                return Serializer.SerializeJson(_Tags);
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _Tags = new Dictionary<string, string>();
                }
                else
                {
                    _Tags = Serializer.DeserializeJson<Dictionary<string, string>>(value);
                    if (_Tags == null) _Tags = new Dictionary<string, string>();
                }
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

        private string _Id = IdGenerator.NewTenantId();
        private string _Name = "My tenant";
        private bool _Active = true;
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();
        private DateTime _CreatedUtc = DateTime.UtcNow;
        private DateTime _LastUpdateUtc = DateTime.UtcNow;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TenantMetadata()
        {
        }

        /// <summary>
        /// Create from a DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <returns>TenantMetadata.</returns>
        public static TenantMetadata FromDataRow(DataRow row)
        {
            if (row == null) return null;

            TenantMetadata tenant = new TenantMetadata();
            tenant.Id = DataTableHelper.GetStringValue(row, "id");
            tenant.Name = DataTableHelper.GetStringValue(row, "name");
            tenant.Active = DataTableHelper.GetBooleanValue(row, "active");
            tenant.LabelsJson = DataTableHelper.GetStringValue(row, "labels_json");
            tenant.TagsJson = DataTableHelper.GetStringValue(row, "tags_json");
            tenant.CreatedUtc = DataTableHelper.GetDateTimeValue(row, "created_utc");
            tenant.LastUpdateUtc = DataTableHelper.GetDateTimeValue(row, "last_update_utc");
            return tenant;
        }

        /// <summary>
        /// Create a list from a DataTable.
        /// </summary>
        /// <param name="table">DataTable.</param>
        /// <returns>List of TenantMetadata.</returns>
        public static List<TenantMetadata> FromDataTable(DataTable table)
        {
            List<TenantMetadata> result = new List<TenantMetadata>();
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
