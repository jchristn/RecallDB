namespace RecallDb.Server.Mcp.Registrations
{
    using System;
    using System.Threading.Tasks;

    using Voltaic.Core;
    using Voltaic.Mcp;

    using RecallDb.Core.Models;
    using RecallDb.Server.Classes;
    using RecallDb.Server.Services;

    /// <summary>
    /// Registers user MCP tools (read, exists, enumerate, create, update, delete).
    /// </summary>
    public static class UserRegistrations
    {
        #region Public-Methods

        /// <summary>
        /// Register user tools on the MCP HTTP server.
        /// </summary>
        /// <param name="server">MCP HTTP server.</param>
        /// <param name="ctx">Tool context.</param>
        public static void Register(McpHttpServer server, McpToolContext ctx)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            server.RegisterTool(
                "user/read",
                "Read a user by ID (password redacted).",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        userId = new { type = "string", description = "User ID." }
                    },
                    required = new[] { "bearerToken", "tenantId", "userId" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("user/read", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.UserId = McpHelpers.GetStringRequired(args, "userId");
                    ServiceResult r = await ctx.Services.Users.ReadAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterTool(
                "user/exists",
                "Test whether a user exists. Returns a boolean.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        userId = new { type = "string", description = "User ID." }
                    },
                    required = new[] { "bearerToken", "tenantId", "userId" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("user/exists", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.UserId = McpHelpers.GetStringRequired(args, "userId");
                    ServiceResult r = await ctx.Services.Users.ExistsAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapExists(r);
                });

            server.RegisterTool(
                "user/enumerate",
                "Enumerate users in a tenant with pagination. Supply an optional EnumerationQuery as a JSON string.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        query = new { type = "string", description = "EnumerationQuery serialized as a JSON string." }
                    },
                    required = new[] { "bearerToken", "tenantId" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("user/enumerate", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.Query = McpHelpers.DeserializeArgOptional<EnumerationQuery>(args, "query");
                    ServiceResult r = await ctx.Services.Users.EnumerateAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterTool(
                "user/create",
                "Create a user (admin or tenant admin). Supply the user as a JSON string.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        user = new { type = "string", description = "UserMaster serialized as a JSON string." }
                    },
                    required = new[] { "bearerToken", "tenantId", "user" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("user/create", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.Payload = McpHelpers.DeserializeArgRequired<UserMaster>(args, "user");
                    ServiceResult r = await ctx.Services.Users.CreateAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterTool(
                "user/update",
                "Update a user (admin or tenant admin).",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        userId = new { type = "string", description = "User ID." },
                        user = new { type = "string", description = "UserMaster serialized as a JSON string." }
                    },
                    required = new[] { "bearerToken", "tenantId", "userId", "user" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("user/update", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.UserId = McpHelpers.GetStringRequired(args, "userId");
                    c.Payload = McpHelpers.DeserializeArgRequired<UserMaster>(args, "user");
                    ServiceResult r = await ctx.Services.Users.UpdateAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterTool(
                "user/delete",
                "Delete a user and its credentials (admin or tenant admin).",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        userId = new { type = "string", description = "User ID." }
                    },
                    required = new[] { "bearerToken", "tenantId", "userId" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("user/delete", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.UserId = McpHelpers.GetStringRequired(args, "userId");
                    ServiceResult r = await ctx.Services.Users.DeleteAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });
        }

        #endregion
    }
}
