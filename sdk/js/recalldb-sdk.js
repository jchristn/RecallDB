/**
 * RecallDB JavaScript SDK.
 *
 * Provides a client for interacting with the RecallDB REST API.
 *
 * Every public client method accepts an optional trailing `options` argument of the form `{ signal }`, where
 * `signal` is an `AbortSignal`. The caller's signal is combined with the client's timeout (see the constructor).
 * When the timeout elapses the call rejects with a {@link RecallDbTimeoutError} (name "TimeoutError"); when the
 * caller's signal aborts, the call rejects with the signal's abort reason (by default a DOMException whose name is
 * "AbortError"). HTTP errors reject with a {@link RecallDbException}.
 */

/** SDK version. */
const VERSION = "0.2.2";

/** Default per-request timeout in milliseconds. */
const DEFAULT_TIMEOUT_MS = 100000;

/**
 * Capability names reported by the server in the Capabilities array of GET /.
 * Use with {@link RecallDbClient#supports}. Older servers report no capabilities.
 */
const Capabilities = Object.freeze({
    HybridRrf: "search.hybrid.rrf",
    HybridRecency: "search.hybrid.recency",
    Collapse: "search.collapse",
    IncludeEmbeddings: "search.include-embeddings",
    FullTextMinimumShouldMatch: "search.fulltext.minimum-should-match"
});

/** Values for Hybrid.Strategy. */
const HybridStrategies = Object.freeze({
    Rrf: "Rrf",
    Linear: "Linear",
    Filter: "Filter"
});

/** Values for FullText.MatchMode. */
const FullTextMatchModes = Object.freeze({
    Any: "Any",
    All: "All",
    Phrase: "Phrase",
    WebSearch: "WebSearch"
});

/** Values for FullText.SearchType. */
const FullTextSearchTypes = Object.freeze({
    TsRank: "TsRank",
    TsRankCd: "TsRankCd"
});

/** Values for Vector.SearchType. */
const VectorSearchTypes = Object.freeze({
    CosineSimilarity: "CosineSimilarity",
    CosineDistance: "CosineDistance",
    EuclideanSimilarity: "EuclideanSimilarity",
    EuclideanDistance: "EuclideanDistance",
    InnerProduct: "InnerProduct"
});

/** Values for SortOrder on search and enumeration queries. */
const SortOrders = Object.freeze({
    ScoreAscending: "ScoreAscending",
    ScoreDescending: "ScoreDescending",
    DistanceAscending: "DistanceAscending",
    DistanceDescending: "DistanceDescending",
    CreatedAscending: "CreatedAscending",
    CreatedDescending: "CreatedDescending",
    TextScoreAscending: "TextScoreAscending",
    TextScoreDescending: "TextScoreDescending"
});

/** Values for Collapse.Field. */
const CollapseFields = Object.freeze({
    DocumentId: "DocumentId",
    Tag: "Tag"
});

/**
 * Error thrown when the server returns a non-success HTTP status.
 *
 * - `statusCode`: HTTP status code.
 * - `responseBody`: raw response body text (empty for HEAD requests).
 * - `errorCode`: the body's `Error` field (for example "BadRequest", "NotAuthorized"), or null when the body is not a
 *   JSON object.
 * - `errorMessage`: the first non-empty of the body's `Context`, `Message`, and `Description` fields, or null when the
 *   body is not a JSON object.
 */
class RecallDbException extends Error {
    /**
     * Create a RecallDbException.
     * @param {number} statusCode - HTTP status code.
     * @param {string} responseBody - Raw response body.
     */
    constructor(statusCode, responseBody) {
        const parsed = RecallDbException._parse(responseBody);
        let message;
        if (parsed) {
            const code = parsed.errorCode ? ` (${parsed.errorCode})` : "";
            const detail = parsed.errorMessage != null ? parsed.errorMessage : responseBody;
            message = `RecallDB API returned ${statusCode}${code}: ${detail}`;
        } else {
            message = `RecallDB API returned ${statusCode}: ${responseBody}`;
        }
        super(message);
        this.name = "RecallDbException";
        this.statusCode = statusCode;
        this.responseBody = responseBody;
        this.errorCode = parsed ? parsed.errorCode : null;
        this.errorMessage = parsed ? parsed.errorMessage : null;
    }

