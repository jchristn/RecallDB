namespace RecallDb.Core.Models
{
    /// <summary>
    /// MCP server information returned by the server/info tool: product identity plus the enabled MCP endpoint.
    /// </summary>
    public class McpServerInfo
    {
        #region Public-Members

        /// <summary>
        /// Server product name.
        /// </summary>
        public string Name { get; set; } = "RecallDB";

        /// <summary>
        /// Server version.
        /// </summary>
        public string Version { get; set; } = null;

        /// <summary>
        /// Server uptime in milliseconds.
        /// </summary>
        public double UptimeMs { get; set; } = 0;

        /// <summary>
        /// Whether the MCP server is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// MCP transport advertised. Only Streamable HTTP is supported.
        /// </summary>
        public string Transport { get; set; } = "streamable-http";

        /// <summary>
        /// Hostname the MCP HTTP endpoint is bound to.
        /// </summary>
        public string Hostname { get; set; } = null;

        /// <summary>
        /// Port the MCP HTTP endpoint listens on.
        /// </summary>
        public int Port { get; set; } = 0;

        /// <summary>
        /// Relative path for the Streamable HTTP endpoint (POST for JSON-RPC, GET for the SSE stream).
        /// </summary>
        public string Endpoint { get; set; } = "/mcp";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public McpServerInfo()
        {
        }

        #endregion
    }
}
