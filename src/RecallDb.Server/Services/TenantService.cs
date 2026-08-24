namespace RecallDb.Server.Services
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    using SyslogLogging;

    using RecallDb.Core.Database;
    using RecallDb.Core.Models;
    using RecallDb.Server.Classes;

    /// <summary>
    /// Tenant operations shared by REST and MCP.
    /// </summary>
    public class TenantService : ServiceBase
    {
        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="logging">Logging module.</param>
        public TenantService(DatabaseDriverBase database, LoggingModule logging)
            : base(database, logging)
        {
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// List tenants. Admins receive a paginated first page of all tenants; other principals receive only the
        /// tenant they authenticated against. This preserves the REST GET-list behavior and is not exposed via MCP.
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping an EnumerationResult of TenantMetadata.</returns>
        public async Task<ServiceResult> ListAsync(RequestContext ctx, CancellationToken token = default)
        {
            AuthenticationResult auth = ctx.Auth;
            Stopwatch sw = Stopwatch.StartNew();

            if (auth.IsAdmin)
            {
                EnumerationQuery query = new EnumerationQuery();
                EnumerationResult<TenantMetadata> result = await _Database.Tenants.EnumerateAsync(query).ConfigureAwait(false);
                result.TotalMs = sw.Elapsed.TotalMilliseconds;
                return ServiceResult.Ok(result);
            }
            else
            {
                List<TenantMetadata> list = new List<TenantMetadata>();
                if (auth.Tenant != null) list.Add(auth.Tenant);

                EnumerationResult<TenantMetadata> result = new EnumerationResult<TenantMetadata>();
                result.Success = true;
                result.MaxResults = 1;
                result.EndOfResults = true;
                result.TotalRecords = list.Count;
                result.RecordsRemaining = 0;
                result.Objects = list;
                result.TotalMs = sw.Elapsed.TotalMilliseconds;
                return ServiceResult.Ok(result);
            }
        }

        /// <summary>
        /// Enumerate tenants with pagination (admin only).
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping an EnumerationResult of TenantMetadata.</returns>
        public async Task<ServiceResult> EnumerateAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ctx.Auth.IsAdmin)
                return ServiceResult.Fail(403, "Forbidden", "Admin access required.");

            EnumerationQuery query = ctx.Query ?? new EnumerationQuery();

            Stopwatch sw = Stopwatch.StartNew();
            EnumerationResult<TenantMetadata> result = await _Database.Tenants.EnumerateAsync(query).ConfigureAwait(false);
            result.TotalMs = sw.Elapsed.TotalMilliseconds;
            return ServiceResult.Ok(result);
        }

        /// <summary>
        /// Read a tenant by ID.
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping a TenantMetadata.</returns>
        public async Task<ServiceResult> ReadAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ctx.Auth.IsAdmin && !ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            TenantMetadata tenant = await _Database.Tenants.ReadAsync(ctx.TenantId).ConfigureAwait(false);
            if (tenant == null)
                return ServiceResult.Fail(404, "Not found", "Tenant not found.");

            return ServiceResult.Ok(tenant);
        }

        /// <summary>
        /// Test tenant existence. Data carries the boolean result; StatusCode carries 200/404 for REST.
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult.</returns>
        public async Task<ServiceResult> ExistsAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ctx.Auth.IsAdmin && !ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            bool exists = await _Database.Tenants.ExistsAsync(ctx.TenantId).ConfigureAwait(false);
            return ServiceResult.Ok(exists, exists ? 200 : 404);
        }

        /// <summary>
        /// Create a tenant (admin only).
        /// </summary>
        /// <param name="ctx">Request context. Payload must be a TenantMetadata.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping the created TenantMetadata (201).</returns>
        public async Task<ServiceResult> CreateAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ctx.Auth.IsAdmin)
                return ServiceResult.Fail(403, "Forbidden", "Admin access required.");

            TenantMetadata tenant = ctx.Payload as TenantMetadata;
            if (tenant == null)
                return ServiceResult.Fail(400, "Bad request", "Request body is required.");

            tenant = await _Database.Tenants.CreateAsync(tenant).ConfigureAwait(false);
            return ServiceResult.Ok(tenant, 201);
        }

        /// <summary>
        /// Update a tenant.
        /// </summary>
        /// <param name="ctx">Request context. Payload must be a TenantMetadata.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping the updated TenantMetadata.</returns>
        public async Task<ServiceResult> UpdateAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ctx.Auth.IsAdmin && !ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            TenantMetadata tenant = ctx.Payload as TenantMetadata;
            if (tenant == null)
                return ServiceResult.Fail(400, "Bad request", "Request body is required.");

            tenant.Id = ctx.TenantId;
            tenant = await _Database.Tenants.UpdateAsync(tenant).ConfigureAwait(false);
            if (tenant == null)
                return ServiceResult.Fail(404, "Not found", "Tenant not found.");

            return ServiceResult.Ok(tenant);
        }

        /// <summary>
        /// Delete a tenant and cascade-delete its collections, credentials, and users (admin only).
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult (204).</returns>
        public async Task<ServiceResult> DeleteAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ctx.Auth.IsAdmin)
                return ServiceResult.Fail(403, "Forbidden", "Admin access required.");

            string id = ctx.TenantId;
            await _Database.Collections.DeleteByTenantIdAsync(id).ConfigureAwait(false);
            await _Database.Credentials.DeleteByTenantIdAsync(id).ConfigureAwait(false);
            await _Database.Users.DeleteByTenantIdAsync(id).ConfigureAwait(false);
            await _Database.Tenants.DeleteAsync(id).ConfigureAwait(false);
            return ServiceResult.NoContent();
        }

        #endregion
    }
}
