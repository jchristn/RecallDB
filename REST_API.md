# RecallDB REST API

## Authentication

All authenticated endpoints require an `Authorization` header:

```
Authorization: Bearer <token>
```

Admin API keys and user bearer tokens are both accepted as bearer tokens.

## Endpoints

### Health

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/` | No | Health check |
| HEAD | `/` | No | Health check (no body) |

### Authentication

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/v1.0/authenticate` | No | Authenticate with bearer token or email+password |

### Tenants

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/v1.0/tenants` | Yes | List tenants (admin: all, user: own) |
| GET | `/v1.0/tenants/{id}` | Yes | Get tenant |
| HEAD | `/v1.0/tenants/{id}` | Yes | Check tenant exists |
| POST | `/v1.0/tenants/enumerate` | Yes | Enumerate tenants with query |
| PUT | `/v1.0/tenants` | Admin | Create tenant |
| PUT | `/v1.0/tenants/{id}` | Yes | Update tenant |
| DELETE | `/v1.0/tenants/{id}` | Admin | Delete tenant |

### Users

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/v1.0/tenants/{tid}/users` | Yes | List users |
| GET | `/v1.0/tenants/{tid}/users/{id}` | Yes | Get user |
| HEAD | `/v1.0/tenants/{tid}/users/{id}` | Yes | Check user exists |
| POST | `/v1.0/tenants/{tid}/users/enumerate` | Yes | Enumerate users |
| PUT | `/v1.0/tenants/{tid}/users` | TenantAdmin | Create user |
| PUT | `/v1.0/tenants/{tid}/users/{id}` | TenantAdmin | Update user |
| DELETE | `/v1.0/tenants/{tid}/users/{id}` | TenantAdmin | Delete user |

### Credentials

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/v1.0/tenants/{tid}/credentials` | Yes | List credentials |
| GET | `/v1.0/tenants/{tid}/credentials/{id}` | Yes | Get credential |
| HEAD | `/v1.0/tenants/{tid}/credentials/{id}` | Yes | Check credential exists |
| POST | `/v1.0/tenants/{tid}/credentials/enumerate` | Yes | Enumerate credentials |
| PUT | `/v1.0/tenants/{tid}/credentials` | TenantAdmin | Create credential |
| PUT | `/v1.0/tenants/{tid}/credentials/{id}` | TenantAdmin | Update credential |
| DELETE | `/v1.0/tenants/{tid}/credentials/{id}` | TenantAdmin | Delete credential |

### Collections

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/v1.0/tenants/{tid}/collections` | Yes | List collections |
| GET | `/v1.0/tenants/{tid}/collections/{id}` | Yes | Get collection |
| HEAD | `/v1.0/tenants/{tid}/collections/{id}` | Yes | Check collection exists |
| POST | `/v1.0/tenants/{tid}/collections/enumerate` | Yes | Enumerate collections |
| PUT | `/v1.0/tenants/{tid}/collections` | TenantAdmin | Create collection |
| PUT | `/v1.0/tenants/{tid}/collections/{id}` | Yes | Update collection |
| DELETE | `/v1.0/tenants/{tid}/collections/{id}` | TenantAdmin | Delete collection |

### Documents

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/v1.0/tenants/{tid}/collections/{cid}/documents` | Yes | List documents |
| GET | `/v1.0/tenants/{tid}/collections/{cid}/documents/{docKey}` | Yes | Get document |
| GET | `/v1.0/tenants/{tid}/collections/{cid}/documents/{docId}/{position}` | Yes | Get document chunk |
| HEAD | `/v1.0/tenants/{tid}/collections/{cid}/documents/{docKey}` | Yes | Check document exists |
| POST | `/v1.0/tenants/{tid}/collections/{cid}/documents/enumerate` | Yes | Enumerate documents |
| POST | `/v1.0/tenants/{tid}/collections/{cid}/documents/batch` | Yes | Batch create documents |
| PUT | `/v1.0/tenants/{tid}/collections/{cid}/documents` | Yes | Create document |
| PUT | `/v1.0/tenants/{tid}/collections/{cid}/documents/{docKey}` | Yes | Update document |
| DELETE | `/v1.0/tenants/{tid}/collections/{cid}/documents/{docKey}` | Yes | Delete document |

### Labels

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/v1.0/tenants/{tid}/collections/{cid}/labels` | Yes | List labels |
| GET | `/v1.0/tenants/{tid}/collections/{cid}/labels/{id}` | Yes | Get label |
| PUT | `/v1.0/tenants/{tid}/collections/{cid}/labels` | Yes | Create label |
| DELETE | `/v1.0/tenants/{tid}/collections/{cid}/labels/{id}` | Yes | Delete label |

### Tags

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/v1.0/tenants/{tid}/collections/{cid}/tags` | Yes | List tags |
| GET | `/v1.0/tenants/{tid}/collections/{cid}/tags/{id}` | Yes | Get tag |
| PUT | `/v1.0/tenants/{tid}/collections/{cid}/tags` | Yes | Create tag |
| DELETE | `/v1.0/tenants/{tid}/collections/{cid}/tags/{id}` | Yes | Delete tag |

### Search

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/v1.0/tenants/{tid}/collections/{cid}/search` | Yes | Search collection |

## Request/Response Examples

### SearchQuery

```json
{
  "SortOrder": "ScoreDescending",
  "Vector": {
    "SearchType": "CosineSimilarity",
    "Embeddings": [0.1, 0.2, 0.3],
    "MinimumScore": 0.7
  },
  "LabelFilter": {
    "Required": ["important"],
    "Excluded": ["draft"]
  },
  "Terms": {
    "Required": ["machine learning"],
    "Excluded": ["deprecated"]
  },
  "MaxResults": 10
}
```

The `Terms` filter performs case-insensitive substring matching on document content. All `Required` terms must be present, and no `Excluded` terms may be present.

### Error Response

```json
{
  "Error": "Not found",
  "StatusCode": 404,
  "Context": "The requested resource was not found."
}
```
