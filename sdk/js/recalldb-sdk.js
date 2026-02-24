/**
 * RecallDB JavaScript SDK.
 *
 * Provides a client for interacting with the RecallDB REST API.
 */

class RecallDbException extends Error {
    /**
     * Create a RecallDbException.
     * @param {number} statusCode - HTTP status code.
     * @param {string} responseBody - Raw response body.
     */
    constructor(statusCode, responseBody) {
        super(`RecallDB API returned ${statusCode}: ${responseBody}`);
        this.name = "RecallDbException";
        this.statusCode = statusCode;
        this.responseBody = responseBody;
    }
}

class RecallDbClient {
    /**
     * Initialize the RecallDB client.
     * @param {string} endpoint - Base URL of the RecallDB server (e.g. http://localhost:8600).
     * @param {string} bearerToken - Bearer token for authentication.
     */
    constructor(endpoint, bearerToken) {
        if (!endpoint) throw new Error("endpoint is required");
        if (!bearerToken) throw new Error("bearerToken is required");

        this._endpoint = endpoint.replace(/\/+$/, "");
        this._bearerToken = bearerToken;
    }

    // -------------------------------------------------------------------------
    // Health
    // -------------------------------------------------------------------------

    /**
     * Retrieve health and version information from the server.
     * @returns {Promise<Object>} Health response fields.
     */
    async health() {
        return this._get("/");
    }

    // -------------------------------------------------------------------------
    // Authenticate
    // -------------------------------------------------------------------------

    /**
     * Authenticate using bearer token or email+password credentials.
     * @param {Object} request - Authentication request with BearerToken or TenantId+Email+Password.
     * @returns {Promise<Object>} Authentication response.
     */
    async authenticate(request) {
        return this._post("/v1.0/authenticate", request);
    }

    // -------------------------------------------------------------------------
    // Tenants
    // -------------------------------------------------------------------------

    /**
     * Create a tenant.
     * @param {Object} tenant - Tenant metadata.
     * @returns {Promise<Object>} Created tenant metadata.
     */
    async createTenant(tenant) {
        return this._put("/v1.0/tenants", tenant);
    }

    /**
     * Retrieve a tenant by ID.
     * @param {string} tenantId - Tenant ID.
     * @returns {Promise<Object>} Tenant metadata.
     */
    async getTenant(tenantId) {
        return this._get(`/v1.0/tenants/${tenantId}`);
    }

    /**
     * Update a tenant.
     * @param {string} tenantId - Tenant ID.
     * @param {Object} tenant - Updated tenant metadata.
     * @returns {Promise<Object>} Updated tenant metadata.
     */
    async updateTenant(tenantId, tenant) {
        return this._put(`/v1.0/tenants/${tenantId}`, tenant);
    }

    /**
     * Delete a tenant.
     * @param {string} tenantId - Tenant ID.
     */
    async deleteTenant(tenantId) {
        await this._delete(`/v1.0/tenants/${tenantId}`);
    }

    /**
     * Check if a tenant exists.
     * @param {string} tenantId - Tenant ID.
     * @returns {Promise<boolean>} True if the tenant exists.
     */
    async tenantExists(tenantId) {
        return this._head(`/v1.0/tenants/${tenantId}`);
    }

    /**
     * Enumerate tenants with a query.
     * @param {Object} [query={}] - Enumeration query parameters.
     * @returns {Promise<Object>} Enumeration result containing tenant metadata.
     */
    async enumerateTenants(query) {
        return this._post("/v1.0/tenants/enumerate", query || {});
    }

    // -------------------------------------------------------------------------
    // Users
    // -------------------------------------------------------------------------

    /**
     * Create a user.
     * @param {string} tenantId - Tenant ID.
     * @param {Object} user - User master record.
     * @returns {Promise<Object>} Created user master record.
     */
    async createUser(tenantId, user) {
        return this._put(`/v1.0/tenants/${tenantId}/users`, user);
    }

    /**
     * Retrieve a user by ID.
     * @param {string} tenantId - Tenant ID.
     * @param {string} userId - User ID.
     * @returns {Promise<Object>} User master record.
     */
    async getUser(tenantId, userId) {
        return this._get(`/v1.0/tenants/${tenantId}/users/${userId}`);
    }

    /**
     * Update a user.
     * @param {string} tenantId - Tenant ID.
     * @param {string} userId - User ID.
     * @param {Object} user - Updated user master record.
     * @returns {Promise<Object>} Updated user master record.
     */
    async updateUser(tenantId, userId, user) {
        return this._put(`/v1.0/tenants/${tenantId}/users/${userId}`, user);
    }

