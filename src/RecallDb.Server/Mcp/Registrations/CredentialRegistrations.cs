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
    /// Registers credential MCP tools (read, exists, enumerate, create, update, delete).
    /// </summary>
    public static class CredentialRegistrations
    {
        #region Public-Methods

        /// <summary>
        /// Register credential tools on the MCP HTTP server.
        /// </summary>
        /// <param name="server">MCP HTTP server.</param>
        /// <param name="ctx">Tool context.</param>
        public static void Register(McpHttpServer server, McpToolContext ctx)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            server.RegisterTool(
                "credential/read",
                "Read a credential by ID.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        credentialId = new { type = "string", description = "Credential ID." }
                    },
                    required = new[] { "bearerToken", "tenantId", "credentialId" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("credential/read", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.ResourceId = McpHelpers.GetStringRequired(args, "credentialId");
                    ServiceResult r = await ctx.Services.Credentials.ReadAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterTool(
                "credential/exists",
                "Test whether a credential exists. Returns a boolean.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        credentialId = new { type = "string", description = "Credential ID." }
                    },
                    required = new[] { "bearerToken", "tenantId", "credentialId" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("credential/exists", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.ResourceId = McpHelpers.GetStringRequired(args, "credentialId");
                    ServiceResult r = await ctx.Services.Credentials.ExistsAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapExists(r);
                });

            server.RegisterTool(
                "credential/enumerate",
                "Enumerate credentials in a tenant with pagination. Supply an optional EnumerationQuery as a JSON string.",
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
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("credential/enumerate", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.Query = McpHelpers.DeserializeArgOptional<EnumerationQuery>(args, "query");
                    ServiceResult r = await ctx.Services.Credentials.EnumerateAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterTool(
                "credential/create",
                "Create a credential (admin or tenant admin). Supply the credential as a JSON string.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        credential = new { type = "string", description = "Credential serialized as a JSON string." }
                    },
                    required = new[] { "bearerToken", "tenantId", "credential" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("credential/create", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.Payload = McpHelpers.DeserializeArgRequired<Credential>(args, "credential");
                    ServiceResult r = await ctx.Services.Credentials.CreateAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterTool(
                "credential/update",
                "Update a credential (admin or tenant admin).",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        credentialId = new { type = "string", description = "Credential ID." },
                        credential = new { type = "string", description = "Credential serialized as a JSON string." }
                    },
                    required = new[] { "bearerToken", "tenantId", "credentialId", "credential" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("credential/update", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.ResourceId = McpHelpers.GetStringRequired(args, "credentialId");
                    c.Payload = McpHelpers.DeserializeArgRequired<Credential>(args, "credential");
                    ServiceResult r = await ctx.Services.Credentials.UpdateAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterTool(
                "credential/delete",
                "Delete a credential (admin or tenant admin).",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        credentialId = new { type = "string", description = "Credential ID." }
                    },
                    required = new[] { "bearerToken", "tenantId", "credentialId" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("credential/delete", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.ResourceId = McpHelpers.GetStringRequired(args, "credentialId");
                    ServiceResult r = await ctx.Services.Credentials.DeleteAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });
        }

        #endregion
    }
}