    static _parse(body) {
        if (typeof body !== "string" || !body) return null;
        let obj;
        try {
            obj = JSON.parse(body);
        } catch (e) {
            return null;
        }
        if (obj === null || typeof obj !== "object" || Array.isArray(obj)) return null;
        const nonEmpty = (v) => (typeof v === "string" && v.trim() !== "") ? v : null;
        const errorCode = nonEmpty(obj.Error);
        const errorMessage = nonEmpty(obj.Context) || nonEmpty(obj.Message) || nonEmpty(obj.Description);
        return { errorCode, errorMessage };
    }
}

/**
 * Error thrown when a request exceeds the client's timeout (constructor option `timeoutMs`).
 * Its `name` is "TimeoutError" and `timeoutMs` holds the configured timeout.
 */
class RecallDbTimeoutError extends Error {
    /**
     * @param {string} method - HTTP method.
     * @param {string} url - Request URL.
     * @param {number} timeoutMs - Configured timeout in milliseconds.
     */
    constructor(method, url, timeoutMs) {
        super(`RecallDB request ${method} ${url} timed out after ${timeoutMs} ms`);
        this.name = "TimeoutError";
        this.timeoutMs = timeoutMs;
    }
}

/**
 * Tagged template that URL-encodes every interpolated value as a single path segment.
 * @private
 */
function path(strings, ...values) {
    let out = strings[0];
    for (let i = 0; i < values.length; i++) {
        out += encodeURIComponent(String(values[i])) + strings[i + 1];
    }
    return out;
}

/**
 * Combine abort signals; the result aborts when any input aborts.
 * @private
 */
function anySignal(signals) {
    const list = signals.filter(Boolean);
    if (list.length === 0) return undefined;
    if (list.length === 1) return list[0];
    if (typeof AbortSignal !== "undefined" && typeof AbortSignal.any === "function") return AbortSignal.any(list);
    const controller = new AbortController();
    for (const s of list) {
        if (s.aborted) {
            controller.abort(s.reason);
            return controller.signal;
        }
    }
    const onAbort = (e) => {
        controller.abort(e.target.reason);
        for (const s of list) s.removeEventListener("abort", onAbort);
    };
    for (const s of list) s.addEventListener("abort", onAbort);
    return controller.signal;
}

class RecallDbClient {
    /**
     * Initialize the RecallDB client.
     * @param {string} endpoint - Base URL of the RecallDB server (e.g. http://127.0.0.1:8600).
     * @param {string} bearerToken - Bearer token for authentication.
     * @param {Object} [options] - Transport options.
     * @param {number|null} [options.timeoutMs=100000] - Per-request timeout in milliseconds, covering the response body
     *   read. 0 or null disables the timeout. Any other value must be a positive number. A timed-out call rejects with a
     *   {@link RecallDbTimeoutError} (name "TimeoutError").
     * @param {Function} [options.fetch] - Custom fetch implementation (for proxies, instrumentation, or tests). Defaults
     *   to the global fetch.
     */
    constructor(endpoint, bearerToken, options) {
        if (!endpoint) throw new Error("endpoint is required");
        if (!bearerToken) throw new Error("bearerToken is required");

        const opts = options || {};
        let timeoutMs = DEFAULT_TIMEOUT_MS;
        if ("timeoutMs" in opts && opts.timeoutMs !== undefined) {
            if (opts.timeoutMs === null || opts.timeoutMs === 0) {
                timeoutMs = 0;
            } else if (typeof opts.timeoutMs !== "number" || !Number.isFinite(opts.timeoutMs) || opts.timeoutMs <= 0) {
                throw new RangeError("timeoutMs must be a positive number, or 0/null to disable the timeout");
            } else {
                timeoutMs = opts.timeoutMs;
            }
        }
        if (opts.fetch !== undefined && opts.fetch !== null && typeof opts.fetch !== "function") {
            throw new TypeError("fetch must be a function");
        }

        this._endpoint = endpoint.replace(/\/+$/, "");
        this._bearerToken = bearerToken;
        this._timeoutMs = timeoutMs;
        this._fetch = opts.fetch || null;
        this._capabilities = null;
    }

    /** Configured per-request timeout in milliseconds (0 when disabled). */
    get timeoutMs() {
        return this._timeoutMs;
    }

    // -------------------------------------------------------------------------
    // Health and server info
    // -------------------------------------------------------------------------

    /**
     * Retrieve health and version information from the server (raw GET / response).
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Health response fields.
     */
    async health(options) {
        return this._get("/", options);
    }

