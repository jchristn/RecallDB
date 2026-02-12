namespace RecallDb.Sdk.Models
{
    /// <summary>
    /// Authentication request body for POST /v1.0/authenticate.
    /// Supports both bearer token validation and email+password login.
    /// </summary>
    public class AuthenticateRequest
    {
        /// <summary>
        /// Bearer token to validate.
        /// </summary>
        public string BearerToken { get; set; }

        /// <summary>
        /// Tenant ID for email+password login.
        /// </summary>
        public string TenantId { get; set; }

        /// <summary>
        /// Email address for login.
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Password for login.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AuthenticateRequest()
        {
        }
    }
}
