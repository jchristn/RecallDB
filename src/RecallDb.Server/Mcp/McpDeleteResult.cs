namespace RecallDb.Server.Mcp
{
    /// <summary>
    /// Result returned by MCP tools that map to a no-content (HTTP 204) operation, such as deletes. MCP tool
    /// results must carry a payload, so a small success object is returned instead of an empty body.
    /// </summary>
    public class McpDeleteResult
    {
        #region Public-Members

        /// <summary>
        /// Always true; indicates the operation completed successfully.
        /// </summary>
        public bool Success { get; set; } = true;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public McpDeleteResult()
        {
        }

        #endregion
    }
}
