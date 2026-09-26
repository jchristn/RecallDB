namespace RecallDb.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Server health information returned by the health endpoint and the MCP server/info tool.
    /// </summary>
    public class HealthInfo
    {
        #region Public-Members

        /// <summary>
        /// Server product name.
        /// </summary>
        public string Name { get; set; } = "RecallDB";

        /// <summary>
        /// Server version.
        /// </summary>
        public string Version { get; set; } = null;

        /// <summary>
        /// Server uptime in milliseconds.
        /// </summary>
        public double UptimeMs { get; set; } = 0;

        /// <summary>
        /// Search capabilities this server supports (see RecallDb.Core.SearchCapabilities), so clients can
        /// detect features on servers that report the same version. Never null; empty on servers that predate it.
        /// </summary>
        public List<string> Capabilities
        {
            get
            {
                return _Capabilities;
            }
            set
            {
                _Capabilities = value ?? new List<string>();
            }
        }

        #endregion

        #region Private-Members

        private List<string> _Capabilities = SearchCapabilities.All;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public HealthInfo()
        {
        }

        #endregion
    }
}
