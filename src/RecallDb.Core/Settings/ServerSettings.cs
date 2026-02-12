namespace RecallDb.Core.Settings
{
    using System.Collections.Generic;

    /// <summary>
    /// Server settings.
    /// </summary>
    public class ServerSettings
    {
        #region Public-Members

        /// <summary>
        /// Webserver settings.
        /// </summary>
        public WebserverSettings Webserver
        {
            get
            {
                return _Webserver;
            }
            set
            {
                if (value == null) throw new System.ArgumentNullException(nameof(Webserver));
                _Webserver = value;
            }
        }

        /// <summary>
        /// Database settings.
        /// </summary>
        public DatabaseSettings Database
        {
            get
            {
                return _Database;
            }
            set
            {
                if (value == null) throw new System.ArgumentNullException(nameof(Database));
                _Database = value;
            }
        }

        /// <summary>
        /// Logging settings.
        /// </summary>
        public LoggingSettings Logging
        {
            get
            {
                return _Logging;
            }
            set
            {
                if (value == null) throw new System.ArgumentNullException(nameof(Logging));
                _Logging = value;
            }
        }

        /// <summary>
        /// Debug settings.
        /// </summary>
        public DebugSettings Debug
        {
            get
            {
                return _Debug;
            }
            set
            {
                if (value == null) throw new System.ArgumentNullException(nameof(Debug));
                _Debug = value;
            }
        }

        /// <summary>
        /// Admin API keys.
        /// Default: ["recalldbadmin"].
        /// </summary>
        public List<string> AdminApiKeys
        {
            get
            {
                return _AdminApiKeys;
            }
            set
            {
                if (value == null) value = new List<string>();
                _AdminApiKeys = value;
            }
        }

        #endregion

        #region Private-Members

        private WebserverSettings _Webserver = new WebserverSettings();
        private DatabaseSettings _Database = new DatabaseSettings();
        private LoggingSettings _Logging = new LoggingSettings();
        private DebugSettings _Debug = new DebugSettings();
        private List<string> _AdminApiKeys = new List<string> { "recalldbadmin" };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ServerSettings()
        {
        }

        #endregion
    }
}
