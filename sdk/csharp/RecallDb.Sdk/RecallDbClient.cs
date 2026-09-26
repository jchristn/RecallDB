namespace RecallDb.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using RecallDb.Sdk.Models;

    /// <summary>
    /// RecallDB SDK client for interacting with the RecallDB REST API.
    /// </summary>
    public class RecallDbClient : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Time allowed for each request, from sending it to reading the whole response body. A request that takes
        /// longer throws <see cref="TimeoutException"/>. Use <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>
        /// to disable the limit.
        /// Default: 100 seconds (the HttpClient default). Must be positive or infinite.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is zero or negative and not infinite.</exception>
        public TimeSpan Timeout
        {
            get
            {
                return _Timeout;
            }
            set
            {
                if (value != System.Threading.Timeout.InfiniteTimeSpan && value <= TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException(nameof(Timeout), "Timeout must be positive or Timeout.InfiniteTimeSpan.");
                _Timeout = value;
            }
        }

        #endregion

        #region Private-Members

        private readonly HttpClient _HttpClient;
        private readonly bool _OwnsHttpClient;
        private readonly string _Endpoint;
        private readonly AuthenticationHeaderValue _Authorization;
        private readonly JsonSerializerOptions _JsonOptions;
        private TimeSpan _Timeout = TimeSpan.FromSeconds(100);
        private List<string>? _Capabilities = null;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the RecallDB client. The client creates and owns its own HttpClient, which it disposes in
        /// <see cref="Dispose"/>. To share a connection pool across clients, or to add retry, logging, or telemetry
        /// handlers, use the constructor that takes an HttpClient or an HttpMessageHandler.
        /// </summary>
        /// <param name="endpoint">Base URL of the RecallDB server (e.g. http://127.0.0.1:8600).</param>
        /// <param name="bearerToken">Bearer token for authentication.</param>
        public RecallDbClient(string endpoint, string bearerToken)
            : this(endpoint, bearerToken, new HttpClient(), true)
        {
        }

        /// <summary>
        /// Instantiate the RecallDB client over a caller-supplied HttpClient. The caller owns the HttpClient:
        /// <see cref="Dispose"/> does not dispose it, and the client does not change its default headers or timeout
        /// (the bearer token is sent on each request, and <see cref="Timeout"/> is applied per request).
        /// </summary>
        /// <param name="endpoint">Base URL of the RecallDB server (e.g. http://127.0.0.1:8600).</param>
        /// <param name="bearerToken">Bearer token for authentication.</param>
        /// <param name="httpClient">HttpClient to send requests with.</param>
        public RecallDbClient(string endpoint, string bearerToken, HttpClient httpClient)
            : this(endpoint, bearerToken, httpClient ?? throw new ArgumentNullException(nameof(httpClient)), false)
        {
        }

        /// <summary>
        /// Instantiate the RecallDB client over a caller-supplied HttpMessageHandler, for example a
        /// DelegatingHandler that retries 429, 502, and 503 responses, or a shared SocketsHttpHandler. The client
        /// owns the HttpClient it builds around the handler; the caller owns the handler, which
        /// <see cref="Dispose"/> does not dispose.
        /// </summary>
        /// <param name="endpoint">Base URL of the RecallDB server (e.g. http://127.0.0.1:8600).</param>
        /// <param name="bearerToken">Bearer token for authentication.</param>
        /// <param name="handler">Message handler to send requests through.</param>
        public RecallDbClient(string endpoint, string bearerToken, HttpMessageHandler handler)
            : this(endpoint, bearerToken, new HttpClient(handler ?? throw new ArgumentNullException(nameof(handler)), false), true)
        {
        }

        private RecallDbClient(string endpoint, string bearerToken, HttpClient httpClient, bool ownsHttpClient)
        {
            if (string.IsNullOrEmpty(endpoint)) throw new ArgumentNullException(nameof(endpoint));
            if (string.IsNullOrEmpty(bearerToken)) throw new ArgumentNullException(nameof(bearerToken));

            _Endpoint = endpoint.TrimEnd('/');
            _HttpClient = httpClient;
            _OwnsHttpClient = ownsHttpClient;
            _Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

            // The per-request Timeout replaces HttpClient's own limit on clients this instance owns.
            if (_OwnsHttpClient) _HttpClient.Timeout = System.Threading.Timeout.InfiniteTimeSpan;

            _JsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        #endregion

        #region Health

        /// <summary>
        /// Retrieve health and version information from the server as untyped fields.
        /// <see cref="GetServerInfoAsync"/> returns the same response typed.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary containing health response fields.</returns>
        public async Task<Dictionary<string, object>> HealthAsync(CancellationToken token = default)
        {
            return await GetAsync<Dictionary<string, object>>("/", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieve the server's name, version, uptime, and search capabilities. Capabilities is empty for servers
        /// that predate the field.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Server information.</returns>
        public async Task<ServerInfo> GetServerInfoAsync(CancellationToken token = default)
        {
            ServerInfo info = await GetAsync<ServerInfo>("/", token).ConfigureAwait(false);
            if (info.Capabilities == null) info.Capabilities = new List<string>();
            return info;
        }

        /// <summary>
        /// Check whether the server supports a search capability (see <see cref="Constants.Capabilities"/>).
        /// New request fields are ignored silently by servers that do not support them, so check before relying on
        /// one. The capability list is fetched once and cached for this client's lifetime; pass refresh to re-read it,
        /// for example after the server is upgraded.
        /// </summary>
        /// <param name="capability">Capability string, for example Capabilities.Collapse.</param>
        /// <param name="refresh">True to re-read the capability list from the server.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the server reports the capability.</returns>
        public async Task<bool> SupportsAsync(string capability, bool refresh = false, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(capability)) throw new ArgumentNullException(nameof(capability));

            List<string>? capabilities = _Capabilities;
            if (capabilities == null || refresh)
            {
                ServerInfo info = await GetServerInfoAsync(token).ConfigureAwait(false);
                capabilities = info.Capabilities;
                _Capabilities = capabilities;
            }

            return capabilities.Contains(capability);
        }

        #endregion

        #region Authenticate

        /// <summary>
        /// Authenticate using bearer token or email+password credentials.
        /// </summary>
        /// <param name="request">Authentication request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Authentication response.</returns>
        public async Task<AuthenticateResponse> AuthenticateAsync(AuthenticateRequest request, CancellationToken token = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return await PostAsync<AuthenticateResponse>("/v1.0/authenticate", request, token).ConfigureAwait(false);
        }

        #endregion

        #region Tenants

        /// <summary>
        /// Create a tenant.
        /// </summary>
        /// <param name="tenant">Tenant metadata.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created tenant metadata.</returns>
        public async Task<TenantMetadata> CreateTenantAsync(TenantMetadata tenant, CancellationToken token = default)
        {
            if (tenant == null) throw new ArgumentNullException(nameof(tenant));
            return await PutAsync<TenantMetadata>("/v1.0/tenants", tenant, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieve a tenant by ID.
        /// </summary>
        /// <param name="id">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tenant metadata.</returns>
        public async Task<TenantMetadata> GetTenantAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            return await GetAsync<TenantMetadata>("/v1.0/tenants/" + Seg(id), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Update a tenant.
        /// </summary>
        /// <param name="id">Tenant ID.</param>
        /// <param name="tenant">Updated tenant metadata.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated tenant metadata.</returns>
        public async Task<TenantMetadata> UpdateTenantAsync(string id, TenantMetadata tenant, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            if (tenant == null) throw new ArgumentNullException(nameof(tenant));
            return await PutAsync<TenantMetadata>("/v1.0/tenants/" + Seg(id), tenant, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete a tenant.
        /// </summary>
        /// <param name="id">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task DeleteTenantAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            await DeleteAsync("/v1.0/tenants/" + Seg(id), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Check if a tenant exists.
        /// </summary>
        /// <param name="id">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the tenant exists.</returns>
        public async Task<bool> TenantExistsAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            return await HeadAsync("/v1.0/tenants/" + Seg(id), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Enumerate tenants with a query.
        /// </summary>
        /// <param name="query">Enumeration query.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result containing tenant metadata.</returns>
        public async Task<EnumerationResult<TenantMetadata>> EnumerateTenantsAsync(EnumerationQuery query, CancellationToken token = default)
        {
            if (query == null) query = new EnumerationQuery();
            return await PostAsync<EnumerationResult<TenantMetadata>>("/v1.0/tenants/enumerate", query, token).ConfigureAwait(false);
        }

        #endregion

        #region Users

        /// <summary>
        /// Create a user.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="user">User master record.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created user master record.</returns>
        public async Task<UserMaster> CreateUserAsync(string tenantId, UserMaster user, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (user == null) throw new ArgumentNullException(nameof(user));
            return await PutAsync<UserMaster>("/v1.0/tenants/" + Seg(tenantId) + "/users", user, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieve a user by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">User ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>User master record.</returns>
        public async Task<UserMaster> GetUserAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            return await GetAsync<UserMaster>("/v1.0/tenants/" + Seg(tenantId) + "/users/" + Seg(id), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Update a user.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">User ID.</param>
        /// <param name="user">Updated user master record.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated user master record.</returns>
        public async Task<UserMaster> UpdateUserAsync(string tenantId, string id, UserMaster user, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            if (user == null) throw new ArgumentNullException(nameof(user));
            return await PutAsync<UserMaster>("/v1.0/tenants/" + Seg(tenantId) + "/users/" + Seg(id), user, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete a user.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">User ID.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task DeleteUserAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            await DeleteAsync("/v1.0/tenants/" + Seg(tenantId) + "/users/" + Seg(id), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Check if a user exists.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">User ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the user exists.</returns>
        public async Task<bool> UserExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            return await HeadAsync("/v1.0/tenants/" + Seg(tenantId) + "/users/" + Seg(id), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Enumerate users with a query.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="query">Enumeration query.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result containing user master records.</returns>
        public async Task<EnumerationResult<UserMaster>> EnumerateUsersAsync(string tenantId, EnumerationQuery query, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (query == null) query = new EnumerationQuery();
            return await PostAsync<EnumerationResult<UserMaster>>("/v1.0/tenants/" + Seg(tenantId) + "/users/enumerate", query, token).ConfigureAwait(false);
        }

        #endregion

        #region Credentials

        /// <summary>
        /// Create a credential.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="credential">Credential.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created credential.</returns>
        public async Task<Credential> CreateCredentialAsync(string tenantId, Credential credential, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (credential == null) throw new ArgumentNullException(nameof(credential));
            return await PutAsync<Credential>("/v1.0/tenants/" + Seg(tenantId) + "/credentials", credential, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieve a credential by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Credential ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Credential.</returns>
        public async Task<Credential> GetCredentialAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            return await GetAsync<Credential>("/v1.0/tenants/" + Seg(tenantId) + "/credentials/" + Seg(id), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Update a credential.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Credential ID.</param>
        /// <param name="credential">Updated credential.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated credential.</returns>
        public async Task<Credential> UpdateCredentialAsync(string tenantId, string id, Credential credential, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            if (credential == null) throw new ArgumentNullException(nameof(credential));
            return await PutAsync<Credential>("/v1.0/tenants/" + Seg(tenantId) + "/credentials/" + Seg(id), credential, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete a credential.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Credential ID.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task DeleteCredentialAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            await DeleteAsync("/v1.0/tenants/" + Seg(tenantId) + "/credentials/" + Seg(id), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Check if a credential exists.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Credential ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the credential exists.</returns>
        public async Task<bool> CredentialExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            return await HeadAsync("/v1.0/tenants/" + Seg(tenantId) + "/credentials/" + Seg(id), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Enumerate credentials with a query.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="query">Enumeration query.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result containing credentials.</returns>
        public async Task<EnumerationResult<Credential>> EnumerateCredentialsAsync(string tenantId, EnumerationQuery query, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (query == null) query = new EnumerationQuery();
            return await PostAsync<EnumerationResult<Credential>>("/v1.0/tenants/" + Seg(tenantId) + "/credentials/enumerate", query, token).ConfigureAwait(false);
        }

        #endregion

        #region Collections

        /// <summary>
        /// Create a collection.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="collection">Collection metadata.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created collection metadata.</returns>
        public async Task<CollectionMetadata> CreateCollectionAsync(string tenantId, CollectionMetadata collection, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            return await PutAsync<CollectionMetadata>("/v1.0/tenants/" + Seg(tenantId) + "/collections", collection, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieve a collection by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Collection metadata.</returns>
        public async Task<CollectionMetadata> GetCollectionAsync(string tenantId, string collectionId, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            return await GetAsync<CollectionMetadata>("/v1.0/tenants/" + Seg(tenantId) + "/collections/" + Seg(collectionId), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Update a collection.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="collection">Updated collection metadata.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated collection metadata.</returns>
        public async Task<CollectionMetadata> UpdateCollectionAsync(string tenantId, string collectionId, CollectionMetadata collection, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            return await PutAsync<CollectionMetadata>("/v1.0/tenants/" + Seg(tenantId) + "/collections/" + Seg(collectionId), collection, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete a collection.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task DeleteCollectionAsync(string tenantId, string collectionId, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            await DeleteAsync("/v1.0/tenants/" + Seg(tenantId) + "/collections/" + Seg(collectionId), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Check if a collection exists.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the collection exists.</returns>
        public async Task<bool> CollectionExistsAsync(string tenantId, string collectionId, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            return await HeadAsync("/v1.0/tenants/" + Seg(tenantId) + "/collections/" + Seg(collectionId), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Enumerate collections with a query.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="query">Enumeration query.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result containing collection metadata.</returns>
        public async Task<EnumerationResult<CollectionMetadata>> EnumerateCollectionsAsync(string tenantId, EnumerationQuery query, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (query == null) query = new EnumerationQuery();
            return await PostAsync<EnumerationResult<CollectionMetadata>>("/v1.0/tenants/" + Seg(tenantId) + "/collections/enumerate", query, token).ConfigureAwait(false);
        }

        #endregion

        #region Documents

        /// <summary>
        /// Create a document.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="document">Document record.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created document record.</returns>
        public async Task<DocumentRecord> CreateDocumentAsync(string tenantId, string collectionId, DocumentRecord document, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (document == null) throw new ArgumentNullException(nameof(document));
            return await PutAsync<DocumentRecord>(
                "/v1.0/tenants/" + Seg(tenantId) + "/collections/" + Seg(collectionId) + "/documents",
                document, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieve a document by document key.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentKey">Document key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Document record.</returns>
        public async Task<DocumentRecord> GetDocumentAsync(string tenantId, string collectionId, string documentKey, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (string.IsNullOrEmpty(documentKey)) throw new ArgumentNullException(nameof(documentKey));
            return await GetAsync<DocumentRecord>(
                "/v1.0/tenants/" + Seg(tenantId) + "/collections/" + Seg(collectionId) + "/documents/" + Seg(documentKey),
                token).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieve a document chunk by document ID and position.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentId">Document ID.</param>
        /// <param name="position">Chunk position.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Document record.</returns>
        public async Task<DocumentRecord> GetDocumentByPositionAsync(string tenantId, string collectionId, string documentId, int position, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (string.IsNullOrEmpty(documentId)) throw new ArgumentNullException(nameof(documentId));
            return await GetAsync<DocumentRecord>(
                "/v1.0/tenants/" + Seg(tenantId) + "/collections/" + Seg(collectionId) + "/documents/" + Seg(documentId) + "/" + position,
                token).ConfigureAwait(false);
        }

        /// <summary>
        /// Update a document.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentKey">Document key.</param>
        /// <param name="document">Updated document record.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated document record.</returns>
        public async Task<DocumentRecord> UpdateDocumentAsync(string tenantId, string collectionId, string documentKey, DocumentRecord document, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (string.IsNullOrEmpty(documentKey)) throw new ArgumentNullException(nameof(documentKey));
            if (document == null) throw new ArgumentNullException(nameof(document));
            return await PutAsync<DocumentRecord>(
                "/v1.0/tenants/" + Seg(tenantId) + "/collections/" + Seg(collectionId) + "/documents/" + Seg(documentKey),
                document, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete a document.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentKey">Document key.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task DeleteDocumentAsync(string tenantId, string collectionId, string documentKey, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (string.IsNullOrEmpty(documentKey)) throw new ArgumentNullException(nameof(documentKey));
            await DeleteAsync(
                "/v1.0/tenants/" + Seg(tenantId) + "/collections/" + Seg(collectionId) + "/documents/" + Seg(documentKey),
                token).ConfigureAwait(false);
        }

        /// <summary>
        /// Check if a document exists.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentKey">Document key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the document exists.</returns>
        public async Task<bool> DocumentExistsAsync(string tenantId, string collectionId, string documentKey, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (string.IsNullOrEmpty(documentKey)) throw new ArgumentNullException(nameof(documentKey));
            return await HeadAsync(
                "/v1.0/tenants/" + Seg(tenantId) + "/collections/" + Seg(collectionId) + "/documents/" + Seg(documentKey),
                token).ConfigureAwait(false);
        }

        /// <summary>
        /// Enumerate documents with a query.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="query">Enumeration query.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result containing document records.</returns>
        public async Task<EnumerationResult<DocumentRecord>> EnumerateDocumentsAsync(string tenantId, string collectionId, EnumerationQuery query, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (query == null) query = new EnumerationQuery();
            return await PostAsync<EnumerationResult<DocumentRecord>>(
                "/v1.0/tenants/" + Seg(tenantId) + "/collections/" + Seg(collectionId) + "/documents/enumerate",
                query, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Batch create documents.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documents">List of document records.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of created document records.</returns>
        public async Task<List<DocumentRecord>> CreateDocumentBatchAsync(string tenantId, string collectionId, List<DocumentRecord> documents, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (documents == null) throw new ArgumentNullException(nameof(documents));
            return await PostAsync<List<DocumentRecord>>(
                "/v1.0/tenants/" + Seg(tenantId) + "/collections/" + Seg(collectionId) + "/documents/batch",
                documents, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Batch delete documents by their document keys.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="documentKeys">List of document keys to delete.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task DeleteDocumentBatchAsync(string tenantId, string collectionId, List<string> documentKeys, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (documentKeys == null) throw new ArgumentNullException(nameof(documentKeys));
            BatchDeleteRequest req = new BatchDeleteRequest();
            req.DocumentKeys = documentKeys;
            await PostAsync(
                "/v1.0/tenants/" + Seg(tenantId) + "/collections/" + Seg(collectionId) + "/documents/batch/delete",
                req, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete documents matching filter criteria.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="filter">Enumeration query with filter criteria.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Delete result with count of deleted documents.</returns>
        public async Task<DeleteResult> DeleteDocumentsByFilterAsync(string tenantId, string collectionId, EnumerationQuery filter, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (filter == null) filter = new EnumerationQuery();
            return await PostAsync<DeleteResult>(
                "/v1.0/tenants/" + Seg(tenantId) + "/collections/" + Seg(collectionId) + "/documents/delete/filter",
                filter, token).ConfigureAwait(false);
        }

        #endregion

        #region Labels

        /// <summary>
        /// Create a label.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="label">Label record.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created label record.</returns>
        public async Task<LabelRecord> CreateLabelAsync(string tenantId, string collectionId, LabelRecord label, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (label == null) throw new ArgumentNullException(nameof(label));
            return await PutAsync<LabelRecord>(
                "/v1.0/tenants/" + Seg(tenantId) + "/collections/" + Seg(collectionId) + "/labels",
                label, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieve a label by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="id">Label ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Label record.</returns>
        public async Task<LabelRecord> GetLabelAsync(string tenantId, string collectionId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            return await GetAsync<LabelRecord>(
                "/v1.0/tenants/" + Seg(tenantId) + "/collections/" + Seg(collectionId) + "/labels/" + Seg(id),
                token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete a label.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="id">Label ID.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task DeleteLabelAsync(string tenantId, string collectionId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            await DeleteAsync(
                "/v1.0/tenants/" + Seg(tenantId) + "/collections/" + Seg(collectionId) + "/labels/" + Seg(id),
                token).ConfigureAwait(false);
        }

        /// <summary>
        /// List all labels in a collection.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result containing label records.</returns>
        public async Task<EnumerationResult<LabelRecord>> ListLabelsAsync(string tenantId, string collectionId, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            return await GetAsync<EnumerationResult<LabelRecord>>(
                "/v1.0/tenants/" + Seg(tenantId) + "/collections/" + Seg(collectionId) + "/labels",
                token).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieve distinct label values in a collection.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of distinct label strings.</returns>
        public async Task<List<string>> DistinctLabelsAsync(string tenantId, string collectionId, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            return await GetAsync<List<string>>(
                "/v1.0/tenants/" + Seg(tenantId) + "/collections/" + Seg(collectionId) + "/labels/distinct",
                token).ConfigureAwait(false);
        }

        #endregion

        #region Tags

        /// <summary>
        /// Create a tag.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="tag">Tag record.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created tag record.</returns>
        public async Task<TagRecord> CreateTagAsync(string tenantId, string collectionId, TagRecord tag, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (tag == null) throw new ArgumentNullException(nameof(tag));
            return await PutAsync<TagRecord>(
                "/v1.0/tenants/" + Seg(tenantId) + "/collections/" + Seg(collectionId) + "/tags",
                tag, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieve a tag by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="id">Tag ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tag record.</returns>
        public async Task<TagRecord> GetTagAsync(string tenantId, string collectionId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            return await GetAsync<TagRecord>(
                "/v1.0/tenants/" + Seg(tenantId) + "/collections/" + Seg(collectionId) + "/tags/" + Seg(id),
                token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete a tag.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="id">Tag ID.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task DeleteTagAsync(string tenantId, string collectionId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            await DeleteAsync(
                "/v1.0/tenants/" + Seg(tenantId) + "/collections/" + Seg(collectionId) + "/tags/" + Seg(id),
                token).ConfigureAwait(false);
        }

        /// <summary>
        /// List all tags in a collection.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result containing tag records.</returns>
        public async Task<EnumerationResult<TagRecord>> ListTagsAsync(string tenantId, string collectionId, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            return await GetAsync<EnumerationResult<TagRecord>>(
                "/v1.0/tenants/" + Seg(tenantId) + "/collections/" + Seg(collectionId) + "/tags",
                token).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieve distinct tag keys in a collection.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of distinct tag key strings.</returns>
        public async Task<List<string>> DistinctTagKeysAsync(string tenantId, string collectionId, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            return await GetAsync<List<string>>(
                "/v1.0/tenants/" + Seg(tenantId) + "/collections/" + Seg(collectionId) + "/tags/distinct",
                token).ConfigureAwait(false);
        }

        #endregion

        #region Search

        /// <summary>
        /// Search documents in a collection.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="query">Search query.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Search result.</returns>
        public async Task<SearchResult> SearchAsync(string tenantId, string collectionId, SearchQuery query, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (query == null) throw new ArgumentNullException(nameof(query));
            return await PostAsync<SearchResult>(
                "/v1.0/tenants/" + Seg(tenantId) + "/collections/" + Seg(collectionId) + "/search",
                query, token).ConfigureAwait(false);
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            if (_OwnsHttpClient) _HttpClient.Dispose();
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// URL-encode a single path segment supplied by the caller (a tenant id, collection id, document id, or
        /// document key). Without this, a key containing '#', '?', '/', or '%' would change the request's path,
        /// query, or fragment instead of being sent literally.
        /// </summary>
        private static string Seg(string value)
        {
            return Uri.EscapeDataString(value);
        }

        private async Task<T> GetAsync<T>(string path, CancellationToken token)
        {
            return RequireBody(await SendAsync<T>(HttpMethod.Get, path, null, token).ConfigureAwait(false), path);
        }

        private async Task<bool> HeadAsync(string path, CancellationToken token)
        {
            // 200 means the resource exists and 404 that it does not. Anything else (401, 403, 429, 5xx) is a failure,
            // not an answer, so it throws instead of reading as "does not exist".
            using CancellationTokenSource? timeoutSource = CreateTimeoutSource(token, out CancellationToken linked);
            try
            {
                using HttpRequestMessage request = CreateRequest(HttpMethod.Head, path, null);
                using HttpResponseMessage response = await _HttpClient.SendAsync(request, linked).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.OK) return true;
                if (response.StatusCode == HttpStatusCode.NotFound) return false;
                string body = await response.Content.ReadAsStringAsync(linked).ConfigureAwait(false);
                throw new RecallDbException(response.StatusCode, body);
            }
            catch (OperationCanceledException e) when (IsTimeout(timeoutSource, token))
            {
                throw CreateTimeoutException(e);
            }
        }

        private async Task<T> PostAsync<T>(string path, object body, CancellationToken token)
        {
            return RequireBody(await SendAsync<T>(HttpMethod.Post, path, body, token).ConfigureAwait(false), path);
        }

        private async Task PostAsync(string path, object body, CancellationToken token)
        {
            await SendAsync<object>(HttpMethod.Post, path, body, token, false).ConfigureAwait(false);
        }

        private async Task<T> PutAsync<T>(string path, object body, CancellationToken token)
        {
            return RequireBody(await SendAsync<T>(HttpMethod.Put, path, body, token).ConfigureAwait(false), path);
        }

        private static T RequireBody<T>(T? value, string path)
        {
            // Every typed call returns a JSON body on success; an empty one is a server or proxy fault, not a null result.
            if (value == null) throw new InvalidOperationException("RecallDB returned a success status with an empty response body for " + path + ".");
            return value;
        }

        private async Task DeleteAsync(string path, CancellationToken token)
        {
            await SendAsync<object>(HttpMethod.Delete, path, null, token, false).ConfigureAwait(false);
        }

        private async Task<T?> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken token, bool deserialize = true)
        {
            using CancellationTokenSource? timeoutSource = CreateTimeoutSource(token, out CancellationToken linked);
            try
            {
                using HttpRequestMessage request = CreateRequest(method, path, body);
                using HttpResponseMessage response = await _HttpClient.SendAsync(request, linked).ConfigureAwait(false);
                string responseBody = await response.Content.ReadAsStringAsync(linked).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                    throw new RecallDbException(response.StatusCode, responseBody);

                if (!deserialize || string.IsNullOrEmpty(responseBody)) return default;
                return JsonSerializer.Deserialize<T>(responseBody, _JsonOptions);
            }
            catch (OperationCanceledException e) when (IsTimeout(timeoutSource, token))
            {
                throw CreateTimeoutException(e);
            }
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string path, object? body)
        {
            // The bearer token goes on each request rather than on DefaultRequestHeaders, so a caller-supplied
            // HttpClient is never modified.
            HttpRequestMessage request = new HttpRequestMessage(method, _Endpoint + path);
            request.Headers.Authorization = _Authorization;
            if (body != null)
            {
                string json = JsonSerializer.Serialize(body, _JsonOptions);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }
            return request;
        }

        private CancellationTokenSource? CreateTimeoutSource(CancellationToken token, out CancellationToken linked)
        {
            if (_Timeout == System.Threading.Timeout.InfiniteTimeSpan)
            {
                linked = token;
                return null;
            }

            CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(token);
            source.CancelAfter(_Timeout);
            linked = source.Token;
            return source;
        }

        private static bool IsTimeout(CancellationTokenSource? timeoutSource, CancellationToken callerToken)
        {
            // Cancelled by the timeout, not by the caller: the caller's own cancellation propagates unchanged.
            return timeoutSource != null && timeoutSource.IsCancellationRequested && !callerToken.IsCancellationRequested;
        }

        private TimeoutException CreateTimeoutException(Exception inner)
        {
            return new TimeoutException("The RecallDB request did not complete within the client Timeout of " + _Timeout.TotalSeconds + " seconds.", inner);
        }

        #endregion
    }
}
