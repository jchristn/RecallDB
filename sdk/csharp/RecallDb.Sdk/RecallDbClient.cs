namespace RecallDb.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
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

        #endregion

        #region Private-Members

        private readonly HttpClient _HttpClient;
        private readonly string _Endpoint;
        private readonly JsonSerializerOptions _JsonOptions;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the RecallDB client.
        /// </summary>
        /// <param name="endpoint">Base URL of the RecallDB server (e.g. http://localhost:8600).</param>
        /// <param name="bearerToken">Bearer token for authentication.</param>
        public RecallDbClient(string endpoint, string bearerToken)
        {
            if (string.IsNullOrEmpty(endpoint)) throw new ArgumentNullException(nameof(endpoint));
            if (string.IsNullOrEmpty(bearerToken)) throw new ArgumentNullException(nameof(bearerToken));

            _Endpoint = endpoint.TrimEnd('/');
            _HttpClient = new HttpClient();
            _HttpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

            _JsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        #endregion

        #region Health

        /// <summary>
        /// Retrieve health and version information from the server.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary containing health response fields.</returns>
        public async Task<Dictionary<string, object>> HealthAsync(CancellationToken token = default)
        {
            return await GetAsync<Dictionary<string, object>>("/", token).ConfigureAwait(false);
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
            return await GetAsync<TenantMetadata>("/v1.0/tenants/" + id, token).ConfigureAwait(false);
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
            return await PutAsync<TenantMetadata>("/v1.0/tenants/" + id, tenant, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete a tenant.
        /// </summary>
        /// <param name="id">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task DeleteTenantAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            await DeleteAsync("/v1.0/tenants/" + id, token).ConfigureAwait(false);
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
            return await HeadAsync("/v1.0/tenants/" + id, token).ConfigureAwait(false);
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
            return await PutAsync<UserMaster>("/v1.0/tenants/" + tenantId + "/users", user, token).ConfigureAwait(false);
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
            return await GetAsync<UserMaster>("/v1.0/tenants/" + tenantId + "/users/" + id, token).ConfigureAwait(false);
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
            return await PutAsync<UserMaster>("/v1.0/tenants/" + tenantId + "/users/" + id, user, token).ConfigureAwait(false);
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
            await DeleteAsync("/v1.0/tenants/" + tenantId + "/users/" + id, token).ConfigureAwait(false);
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
            return await HeadAsync("/v1.0/tenants/" + tenantId + "/users/" + id, token).ConfigureAwait(false);
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
            return await PostAsync<EnumerationResult<UserMaster>>("/v1.0/tenants/" + tenantId + "/users/enumerate", query, token).ConfigureAwait(false);
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
            return await PutAsync<Credential>("/v1.0/tenants/" + tenantId + "/credentials", credential, token).ConfigureAwait(false);
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
            return await GetAsync<Credential>("/v1.0/tenants/" + tenantId + "/credentials/" + id, token).ConfigureAwait(false);
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
            return await PutAsync<Credential>("/v1.0/tenants/" + tenantId + "/credentials/" + id, credential, token).ConfigureAwait(false);
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
            await DeleteAsync("/v1.0/tenants/" + tenantId + "/credentials/" + id, token).ConfigureAwait(false);
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
            return await HeadAsync("/v1.0/tenants/" + tenantId + "/credentials/" + id, token).ConfigureAwait(false);
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
            return await PostAsync<EnumerationResult<Credential>>("/v1.0/tenants/" + tenantId + "/credentials/enumerate", query, token).ConfigureAwait(false);
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
            return await PutAsync<CollectionMetadata>("/v1.0/tenants/" + tenantId + "/collections", collection, token).ConfigureAwait(false);
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
            return await GetAsync<CollectionMetadata>("/v1.0/tenants/" + tenantId + "/collections/" + collectionId, token).ConfigureAwait(false);
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
            return await PutAsync<CollectionMetadata>("/v1.0/tenants/" + tenantId + "/collections/" + collectionId, collection, token).ConfigureAwait(false);
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
            await DeleteAsync("/v1.0/tenants/" + tenantId + "/collections/" + collectionId, token).ConfigureAwait(false);
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
            return await HeadAsync("/v1.0/tenants/" + tenantId + "/collections/" + collectionId, token).ConfigureAwait(false);
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
            return await PostAsync<EnumerationResult<CollectionMetadata>>("/v1.0/tenants/" + tenantId + "/collections/enumerate", query, token).ConfigureAwait(false);
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
                "/v1.0/tenants/" + tenantId + "/collections/" + collectionId + "/documents",
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
                "/v1.0/tenants/" + tenantId + "/collections/" + collectionId + "/documents/" + documentKey,
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
                "/v1.0/tenants/" + tenantId + "/collections/" + collectionId + "/documents/" + documentId + "/" + position,
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
                "/v1.0/tenants/" + tenantId + "/collections/" + collectionId + "/documents/" + documentKey,
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
                "/v1.0/tenants/" + tenantId + "/collections/" + collectionId + "/documents/" + documentKey,
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
                "/v1.0/tenants/" + tenantId + "/collections/" + collectionId + "/documents/" + documentKey,
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
                "/v1.0/tenants/" + tenantId + "/collections/" + collectionId + "/documents/enumerate",
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
                "/v1.0/tenants/" + tenantId + "/collections/" + collectionId + "/documents/batch",
                documents, token).ConfigureAwait(false);
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
                "/v1.0/tenants/" + tenantId + "/collections/" + collectionId + "/labels",
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
                "/v1.0/tenants/" + tenantId + "/collections/" + collectionId + "/labels/" + id,
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
                "/v1.0/tenants/" + tenantId + "/collections/" + collectionId + "/labels/" + id,
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
                "/v1.0/tenants/" + tenantId + "/collections/" + collectionId + "/labels",
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
                "/v1.0/tenants/" + tenantId + "/collections/" + collectionId + "/tags",
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
                "/v1.0/tenants/" + tenantId + "/collections/" + collectionId + "/tags/" + id,
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
                "/v1.0/tenants/" + tenantId + "/collections/" + collectionId + "/tags/" + id,
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
                "/v1.0/tenants/" + tenantId + "/collections/" + collectionId + "/tags",
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
                "/v1.0/tenants/" + tenantId + "/collections/" + collectionId + "/search",
                query, token).ConfigureAwait(false);
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        public void Dispose()
        {
            if (_HttpClient != null) _HttpClient.Dispose();
        }

        #endregion

        #region Private-Methods

        private async Task<T> GetAsync<T>(string path, CancellationToken token)
        {
            using HttpResponseMessage response = await _HttpClient.GetAsync(_Endpoint + path, token).ConfigureAwait(false);
            return await HandleResponseAsync<T>(response).ConfigureAwait(false);
        }

        private async Task<bool> HeadAsync(string path, CancellationToken token)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, _Endpoint + path);
            using HttpResponseMessage response = await _HttpClient.SendAsync(request, token).ConfigureAwait(false);
            return response.StatusCode == HttpStatusCode.OK;
        }

        private async Task<T> PostAsync<T>(string path, object body, CancellationToken token)
        {
            string json = JsonSerializer.Serialize(body, _JsonOptions);
            using StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await _HttpClient.PostAsync(_Endpoint + path, content, token).ConfigureAwait(false);
            return await HandleResponseAsync<T>(response).ConfigureAwait(false);
        }

        private async Task<T> PutAsync<T>(string path, object body, CancellationToken token)
        {
            string json = JsonSerializer.Serialize(body, _JsonOptions);
            using StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await _HttpClient.PutAsync(_Endpoint + path, content, token).ConfigureAwait(false);
            return await HandleResponseAsync<T>(response).ConfigureAwait(false);
        }

        private async Task DeleteAsync(string path, CancellationToken token)
        {
            using HttpResponseMessage response = await _HttpClient.DeleteAsync(_Endpoint + path, token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NoContent)
            {
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new RecallDbException(response.StatusCode, body);
            }
        }

        private async Task<T> HandleResponseAsync<T>(HttpResponseMessage response)
        {
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new RecallDbException(response.StatusCode, body);
            }

            if (string.IsNullOrEmpty(body)) return default;
            return JsonSerializer.Deserialize<T>(body, _JsonOptions);
        }

        #endregion
    }
}
