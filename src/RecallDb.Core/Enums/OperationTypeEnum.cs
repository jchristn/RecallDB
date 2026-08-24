namespace RecallDb.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Operation type a request performs against a resource. One half of the centralized operation-scope mapping.
    /// Read-type operations require read access; the remainder require the corresponding write/delete/admin gate.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OperationTypeEnum
    {
        /// <summary>
        /// Create a new resource.
        /// </summary>
        Create,

        /// <summary>
        /// Read a single resource, test existence, or retrieve statistics.
        /// </summary>
        Read,

        /// <summary>
        /// Enumerate resources with pagination.
        /// </summary>
        Enumerate,

        /// <summary>
        /// Search resources.
        /// </summary>
        Search,

        /// <summary>
        /// Update an existing resource.
        /// </summary>
        Update,

        /// <summary>
        /// Delete a resource.
        /// </summary>
        Delete,

        /// <summary>
        /// Execute a non-CRUD action (e.g. authenticate).
        /// </summary>
        Execute
    }
}
