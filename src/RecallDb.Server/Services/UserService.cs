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
    /// User operations shared by REST and MCP. User passwords are always redacted in responses.
    /// </summary>
    public class UserService : ServiceBase
    {
        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="logging">Logging module.</param>
        public UserService(DatabaseDriverBase database, LoggingModule logging)
            : base(database, logging)
        {
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// List users in a tenant (first page).
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping an EnumerationResult of UserMaster.</returns>
        public async Task<ServiceResult> ListAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            Stopwatch sw = Stopwatch.StartNew();
            EnumerationQuery query = new EnumerationQuery();
            EnumerationResult<UserMaster> result = await _Database.Users.EnumerateAsync(ctx.TenantId, query).ConfigureAwait(false);
            result.TotalMs = sw.Elapsed.TotalMilliseconds;
            return ServiceResult.Ok(result);
        }

        /// <summary>
        /// Enumerate users in a tenant with pagination.
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping an EnumerationResult of UserMaster.</returns>
        public async Task<ServiceResult> EnumerateAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            EnumerationQuery query = ctx.Query ?? new EnumerationQuery();

            Stopwatch sw = Stopwatch.StartNew();
            EnumerationResult<UserMaster> result = await _Database.Users.EnumerateAsync(ctx.TenantId, query).ConfigureAwait(false);
            result.TotalMs = sw.Elapsed.TotalMilliseconds;
            return ServiceResult.Ok(result);
        }

        /// <summary>
        /// Read a user by ID (password redacted).
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping a redacted UserMaster.</returns>
        public async Task<ServiceResult> ReadAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            UserMaster user = await _Database.Users.ReadAsync(ctx.TenantId, ctx.UserId).ConfigureAwait(false);
            if (user == null)
                return ServiceResult.Fail(404, "Not found", "User not found.");

            return ServiceResult.Ok(UserMaster.Redact(user));
        }

        /// <summary>
        /// Test user existence.
        /// </summary>
        /// <param name="ctx">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult.</returns>
        public async Task<ServiceResult> ExistsAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            bool exists = await _Database.Users.ExistsAsync(ctx.TenantId, ctx.UserId).ConfigureAwait(false);
            return ServiceResult.Ok(exists, exists ? 200 : 404);
        }

        /// <summary>
        /// Create a user (admin or tenant admin).
        /// </summary>
        /// <param name="ctx">Request context. Payload must be a UserMaster.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping the created redacted UserMaster (201).</returns>
        public async Task<ServiceResult> CreateAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ctx.Auth.IsAdmin && !ctx.Auth.IsTenantAdmin)
                return ServiceResult.Fail(403, "Forbidden", "Admin or tenant admin required.");

            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            UserMaster user = ctx.Payload as UserMaster;
            if (user == null)
                return ServiceResult.Fail(400, "Bad request", "Request body is required.");

            user.TenantId = ctx.TenantId;
            user = await _Database.Users.CreateAsync(user).ConfigureAwait(false);
            return ServiceResult.Ok(UserMaster.Redact(user), 201);
        }

        /// <summary>
        /// Update a user (admin or tenant admin).
        /// </summary>
        /// <param name="ctx">Request context. Payload must be a UserMaster.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping the updated redacted UserMaster.</returns>
        public async Task<ServiceResult> UpdateAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ctx.Auth.IsAdmin && !ctx.Auth.IsTenantAdmin)
                return ServiceResult.Fail(403, "Forbidden", "Admin or tenant admin required.");

            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            UserMaster user = ctx.Payload as UserMaster;
            if (user == null)
                return ServiceResult.Fail(400, "Bad request", "Request body is required.");

            user.Id = ctx.UserId;
            user.TenantId = ctx.TenantId;
            user = await _Database.Users.UpdateAsync(user).ConfigureAwait(false);
            if (user == null)
                return ServiceResult.Fail(404, "Not found", "User not found.");

            return ServiceResult.Ok(UserMaster.Redact(user));
        }

        /// <summary>
        /// Delete a user and its credentials (admin or tenant admin).
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

            await _Database.Credentials.DeleteByUserIdAsync(ctx.TenantId, ctx.UserId).ConfigureAwait(false);
            await _Database.Users.DeleteAsync(ctx.TenantId, ctx.UserId).ConfigureAwait(false);
            return ServiceResult.NoContent();
        }

        #endregion
    }
}