    /**
     * Retrieve server information from GET /.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<{Name: string, Version: string, UptimeMs: number, Capabilities: string[]}>} Server info.
     *   Capabilities defaults to an empty array for older servers that do not report it.
     */
    async getServerInfo(options) {
        const info = (await this._get("/", options)) || {};
        if (!Array.isArray(info.Capabilities)) info.Capabilities = [];
        this._capabilities = info.Capabilities.slice();
        return info;
    }

    /**
     * Check whether the server reports a capability (see {@link Capabilities}). The capability list is fetched on the
     * first call and cached per client instance.
     * @param {string} name - Capability name, e.g. Capabilities.Collapse.
     * @param {Object} [options] - Options.
     * @param {boolean} [options.refresh=false] - Re-fetch the capability list from the server.
     * @param {AbortSignal} [options.signal] - Abort signal.
     * @returns {Promise<boolean>} True when the server reports the capability.
     */
    async supports(name, options) {
        const opts = options || {};
        if (opts.refresh || !this._capabilities) {
            await this.getServerInfo({ signal: opts.signal });
        }
        return this._capabilities.includes(name);
    }

    // -------------------------------------------------------------------------
    // Authenticate
    // -------------------------------------------------------------------------

    /**
     * Authenticate using bearer token or email+password credentials.
     * @param {Object} request - Authentication request with BearerToken or TenantId+Email+Password.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Authentication response.
     */
    async authenticate(request, options) {
        return this._post("/v1.0/authenticate", request, options);
    }

    // -------------------------------------------------------------------------
    // Tenants
    // -------------------------------------------------------------------------

    /**
     * Create a tenant.
     * @param {Object} tenant - Tenant metadata.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Created tenant metadata.
     */
    async createTenant(tenant, options) {
        return this._put("/v1.0/tenants", tenant, options);
    }

    /**
     * Retrieve a tenant by ID.
     * @param {string} tenantId - Tenant ID.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Tenant metadata.
     */
    async getTenant(tenantId, options) {
        return this._get(path`/v1.0/tenants/${tenantId}`, options);
    }

    /**
     * Update a tenant.
     * @param {string} tenantId - Tenant ID.
     * @param {Object} tenant - Updated tenant metadata.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Updated tenant metadata.
     */
    async updateTenant(tenantId, tenant, options) {
        return this._put(path`/v1.0/tenants/${tenantId}`, tenant, options);
    }

    /**
     * Delete a tenant.
     * @param {string} tenantId - Tenant ID.
     * @param {Object} [options] - Request options, e.g. { signal }.
     */
    async deleteTenant(tenantId, options) {
        await this._delete(path`/v1.0/tenants/${tenantId}`, options);
    }

    /**
     * Check if a tenant exists.
     * @param {string} tenantId - Tenant ID.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<boolean>} True for 200, false for 404. Throws RecallDbException for any other status.
     */
    async tenantExists(tenantId, options) {
        return this._head(path`/v1.0/tenants/${tenantId}`, options);
    }

    /**
     * Enumerate tenants with a query.
     * @param {Object} [query={}] - Enumeration query parameters.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Enumeration result containing tenant metadata.
     */
    async enumerateTenants(query, options) {
        return this._post("/v1.0/tenants/enumerate", query || {}, options);
    }

    // -------------------------------------------------------------------------
    // Users
    // -------------------------------------------------------------------------

    /**
     * Create a user.
     * @param {string} tenantId - Tenant ID.
     * @param {Object} user - User master record.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Created user master record.
     */
    async createUser(tenantId, user, options) {
        return this._put(path`/v1.0/tenants/${tenantId}/users`, user, options);
    }

    /**
     * Retrieve a user by ID.
     * @param {string} tenantId - Tenant ID.
     * @param {string} userId - User ID.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} User master record.
     */
    async getUser(tenantId, userId, options) {
        return this._get(path`/v1.0/tenants/${tenantId}/users/${userId}`, options);
    }

    /**
     * Update a user.
     * @param {string} tenantId - Tenant ID.
     * @param {string} userId - User ID.
     * @param {Object} user - Updated user master record.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Updated user master record.
     */
    async updateUser(tenantId, userId, user, options) {
        return this._put(path`/v1.0/tenants/${tenantId}/users/${userId}`, user, options);
    }

