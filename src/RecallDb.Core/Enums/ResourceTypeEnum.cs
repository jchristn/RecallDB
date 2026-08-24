namespace RecallDb.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Resource type targeted by a request. One half of the centralized operation-scope mapping.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ResourceTypeEnum
    {
        /// <summary>
        /// Server-level, non-resource operations (health, server info).
        /// </summary>
        Server,

        /// <summary>
        /// Authentication operations.
        /// </summary>
        Authentication,

        /// <summary>
        /// Tenant resource.
        /// </summary>
        Tenant,

        /// <summary>
        /// User resource.
        /// </summary>
        User,

        /// <summary>
        /// Credential resource.
        /// </summary>
        Credential,

        /// <summary>
        /// Collection resource.
        /// </summary>
        Collection,

        /// <summary>
        /// Document resource.
        /// </summary>
        Document,

        /// <summary>
        /// Label resource.
        /// </summary>
        Label,

        /// <summary>
        /// Tag resource.
        /// </summary>
        Tag,

        /// <summary>
        /// Search operations.
        /// </summary>
        Search,

        /// <summary>
        /// Request-history resource (administrative).
        /// </summary>
        RequestHistory
    }
}
