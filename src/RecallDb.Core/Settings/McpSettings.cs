namespace RecallDb.Core.Settings
{
    /// <summary>
    /// MCP (Model Context Protocol) server settings. The MCP server is hosted in-process alongside the REST API
    /// and exposes the same operations over Streamable HTTP (POST /mcp for JSON-RPC, GET /mcp for the SSE stream).
    /// </summary>
    public class McpSettings
    {
        #region Public-Members

        /// <summary>
        /// Whether the MCP server is enabled. Default: true.
        /// </summary>
        public bool Enabled
        {
            get
            {
                return _Enabled;
            }
            set
            {
                _Enabled = value;
            }
        }

        /// <summary>
        /// Hostname the MCP HTTP endpoint binds to. Default: 127.0.0.1.
        /// </summary>
        public string Hostname
        {
            get
            {
                return _Hostname;
            }
            set
            {
                if (string.IsNullOrEmpty(value)) value = "127.0.0.1";
                _Hostname = value;
            }
        }

        /// <summary>
        /// Port the MCP HTTP endpoint listens on. Default: 8620. Clamped to 1-65535.
        /// </summary>
        public int Port
        {
            get
            {
                return _Port;
            }
            set
            {
                if (value < 1) value = 1;
                if (value > 65535) value = 65535;
                _Port = value;
            }
        }

        /// <summary>
        /// Server name advertised to MCP clients during initialization. Default: RecallDB.McpServer.
        /// </summary>
        public string ServerName
        {
            get
            {
                return _ServerName;
            }
            set
            {
                if (string.IsNullOrEmpty(value)) value = "RecallDB.McpServer";
                _ServerName = value;
            }
        }

        /// <summary>
        /// Server version advertised to MCP clients during initialization. Default: 0.2.0.
        /// </summary>
        public string ServerVersion
        {
            get
            {
                return _ServerVersion;
            }
            set
            {
                if (string.IsNullOrEmpty(value)) value = "0.2.0";
                _ServerVersion = value;
            }
        }

        /// <summary>
        /// Whether to log MCP tool invocations at debug level. Default: false.
        /// </summary>
        public bool LogOperations
        {
            get
            {
                return _LogOperations;
            }
            set
            {
                _LogOperations = value;
            }
        }

        #endregion

        #region Private-Members

        private bool _Enabled = true;
        private string _Hostname = "127.0.0.1";
        private int _Port = 8620;
        private string _ServerName = "RecallDB.McpServer";
        private string _ServerVersion = "0.2.0";
        private bool _LogOperations = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public McpSettings()
        {
        }

        #endregion
    }
}
