namespace RecallDb.Server.Classes
{
    /// <summary>
    /// Authentication request body for POST /v1.0/authenticate.
    /// Supports both bearer token validation and email+password login.
    /// </summary>
    public class AuthenticateRequest
    {
        #region Public-Members

        /// <summary>
        /// Bearer token to validate.
        /// </summary>
        public string BearerToken
        {
            get
            {
                return _BearerToken;
            }
            set
            {
                _BearerToken = value;
            }
        }

        /// <summary>
        /// Tenant ID for email+password login.
        /// </summary>
        public string TenantId
        {
            get
            {
                return _TenantId;
            }
            set
            {
                _TenantId = value;
            }
        }

        /// <summary>
        /// Email address for login.
        /// </summary>
        public string Email
        {
            get
            {
                return _Email;
            }
            set
            {
                _Email = value;
            }
        }

        /// <summary>
        /// Password for login.
        /// </summary>
        public string Password
        {
            get
            {
                return _Password;
            }
            set
            {
                _Password = value;
            }
        }

        #endregion

        #region Private-Members

        private string _BearerToken = null;
        private string _TenantId = null;
        private string _Email = null;
        private string _Password = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AuthenticateRequest()
        {
        }

        #endregion
    }
}
