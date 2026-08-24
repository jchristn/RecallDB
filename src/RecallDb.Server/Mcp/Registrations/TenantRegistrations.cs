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
    /// Registers tenant MCP tools (read, exists, enumerate, create, update, delete).
    /// </summary>
    public static class TenantRegistrations
    {
        #region Public-Methods

        /// <summary>
        /// Register tenant tools on the MCP HTTP server.
        /// </summary>
        /// <param name="server">MCP HTTP server.</param>
        /// <param name="ctx">Tool context.</param>
        public static void Register(McpHttpServer server, McpToolContext ctx)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            server.RegisterTool(
                "tenant/read",
                "Read a tenant by ID.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." }
                    },
                    required = new[] { "bearerToken", "tenantId" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("tenant/read", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    ServiceResult r = await ctx.Services.Tenants.ReadAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterTool(
                "tenant/exists",
                "Test whether a tenant exists. Returns a boolean.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." }
                    },
                    required = new[] { "bearerToken", "tenantId" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("tenant/exists", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    ServiceResult r = await ctx.Services.Tenants.ExistsAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapExists(r);
                });

            server.RegisterTool(
                "tenant/enumerate",
                "Enumerate tenants with pagination (admin only). Supply an optional EnumerationQuery as a JSON string.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        query = new { type = "string", description = "EnumerationQuery serialized as a JSON string (MaxResults, ContinuationToken, Ordering, filters)." }
                    },
                    required = new[] { "bearerToken" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("tenant/enumerate", args, ctx.Authentication).ConfigureAwait(false);
                    c.Query = McpHelpers.DeserializeArgOptional<EnumerationQuery>(args, "query");
                    ServiceResult r = await ctx.Services.Tenants.EnumerateAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterTool(
                "tenant/create",
                "Create a tenant (admin only). Supply the tenant as a JSON string.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenant = new { type = "string", description = "TenantMetadata serialized as a JSON string." }
                    },
                    required = new[] { "bearerToken", "tenant" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("tenant/create", args, ctx.Authentication).ConfigureAwait(false);
                    c.Payload = McpHelpers.DeserializeArgRequired<TenantMetadata>(args, "tenant");
                    ServiceResult r = await ctx.Services.Tenants.CreateAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterTool(
                "tenant/update",
                "Update a tenant. Supply the tenant ID and the tenant as a JSON string.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        tenant = new { type = "string", description = "TenantMetadata serialized as a JSON string." }
                    },
                    required = new[] { "bearerToken", "tenantId", "tenant" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("tenant/update", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.Payload = McpHelpers.DeserializeArgRequired<TenantMetadata>(args, "tenant");
                    ServiceResult r = await ctx.Services.Tenants.UpdateAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterTool(
                "tenant/delete",
                "Delete a tenant and cascade-delete its collections, credentials, and users (admin only).",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." }
                    },
                    required = new[] { "bearerToken", "tenantId" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("tenant/delete", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    ServiceResult r = await ctx.Services.Tenants.DeleteAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });
        }

        #endregion
    }
}
