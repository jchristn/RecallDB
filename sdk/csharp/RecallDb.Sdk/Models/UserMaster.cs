namespace RecallDb.Sdk.Models
{
    using System;

    /// <summary>
    /// User master record.
    /// </summary>
    public class UserMaster
    {
        /// <summary>
        /// User ID.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string TenantId { get; set; }

        /// <summary>
        /// Email address.
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// SHA256 hash of the password.
        /// </summary>
        public string PasswordSha256 { get; set; }

        /// <summary>
        /// First name.
        /// </summary>
        public string FirstName { get; set; }

        /// <summary>
        /// Last name.
        /// </summary>
        public string LastName { get; set; }

        /// <summary>
        /// Whether the user is a global administrator.
        /// </summary>
        public bool IsAdmin { get; set; }

        /// <summary>
        /// Whether the user is a tenant administrator.
        /// </summary>
        public bool IsTenantAdmin { get; set; }

        /// <summary>
        /// Whether the user is active.
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
        public UserMaster()
        {
            Active = true;
            CreatedUtc = DateTime.UtcNow;
            LastUpdateUtc = DateTime.UtcNow;
        }
    }
}
