namespace RecallDb.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using SyslogLogging;
    using RecallDb.Core.Database;
    using RecallDb.Core.Models;
    using RecallDb.Core.Settings;
    using RecallDb.Server.Classes;

    /// <summary>
    /// Authentication service for validating API requests.
    /// </summary>
    public class AuthenticationService
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private readonly ServerSettings _Settings;
        private readonly DatabaseDriverBase _Database;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[AuthService] ";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="database">Database driver.</param>
        /// <param name="logging">Logging module.</param>
        public AuthenticationService(ServerSettings settings, DatabaseDriverBase database, LoggingModule logging)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Logging = logging;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Authenticate a bearer token.
        /// Checks admin API keys first, then credential chain.
        /// </summary>
        /// <param name="token">Bearer token.</param>
        /// <returns>AuthenticationResult.</returns>
        public async Task<AuthenticationResult> AuthenticateBearerAsync(string token)
        {
            AuthenticationResult result = new AuthenticationResult();

            if (string.IsNullOrEmpty(token))
            {
                result.ErrorMessage = "No token supplied.";
                return result;
            }

            // Check admin API keys
            if (_Settings.AdminApiKeys != null && _Settings.AdminApiKeys.Contains(token))
            {
                result.IsAuthenticated = true;
                result.IsAdminApiKey = true;
                result.AuthMethod = "AdminApiKey";

                if (_Settings.Debug.Authentication && _Logging != null)
                    _Logging.Debug(_Header + "authenticated via admin API key");

                return result;
            }

            // Look up credential by bearer token
            Credential credential = null;

            try
            {
                credential = await _Database.Credentials.ReadByBearerTokenAsync(token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                if (_Settings.Debug.Authentication && _Logging != null)
                    _Logging.Warn(_Header + "exception looking up credential: " + e.Message);
                result.ErrorMessage = "Unable to validate token.";
                return result;
            }

            if (credential == null || !credential.Active)
            {
                result.ErrorMessage = "Invalid or inactive token.";
                return result;
            }

            // Look up user
            UserMaster user = null;

            try
            {
                user = await _Database.Users.ReadAsync(credential.TenantId, credential.UserId).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                if (_Settings.Debug.Authentication && _Logging != null)
                    _Logging.Warn(_Header + "exception looking up user: " + e.Message);
                result.ErrorMessage = "Unable to validate user.";
                return result;
            }

            if (user == null || !user.Active)
            {
                result.ErrorMessage = "Invalid or inactive user.";
                return result;
            }

            // Look up tenant
            TenantMetadata tenant = null;

            try
            {
                tenant = await _Database.Tenants.ReadAsync(credential.TenantId).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                if (_Settings.Debug.Authentication && _Logging != null)
                    _Logging.Warn(_Header + "exception looking up tenant: " + e.Message);
                result.ErrorMessage = "Unable to validate tenant.";
                return result;
            }

            if (tenant == null || !tenant.Active)
            {
                result.ErrorMessage = "Invalid or inactive tenant.";
                return result;
            }

            result.IsAuthenticated = true;
            result.Tenant = tenant;
            result.User = user;
            result.Credential = credential;
            result.AuthMethod = "BearerToken";

            if (_Settings.Debug.Authentication && _Logging != null)
                _Logging.Debug(_Header + "authenticated user " + user.Email + " tenant " + tenant.Id);

            return result;
        }

        /// <summary>
        /// Authenticate via email and password.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="email">Email address.</param>
        /// <param name="password">Password.</param>
        /// <returns>AuthenticationResult.</returns>
        public async Task<AuthenticationResult> AuthenticateEmailPasswordAsync(string tenantId, string email, string password)
        {
            AuthenticationResult result = new AuthenticationResult();

            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                result.ErrorMessage = "Tenant ID, email, and password are required.";
                return result;
            }

            // Look up tenant
            TenantMetadata tenant = null;

            try
            {
                tenant = await _Database.Tenants.ReadAsync(tenantId).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                if (_Settings.Debug.Authentication && _Logging != null)
                    _Logging.Warn(_Header + "exception looking up tenant: " + e.Message);
                result.ErrorMessage = "Unable to validate tenant.";
                return result;
            }

            if (tenant == null || !tenant.Active)
            {
                result.ErrorMessage = "Invalid or inactive tenant.";
                return result;
            }

            // Look up user by email
            UserMaster user = null;

            try
            {
                user = await _Database.Users.ReadByEmailAsync(tenantId, email).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                if (_Settings.Debug.Authentication && _Logging != null)
                    _Logging.Warn(_Header + "exception looking up user: " + e.Message);
                result.ErrorMessage = "Unable to validate user.";
                return result;
            }

            if (user == null || !user.Active)
            {
                result.ErrorMessage = "Invalid or inactive user.";
                return result;
            }

            // Verify password
            if (!user.VerifyPassword(password))
            {
                result.ErrorMessage = "Invalid password.";
                return result;
            }

            result.IsAuthenticated = true;
            result.Tenant = tenant;
            result.User = user;
            result.AuthMethod = "EmailPassword";

            if (_Settings.Debug.Authentication && _Logging != null)
                _Logging.Debug(_Header + "authenticated user " + user.Email + " via email/password");

            return result;
        }

        #endregion
    }
}
