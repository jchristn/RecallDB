const DEFAULT_API_URL = 'http://localhost:8600';

class RecallDbApi {
  constructor() {
    this.baseUrl = localStorage.getItem('recalldb_server_url') || DEFAULT_API_URL;
    this.bearerToken = localStorage.getItem('recalldb_token') || '';
  }

  setBaseUrl(url) {
    this.baseUrl = url.replace(/\/+$/, '');
    localStorage.setItem('recalldb_server_url', this.baseUrl);
  }

  setBearerToken(token) {
    this.bearerToken = token;
    localStorage.setItem('recalldb_token', token);
  }

  setUserInfo(user, tenant) {
    localStorage.setItem('recalldb_user', JSON.stringify(user));
    localStorage.setItem('recalldb_tenant', JSON.stringify(tenant));
  }

  getUserInfo() {
    const user = localStorage.getItem('recalldb_user');
    const tenant = localStorage.getItem('recalldb_tenant');
    try {
      return {
        user: user && user !== 'undefined' ? JSON.parse(user) : null,
        tenant: tenant && tenant !== 'undefined' ? JSON.parse(tenant) : null
      };
    } catch {
      return { user: null, tenant: null };
    }
  }

  clearAuth() {
    localStorage.removeItem('recalldb_token');
    localStorage.removeItem('recalldb_user');
    localStorage.removeItem('recalldb_tenant');
    this.bearerToken = '';
  }

  async request(method, endpoint, body = null) {
    const url = `${this.baseUrl}${endpoint}`;
    const headers = { 'Content-Type': 'application/json' };
    if (this.bearerToken) {
      headers['Authorization'] = `Bearer ${this.bearerToken}`;
    }

    const options = { method, headers };
    if (body && method !== 'GET' && method !== 'HEAD') {
      options.body = JSON.stringify(body);
    }

    const response = await fetch(url, options);

    if (method === 'HEAD') {
      return { ok: response.ok, status: response.status };
    }

    if (response.status === 204) {
      return { ok: true, status: 204 };
    }

    const text = await response.text();
    let data = null;
    if (text) {
      try { data = JSON.parse(text); } catch (e) { data = text; }
    }

    if (!response.ok) {
      const err = new Error(data?.Error || data?.ErrorMessage || data?.Context || `HTTP ${response.status}`);
      err.status = response.status;
      err.responseData = data;
      throw err;
    }

    return data;
  }

  // Health
  async health() { return this.request('GET', '/'); }

  // Auth
  async authenticateBearer(token) {
    return this.request('POST', '/v1.0/authenticate', { BearerToken: token });
  }

  async authenticateEmailPassword(tenantId, email, password) {
    return this.request('POST', '/v1.0/authenticate', { TenantId: tenantId, Email: email, Password: password });
  }

  // Tenants
  async listTenants() { return this.request('GET', '/v1.0/tenants'); }
  async getTenant(id) { return this.request('GET', `/v1.0/tenants/${id}`); }
  async createTenant(tenant) { return this.request('PUT', '/v1.0/tenants', tenant); }
  async updateTenant(id, tenant) { return this.request('PUT', `/v1.0/tenants/${id}`, tenant); }
  async deleteTenant(id) { return this.request('DELETE', `/v1.0/tenants/${id}`); }
  async enumerateTenants(query) { return this.request('POST', '/v1.0/tenants/enumerate', query); }

  // Users
  async listUsers(tid) { return this.request('GET', `/v1.0/tenants/${tid}/users`); }
  async getUser(tid, id) { return this.request('GET', `/v1.0/tenants/${tid}/users/${id}`); }
  async createUser(tid, user) { return this.request('PUT', `/v1.0/tenants/${tid}/users`, user); }
  async updateUser(tid, id, user) { return this.request('PUT', `/v1.0/tenants/${tid}/users/${id}`, user); }
  async deleteUser(tid, id) { return this.request('DELETE', `/v1.0/tenants/${tid}/users/${id}`); }

