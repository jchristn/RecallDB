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
    /// Registers collection MCP tools (read, exists, enumerate, create, update, delete, stats).
    /// </summary>
    public static class CollectionRegistrations
    {
        #region Public-Methods

        /// <summary>
        /// Register collection tools on the MCP HTTP server.
        /// </summary>
        /// <param name="server">MCP HTTP server.</param>
        /// <param name="ctx">Tool context.</param>
        public static void Register(McpHttpServer server, McpToolContext ctx)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            server.RegisterInstrumentedTool(
                "collection/read",
                "Read a collection by ID.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        collectionId = new { type = "string", description = "Collection ID." }
                    },
                    required = new[] { "bearerToken", "tenantId", "collectionId" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("collection/read", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.CollectionId = McpHelpers.GetStringRequired(args, "collectionId");
                    ServiceResult r = await ctx.Services.Collections.ReadAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterInstrumentedTool(
                "collection/exists",
                "Test whether a collection exists. Returns a boolean.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        collectionId = new { type = "string", description = "Collection ID." }
                    },
                    required = new[] { "bearerToken", "tenantId", "collectionId" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("collection/exists", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.CollectionId = McpHelpers.GetStringRequired(args, "collectionId");
                    ServiceResult r = await ctx.Services.Collections.ExistsAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapExists(r);
                });

            server.RegisterInstrumentedTool(
                "collection/enumerate",
                "Enumerate collections with pagination. Supply an optional EnumerationQuery as a JSON string.",
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
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("collection/enumerate", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.Query = McpHelpers.DeserializeArgOptional<EnumerationQuery>(args, "query");
                    ServiceResult r = await ctx.Services.Collections.EnumerateAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterInstrumentedTool(
                "collection/create",
                "Create a collection. Supply the collection as a JSON string.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        collection = new { type = "string", description = "CollectionMetadata serialized as a JSON string." }
                    },
                    required = new[] { "bearerToken", "tenantId", "collection" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("collection/create", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.Payload = McpHelpers.DeserializeArgRequired<CollectionMetadata>(args, "collection");
                    ServiceResult r = await ctx.Services.Collections.CreateAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterInstrumentedTool(
                "collection/update",
                "Update a collection. Supply the collection ID and the collection as a JSON string.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        collectionId = new { type = "string", description = "Collection ID." },
                        collection = new { type = "string", description = "CollectionMetadata serialized as a JSON string." }
                    },
                    required = new[] { "bearerToken", "tenantId", "collectionId", "collection" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("collection/update", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.CollectionId = McpHelpers.GetStringRequired(args, "collectionId");
                    c.Payload = McpHelpers.DeserializeArgRequired<CollectionMetadata>(args, "collection");
                    ServiceResult r = await ctx.Services.Collections.UpdateAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterInstrumentedTool(
                "collection/delete",
                "Delete a collection and cascade-delete its documents.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        collectionId = new { type = "string", description = "Collection ID." }
                    },
                    required = new[] { "bearerToken", "tenantId", "collectionId" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("collection/delete", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.CollectionId = McpHelpers.GetStringRequired(args, "collectionId");
                    ServiceResult r = await ctx.Services.Collections.DeleteAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterInstrumentedTool(
                "collection/stats",
                "Retrieve statistics for a collection.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        collectionId = new { type = "string", description = "Collection ID." }
                    },
                    required = new[] { "bearerToken", "tenantId", "collectionId" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("collection/stats", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.CollectionId = McpHelpers.GetStringRequired(args, "collectionId");
                    ServiceResult r = await ctx.Services.Collections.StatsAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });
        }

        #endregion
    }
}
