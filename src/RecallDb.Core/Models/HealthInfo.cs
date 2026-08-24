namespace RecallDb.Core.Models
{
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