    /**
     * Delete a user.
     * @param {string} tenantId - Tenant ID.
     * @param {string} userId - User ID.
     */
    async deleteUser(tenantId, userId) {
        await this._delete(`/v1.0/tenants/${tenantId}/users/${userId}`);
    }

    /**
     * Check if a user exists.
     * @param {string} tenantId - Tenant ID.
     * @param {string} userId - User ID.
     * @returns {Promise<boolean>} True if the user exists.
     */
    async userExists(tenantId, userId) {
        return this._head(`/v1.0/tenants/${tenantId}/users/${userId}`);
    }

    /**
     * Enumerate users with a query.
     * @param {string} tenantId - Tenant ID.
     * @param {Object} [query={}] - Enumeration query parameters.
     * @returns {Promise<Object>} Enumeration result containing user master records.
     */
    async enumerateUsers(tenantId, query) {
        return this._post(`/v1.0/tenants/${tenantId}/users/enumerate`, query || {});
    }

    // -------------------------------------------------------------------------
    // Credentials
    // -------------------------------------------------------------------------

    /**
     * Create a credential.
     * @param {string} tenantId - Tenant ID.
     * @param {Object} credential - Credential data.
     * @returns {Promise<Object>} Created credential.
     */
    async createCredential(tenantId, credential) {
        return this._put(`/v1.0/tenants/${tenantId}/credentials`, credential);
    }

    /**
     * Retrieve a credential by ID.
     * @param {string} tenantId - Tenant ID.
     * @param {string} credentialId - Credential ID.
     * @returns {Promise<Object>} Credential.
     */
    async getCredential(tenantId, credentialId) {
        return this._get(`/v1.0/tenants/${tenantId}/credentials/${credentialId}`);
    }

    /**
     * Update a credential.
     * @param {string} tenantId - Tenant ID.
     * @param {string} credentialId - Credential ID.
     * @param {Object} credential - Updated credential data.
     * @returns {Promise<Object>} Updated credential.
     */
    async updateCredential(tenantId, credentialId, credential) {
        return this._put(`/v1.0/tenants/${tenantId}/credentials/${credentialId}`, credential);
    }

    /**
     * Delete a credential.
     * @param {string} tenantId - Tenant ID.
     * @param {string} credentialId - Credential ID.
     */
    async deleteCredential(tenantId, credentialId) {
        await this._delete(`/v1.0/tenants/${tenantId}/credentials/${credentialId}`);
    }

    /**
     * Check if a credential exists.
     * @param {string} tenantId - Tenant ID.
     * @param {string} credentialId - Credential ID.
     * @returns {Promise<boolean>} True if the credential exists.
     */
    async credentialExists(tenantId, credentialId) {
        return this._head(`/v1.0/tenants/${tenantId}/credentials/${credentialId}`);
    }

    /**
     * Enumerate credentials with a query.
     * @param {string} tenantId - Tenant ID.
     * @param {Object} [query={}] - Enumeration query parameters.
     * @returns {Promise<Object>} Enumeration result containing credentials.
     */
    async enumerateCredentials(tenantId, query) {
        return this._post(`/v1.0/tenants/${tenantId}/credentials/enumerate`, query || {});
    }

    // -------------------------------------------------------------------------
    // Collections
    // -------------------------------------------------------------------------

    /**
     * Create a collection.
     * @param {string} tenantId - Tenant ID.
     * @param {Object} collection - Collection metadata.
     * @returns {Promise<Object>} Created collection metadata.
     */
    async createCollection(tenantId, collection) {
        return this._put(`/v1.0/tenants/${tenantId}/collections`, collection);
    }

    /**
     * Retrieve a collection by ID.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @returns {Promise<Object>} Collection metadata.
     */
    async getCollection(tenantId, collectionId) {
        return this._get(`/v1.0/tenants/${tenantId}/collections/${collectionId}`);
    }

    /**
     * Update a collection.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {Object} collection - Updated collection metadata.
     * @returns {Promise<Object>} Updated collection metadata.
     */
    async updateCollection(tenantId, collectionId, collection) {
        return this._put(`/v1.0/tenants/${tenantId}/collections/${collectionId}`, collection);
    }

    /**
     * Delete a collection.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     */
    async deleteCollection(tenantId, collectionId) {
        await this._delete(`/v1.0/tenants/${tenantId}/collections/${collectionId}`);
    }

    /**
     * Check if a collection exists.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @returns {Promise<boolean>} True if the collection exists.
     */
    async collectionExists(tenantId, collectionId) {
        return this._head(`/v1.0/tenants/${tenantId}/collections/${collectionId}`);
    }

