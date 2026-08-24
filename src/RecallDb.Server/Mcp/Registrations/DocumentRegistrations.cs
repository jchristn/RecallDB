namespace RecallDb.Server.Mcp.Registrations
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Voltaic.Core;
    using Voltaic.Mcp;

    using RecallDb.Core.Models;
    using RecallDb.Server.Classes;
    using RecallDb.Server.Services;

    /// <summary>
    /// Registers document MCP tools (read, readByPosition, exists, enumerate, create, update, delete, batchCreate, batchDelete, deleteByFilter, stats).
    /// </summary>
    public static class DocumentRegistrations
    {
        #region Public-Methods

        /// <summary>
        /// Register document tools on the MCP HTTP server.
        /// </summary>
        /// <param name="server">MCP HTTP server.</param>
        /// <param name="ctx">Tool context.</param>
        public static void Register(McpHttpServer server, McpToolContext ctx)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            server.RegisterInstrumentedTool(
                "document/read",
                "Read a document by key.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        collectionId = new { type = "string", description = "Collection ID." },
                        documentKey = new { type = "string", description = "Document key." }
                    },
                    required = new[] { "bearerToken", "tenantId", "collectionId", "documentKey" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("document/read", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.CollectionId = McpHelpers.GetStringRequired(args, "collectionId");
                    c.DocumentKey = McpHelpers.GetStringRequired(args, "documentKey");
                    ServiceResult r = await ctx.Services.Documents.ReadAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterInstrumentedTool(
                "document/readByPosition",
                "Read a document chunk by document ID and position.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        collectionId = new { type = "string", description = "Collection ID." },
                        documentId = new { type = "string", description = "Document ID (chunk grouping)." },
                        position = new { type = "integer", description = "Chunk position." }
                    },
                    required = new[] { "bearerToken", "tenantId", "collectionId", "documentId", "position" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("document/readByPosition", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.CollectionId = McpHelpers.GetStringRequired(args, "collectionId");
                    c.DocumentId = McpHelpers.GetStringRequired(args, "documentId");
                    c.Position = McpHelpers.GetIntOptional(args, "position");
                    ServiceResult r = await ctx.Services.Documents.ReadByPositionAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterInstrumentedTool(
                "document/exists",
                "Test whether a document exists. Returns a boolean.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        collectionId = new { type = "string", description = "Collection ID." },
                        documentKey = new { type = "string", description = "Document key." }
                    },
                    required = new[] { "bearerToken", "tenantId", "collectionId", "documentKey" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("document/exists", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.CollectionId = McpHelpers.GetStringRequired(args, "collectionId");
                    c.DocumentKey = McpHelpers.GetStringRequired(args, "documentKey");
                    ServiceResult r = await ctx.Services.Documents.ExistsAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapExists(r);
                });

            server.RegisterInstrumentedTool(
                "document/enumerate",
                "Enumerate documents with pagination. Supply an optional EnumerationQuery as a JSON string.",
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
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("document/enumerate", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.CollectionId = McpHelpers.GetStringRequired(args, "collectionId");
                    c.Query = McpHelpers.DeserializeArgOptional<EnumerationQuery>(args, "query");
                    ServiceResult r = await ctx.Services.Documents.EnumerateAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterInstrumentedTool(
                "document/create",
                "Create a document. Supply the document as a JSON string.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        collectionId = new { type = "string", description = "Collection ID." },
                        document = new { type = "string", description = "DocumentRecord serialized as a JSON string." }
                    },
                    required = new[] { "bearerToken", "tenantId", "collectionId", "document" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("document/create", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.CollectionId = McpHelpers.GetStringRequired(args, "collectionId");
                    c.Payload = McpHelpers.DeserializeArgRequired<DocumentRecord>(args, "document");
                    ServiceResult r = await ctx.Services.Documents.CreateAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterInstrumentedTool(
                "document/update",
                "Update a document. Supply the document key and the document as a JSON string.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        collectionId = new { type = "string", description = "Collection ID." },
                        documentKey = new { type = "string", description = "Document key." },
                        document = new { type = "string", description = "DocumentRecord serialized as a JSON string." }
                    },
                    required = new[] { "bearerToken", "tenantId", "collectionId", "documentKey", "document" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("document/update", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.CollectionId = McpHelpers.GetStringRequired(args, "collectionId");
                    c.DocumentKey = McpHelpers.GetStringRequired(args, "documentKey");
                    c.Payload = McpHelpers.DeserializeArgRequired<DocumentRecord>(args, "document");
                    ServiceResult r = await ctx.Services.Documents.UpdateAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterInstrumentedTool(
                "document/delete",
                "Delete a document by key.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        collectionId = new { type = "string", description = "Collection ID." },
                        documentKey = new { type = "string", description = "Document key." }
                    },
                    required = new[] { "bearerToken", "tenantId", "collectionId", "documentKey" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("document/delete", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.CollectionId = McpHelpers.GetStringRequired(args, "collectionId");
                    c.DocumentKey = McpHelpers.GetStringRequired(args, "documentKey");
                    ServiceResult r = await ctx.Services.Documents.DeleteAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterInstrumentedTool(
                "document/batchCreate",
                "Create multiple documents. Supply the documents as a JSON string.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        collectionId = new { type = "string", description = "Collection ID." },
                        documents = new { type = "string", description = "List of DocumentRecord serialized as a JSON string." }
                    },
                    required = new[] { "bearerToken", "tenantId", "collectionId", "documents" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("document/batchCreate", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.CollectionId = McpHelpers.GetStringRequired(args, "collectionId");
                    c.Payload = McpHelpers.DeserializeArgRequired<List<DocumentRecord>>(args, "documents");
                    ServiceResult r = await ctx.Services.Documents.BatchCreateAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterInstrumentedTool(
                "document/batchDelete",
                "Delete multiple documents. Supply the batch delete request as a JSON string.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        collectionId = new { type = "string", description = "Collection ID." },
                        batchDelete = new { type = "string", description = "BatchDeleteRequest serialized as a JSON string." }
                    },
                    required = new[] { "bearerToken", "tenantId", "collectionId", "batchDelete" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("document/batchDelete", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.CollectionId = McpHelpers.GetStringRequired(args, "collectionId");
                    c.Payload = McpHelpers.DeserializeArgRequired<BatchDeleteRequest>(args, "batchDelete");
                    ServiceResult r = await ctx.Services.Documents.BatchDeleteAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterInstrumentedTool(
                "document/deleteByFilter",
                "Delete documents matching a filter. Supply an optional EnumerationQuery as a JSON string.",
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
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("document/deleteByFilter", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.CollectionId = McpHelpers.GetStringRequired(args, "collectionId");
                    c.Query = McpHelpers.DeserializeArgOptional<EnumerationQuery>(args, "query");
                    ServiceResult r = await ctx.Services.Documents.DeleteByFilterAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });

            server.RegisterInstrumentedTool(
                "document/stats",
                "Retrieve statistics for a document.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        bearerToken = new { type = "string", description = "Caller bearer token." },
                        tenantId = new { type = "string", description = "Tenant ID." },
                        collectionId = new { type = "string", description = "Collection ID." },
                        documentKey = new { type = "string", description = "Document key." }
                    },
                    required = new[] { "bearerToken", "tenantId", "collectionId", "documentKey" }
                },
                async (RpcParameters args) =>
                {
                    RequestContext c = await McpHelpers.BuildAuthenticatedContextAsync("document/stats", args, ctx.Authentication).ConfigureAwait(false);
                    c.TenantId = McpHelpers.GetStringRequired(args, "tenantId");
                    c.CollectionId = McpHelpers.GetStringRequired(args, "collectionId");
                    c.DocumentKey = McpHelpers.GetStringRequired(args, "documentKey");
                    ServiceResult r = await ctx.Services.Documents.StatsAsync(c).ConfigureAwait(false);
                    return McpHelpers.MapResult(r);
                });
        }

        #endregion
    }
}
