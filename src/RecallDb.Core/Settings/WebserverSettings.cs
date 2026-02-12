namespace RecallDb.Core.Settings
{
    /// <summary>
    /// Webserver settings.
    /// </summary>
    public class WebserverSettings
    {
        #region Public-Members

        /// <summary>
        /// Hostname on which to listen.
        /// Default: localhost.
        /// </summary>
        public string Hostname
        {
            get
            {
                return _Hostname;
            }
            set
            {
                if (string.IsNullOrEmpty(value)) throw new System.ArgumentNullException(nameof(Hostname));
                _Hostname = value;
            }
        }

        /// <summary>
        /// Port on which to listen.
        /// Default: 8600.  Minimum: 1.  Maximum: 65535.
        /// </summary>
        public int Port
        {
            get
            {
                return _Port;
            }
            set
            {
                if (value < 1 || value > 65535) throw new System.ArgumentOutOfRangeException(nameof(Port));
                _Port = value;
            }
        }

        /// <summary>
        /// Enable SSL.
        /// Default: false.
        /// </summary>
        public bool Ssl
        {
            get
            {
                return _Ssl;
            }
            set
            {
                _Ssl = value;
            }
        }

        #endregion

        #region Private-Members

        private string _Hostname = "localhost";
        private int _Port = 8600;
        private bool _Ssl = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public WebserverSettings()
        {
        }

        #endregion
    }
}
