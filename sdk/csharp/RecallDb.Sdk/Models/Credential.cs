namespace RecallDb.Sdk.Models
{
    using System;

    /// <summary>
    /// API credential with bearer token.
    /// </summary>
    public class Credential
    {
        /// <summary>
        /// Credential ID.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string TenantId { get; set; }

        /// <summary>
        /// User ID.
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Bearer token.
        /// </summary>
        public string BearerToken { get; set; }

        /// <summary>
        /// Credential name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Whether the credential is active.
        /// </summary>
        public bool Active { get; set; }

        /// <summary>
        /// Creation timestamp in UTC.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Last update timestamp in UTC.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public Credential()
        {
            Active = true;
            CreatedUtc = DateTime.UtcNow;
            LastUpdateUtc = DateTime.UtcNow;
        }
    }
}
