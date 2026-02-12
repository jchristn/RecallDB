namespace RecallDb.Core.Settings
{
    using System.Collections.Generic;

    /// <summary>
    /// Logging settings.
    /// </summary>
    public class LoggingSettings
    {
        #region Public-Members

        /// <summary>
        /// Enable console logging.
        /// Default: true.
        /// </summary>
        public bool ConsoleLogging
        {
            get
            {
                return _ConsoleLogging;
            }
            set
            {
                _ConsoleLogging = value;
            }
        }

        /// <summary>
        /// Enable colors in console output.
        /// Default: true.
        /// </summary>
        public bool EnableColors
        {
            get
            {
                return _EnableColors;
            }
            set
            {
                _EnableColors = value;
            }
        }

        /// <summary>
        /// Minimum severity level for logging.
        /// Default: 0 (Debug).
        /// </summary>
        public int MinimumSeverity
        {
            get
            {
                return _MinimumSeverity;
            }
            set
            {
                if (value < 0) throw new System.ArgumentOutOfRangeException(nameof(MinimumSeverity));
                _MinimumSeverity = value;
            }
        }

        /// <summary>
        /// Log directory.
        /// Default: ./logs/.
        /// </summary>
        public string LogDirectory
        {
            get
            {
                return _LogDirectory;
            }
            set
            {
                _LogDirectory = value;
            }
        }

        /// <summary>
        /// Log filename.
        /// Default: recalldb.log.
        /// </summary>
        public string LogFilename
        {
            get
            {
                return _LogFilename;
            }
            set
            {
                _LogFilename = value;
            }
        }

        /// <summary>
        /// Syslog servers.
        /// </summary>
        public List<SyslogServer> Servers
        {
            get
            {
                return _Servers;
            }
            set
            {
                if (value == null) value = new List<SyslogServer>();
                _Servers = value;
            }
        }

        #endregion

        #region Private-Members

        private bool _ConsoleLogging = true;
        private bool _EnableColors = true;
        private int _MinimumSeverity = 0;
        private string _LogDirectory = "./logs/";
        private string _LogFilename = "recalldb.log";
        private List<SyslogServer> _Servers = new List<SyslogServer>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public LoggingSettings()
        {
        }

        #endregion
    }
}