    /**
     * Enumerate collections with a query.
     * @param {string} tenantId - Tenant ID.
     * @param {Object} [query={}] - Enumeration query parameters.
     * @returns {Promise<Object>} Enumeration result containing collection metadata.
     */
    async enumerateCollections(tenantId, query) {
        return this._post(`/v1.0/tenants/${tenantId}/collections/enumerate`, query || {});
    }

    // -------------------------------------------------------------------------
    // Documents
    // -------------------------------------------------------------------------

    /**
     * Create a document.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {Object} document - Document record.
     * @returns {Promise<Object>} Created document record.
     */
    async createDocument(tenantId, collectionId, document) {
        return this._put(
            `/v1.0/tenants/${tenantId}/collections/${collectionId}/documents`,
            document);
    }

    /**
     * Retrieve a document by document key.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {string} documentKey - Document key.
     * @returns {Promise<Object>} Document record.
     */
    async getDocument(tenantId, collectionId, documentKey) {
        return this._get(
            `/v1.0/tenants/${tenantId}/collections/${collectionId}/documents/${documentKey}`);
    }

    /**
     * Retrieve a document chunk by document ID and position.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {string} documentId - Document ID.
     * @param {number} position - Chunk position.
     * @returns {Promise<Object>} Document record.
     */
    async getDocumentByPosition(tenantId, collectionId, documentId, position) {
        return this._get(
            `/v1.0/tenants/${tenantId}/collections/${collectionId}/documents/${documentId}/${position}`);
    }

    /**
     * Update a document.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {string} documentKey - Document key.
     * @param {Object} document - Updated document record.
     * @returns {Promise<Object>} Updated document record.
     */
    async updateDocument(tenantId, collectionId, documentKey, document) {
        return this._put(
            `/v1.0/tenants/${tenantId}/collections/${collectionId}/documents/${documentKey}`,
            document);
    }

    /**
     * Delete a document.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {string} documentKey - Document key.
     */
    async deleteDocument(tenantId, collectionId, documentKey) {
        await this._delete(
            `/v1.0/tenants/${tenantId}/collections/${collectionId}/documents/${documentKey}`);
    }

    /**
     * Check if a document exists.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {string} documentKey - Document key.
     * @returns {Promise<boolean>} True if the document exists.
     */
    async documentExists(tenantId, collectionId, documentKey) {
        return this._head(
            `/v1.0/tenants/${tenantId}/collections/${collectionId}/documents/${documentKey}`);
    }

    /**
     * Enumerate documents with a query.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {Object} [query={}] - Enumeration query parameters.
     * @returns {Promise<Object>} Enumeration result containing document records.
     */
    async enumerateDocuments(tenantId, collectionId, query) {
        return this._post(
            `/v1.0/tenants/${tenantId}/collections/${collectionId}/documents/enumerate`,
            query || {});
    }

    /**
     * Batch create documents.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {Array<Object>} documents - List of document records.
     * @returns {Promise<Array<Object>>} List of created document records.
     */
    async createDocumentBatch(tenantId, collectionId, documents) {
        return this._post(
            `/v1.0/tenants/${tenantId}/collections/${collectionId}/documents/batch`,
            documents);
    }

    // -------------------------------------------------------------------------
    // Labels
    // -------------------------------------------------------------------------

    /**
     * Create a label.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {Object} label - Label record.
     * @returns {Promise<Object>} Created label record.
     */
    async createLabel(tenantId, collectionId, label) {
        return this._put(
            `/v1.0/tenants/${tenantId}/collections/${collectionId}/labels`,
            label);
    }

    /**
     * Retrieve a label by ID.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {string} labelId - Label ID.
     * @returns {Promise<Object>} Label record.
     */
    async getLabel(tenantId, collectionId, labelId) {
        return this._get(
            `/v1.0/tenants/${tenantId}/collections/${collectionId}/labels/${labelId}`);
    }

    /**
     * Delete a label.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {string} labelId - Label ID.
     */
    async deleteLabel(tenantId, collectionId, labelId) {
        await this._delete(
            `/v1.0/tenants/${tenantId}/collections/${collectionId}/labels/${labelId}`);
    }

    /**
     * List all labels in a collection.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @returns {Promise<Object>} Enumeration result containing label records.
     */
    async listLabels(tenantId, collectionId) {
        return this._get(
            `/v1.0/tenants/${tenantId}/collections/${collectionId}/labels`);
    }

    // -------------------------------------------------------------------------
    // Tags
    // -------------------------------------------------------------------------

    /**
     * Create a tag.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {Object} tag - Tag record.
     * @returns {Promise<Object>} Created tag record.
     */
    async createTag(tenantId, collectionId, tag) {
        return this._put(
            `/v1.0/tenants/${tenantId}/collections/${collectionId}/tags`,
            tag);
    }

