namespace RecallDb.Core.Settings
{
    /// <summary>
    /// Syslog server configuration.
    /// </summary>
    public class SyslogServer
    {
        #region Public-Members

        /// <summary>
        /// Hostname of the syslog server.
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
        /// Port of the syslog server.
        /// Default: 514.  Minimum: 1.  Maximum: 65535.
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

        #endregion

        #region Private-Members

        private string _Hostname = "localhost";
        private int _Port = 514;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SyslogServer()
        {
        }

        #endregion
    }
}
