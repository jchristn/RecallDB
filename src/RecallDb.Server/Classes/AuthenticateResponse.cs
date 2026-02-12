namespace RecallDb.Server.Classes
{
    using RecallDb.Core.Models;

    /// <summary>
    /// Authentication response for POST /v1.0/authenticate.
    /// </summary>
    public class AuthenticateResponse
    {
        #region Public-Members

        /// <summary>
        /// Whether authentication succeeded.
        /// </summary>
        public bool Success
        {
            get
            {
                return _Success;
            }
            set
            {
                _Success = value;
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
        /// User master (password redacted).
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

        private bool _Success = false;
        private TenantMetadata _Tenant = null;
        private UserMaster _User = null;
        private Credential _Credential = null;
        private string _ErrorMessage = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AuthenticateResponse()
        {
        }

        #endregion
    }
}
