namespace RecallDb.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Identifies the transport that originated a request. Both feeds normalize into a single RequestContext.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RequestOriginEnum
    {
        /// <summary>
        /// The request originated from the REST/HTTP API.
        /// </summary>
        Rest,

        /// <summary>
        /// The request originated from the MCP (Model Context Protocol) server.
        /// </summary>
        Mcp
    }
}