    /**
     * Delete a user.
     * @param {string} tenantId - Tenant ID.
     * @param {string} userId - User ID.
     * @param {Object} [options] - Request options, e.g. { signal }.
     */
    async deleteUser(tenantId, userId, options) {
        await this._delete(path`/v1.0/tenants/${tenantId}/users/${userId}`, options);
    }

    /**
     * Check if a user exists.
     * @param {string} tenantId - Tenant ID.
     * @param {string} userId - User ID.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<boolean>} True for 200, false for 404. Throws RecallDbException for any other status.
     */
    async userExists(tenantId, userId, options) {
        return this._head(path`/v1.0/tenants/${tenantId}/users/${userId}`, options);
    }

    /**
     * Enumerate users with a query.
     * @param {string} tenantId - Tenant ID.
     * @param {Object} [query={}] - Enumeration query parameters.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Enumeration result containing user master records.
     */
    async enumerateUsers(tenantId, query, options) {
        return this._post(path`/v1.0/tenants/${tenantId}/users/enumerate`, query || {}, options);
    }

    // -------------------------------------------------------------------------
    // Credentials
    // -------------------------------------------------------------------------

    /**
     * Create a credential.
     * @param {string} tenantId - Tenant ID.
     * @param {Object} credential - Credential data.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Created credential.
     */
    async createCredential(tenantId, credential, options) {
        return this._put(path`/v1.0/tenants/${tenantId}/credentials`, credential, options);
    }

    /**
     * Retrieve a credential by ID.
     * @param {string} tenantId - Tenant ID.
     * @param {string} credentialId - Credential ID.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Credential.
     */
    async getCredential(tenantId, credentialId, options) {
        return this._get(path`/v1.0/tenants/${tenantId}/credentials/${credentialId}`, options);
    }

    /**
     * Update a credential.
     * @param {string} tenantId - Tenant ID.
     * @param {string} credentialId - Credential ID.
     * @param {Object} credential - Updated credential data.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Updated credential.
     */
    async updateCredential(tenantId, credentialId, credential, options) {
        return this._put(path`/v1.0/tenants/${tenantId}/credentials/${credentialId}`, credential, options);
    }

    /**
     * Delete a credential.
     * @param {string} tenantId - Tenant ID.
     * @param {string} credentialId - Credential ID.
     * @param {Object} [options] - Request options, e.g. { signal }.
     */
    async deleteCredential(tenantId, credentialId, options) {
        await this._delete(path`/v1.0/tenants/${tenantId}/credentials/${credentialId}`, options);
    }

    /**
     * Check if a credential exists.
     * @param {string} tenantId - Tenant ID.
     * @param {string} credentialId - Credential ID.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<boolean>} True for 200, false for 404. Throws RecallDbException for any other status.
     */
    async credentialExists(tenantId, credentialId, options) {
        return this._head(path`/v1.0/tenants/${tenantId}/credentials/${credentialId}`, options);
    }

    /**
     * Enumerate credentials with a query.
     * @param {string} tenantId - Tenant ID.
     * @param {Object} [query={}] - Enumeration query parameters.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Enumeration result containing credentials.
     */
    async enumerateCredentials(tenantId, query, options) {
        return this._post(path`/v1.0/tenants/${tenantId}/credentials/enumerate`, query || {}, options);
    }

    // -------------------------------------------------------------------------
    // Collections
    // -------------------------------------------------------------------------

    /**
     * Create a collection.
     * @param {string} tenantId - Tenant ID.
     * @param {Object} collection - Collection metadata.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Created collection metadata.
     */
    async createCollection(tenantId, collection, options) {
        return this._put(path`/v1.0/tenants/${tenantId}/collections`, collection, options);
    }

    /**
     * Retrieve a collection by ID.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Collection metadata.
     */
    async getCollection(tenantId, collectionId, options) {
        return this._get(path`/v1.0/tenants/${tenantId}/collections/${collectionId}`, options);
    }

    /**
     * Update a collection.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {Object} collection - Updated collection metadata.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Updated collection metadata.
     */
    async updateCollection(tenantId, collectionId, collection, options) {
        return this._put(path`/v1.0/tenants/${tenantId}/collections/${collectionId}`, collection, options);
    }

    /**
     * Delete a collection.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {Object} [options] - Request options, e.g. { signal }.
     */
    async deleteCollection(tenantId, collectionId, options) {
        await this._delete(path`/v1.0/tenants/${tenantId}/collections/${collectionId}`, options);
    }