    /**
     * Retrieve a tag by ID.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {string} tagId - Tag ID.
     * @returns {Promise<Object>} Tag record.
     */
    async getTag(tenantId, collectionId, tagId) {
        return this._get(
            `/v1.0/tenants/${tenantId}/collections/${collectionId}/tags/${tagId}`);
    }

    /**
     * Delete a tag.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {string} tagId - Tag ID.
     */
    async deleteTag(tenantId, collectionId, tagId) {
        await this._delete(
            `/v1.0/tenants/${tenantId}/collections/${collectionId}/tags/${tagId}`);
    }

    /**
     * List all tags in a collection.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @returns {Promise<Object>} Enumeration result containing tag records.
     */
    async listTags(tenantId, collectionId) {
        return this._get(
            `/v1.0/tenants/${tenantId}/collections/${collectionId}/tags`);
    }

    // -------------------------------------------------------------------------
    // Search
    // -------------------------------------------------------------------------

    /**
     * Search documents in a collection.
     *
     * Supports three search modes:
     * - Vector-only: provide Vector without FullText.
     * - Full-text-only: provide FullText without Vector.
     * - Hybrid: provide both Vector and FullText for blended scoring.
     *
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {Object} query - Search query parameters.
     * @param {string} [query.SortOrder] - Sort order (e.g. "ScoreDescending", "TextScoreDescending").
     * @param {Object} [query.Vector] - Vector query with SearchType, Embeddings, MinimumScore, etc.
     * @param {Object} [query.FullText] - Full-text search query parameters.
     * @param {string} query.FullText.Query - Search text (required). Processed with stemming and stop word removal.
     * @param {string} [query.FullText.SearchType] - Ranking function: "TsRank" (default) or "TsRankCd" (cover density).
     * @param {string} [query.FullText.Language] - Text search configuration, default "english".
     * @param {number} [query.FullText.Normalization] - ts_rank normalization bitmask, default 32 (0-1 range).
     * @param {number} [query.FullText.MinimumScore] - Minimum text relevance score threshold.
     * @param {number} [query.FullText.TextWeight] - Weight for text score in hybrid mode (0.0-1.0, default 0.5).
     * @param {Object} [query.LabelFilter] - Label filter with Required and Excluded arrays.
     * @param {Object} [query.TagFilter] - Tag filter with Required and Excluded condition arrays.
     * @param {Object} [query.Terms] - Terms filter for content matching, e.g. { Required: ["term1"], Excluded: ["term2"] }.
     * @param {number} [query.MaxResults] - Maximum results (1-1000, default 10).
     * @param {number} [query.IncludeNeighbors] - Number of neighboring chunks before and after each matched chunk to include (0-10). When set, each document in the response will include a Neighbors array of surrounding chunks ordered by position.
     * @returns {Promise<Object>} Search result. Documents include Score and, when FullText is used, TextScore (number). When IncludeNeighbors is set, documents include a Neighbors array.
     */
    async search(tenantId, collectionId, query) {
        return this._post(
            `/v1.0/tenants/${tenantId}/collections/${collectionId}/search`,
            query);
    }

    // -------------------------------------------------------------------------
    // Private HTTP helpers
    // -------------------------------------------------------------------------

    async _get(path) {
        const response = await fetch(this._endpoint + path, {
            method: "GET",
            headers: this._headers()
        });
        return this._handleResponse(response);
    }

    async _head(path) {
        const response = await fetch(this._endpoint + path, {
            method: "HEAD",
            headers: this._headers()
        });
        return response.status === 200;
    }

    async _post(path, body) {
        const response = await fetch(this._endpoint + path, {
            method: "POST",
            headers: this._headers(),
            body: JSON.stringify(body)
        });
        return this._handleResponse(response);
    }

    async _put(path, body) {
        const response = await fetch(this._endpoint + path, {
            method: "PUT",
            headers: this._headers(),
            body: JSON.stringify(body)
        });
        return this._handleResponse(response);
    }

    async _delete(path) {
        const response = await fetch(this._endpoint + path, {
            method: "DELETE",
            headers: this._headers()
        });
        if (!response.ok && response.status !== 204) {
            const text = await response.text();
            throw new RecallDbException(response.status, text);
        }
    }

    _headers() {
        return {
            "Authorization": `Bearer ${this._bearerToken}`,
            "Content-Type": "application/json"
        };
    }

    async _handleResponse(response) {
        const text = await response.text();
        if (!response.ok) {
            throw new RecallDbException(response.status, text);
        }
        if (!text) return null;
        return JSON.parse(text);
    }
}

module.exports = { RecallDbClient, RecallDbException };
