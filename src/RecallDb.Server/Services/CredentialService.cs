namespace RecallDb.Server.Services
{
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    using SyslogLogging;

    using RecallDb.Core.Database;
    using RecallDb.Core.Models;
    using RecallDb.Server.Classes;

    /// <summary>
    /// Credential operations shared by REST and MCP.
    /// </summary>
    public class CredentialService : ServiceBase
    {
        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="logging">Logging module.</param>
        public CredentialService(DatabaseDriverBase database, LoggingModule logging)
            : base(database, logging)
        {
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// List credentials in a tenant (first page).
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping an EnumerationResult of Credential.</returns>
        public async Task<ServiceResult> ListAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            Stopwatch sw = Stopwatch.StartNew();
            EnumerationQuery query = new EnumerationQuery();
            EnumerationResult<Credential> result = await _Database.Credentials.EnumerateAsync(ctx.TenantId, query).ConfigureAwait(false);
            result.TotalMs = sw.Elapsed.TotalMilliseconds;
            return ServiceResult.Ok(result);
        }

        /// <summary>
        /// Enumerate credentials in a tenant with pagination.
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping an EnumerationResult of Credential.</returns>
        public async Task<ServiceResult> EnumerateAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            EnumerationQuery query = ctx.Query ?? new EnumerationQuery();

            Stopwatch sw = Stopwatch.StartNew();
            EnumerationResult<Credential> result = await _Database.Credentials.EnumerateAsync(ctx.TenantId, query).ConfigureAwait(false);
            result.TotalMs = sw.Elapsed.TotalMilliseconds;
            return ServiceResult.Ok(result);
        }

        /// <summary>
        /// Read a credential by ID.
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping a Credential.</returns>
        public async Task<ServiceResult> ReadAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            Credential cred = await _Database.Credentials.ReadAsync(ctx.TenantId, ctx.ResourceId).ConfigureAwait(false);
            if (cred == null)
                return ServiceResult.Fail(404, "Not found", "Credential not found.");

            return ServiceResult.Ok(cred);
        }

        /// <summary>
        /// Test credential existence.
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult.</returns>
        public async Task<ServiceResult> ExistsAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            bool exists = await _Database.Credentials.ExistsAsync(ctx.TenantId, ctx.ResourceId).ConfigureAwait(false);
            return ServiceResult.Ok(exists, exists ? 200 : 404);
        }

        /// <summary>
        /// Create a credential (admin or tenant admin).
        /// </summary>
        /// <param name="ctx">Request context. Payload must be a Credential.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping the created Credential (201).</returns>
        public async Task<ServiceResult> CreateAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ctx.Auth.IsAdmin && !ctx.Auth.IsTenantAdmin)
                return ServiceResult.Fail(403, "Forbidden", "Admin or tenant admin required.");

            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            Credential cred = ctx.Payload as Credential;
            if (cred == null)
                return ServiceResult.Fail(400, "Bad request", "Request body is required.");

            cred.TenantId = ctx.TenantId;
            cred = await _Database.Credentials.CreateAsync(cred).ConfigureAwait(false);
            return ServiceResult.Ok(cred, 201);
        }

        /// <summary>
        /// Update a credential (admin or tenant admin).
        /// </summary>
        /// <param name="ctx">Request context. Payload must be a Credential.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping the updated Credential.</returns>
        public async Task<ServiceResult> UpdateAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ctx.Auth.IsAdmin && !ctx.Auth.IsTenantAdmin)
                return ServiceResult.Fail(403, "Forbidden", "Admin or tenant admin required.");

            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            Credential cred = ctx.Payload as Credential;
            if (cred == null)
                return ServiceResult.Fail(400, "Bad request", "Request body is required.");

            cred.Id = ctx.ResourceId;
            cred.TenantId = ctx.TenantId;
            cred = await _Database.Credentials.UpdateAsync(cred).ConfigureAwait(false);
            if (cred == null)
                return ServiceResult.Fail(404, "Not found", "Credential not found.");

            return ServiceResult.Ok(cred);
        }

        /// <summary>
        /// Delete a credential (admin or tenant admin).
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult (204).</returns>
        public async Task<ServiceResult> DeleteAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ctx.Auth.IsAdmin && !ctx.Auth.IsTenantAdmin)
                return ServiceResult.Fail(403, "Forbidden", "Admin or tenant admin required.");

            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            await _Database.Credentials.DeleteAsync(ctx.TenantId, ctx.ResourceId).ConfigureAwait(false);
            return ServiceResult.NoContent();
        }

        #endregion
    }
}
