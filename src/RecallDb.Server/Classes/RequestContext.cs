namespace RecallDb.Server.Classes
{
    using System;

    /// <summary>
    /// Request context for tracking API requests.
    /// </summary>
    public class RequestContext
    {
        #region Public-Members

        /// <summary>
        /// Request ID.
        /// </summary>
        public string RequestId
        {
            get
            {
                return _RequestId;
            }
            set
            {
                _RequestId = value;
            }
        }

        /// <summary>
        /// Timestamp when the request was received.
        /// </summary>
        public DateTime ReceivedUtc
        {
            get
            {
                return _ReceivedUtc;
            }
            set
            {
                _ReceivedUtc = value;
            }
        }

        /// <summary>
        /// Tenant ID from authentication.
        /// </summary>
        public string TenantId
        {
            get
            {
                return _TenantId;
            }
            set
            {
                _TenantId = value;
            }
        }

        /// <summary>
        /// User ID from authentication.
        /// </summary>
        public string UserId
        {
            get
            {
                return _UserId;
            }
            set
            {
                _UserId = value;
            }
        }

        /// <summary>
        /// HTTP method.
        /// </summary>
        public string HttpMethod
        {
            get
            {
                return _HttpMethod;
            }
            set
            {
                _HttpMethod = value;
            }
        }

        /// <summary>
        /// Original URL.
        /// </summary>
        public string OriginalUrl
        {
            get
            {
                return _OriginalUrl;
            }
            set
            {
                _OriginalUrl = value;
            }
        }

        /// <summary>
        /// Path.
        /// </summary>
        public string Path
        {
            get
            {
                return _Path;
            }
            set
            {
                _Path = value;
            }
        }

        /// <summary>
        /// Client IP address.
        /// </summary>
        public string ClientIpAddress
        {
            get
            {
                return _ClientIpAddress;
            }
            set
            {
                _ClientIpAddress = value;
            }
        }

        /// <summary>
        /// Request body data.
        /// </summary>
        public byte[] Data
        {
            get
            {
                return _Data;
            }
            set
            {
                _Data = value;
            }
        }

        #endregion

        #region Private-Members

        private string _RequestId = Guid.NewGuid().ToString();
        private DateTime _ReceivedUtc = DateTime.UtcNow;
        private string _TenantId = null;
        private string _UserId = null;
        private string _HttpMethod = null;
        private string _OriginalUrl = null;
        private string _Path = null;
        private string _ClientIpAddress = null;
        private byte[] _Data = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RequestContext()
        {
        }

        #endregion
    }
}
