namespace RecallDb.Server.Classes
{
    using System;

    using RecallDb.Core.Enums;
    using RecallDb.Core.Models;

    /// <summary>
    /// Normalized request context and the single source of truth for a request. Both the REST and MCP transports
    /// populate an instance of this type and pass it into the service layer, so business logic never depends on
    /// which transport originated the call. Holds the resolved authentication result, the classified resource and
    /// operation, the target identifiers, and the typed payload.
    /// </summary>
    public class RequestContext
    {
        #region Public-Members

        /// <summary>
        /// Request ID. Defaults to a new GUID string.
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
        /// Timestamp when the request was received (UTC).
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
        /// Transport that originated the request. Default: Rest.
        /// </summary>
        public RequestOriginEnum Origin
        {
            get
            {
                return _Origin;
            }
            set
            {
                _Origin = value;
            }
        }

        /// <summary>
        /// Resolved authentication result. Null until authentication runs.
        /// </summary>
        public AuthenticationResult Auth
        {
            get
            {
                return _Auth;
            }
            set
            {
                _Auth = value;
            }
        }

        /// <summary>
        /// Resource type targeted by the request (from the centralized operation-scope mapping).
        /// </summary>
        public ResourceTypeEnum ResourceType
        {
            get
            {
                return _ResourceType;
            }
            set
            {
                _ResourceType = value;
            }
        }

        /// <summary>
        /// Operation the request performs (from the centralized operation-scope mapping).
        /// </summary>
        public OperationTypeEnum Operation
        {
            get
            {
                return _Operation;
            }
            set
            {
                _Operation = value;
            }
        }

        /// <summary>
        /// The request-type key (e.g. "tenant/create") used for the operation-scope mapping and accounting.
        /// For MCP this is the tool name; for REST it is the synthesized equivalent.
        /// </summary>
        public string RequestType
        {
            get
            {
                return _RequestType;
            }
            set
            {
                _RequestType = value;
            }
        }

        /// <summary>
        /// Tenant identifier target, when applicable.
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
        /// User identifier target, when applicable.
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
        /// Collection identifier target, when applicable.
        /// </summary>
        public string CollectionId
        {
            get
            {
                return _CollectionId;
            }
            set
            {
                _CollectionId = value;
            }
        }

        /// <summary>
        /// Document key target, when applicable.
        /// </summary>
        public string DocumentKey
        {
            get
            {
                return _DocumentKey;
            }
            set
            {
                _DocumentKey = value;
            }
        }

        /// <summary>
        /// Document identifier (chunk grouping) target, when applicable.
        /// </summary>
        public string DocumentId
        {
            get
            {
                return _DocumentId;
            }
            set
            {
                _DocumentId = value;
            }
        }

        /// <summary>
        /// Chunk position target, when applicable.
        /// </summary>
        public int? Position
        {
            get
            {
                return _Position;
            }
            set
            {
                _Position = value;
            }
        }

        /// <summary>
        /// Generic resource identifier for resources keyed by a single id (credential, label, tag, request-history GUID).
        /// </summary>
        public string ResourceId
        {
            get
            {
                return _ResourceId;
            }
            set
            {
                _ResourceId = value;
            }
        }

        /// <summary>
        /// Typed request payload (e.g. TenantMetadata, DocumentRecord, list of documents). Service methods cast
        /// this to the type they expect. Null when the request carries no body.
        /// </summary>
        public object Payload
        {
            get
            {
                return _Payload;
            }
            set
            {
                _Payload = value;
            }
        }

        /// <summary>
        /// Enumeration query for paginated listing requests. Null for non-enumeration requests.
        /// </summary>
        public EnumerationQuery Query
        {
            get
            {
                return _Query;
            }
            set
            {
                _Query = value;
            }
        }

        /// <summary>
        /// Search query for search requests. Null for non-search requests.
        /// </summary>
        public SearchQuery Search
        {
            get
            {
                return _Search;
            }
            set
            {
                _Search = value;
            }
        }

        /// <summary>
        /// HTTP method (REST only).
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
        /// Original URL (REST only).
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
        /// Path (REST only).
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
        /// Raw request body bytes (REST only), when captured.
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
        private RequestOriginEnum _Origin = RequestOriginEnum.Rest;
        private AuthenticationResult _Auth = null;
        private ResourceTypeEnum _ResourceType = ResourceTypeEnum.Server;
        private OperationTypeEnum _Operation = OperationTypeEnum.Read;
        private string _RequestType = null;
        private string _TenantId = null;
        private string _UserId = null;
        private string _CollectionId = null;
        private string _DocumentKey = null;
        private string _DocumentId = null;
        private int? _Position = null;
        private string _ResourceId = null;
        private object _Payload = null;
        private EnumerationQuery _Query = null;
        private SearchQuery _Search = null;
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
