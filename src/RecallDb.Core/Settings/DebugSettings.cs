namespace RecallDb.Core.Settings
{
    /// <summary>
    /// Debug settings.
    /// </summary>
    public class DebugSettings
    {
        #region Public-Members

        /// <summary>
        /// Debug authentication.
        /// Default: false.
        /// </summary>
        public bool Authentication
        {
            get
            {
                return _Authentication;
            }
            set
            {
                _Authentication = value;
            }
        }

        /// <summary>
        /// Debug exceptions.
        /// Default: false.
        /// </summary>
        public bool Exceptions
        {
            get
            {
                return _Exceptions;
            }
            set
            {
                _Exceptions = value;
            }
        }

        /// <summary>
        /// Debug requests.
        /// Default: false.
        /// </summary>
        public bool Requests
        {
            get
            {
                return _Requests;
            }
            set
            {
                _Requests = value;
            }
        }

        /// <summary>
        /// Debug database queries.
        /// Default: false.
        /// </summary>
        public bool DatabaseQueries
        {
            get
            {
                return _DatabaseQueries;
            }
            set
            {
                _DatabaseQueries = value;
            }
        }

        #endregion

        #region Private-Members

        private bool _Authentication = false;
        private bool _Exceptions = false;
        private bool _Requests = false;
        private bool _DatabaseQueries = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public DebugSettings()
        {
        }

        #endregion
    }
}
