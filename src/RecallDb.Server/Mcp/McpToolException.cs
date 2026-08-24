namespace RecallDb.Server.Mcp
{
    using System;

    /// <summary>
    /// Exception thrown by an MCP tool handler when the underlying operation fails. Voltaic surfaces the message
    /// as a JSON-RPC error to the client. The message embeds the HTTP-equivalent status code so callers (and tests)
    /// can distinguish authorization failures (403), not-found (404), and bad-request (400) conditions.
    /// </summary>
    public class McpToolException : Exception
    {
        #region Public-Members

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
            : base(FormatMessage(statusCode, error, context))
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
