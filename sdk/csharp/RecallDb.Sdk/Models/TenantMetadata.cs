namespace RecallDb.Sdk.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Tenant metadata.
    /// </summary>
    public class TenantMetadata
    {
        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Tenant name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Whether the tenant is active.
        /// </summary>
        public bool Active { get; set; }

        /// <summary>
        /// Labels.
        /// </summary>
        public List<string> Labels { get; set; }

        /// <summary>
        /// Tags.
        /// </summary>
        public Dictionary<string, string> Tags { get; set; }

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
        public TenantMetadata()
        {
            Active = true;
            Labels = new List<string>();
            Tags = new Dictionary<string, string>();
            CreatedUtc = DateTime.UtcNow;
            LastUpdateUtc = DateTime.UtcNow;
        }
    }
}
