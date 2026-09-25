namespace RecallDb.Server.Mcp
{
    using Voltaic.Mcp;

    /// <summary>
    /// Exception thrown by an MCP tool handler when the underlying operation fails. Deriving from
    /// <see cref="McpProtocolException"/> makes Voltaic put the message in the JSON-RPC error itself (code -32603, with
    /// the status code in <c>data</c>); any other exception is reduced to a bare "Internal error". The message embeds the HTTP-equivalent status code so callers (and tests)
    /// can distinguish authorization failures (403), not-found (404), and bad-request (400) conditions.
    /// </summary>
    public class McpToolException : McpProtocolException
    {
        #region Public-Members

        /// <summary>
        /// JSON-RPC error code used for every tool failure.
        /// </summary>
        public const int InternalErrorCode = -32603;

        /// <summary>
        /// HTTP-equivalent status code for the failure.
        /// </summary>
        public int StatusCode { get; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="statusCode">HTTP-equivalent status code.</param>
        /// <param name="error">Short error label.</param>
        /// <param name="context">Human-readable error context.</param>
        public McpToolException(int statusCode, string error, string context)
            : base(InternalErrorCode, FormatMessage(statusCode, error, context), new McpToolErrorData(statusCode))
        {
            StatusCode = statusCode;
        }

        #endregion

        #region Private-Methods

        private static string FormatMessage(int statusCode, string error, string context)
        {
            string message = statusCode + " " + (error ?? "Error");
            if (!string.IsNullOrEmpty(context)) message += ": " + context;
            return message;
        }

        #endregion
    }
}
