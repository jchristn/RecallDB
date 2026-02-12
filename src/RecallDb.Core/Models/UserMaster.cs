namespace RecallDb.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Security.Cryptography;
    using System.Text;
    using RecallDb.Core.Helpers;

    /// <summary>
    /// User master record.
    /// </summary>
    public class UserMaster
    {
        #region Public-Members

        /// <summary>
        /// User ID.
        /// Default: auto-generated K-sortable ID with prefix usr_.
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
        /// Email address.
        /// </summary>
        public string Email
        {
            get
            {
                return _Email;
            }
            set
            {
                if (string.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Email));
                _Email = value;
            }
        }

        /// <summary>
        /// SHA256 hash of the password.
        /// </summary>
        public string PasswordSha256
        {
            get
            {
                return _PasswordSha256;
            }
            set
            {
                _PasswordSha256 = value;
            }
        }

        /// <summary>
        /// First name.
        /// </summary>
        public string FirstName
        {
            get
            {
                return _FirstName;
            }
            set
            {
                _FirstName = value;
            }
        }

        /// <summary>
        /// Last name.
        /// </summary>
        public string LastName
        {
            get
            {
                return _LastName;
            }
            set
            {
                _LastName = value;
            }
        }

        /// <summary>
        /// Whether the user is a global administrator.
        /// Default: false.
        /// </summary>
        public bool IsAdmin
        {
            get
            {
                return _IsAdmin;
            }
            set
            {
                _IsAdmin = value;
            }
        }

        /// <summary>
        /// Whether the user is a tenant administrator.
        /// Default: false.
        /// </summary>
        public bool IsTenantAdmin
        {
            get
            {
                return _IsTenantAdmin;
            }
            set
            {
                _IsTenantAdmin = value;
            }
        }

        /// <summary>
        /// Whether the user is active.
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

        private string _Id = IdGenerator.NewUserId();
        private string _TenantId = "default";
        private string _Email = "user@recall";
        private string _PasswordSha256 = null;
        private string _FirstName = null;
        private string _LastName = null;
        private bool _IsAdmin = false;
        private bool _IsTenantAdmin = false;
        private bool _Active = true;
        private DateTime _CreatedUtc = DateTime.UtcNow;
        private DateTime _LastUpdateUtc = DateTime.UtcNow;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public UserMaster()
        {
        }

        /// <summary>
        /// Create from a DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <returns>UserMaster.</returns>
        public static UserMaster FromDataRow(DataRow row)
        {
            if (row == null) return null;

            UserMaster user = new UserMaster();
            user.Id = DataTableHelper.GetStringValue(row, "id");
            user.TenantId = DataTableHelper.GetStringValue(row, "tenant_id");
            user.Email = DataTableHelper.GetStringValue(row, "email");
            user.PasswordSha256 = DataTableHelper.GetStringValue(row, "password_sha256");
            user.FirstName = DataTableHelper.GetStringValue(row, "first_name");
            user.LastName = DataTableHelper.GetStringValue(row, "last_name");
            user.IsAdmin = DataTableHelper.GetBooleanValue(row, "is_admin");
            user.IsTenantAdmin = DataTableHelper.GetBooleanValue(row, "is_tenant_admin");
            user.Active = DataTableHelper.GetBooleanValue(row, "active");
            user.CreatedUtc = DataTableHelper.GetDateTimeValue(row, "created_utc");
            user.LastUpdateUtc = DataTableHelper.GetDateTimeValue(row, "last_update_utc");
            return user;
        }

        /// <summary>
        /// Create a list from a DataTable.
        /// </summary>
        /// <param name="table">DataTable.</param>
        /// <returns>List of UserMaster.</returns>
        public static List<UserMaster> FromDataTable(DataTable table)
        {
            List<UserMaster> result = new List<UserMaster>();
            if (table == null || table.Rows.Count == 0) return result;
            foreach (DataRow row in table.Rows)
            {
                result.Add(FromDataRow(row));
            }
            return result;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Set the password by computing its SHA256 hash.
        /// </summary>
        /// <param name="password">Plain text password.</param>
        public void SetPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) throw new ArgumentNullException(nameof(password));
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    sb.Append(bytes[i].ToString("x2"));
                }
                _PasswordSha256 = sb.ToString();
            }
        }

        /// <summary>
        /// Verify a password against the stored SHA256 hash.
        /// </summary>
        /// <param name="password">Plain text password.</param>
        /// <returns>True if the password matches.</returns>
        public bool VerifyPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return false;
            if (string.IsNullOrEmpty(_PasswordSha256)) return false;
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    sb.Append(bytes[i].ToString("x2"));
                }
                return string.Equals(_PasswordSha256, sb.ToString(), StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Return a redacted copy of the user (password masked).
        /// </summary>
        /// <param name="user">User to redact.</param>
        /// <returns>Redacted copy.</returns>
        public static UserMaster Redact(UserMaster user)
        {
            if (user == null) return null;
            UserMaster redacted = new UserMaster();
            redacted.Id = user.Id;
            redacted.TenantId = user.TenantId;
            redacted.Email = user.Email;
            redacted.PasswordSha256 = "********";
            redacted.FirstName = user.FirstName;
            redacted.LastName = user.LastName;
            redacted.IsAdmin = user.IsAdmin;
            redacted.IsTenantAdmin = user.IsTenantAdmin;
            redacted.Active = user.Active;
            redacted.CreatedUtc = user.CreatedUtc;
            redacted.LastUpdateUtc = user.LastUpdateUtc;
            return redacted;
        }

        #endregion
    }
}
