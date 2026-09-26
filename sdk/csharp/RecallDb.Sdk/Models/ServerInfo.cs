namespace RecallDb.Sdk.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Server information returned by GET /.
    /// </summary>
    public class ServerInfo
    {
        /// <summary>
        /// Server product name.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Server version.
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// Server uptime in milliseconds.
        /// </summary>
        public double UptimeMs { get; set; }

        /// <summary>
        /// Search capabilities the server supports (see <see cref="RecallDb.Sdk.Constants.Capabilities"/>).
        /// Servers that report the same version can differ in capabilities, so check this list before relying on a
        /// feature. Empty for servers that predate the field.
        /// </summary>
        public List<string> Capabilities { get; set; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ServerInfo()
        {
            Capabilities = new List<string>();
        }
    }
}
