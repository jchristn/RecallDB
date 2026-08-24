namespace RecallDb.Server.Mcp.Registrations
{
    using System;
    using System.Threading.Tasks;

    using Voltaic.Core;
    using Voltaic.Mcp;

    using RecallDb.Server.Classes;
    using RecallDb.Server.Services;

    /// <summary>
    /// Registers the auth/authenticate MCP tool.
    /// </summary>
    public static class AuthRegistrations
    {
        #region Public-Methods

        /// <summary>
        /// Register authentication tools on the MCP HTTP server.
        /// </summary>
        /// <param name="server">MCP HTTP server.</param>
        /// <param name="ctx">Tool context.</param>
        public static void Register(McpHttpServer server, McpToolContext ctx)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            server.RegisterInstrumentedTool(
                "auth/authenticate",
                "Validate a credential and return the resolved tenant, redacted user, and credential. Supply either a bearer token, or tenant ID plus email and password. No prior authentication required.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Bearer token to validate (admin API key or credential bearer token)." },
                        tenantId = new { type = "string", description = "Tenant ID for email/password login." },
                        email = new { type = "string", description = "Email address for login." },
                        password = new { type = "string", description = "Password for login." }
                    }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = McpHelpers.BuildAnonymousContext("auth/authenticate");

                    AuthenticateRequest body = new AuthenticateRequest();
                    body.BearerToken = McpHelpers.GetStringOptional(args, "bearerToken");
                    body.TenantId = McpHelpers.GetStringOptional(args, "tenantId");
                    body.Email = McpHelpers.GetStringOptional(args, "email");
                    body.Password = McpHelpers.GetStringOptional(args, "password");
                    c.Payload = body;

                    ServiceResult result = await ctx.Services.Auth.AuthenticateAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(result);
                });
        }

        #endregion
    }
}
