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
    /// Registers tag MCP tools (read, enumerate, create, delete).
    /// </summary>
    public static class TagRegistrations
    {
        #region Public-Methods

        /// <summary>
        /// Register tag tools on the MCP HTTP server.
        /// </summary>
        /// <param name="server">MCP HTTP server.</param>
        /// <param name="ctx">Tool context.</param>
        public static void Register(McpHttpServer server, McpToolContext ctx)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            server.RegisterTool(
                "tag/read",
                "Read a tag by ID.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        collectionId = new { type = "string", description = "Collection ID." },
                        tagId = new { type = "string", description = "Tag ID." }
                    },
                    required = new[] { "bearerToken", "tenantId", "collectionId", "tagId" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("tag/read", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.CollectionId = McpHelpers.GetStringRequired(args, "collectionId");
                    c.ResourceId = McpHelpers.GetStringRequired(args, "tagId");
                    ServiceResult r = await ctx.Services.Tags.ReadAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterTool(
                "tag/enumerate",
                "Enumerate tags in a collection with pagination. Supply an optional EnumerationQuery as a JSON string.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        collectionId = new { type = "string", description = "Collection ID." },
                        query = new { type = "string", description = "EnumerationQuery serialized as a JSON string." }
                    },
                    required = new[] { "bearerToken", "tenantId", "collectionId" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("tag/enumerate", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.CollectionId = McpHelpers.GetStringRequired(args, "collectionId");
                    c.Query = McpHelpers.DeserializeArgOptional<EnumerationQuery>(args, "query");
                    ServiceResult r = await ctx.Services.Tags.EnumerateAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterTool(
                "tag/create",
                "Create a tag in a collection. Supply the tag as a JSON string.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        collectionId = new { type = "string", description = "Collection ID." },
                        tag = new { type = "string", description = "TagRecord serialized as a JSON string." }
                    },
                    required = new[] { "bearerToken", "tenantId", "collectionId", "tag" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("tag/create", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.CollectionId = McpHelpers.GetStringRequired(args, "collectionId");
                    c.Payload = McpHelpers.DeserializeArgRequired<TagRecord>(args, "tag");
                    ServiceResult r = await ctx.Services.Tags.CreateAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterTool(
                "tag/delete",
                "Delete a tag by ID.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        collectionId = new { type = "string", description = "Collection ID." },
                        tagId = new { type = "string", description = "Tag ID." }
                    },
                    required = new[] { "bearerToken", "tenantId", "collectionId", "tagId" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("tag/delete", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.CollectionId = McpHelpers.GetStringRequired(args, "collectionId");
                    c.ResourceId = McpHelpers.GetStringRequired(args, "tagId");
                    ServiceResult r = await ctx.Services.Tags.DeleteAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });
        }

        #endregion
    }
}
