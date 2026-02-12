namespace RecallDb.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using RecallDb.Core.Helpers;

    /// <summary>
    /// API credential with bearer token.
    /// </summary>
    public class Credential
    {
        #region Public-Members

        /// <summary>
        /// Credential ID.
        /// Default: auto-generated K-sortable ID with prefix cred_.
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
        /// User ID.
        /// </summary>
        public string UserId
        {
            get
            {
                return _UserId;
            }
            set
            {
                if (string.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(UserId));
                _UserId = value;
            }
        }

        /// <summary>
        /// Bearer token.
        /// Default: auto-generated 64-char alphanumeric token.
        /// </summary>
        public string BearerToken
        {
            get
            {
                return _BearerToken;
            }
            set
            {
                if (string.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(BearerToken));
                _BearerToken = value;
            }
        }

        /// <summary>
        /// Credential name.
        /// </summary>
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                _Name = value;
            }
        }

        /// <summary>
        /// Whether the credential is active.
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

        private string _Id = IdGenerator.NewCredentialId();
        private string _TenantId = "default";
        private string _UserId = "default";
        private string _BearerToken = IdGenerator.NewBearerToken();
        private string _Name = null;
        private bool _Active = true;
        private DateTime _CreatedUtc = DateTime.UtcNow;
        private DateTime _LastUpdateUtc = DateTime.UtcNow;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public Credential()
        {
        }

        /// <summary>
        /// Create from a DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <returns>Credential.</returns>
        public static Credential FromDataRow(DataRow row)
        {
            if (row == null) return null;

            Credential cred = new Credential();
            cred.Id = DataTableHelper.GetStringValue(row, "id");
            cred.TenantId = DataTableHelper.GetStringValue(row, "tenant_id");
            cred.UserId = DataTableHelper.GetStringValue(row, "user_id");
            cred.BearerToken = DataTableHelper.GetStringValue(row, "bearer_token");
            cred.Name = DataTableHelper.GetStringValue(row, "name");
            cred.Active = DataTableHelper.GetBooleanValue(row, "active");
            cred.CreatedUtc = DataTableHelper.GetDateTimeValue(row, "created_utc");
            cred.LastUpdateUtc = DataTableHelper.GetDateTimeValue(row, "last_update_utc");
            return cred;
        }

        /// <summary>
        /// Create a list from a DataTable.
        /// </summary>
        /// <param name="table">DataTable.</param>
        /// <returns>List of Credential.</returns>
        public static List<Credential> FromDataTable(DataTable table)
        {
            List<Credential> result = new List<Credential>();
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
