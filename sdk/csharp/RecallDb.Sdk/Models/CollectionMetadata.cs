namespace RecallDb.Sdk.Models
{
    using System;

    /// <summary>
    /// Collection metadata.
    /// </summary>
    public class CollectionMetadata
    {
        /// <summary>
        /// Collection ID.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string TenantId { get; set; }

        /// <summary>
        /// Collection name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Description of the collection.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Vector dimensionality.
        /// </summary>
        public int Dimensionality { get; set; }

        /// <summary>
        /// Whether the collection is active.
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
        public CollectionMetadata()
        {
            Dimensionality = 384;
            Active = true;
            CreatedUtc = DateTime.UtcNow;
            LastUpdateUtc = DateTime.UtcNow;
        }
    }
}