    /**
     * Check if a collection exists.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<boolean>} True for 200, false for 404. Throws RecallDbException for any other status.
     */
    async collectionExists(tenantId, collectionId, options) {
        return this._head(path`/v1.0/tenants/${tenantId}/collections/${collectionId}`, options);
    }

    /**
     * Enumerate collections with a query.
     * @param {string} tenantId - Tenant ID.
     * @param {Object} [query={}] - Enumeration query parameters.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Enumeration result containing collection metadata.
     */
    async enumerateCollections(tenantId, query, options) {
        return this._post(path`/v1.0/tenants/${tenantId}/collections/enumerate`, query || {}, options);
    }

    // -------------------------------------------------------------------------
    // Documents
    // -------------------------------------------------------------------------

    /**
     * Create a document.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {Object} document - Document record.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Created document record.
     */
    async createDocument(tenantId, collectionId, document, options) {
        return this._put(path`/v1.0/tenants/${tenantId}/collections/${collectionId}/documents`, document, options);
    }

    /**
     * Retrieve a document by document key.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {string} documentKey - Document key.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Document record.
     */
    async getDocument(tenantId, collectionId, documentKey, options) {
        return this._get(path`/v1.0/tenants/${tenantId}/collections/${collectionId}/documents/${documentKey}`, options);
    }

    /**
     * Retrieve a document chunk by document ID and position.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {string} documentId - Document ID.
     * @param {number} position - Chunk position.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Document record.
     */
    async getDocumentByPosition(tenantId, collectionId, documentId, position, options) {
        return this._get(
            path`/v1.0/tenants/${tenantId}/collections/${collectionId}/documents/${documentId}/${position}`, options);
    }

    /**
     * Update a document.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {string} documentKey - Document key.
     * @param {Object} document - Updated document record.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Updated document record.
     */
    async updateDocument(tenantId, collectionId, documentKey, document, options) {
        return this._put(
            path`/v1.0/tenants/${tenantId}/collections/${collectionId}/documents/${documentKey}`, document, options);
    }

    /**
     * Delete a document.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {string} documentKey - Document key.
     * @param {Object} [options] - Request options, e.g. { signal }.
     */
    async deleteDocument(tenantId, collectionId, documentKey, options) {
        await this._delete(path`/v1.0/tenants/${tenantId}/collections/${collectionId}/documents/${documentKey}`, options);
    }

    /**
     * Check if a document exists.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {string} documentKey - Document key.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<boolean>} True for 200, false for 404. Throws RecallDbException for any other status.
     */
    async documentExists(tenantId, collectionId, documentKey, options) {
        return this._head(path`/v1.0/tenants/${tenantId}/collections/${collectionId}/documents/${documentKey}`, options);
    }

    /**
     * Enumerate documents with a query.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {Object} [query={}] - Enumeration query parameters.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Enumeration result containing document records.
     */
    async enumerateDocuments(tenantId, collectionId, query, options) {
        return this._post(
            path`/v1.0/tenants/${tenantId}/collections/${collectionId}/documents/enumerate`, query || {}, options);
    }

    /**
     * Batch create documents.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {Array<Object>} documents - List of document records.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Array<Object>>} List of created document records.
     */
    async createDocumentBatch(tenantId, collectionId, documents, options) {
        return this._post(
            path`/v1.0/tenants/${tenantId}/collections/${collectionId}/documents/batch`, documents, options);
    }

    /**
     * Batch delete documents by their document keys.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {Array<string>} documentKeys - List of document keys to delete.
     * @param {Object} [options] - Request options, e.g. { signal }.
     */
    async deleteDocumentBatch(tenantId, collectionId, documentKeys, options) {
        await this._post(
            path`/v1.0/tenants/${tenantId}/collections/${collectionId}/documents/batch/delete`,
            { DocumentKeys: documentKeys }, options);
    }

    /**
     * Delete documents matching filter criteria.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {Object} [filterQuery={}] - Enumeration query with filter criteria.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Delete result with DocumentsDeleted count.
     */
    async deleteDocumentsByFilter(tenantId, collectionId, filterQuery, options) {
        return this._post(
            path`/v1.0/tenants/${tenantId}/collections/${collectionId}/documents/delete/filter`,
            filterQuery || {}, options);
    }

    // -------------------------------------------------------------------------
    // Labels
    // -------------------------------------------------------------------------

