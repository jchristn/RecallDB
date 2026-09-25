namespace RecallDb.Core.Database
{
    using System;

    /// <summary>
    /// Thrown when a collection cannot be created because another collection with the same name already exists in
    /// the tenant (the unique constraint idx_collections_tenant_name). Callers translate this into a 409 Conflict so
    /// a client can adopt the existing collection instead of retrying a create that will always fail.
    /// </summary>
    public class DuplicateCollectionException : Exception
    {
        /// <summary>
        /// Tenant id the conflict occurred in.
        /// </summary>
        public string TenantId { get; }

        /// <summary>
        /// Collection name that already exists.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="tenantId">Tenant id.</param>
        /// <param name="name">Collection name that already exists.</param>
        /// <param name="inner">Underlying database exception, if any.</param>
        public DuplicateCollectionException(string tenantId, string name, Exception inner = null)
            : base("A collection named '" + name + "' already exists in tenant '" + tenantId + "'.", inner)
        {
            TenantId = tenantId;
            Name = name;
        }
    }
}
