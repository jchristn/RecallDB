namespace RecallDb.Server.Mcp.Registrations
{
    using System;

    using Voltaic.Core;
    using Voltaic.Mcp;

    using RecallDb.Core.Models;

    /// <summary>
    /// Registers the server/info MCP tool.
    /// </summary>
    public static class ServerRegistrations
    {
        #region Public-Methods

        /// <summary>
        /// Register server tools on the MCP HTTP server.
        /// </summary>
        /// <param name="server">MCP HTTP server.</param>
        /// <param name="ctx">Tool context.</param>
        public static void Register(McpHttpServer server, McpToolContext ctx)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            server.RegisterTool(
                "server/info",
                "Returns the RecallDB server name, version, uptime, and the MCP Streamable HTTP endpoint. No authentication required.",
                new
                {
                    type = "object",
                    properties = new { }
                },
                (RpcParameters args) =>
                {
                    McpServerInfo info = new McpServerInfo();
                    info.Version = ctx.Version;
                    info.UptimeMs = (DateTime.UtcNow - ctx.StartTimeUtc).TotalMilliseconds;
                    info.Hostname = ctx.Settings.Hostname;
                    info.Port = ctx.Settings.Port;
                    return (object)info;
                });
        }

        #endregion
    }
}