    /**
     * Create a label.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {Object} label - Label record.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Created label record.
     */
    async createLabel(tenantId, collectionId, label, options) {
        return this._put(path`/v1.0/tenants/${tenantId}/collections/${collectionId}/labels`, label, options);
    }

    /**
     * Retrieve a label by ID.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {string} labelId - Label ID.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Label record.
     */
    async getLabel(tenantId, collectionId, labelId, options) {
        return this._get(path`/v1.0/tenants/${tenantId}/collections/${collectionId}/labels/${labelId}`, options);
    }

    /**
     * Delete a label.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {string} labelId - Label ID.
     * @param {Object} [options] - Request options, e.g. { signal }.
     */
    async deleteLabel(tenantId, collectionId, labelId, options) {
        await this._delete(path`/v1.0/tenants/${tenantId}/collections/${collectionId}/labels/${labelId}`, options);
    }

    /**
     * List all labels in a collection.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Enumeration result containing label records.
     */
    async listLabels(tenantId, collectionId, options) {
        return this._get(path`/v1.0/tenants/${tenantId}/collections/${collectionId}/labels`, options);
    }

    // -------------------------------------------------------------------------
    // Tags
    // -------------------------------------------------------------------------

    /**
     * Create a tag.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {Object} tag - Tag record.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Created tag record.
     */
    async createTag(tenantId, collectionId, tag, options) {
        return this._put(path`/v1.0/tenants/${tenantId}/collections/${collectionId}/tags`, tag, options);
    }

    /**
     * Retrieve a tag by ID.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {string} tagId - Tag ID.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Tag record.
     */
    async getTag(tenantId, collectionId, tagId, options) {
        return this._get(path`/v1.0/tenants/${tenantId}/collections/${collectionId}/tags/${tagId}`, options);
    }

    /**
     * Delete a tag.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {string} tagId - Tag ID.
     * @param {Object} [options] - Request options, e.g. { signal }.
     */
    async deleteTag(tenantId, collectionId, tagId, options) {
        await this._delete(path`/v1.0/tenants/${tenantId}/collections/${collectionId}/tags/${tagId}`, options);
    }

