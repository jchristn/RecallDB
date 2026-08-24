namespace RecallDb.Server.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using RecallDb.Core.Models;
    using RecallDb.Server.Classes;

    /// <summary>
    /// Authentication operations shared by REST and MCP. Validates a bearer token, or a tenant/email/password
    /// combination, and returns the resolved tenant, redacted user, and credential.
    /// </summary>
    public class AuthService
    {
        #region Private-Members

        private readonly AuthenticationService _AuthenticationService;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="authenticationService">Authentication service.</param>
        public AuthService(AuthenticationService authenticationService)
        {
            if (authenticationService == null) throw new ArgumentNullException(nameof(authenticationService));
            _AuthenticationService = authenticationService;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Authenticate a credential. This operation does not require the caller to already be authenticated.
        /// </summary>
        /// <param name="ctx">Request context. Payload must be an AuthenticateRequest.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping an AuthenticateResponse; StatusCode is 401 when authentication fails.</returns>
        public async Task<ServiceResult> AuthenticateAsync(RequestContext ctx, CancellationToken token = default)
        {
            AuthenticateRequest body = ctx.Payload as AuthenticateRequest;
            if (body == null)
                return ServiceResult.Fail(400, "Bad request", "Request body is required.");

            AuthenticationResult authResult;

            if (!string.IsNullOrEmpty(body.BearerToken))
            {
                authResult = await _AuthenticationService.AuthenticateBearerAsync(body.BearerToken).ConfigureAwait(false);
            }
            else if (!string.IsNullOrEmpty(body.Email) && !string.IsNullOrEmpty(body.Password) && !string.IsNullOrEmpty(body.TenantId))
            {
                authResult = await _AuthenticationService.AuthenticateEmailPasswordAsync(body.TenantId, body.Email, body.Password).ConfigureAwait(false);
            }
            else
            {
                return ServiceResult.Fail(400, "Bad request", "Supply BearerToken or TenantId+Email+Password.");
            }

            AuthenticateResponse resp = new AuthenticateResponse();
            resp.Success = authResult.IsAuthenticated;
            resp.Tenant = authResult.Tenant;
            resp.User = authResult.User != null ? UserMaster.Redact(authResult.User) : null;
            resp.Credential = authResult.Credential;
            resp.ErrorMessage = authResult.ErrorMessage;

            return ServiceResult.Ok(resp, authResult.IsAuthenticated ? 200 : 401);
        }

        #endregion
    }
}
