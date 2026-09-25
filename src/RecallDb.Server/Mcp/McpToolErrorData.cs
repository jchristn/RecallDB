namespace RecallDb.Server.Mcp
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// JSON-RPC error data attached to a failed MCP tool call.
    /// </summary>
    public class McpToolErrorData
    {
        #region Public-Members

        /// <summary>
        /// HTTP-equivalent status code for the failure.
        /// </summary>
        [JsonPropertyName("statusCode")]
        public int StatusCode { get; set; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="statusCode">HTTP-equivalent status code.</param>
        public McpToolErrorData(int statusCode)
        {
            StatusCode = statusCode;
        }

        #endregion
    }
}