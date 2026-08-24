namespace RecallDb.Server.Mcp
{
    using System;

    using RecallDb.Core.Settings;
    using RecallDb.Server.Services;

    /// <summary>
    /// Dependencies passed to each MCP tool registration class: the shared service layer, the authentication
    /// service (for per-caller bearer resolution), and server metadata for the server/info tool.
    /// </summary>
    public class McpToolContext
    {
        #region Public-Members

        /// <summary>
        /// Shared service layer.
        /// </summary>
        public RecallDbServices Services { get; }

        /// <summary>
        /// Authentication service used to resolve the caller's bearer token.
        /// </summary>
        public AuthenticationService Authentication { get; }

        /// <summary>
        /// MCP settings (used by the server/info tool to report the endpoint).
        /// </summary>
        public McpSettings Settings { get; }

        /// <summary>
        /// Server version.
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// Server start time (UTC), used to compute uptime.
        /// </summary>
        public DateTime StartTimeUtc { get; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="services">Shared service layer.</param>
        /// <param name="authentication">Authentication service.</param>
        /// <param name="settings">MCP settings.</param>
        /// <param name="version">Server version.</param>
        /// <param name="startTimeUtc">Server start time (UTC).</param>
        public McpToolContext(RecallDbServices services, AuthenticationService authentication, McpSettings settings, string version, DateTime startTimeUtc)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (authentication == null) throw new ArgumentNullException(nameof(authentication));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            Services = services;
            Authentication = authentication;
            Settings = settings;
            Version = version;
            StartTimeUtc = startTimeUtc;
        }

        #endregion
    }
}
