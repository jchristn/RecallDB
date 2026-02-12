namespace RecallDb.Sdk.Models
{
    /// <summary>
    /// Authentication response for POST /v1.0/authenticate.
    /// </summary>
    public class AuthenticateResponse
    {
        /// <summary>
        /// Whether authentication succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Tenant metadata.
        /// </summary>
        public TenantMetadata Tenant { get; set; }

        /// <summary>
        /// User master (password redacted).
        /// </summary>
        public UserMaster User { get; set; }

        /// <summary>
        /// Credential.
        /// </summary>
        public Credential Credential { get; set; }

        /// <summary>
        /// Error message if authentication failed.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AuthenticateResponse()
        {
        }
    }
}