  // Credentials
  async listCredentials(tid) { return this.request('GET', `/v1.0/tenants/${tid}/credentials`); }
  async getCredential(tid, id) { return this.request('GET', `/v1.0/tenants/${tid}/credentials/${id}`); }
  async createCredential(tid, cred) { return this.request('PUT', `/v1.0/tenants/${tid}/credentials`, cred); }
  async updateCredential(tid, id, cred) { return this.request('PUT', `/v1.0/tenants/${tid}/credentials/${id}`, cred); }
  async deleteCredential(tid, id) { return this.request('DELETE', `/v1.0/tenants/${tid}/credentials/${id}`); }

  // Collections
  async listCollections(tid) { return this.request('GET', `/v1.0/tenants/${tid}/collections`); }
  async getCollection(tid, id) { return this.request('GET', `/v1.0/tenants/${tid}/collections/${id}`); }
  async createCollection(tid, col) { return this.request('PUT', `/v1.0/tenants/${tid}/collections`, col); }
  async updateCollection(tid, id, col) { return this.request('PUT', `/v1.0/tenants/${tid}/collections/${id}`, col); }
  async deleteCollection(tid, id) { return this.request('DELETE', `/v1.0/tenants/${tid}/collections/${id}`); }
  async collectionStats(tid, cid) { return this.request('GET', `/v1.0/tenants/${tid}/collections/${cid}/stats`); }

  // Documents
  async listDocuments(tid, cid) { return this.request('GET', `/v1.0/tenants/${tid}/collections/${cid}/documents`); }
  async getDocument(tid, cid, docKey) { return this.request('GET', `/v1.0/tenants/${tid}/collections/${cid}/documents/${docKey}`); }
  async createDocument(tid, cid, doc) { return this.request('PUT', `/v1.0/tenants/${tid}/collections/${cid}/documents`, doc); }
  async updateDocument(tid, cid, docKey, doc) { return this.request('PUT', `/v1.0/tenants/${tid}/collections/${cid}/documents/${docKey}`, doc); }
  async deleteDocument(tid, cid, docKey) { return this.request('DELETE', `/v1.0/tenants/${tid}/collections/${cid}/documents/${docKey}`); }
  async batchCreateDocuments(tid, cid, docs) { return this.request('POST', `/v1.0/tenants/${tid}/collections/${cid}/documents/batch`, docs); }
  async documentStats(tid, cid, docKey) { return this.request('GET', `/v1.0/tenants/${tid}/collections/${cid}/documents/stats/${docKey}`); }

  // Labels
  async listLabels(tid, cid) { return this.request('GET', `/v1.0/tenants/${tid}/collections/${cid}/labels`); }
  async createLabel(tid, cid, label) { return this.request('PUT', `/v1.0/tenants/${tid}/collections/${cid}/labels`, label); }
  async deleteLabel(tid, cid, id) { return this.request('DELETE', `/v1.0/tenants/${tid}/collections/${cid}/labels/${id}`); }

  // Tags
  async listTags(tid, cid) { return this.request('GET', `/v1.0/tenants/${tid}/collections/${cid}/tags`); }
  async createTag(tid, cid, tag) { return this.request('PUT', `/v1.0/tenants/${tid}/collections/${cid}/tags`, tag); }
  async deleteTag(tid, cid, id) { return this.request('DELETE', `/v1.0/tenants/${tid}/collections/${cid}/tags/${id}`); }

  // Search
  async search(tid, cid, query) { return this.request('POST', `/v1.0/tenants/${tid}/collections/${cid}/search`, query); }

  // Enumerate documents
  async enumerateDocuments(tid, cid, query) { return this.request('POST', `/v1.0/tenants/${tid}/collections/${cid}/documents/enumerate`, query); }
}

const api = new RecallDbApi();
export default api;
