namespace RecallDb.Server.Mcp
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    using SyslogLogging;
    using Voltaic.Mcp;

    using RecallDb.Core.Settings;
    using RecallDb.Server.Mcp.Registrations;
    using RecallDb.Server.Services;

    using VoltaicAuth = Voltaic.Core.AuthenticationResult;

    /// <summary>
    /// Hosts the MCP server in-process over Streamable HTTP (POST /mcp for JSON-RPC, GET /mcp for the SSE stream).
    /// Authentication is per-caller bearer: the transport-level handler early-rejects an invalid Authorization
    /// header, while each tool also accepts a bearerToken argument that the service layer authorizes identically
    /// to REST.
    /// </summary>
    public class McpServerService : IDisposable
    {
        #region Private-Members

        private readonly string _Header = "[McpServer] ";
        private readonly McpSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly RecallDbServices _Services;
        private readonly AuthenticationService _Authentication;
        private readonly string _Version;
        private readonly DateTime _StartTimeUtc;

        private McpHttpServer _Server;
        private Task _ServerTask;
        private bool _Disposed;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">MCP settings.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="services">Shared service layer.</param>
        /// <param name="authentication">Authentication service.</param>
        /// <param name="version">Server version.</param>
        /// <param name="startTimeUtc">Server start time (UTC).</param>
        public McpServerService(
            McpSettings settings,
            LoggingModule logging,
            RecallDbServices services,
            AuthenticationService authentication,
            string version,
            DateTime startTimeUtc)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Services = services ?? throw new ArgumentNullException(nameof(services));
            _Authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
            _Version = version;
            _StartTimeUtc = startTimeUtc;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Construct the MCP HTTP server, register every tool, and begin listening. No-op when disabled.
        /// </summary>
        /// <param name="token">Cancellation token controlling the server lifetime.</param>
        public void Start(CancellationToken token)
        {
            if (!_Settings.Enabled)
            {
                _Logging.Info(_Header + "MCP server disabled by settings");
                return;
            }

            _Server = new McpHttpServer(_Settings.Hostname, _Settings.Port);
            _Server.ServerName = _Settings.ServerName;
            _Server.ServerVersion = _Settings.ServerVersion;
            _Server.AuthenticationHandler = AuthenticateAsync;

            McpToolContext toolContext = new McpToolContext(_Services, _Authentication, _Settings, _Version, _StartTimeUtc);
            RegisterAllTools(toolContext);

            _ServerTask = _Server.StartAsync(token);

            _Logging.Info(_Header + "MCP server listening on http://" + _Settings.Hostname + ":" + _Settings.Port + "/mcp (Streamable HTTP)");
        }

        /// <summary>
        /// Dispose the MCP server.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Dispose pattern implementation.
        /// </summary>
        /// <param name="disposing">True when called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;

            if (disposing)
            {
                if (_Server != null)
                {
                    _Server.Dispose();
                    _Server = null;
                }
            }

            _Disposed = true;
        }

        #endregion

        #region Private-Methods

        private void RegisterAllTools(McpToolContext toolContext)
        {
            ServerRegistrations.Register(_Server, toolContext);
            AuthRegistrations.Register(_Server, toolContext);
            TenantRegistrations.Register(_Server, toolContext);
            UserRegistrations.Register(_Server, toolContext);
            CredentialRegistrations.Register(_Server, toolContext);
            CollectionRegistrations.Register(_Server, toolContext);
            DocumentRegistrations.Register(_Server, toolContext);
            LabelRegistrations.Register(_Server, toolContext);
            TagRegistrations.Register(_Server, toolContext);
            SearchRegistrations.Register(_Server, toolContext);
            RequestHistoryRegistrations.Register(_Server, toolContext);
        }

        private async Task<VoltaicAuth> AuthenticateAsync(HttpListenerRequest request)
        {
            // Absent Authorization header: allow through. Authenticated tools require the bearerToken argument,
            // which the service layer authorizes; unauthenticated tools (server/info, auth/authenticate) are open.
            string header = request != null && request.Headers != null ? request.Headers["Authorization"] : null;
            if (string.IsNullOrEmpty(header))
                return new VoltaicAuth { IsAuthenticated = true };

            string bearerToken = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? header.Substring(7).Trim()
                : header.Trim();

            RecallDb.Server.Classes.AuthenticationResult result = await _Authentication.AuthenticateBearerAsync(bearerToken).ConfigureAwait(false);
            if (result.IsAuthenticated)
            {
                VoltaicAuth ok = new VoltaicAuth();
                ok.IsAuthenticated = true;
                ok.Principal = result.Tenant != null ? result.Tenant.Id : "admin";
                return ok;
            }

            VoltaicAuth denied = new VoltaicAuth();
            denied.IsAuthenticated = false;
            denied.StatusCode = 401;
            denied.ErrorMessage = "Invalid bearer token.";
            return denied;
        }

        #endregion
    }
}