    /**
     * List all tags in a collection.
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Enumeration result containing tag records.
     */
    async listTags(tenantId, collectionId, options) {
        return this._get(path`/v1.0/tenants/${tenantId}/collections/${collectionId}/tags`, options);
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
     * - Hybrid: provide both Vector and FullText for blended scoring. By default the two legs are
     *   combined with reciprocal rank fusion (Hybrid.Strategy "Rrf"), so a text match is not required.
     *
     * Full-text queries match documents containing any of the query's terms by default
     * (FullText.MatchMode "Any"). Use MatchMode "All" to require every term (the previous behavior), and
     * Hybrid.Strategy "Filter" to restore the previous hybrid behavior (text match required, raw scores blended).
     *
     * Compatibility: the request object is passed through as JSON, and older servers silently ignore fields they do
     * not know (Hybrid.RecencyWeight, Collapse, FullText.MinimumShouldMatch, IncludeEmbeddings). Check
     * `await client.supports(Capabilities.Collapse)` (and the other {@link Capabilities}) before relying on them.
     *
     * @param {string} tenantId - Tenant ID.
     * @param {string} collectionId - Collection ID.
     * @param {Object} query - Search query parameters.
     * @param {string} [query.SortOrder] - Sort order (see SortOrders, e.g. "ScoreDescending", "TextScoreDescending").
     * @param {Object} [query.Vector] - Vector query.
     * @param {string} [query.Vector.SearchType] - See VectorSearchTypes: "CosineSimilarity", "CosineDistance",
     *   "EuclideanSimilarity", "EuclideanDistance", or "InnerProduct".
     * @param {number[]} [query.Vector.Embeddings] - Query vector; its length must match the collection dimensionality.
     * @param {number} [query.Vector.MinimumScore] - Minimum score threshold (also MaximumScore, MinimumDistance,
     *   MaximumDistance).
     * @param {Object} [query.FullText] - Full-text search query parameters.
     * @param {string} query.FullText.Query - Search text (required). Processed with stemming and stop word removal.
     * @param {string} [query.FullText.SearchType] - Ranking function: "TsRank" (default) or "TsRankCd" (cover density).
     * @param {string} [query.FullText.MatchMode] - How the query text is matched: "Any" (default, any term matches),
     *   "All" (every term required), "Phrase" (terms adjacent and in order), or "WebSearch"
     *   (web-search syntax: "quoted phrase", or, -exclude).
     * @param {number} [query.FullText.MinimumShouldMatch] - Minimum number of distinct query terms a document must
     *   contain (1-3, default 1). Only valid with MatchMode "Any"; any other MatchMode, or a value outside 1-3, is
     *   rejected with 400. Capability: Capabilities.FullTextMinimumShouldMatch.
     * @param {string} [query.FullText.Language] - Text search configuration installed on the server, default "english".
     *   Unknown configurations are rejected with 400.
     * @param {number} [query.FullText.Normalization] - ts_rank normalization bitmask (0-63), default 32 (0-1 range).
     * @param {number} [query.FullText.MinimumScore] - Minimum text relevance score threshold.
     * @param {number} [query.FullText.TextWeight] - Share of the text leg in hybrid mode (0.0-1.0, default 0.5); the vector
     *   leg gets 1 - TextWeight. Values outside 0.0-1.0 are rejected with 400.
     * @param {Object} [query.Hybrid] - Hybrid options, used only when both Vector (with Embeddings) and FullText (with a
     *   non-blank Query) are provided. Default null (reciprocal rank fusion with default settings). When supplied on a
     *   search that is not hybrid, the options are ignored and the result carries a Notice.
     * @param {string} [query.Hybrid.Strategy] - See HybridStrategies: "Rrf" (default, reciprocal rank fusion, scores 0-1),
     *   "Linear" (weighted sum of normalized scores, 0-1), or "Filter" (legacy: text match required, raw scores blended).
     * @param {number} [query.Hybrid.RrfK] - Reciprocal rank fusion constant k (1-100000, default 60).
     * @param {number} [query.Hybrid.CandidatePool] - Candidates each leg retrieves before fusion (1-10000). Default null
     *   (max(MaxResults * 4, 100), capped at 1000).
     * @param {number} [query.Hybrid.RecencyWeight] - Weight of a third, recency-ranked leg (newest first) in Rrf fusion
     *   (0.0-1.0, default 0, which disables it). Values outside 0.0-1.0 are rejected with 400. Only Rrf uses it; Linear
     *   and Filter ignore it and add a Notice. Capability: Capabilities.HybridRecency.
     * @param {Object} [query.Collapse] - Collapse results so that one hit is returned per group. MaxResults,
     *   TotalRecords, and continuation count groups, not chunks. Rejected with 400 when Hybrid.Strategy is "Filter" and
     *   when the query has neither a vector nor a text query. Capability: Capabilities.Collapse.
     * @param {string} query.Collapse.Field - See CollapseFields: "DocumentId" (group chunks of the same document) or
     *   "Tag" (group by the value of the tag named by TagKey).
     * @param {string} [query.Collapse.TagKey] - Tag key to group by; required when Field is "Tag" (400 otherwise).
     * @param {number} [query.Collapse.CandidatePool] - Candidates retrieved before grouping (1-10000). Groups are
     *   formed within the pool, so a pool with fewer distinct groups than MaxResults returns fewer hits and a Notice.
     * @param {boolean} [query.IncludeEmbeddings=false] - Return each hit's stored vector in Embeddings. Vectors are JSON
     *   numbers, about 4 KB per hit at 384 dimensions and 8 KB at 768, so request them only when needed (for example
     *   for client-side reranking or deduplication). Capability: Capabilities.IncludeEmbeddings.
     * @param {Object} [query.LabelFilter] - Label filter with Required and Excluded arrays.
     * @param {Object} [query.TagFilter] - Tag filter with Required and Excluded condition arrays.
     * @param {Object} [query.Terms] - Terms filter for content matching, e.g. { Required: ["term1"], Excluded: ["term2"] }.
     * @param {string[]} [query.DocumentIds] - Restrict results to these document IDs.
     * @param {string} [query.CreatedAfter] - ISO 8601 lower bound on CreatedUtc (also CreatedBefore).
     * @param {number} [query.MaxResults] - Maximum results (1-1000, default 10). Counts groups when Collapse is set.
     * @param {string} [query.ContinuationToken] - Continuation token from a previous result.
     * @param {number} [query.IncludeNeighbors] - Number of neighboring chunks before and after each matched chunk to
     *   include (0-10). When set, each document in the response will include a Neighbors array of surrounding chunks
     *   ordered by position.
     * @param {Object} [options] - Request options, e.g. { signal }.
     * @returns {Promise<Object>} Search result with Success, MaxResults, TotalRecords, RecordsRemaining, EndOfResults,
     *   ContinuationToken, Documents, and an optional Notice string describing how the search was evaluated (for
     *   example, when the text query had no searchable terms or hybrid options were ignored).
     *
     *   Each hit in Documents carries the document fields plus:
     *   - Score: the ranking score. In hybrid Rrf and Linear searches this is the fused score (0-1).
     *   - VectorScore: raw similarity in the vector metric's units (vector-only and hybrid searches).
     *   - TextScore: text relevance, when FullText is used; absent in hybrid Rrf and Linear when the hit was not in the
     *     text leg.
     *   - VectorRank, TextRank: 1-based ranks in the hybrid vector and text legs (Rrf and Linear only); null or absent
     *     when the hit was not in that leg (a text-only hit has no VectorRank).
     *   - RecencyRank: 1-based rank in the recency leg, 1 = newest (hybrid Rrf with RecencyWeight > 0). Every hit
     *     of one collapse group shares a rank.
     *   - GroupKey: the group this hit represents when Collapse is set: the DocumentId or tag value it was grouped
     *     by, or its DocumentKey when it has none.
     *   - GroupHits: number of candidates in the group, including this hit, when Collapse is set.
     *   - Embeddings: the stored vector, only when IncludeEmbeddings is true.
     *   - Neighbors: surrounding chunks, when IncludeNeighbors is set.
     */
    async search(tenantId, collectionId, query, options) {
        return this._post(path`/v1.0/tenants/${tenantId}/collections/${collectionId}/search`, query, options);
    }

    // -------------------------------------------------------------------------
    // Private HTTP helpers
    // -------------------------------------------------------------------------

    async _get(p, options) {
        return this._request("GET", p, undefined, options, "json");
    }

    async _head(p, options) {
        return this._request("HEAD", p, undefined, options, "head");
    }

    async _post(p, body, options) {
        return this._request("POST", p, body, options, "json");
    }

    async _put(p, body, options) {
        return this._request("PUT", p, body, options, "json");
    }

    async _delete(p, options) {
        await this._request("DELETE", p, undefined, options, "none");
    }

    _headers() {
        return {
            "Authorization": `Bearer ${this._bearerToken}`,
            "Content-Type": "application/json"
        };
    }

    _fetchImpl() {
        if (this._fetch) return this._fetch;
        if (typeof globalThis.fetch !== "function") {
            throw new Error("No global fetch is available; pass options.fetch to the RecallDbClient constructor");
        }
        return globalThis.fetch;
    }

    async _request(method, p, body, options, mode) {
        const url = this._endpoint + p;
        const callerSignal = options && options.signal ? options.signal : undefined;
        const fetchImpl = this._fetchImpl();

        let timer = null;
        let timedOut = false;
        let timeoutController = null;
        if (this._timeoutMs > 0) {
            timeoutController = new AbortController();
            timer = setTimeout(() => {
                timedOut = true;
                timeoutController.abort(new RecallDbTimeoutError(method, url, this._timeoutMs));
            }, this._timeoutMs);
        }

        const signal = anySignal([callerSignal, timeoutController ? timeoutController.signal : null]);
        const init = { method, headers: this._headers() };
        if (body !== undefined) init.body = JSON.stringify(body);
        if (signal) init.signal = signal;

        try {
            const response = await fetchImpl(url, init);

            if (mode === "head") {
                if (response.status === 200) return true;
                if (response.status === 404) return false;
                let text = "";
                try { text = await response.text(); } catch (e) { text = ""; }
                throw new RecallDbException(response.status, text);
            }

            const text = await response.text();
            if (!response.ok) {
                throw new RecallDbException(response.status, text);
            }
            if (mode === "none" || !text) return null;
            return JSON.parse(text);
        } catch (e) {
            if (timedOut && !(e instanceof RecallDbException)) {
                throw new RecallDbTimeoutError(method, url, this._timeoutMs);
            }
            throw e;
        } finally {
            if (timer) clearTimeout(timer);
        }
    }
}

module.exports = {
    RecallDbClient,
    RecallDbException,
    RecallDbTimeoutError,
    VERSION,
    Capabilities,
    HybridStrategies,
    FullTextMatchModes,
    FullTextSearchTypes,
    VectorSearchTypes,
    SortOrders,
    CollapseFields
};
