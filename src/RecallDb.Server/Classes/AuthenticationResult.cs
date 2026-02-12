namespace RecallDb.Server.Classes
{
    using RecallDb.Core.Models;

    /// <summary>
    /// Authentication result.
    /// </summary>
    public class AuthenticationResult
    {
        #region Public-Members

        /// <summary>
        /// Whether the request is authenticated.
        /// </summary>
        public bool IsAuthenticated
        {
            get
            {
                return _IsAuthenticated;
            }
            set
            {
                _IsAuthenticated = value;
            }
        }

        /// <summary>
        /// Whether the authenticated user is a global admin (via AdminApiKey or User.IsAdmin).
        /// </summary>
        public bool IsAdmin
        {
            get
            {
                if (_IsAdminApiKey) return true;
                if (_User != null && _User.IsAdmin) return true;
                return false;
            }
        }

        /// <summary>
        /// Whether the authenticated user is a tenant admin.
        /// </summary>
        public bool IsTenantAdmin
        {
            get
            {
                if (IsAdmin) return true;
                if (_User != null && _User.IsTenantAdmin) return true;
                return false;
            }
        }

        /// <summary>
        /// Whether the user can manage users (admin or tenant admin).
        /// </summary>
        public bool CanManageUsers
        {
            get
            {
                return IsAdmin || IsTenantAdmin;
            }
        }

        /// <summary>
        /// Whether authentication was via admin API key.
        /// </summary>
        public bool IsAdminApiKey
        {
            get
            {
                return _IsAdminApiKey;
            }
            set
            {
                _IsAdminApiKey = value;
            }
        }

        /// <summary>
        /// Tenant metadata.
        /// </summary>
        public TenantMetadata Tenant
        {
            get
            {
                return _Tenant;
            }
            set
            {
                _Tenant = value;
            }
        }

        /// <summary>
        /// User master.
        /// </summary>
        public UserMaster User
        {
            get
            {
                return _User;
            }
            set
            {
                _User = value;
            }
        }

        /// <summary>
        /// Credential.
        /// </summary>
        public Credential Credential
        {
            get
            {
                return _Credential;
            }
            set
            {
                _Credential = value;
            }
        }

        /// <summary>
        /// Authentication method.
        /// </summary>
        public string AuthMethod
        {
            get
            {
                return _AuthMethod;
            }
            set
            {
                _AuthMethod = value;
            }
        }

        /// <summary>
        /// Error message if authentication failed.
        /// </summary>
        public string ErrorMessage
        {
            get
            {
                return _ErrorMessage;
            }
            set
            {
                _ErrorMessage = value;
            }
        }

        #endregion

        #region Private-Members

        private bool _IsAuthenticated = false;
        private bool _IsAdminApiKey = false;
        private TenantMetadata _Tenant = null;
        private UserMaster _User = null;
        private Credential _Credential = null;
        private string _AuthMethod = null;
        private string _ErrorMessage = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AuthenticationResult()
        {
        }

        #endregion
    }
}
