# RecallDB REST API

RecallDB is a multi-tenant RESTful vector database built on PostgreSQL with pgvector. All request and response bodies use JSON (`Content-Type: application/json`).

## Base URL

```
http://{hostname}:{port}
```

## Authentication

All authenticated endpoints require an `Authorization` header with a bearer token:

```
Authorization: Bearer <token>
```

Two types of bearer tokens are accepted:

- **Admin API keys** - configured in `recalldb.json`, grant full administrative access to all tenants
- **Credential bearer tokens** - scoped to a specific tenant and user, created via the Credentials API

### Permission Levels

| Level | Description |
|-------|-------------|
| **None** | No authentication required |
| **Authenticated** | Any valid bearer token (admin API key or credential token) |
| **Admin** | Admin API key required |
| **TenantAdmin** | Admin API key, or credential token for a user with `IsTenantAdmin: true` |

---

## Error Responses

All error responses share a common structure:

```json
{
  "Error": "Not found",
  "StatusCode": 404,
  "Context": "The requested resource was not found."
}
```

| Field | Type | Description |
|-------|------|-------------|
| `Error` | string | Short error description |
| `StatusCode` | int | HTTP status code |
| `Context` | string | Additional detail (may be null) |

Common status codes: `400` Bad Request, `401` Unauthorized, `403` Forbidden, `404` Not Found.

---

## Health

### `GET /`

Health check. No authentication required.

**Response `200`**

```json
{
  "Name": "RecallDB",
  "Version": "0.2.0",
  "UptimeMs": 123456.78
}
```

### `HEAD /`

Health check (no body). Returns `200` if the server is running.

---

## Authentication

### `POST /v1.0/authenticate`

Authenticate using a bearer token or email and password. No `Authorization` header required.

**Request (bearer token)**

```json
{
  "BearerToken": "your-bearer-token"
}
```

**Request (email + password)**

```json
{
  "TenantId": "default",
  "Email": "admin@recall",
  "Password": "password"
}
```

**Response `200`**

```json
{
  "Success": true,
  "Tenant": {
    "Id": "default",
    "Name": "Default Tenant",
    "Active": true,
    "Labels": [],
    "Tags": {},
    "CreatedUtc": "2025-01-15T12:00:00Z",
    "LastUpdateUtc": "2025-01-15T12:00:00Z"
  },
  "User": {
    "Id": "default",
    "TenantId": "default",
    "Email": "admin@recall",
    "PasswordSha256": "********",
    "FirstName": "Admin",
    "LastName": "User",
    "IsAdmin": true,
    "IsTenantAdmin": true,
    "Active": true,
    "CreatedUtc": "2025-01-15T12:00:00Z",
    "LastUpdateUtc": "2025-01-15T12:00:00Z"
  },
  "Credential": {
    "Id": "default",
    "TenantId": "default",
    "UserId": "default",
    "BearerToken": "default",
    "Name": "Default API Key",
    "Active": true,
    "CreatedUtc": "2025-01-15T12:00:00Z",
    "LastUpdateUtc": "2025-01-15T12:00:00Z"
  },
  "ErrorMessage": null
}
```

**Response `401`** (failed authentication)

```json
{
  "Success": false,
  "Tenant": null,
  "User": null,
  "Credential": null,
  "ErrorMessage": "Authentication failed."
}
```

---

## Tenants

### `GET /v1.0/tenants`

List tenants. Admins see all tenants; authenticated users see only their own tenant.

**Auth:** Authenticated

**Response `200`**

```json
[
  {
    "Id": "default",
    "Name": "Default Tenant",
    "Active": true,
    "Labels": ["production"],
    "Tags": { "region": "us-east" },
    "CreatedUtc": "2025-01-15T12:00:00Z",
    "LastUpdateUtc": "2025-01-15T12:00:00Z"
  }
]
```

### `GET /v1.0/tenants/{id}`

Retrieve a tenant by ID.

**Auth:** Authenticated

**Response `200`**

```json
{
  "Id": "default",
  "Name": "Default Tenant",
  "Active": true,
  "Labels": [],
  "Tags": {},
  "CreatedUtc": "2025-01-15T12:00:00Z",
  "LastUpdateUtc": "2025-01-15T12:00:00Z"
}
```

### `HEAD /v1.0/tenants/{id}`

Check if a tenant exists. Returns `200` if found, `404` if not.

**Auth:** Authenticated

### `POST /v1.0/tenants/enumerate`

Enumerate tenants with pagination.

**Auth:** Authenticated (admins see all, users see own tenant)

**Request**

```json
{
  "MaxResults": 100,
  "ContinuationToken": null,
  "Ordering": "CreatedDescending"
}
```

**Response `200`**

```json
{
  "Success": true,
  "MaxResults": 100,
  "ContinuationToken": null,
  "EndOfResults": true,
  "TotalRecords": 1,
  "RecordsRemaining": 0,
  "Objects": [
    {
      "Id": "default",
      "Name": "Default Tenant",
      "Active": true,
      "Labels": [],
      "Tags": {},
      "CreatedUtc": "2025-01-15T12:00:00Z",
      "LastUpdateUtc": "2025-01-15T12:00:00Z"
    }
  ],
  "TotalMs": 2.45
}
```

### `PUT /v1.0/tenants`

Create a new tenant.

**Auth:** Admin

**Request**

```json
{
  "Name": "Acme Corp",
  "Labels": ["enterprise"],
  "Tags": { "plan": "premium" }
}
```

`Id` is auto-generated if not provided. `Active` defaults to `true`.

**Response `201`**

```json
{
  "Id": "ten_01JEXAMPLE",
  "Name": "Acme Corp",
  "Active": true,
  "Labels": ["enterprise"],
  "Tags": { "plan": "premium" },
  "CreatedUtc": "2025-01-15T12:00:00Z",
  "LastUpdateUtc": "2025-01-15T12:00:00Z"
}
```

### `PUT /v1.0/tenants/{id}`

Update an existing tenant.

**Auth:** Authenticated

**Request**

```json
{
  "Id": "ten_01JEXAMPLE",
  "Name": "Acme Corp Updated",
  "Active": true,
  "Labels": ["enterprise", "updated"],
  "Tags": { "plan": "premium", "region": "eu-west" }
}
```

**Response `200`** — Returns the updated tenant object.

### `DELETE /v1.0/tenants/{id}`

Delete a tenant.

**Auth:** Admin

**Query Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `force` | any | When present, also drops collection tables belonging to the tenant |

**Response `204`** — No content.

---

## Users

Users are scoped to a tenant. User passwords are stored as SHA256 hashes and are redacted (shown as `"********"`) in all responses.

### `GET /v1.0/tenants/{tid}/users`

List all users for a tenant.

**Auth:** Authenticated

**Response `200`**

```json
[
  {
    "Id": "default",
    "TenantId": "default",
    "Email": "admin@recall",
    "PasswordSha256": "********",
    "FirstName": "Admin",
    "LastName": "User",
    "IsAdmin": true,
    "IsTenantAdmin": true,
    "Active": true,
    "CreatedUtc": "2025-01-15T12:00:00Z",
    "LastUpdateUtc": "2025-01-15T12:00:00Z"
  }
]
```

### `GET /v1.0/tenants/{tid}/users/{id}`

Retrieve a user by ID. Password is redacted.

**Auth:** Authenticated

**Response `200`**

```json
{
  "Id": "usr_01JEXAMPLE",
  "TenantId": "default",
  "Email": "jane@example.com",
  "PasswordSha256": "********",
  "FirstName": "Jane",
  "LastName": "Doe",
  "IsAdmin": false,
  "IsTenantAdmin": false,
  "Active": true,
  "CreatedUtc": "2025-01-15T12:00:00Z",
  "LastUpdateUtc": "2025-01-15T12:00:00Z"
}
```

### `HEAD /v1.0/tenants/{tid}/users/{id}`

Check if a user exists. Returns `200` if found, `404` if not.

**Auth:** Authenticated

### `POST /v1.0/tenants/{tid}/users/enumerate`

Enumerate users for a tenant with pagination.

**Auth:** Authenticated

**Request**

```json
{
  "MaxResults": 100,
  "ContinuationToken": null,
  "Ordering": "CreatedDescending"
}
```

**Response `200`**

```json
{
  "Success": true,
  "MaxResults": 100,
  "ContinuationToken": null,
  "EndOfResults": true,
  "TotalRecords": 2,
  "RecordsRemaining": 0,
  "Objects": [
    {
      "Id": "usr_01JEXAMPLE",
      "TenantId": "default",
      "Email": "jane@example.com",
      "PasswordSha256": "********",
      "FirstName": "Jane",
      "LastName": "Doe",
      "IsAdmin": false,
      "IsTenantAdmin": false,
      "Active": true,
      "CreatedUtc": "2025-01-15T12:00:00Z",
      "LastUpdateUtc": "2025-01-15T12:00:00Z"
    }
  ],
  "TotalMs": 1.83
}
```

### `PUT /v1.0/tenants/{tid}/users`

Create a new user.

**Auth:** TenantAdmin

**Request**

```json
{
  "TenantId": "default",
  "Email": "jane@example.com",
  "PasswordSha256": "5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8",
  "FirstName": "Jane",
  "LastName": "Doe",
  "IsAdmin": false,
  "IsTenantAdmin": false
}
```

Set the password by providing a SHA256 hex hash of the plaintext password. `Id` is auto-generated if not provided. `Active` defaults to `true`.

**Response `201`** — Returns the created user object (password redacted).

### `PUT /v1.0/tenants/{tid}/users/{id}`

Update an existing user.

**Auth:** TenantAdmin

**Request** — Full user object with updated fields.

**Response `200`** — Returns the updated user object (password redacted).

### `DELETE /v1.0/tenants/{tid}/users/{id}`

Delete a user.

**Auth:** TenantAdmin

**Response `204`** — No content.

---

## Credentials

Credentials are bearer tokens scoped to a tenant and user. Each credential contains an auto-generated 64-character alphanumeric bearer token.

### `GET /v1.0/tenants/{tid}/credentials`

List all credentials for a tenant.

**Auth:** Authenticated

**Response `200`**

```json
[
  {
    "Id": "default",
    "TenantId": "default",
    "UserId": "default",
    "BearerToken": "default",
    "Name": "Default API Key",
    "Active": true,
    "CreatedUtc": "2025-01-15T12:00:00Z",
    "LastUpdateUtc": "2025-01-15T12:00:00Z"
  }
]
```

### `GET /v1.0/tenants/{tid}/credentials/{id}`

Retrieve a credential by ID.

**Auth:** Authenticated

**Response `200`**

```json
{
  "Id": "cred_01JEXAMPLE",
  "TenantId": "default",
  "UserId": "usr_01JEXAMPLE",
  "BearerToken": "a1b2c3d4e5f6...",
  "Name": "My API Key",
  "Active": true,
  "CreatedUtc": "2025-01-15T12:00:00Z",
  "LastUpdateUtc": "2025-01-15T12:00:00Z"
}
```

### `HEAD /v1.0/tenants/{tid}/credentials/{id}`

Check if a credential exists. Returns `200` if found, `404` if not.

**Auth:** Authenticated

### `POST /v1.0/tenants/{tid}/credentials/enumerate`

Enumerate credentials for a tenant with pagination.

**Auth:** Authenticated

**Request**

```json
{
  "MaxResults": 100,
  "ContinuationToken": null,
  "Ordering": "CreatedDescending"
}
```

**Response `200`**

```json
{
  "Success": true,
  "MaxResults": 100,
  "ContinuationToken": null,
  "EndOfResults": true,
  "TotalRecords": 1,
  "RecordsRemaining": 0,
  "Objects": [
    {
      "Id": "cred_01JEXAMPLE",
      "TenantId": "default",
      "UserId": "usr_01JEXAMPLE",
      "BearerToken": "a1b2c3d4e5f6...",
      "Name": "My API Key",
      "Active": true,
      "CreatedUtc": "2025-01-15T12:00:00Z",
      "LastUpdateUtc": "2025-01-15T12:00:00Z"
    }
  ],
  "TotalMs": 1.22
}
```

### `PUT /v1.0/tenants/{tid}/credentials`

Create a new credential. A 64-character bearer token is auto-generated if `BearerToken` is not provided.

**Auth:** TenantAdmin

**Request**

```json
{
  "TenantId": "default",
  "UserId": "usr_01JEXAMPLE",
  "Name": "My API Key"
}
```

**Response `201`**

```json
{
  "Id": "cred_01JEXAMPLE",
  "TenantId": "default",
  "UserId": "usr_01JEXAMPLE",
  "BearerToken": "a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0u1v2w3x4y5z6a7b8c9d0e1f2",
  "Name": "My API Key",
  "Active": true,
  "CreatedUtc": "2025-01-15T12:00:00Z",
  "LastUpdateUtc": "2025-01-15T12:00:00Z"
}
```

### `PUT /v1.0/tenants/{tid}/credentials/{id}`

Update an existing credential.

**Auth:** TenantAdmin

**Request** — Full credential object with updated fields.

**Response `200`** — Returns the updated credential object.

### `DELETE /v1.0/tenants/{tid}/credentials/{id}`

Delete a credential.

**Auth:** TenantAdmin

**Response `204`** — No content.

---

## Collections

Collections are vector stores within a tenant. Each collection has a fixed vector dimensionality set at creation time. Creating a collection also creates the backing database tables for documents, labels, and tags.

### `GET /v1.0/tenants/{tid}/collections`

List all collections for a tenant.

**Auth:** Authenticated

**Response `200`**

```json
[
  {
    "Id": "col_01JEXAMPLE",
    "TenantId": "default",
    "Name": "Research Papers",
    "Description": "Embeddings for ML research papers",
    "Dimensionality": 384,
    "Active": true,
    "CreatedUtc": "2025-01-15T12:00:00Z",
    "LastUpdateUtc": "2025-01-15T12:00:00Z"
  }
]
```

### `GET /v1.0/tenants/{tid}/collections/{cid}`

Retrieve a collection by ID.

**Auth:** Authenticated

**Response `200`**

```json
{
  "Id": "col_01JEXAMPLE",
  "TenantId": "default",
  "Name": "Research Papers",
  "Description": "Embeddings for ML research papers",
  "Dimensionality": 384,
  "Active": true,
  "CreatedUtc": "2025-01-15T12:00:00Z",
  "LastUpdateUtc": "2025-01-15T12:00:00Z"
}
```

### `HEAD /v1.0/tenants/{tid}/collections/{cid}`

Check if a collection exists. Returns `200` if found, `404` if not.

**Auth:** Authenticated

### `POST /v1.0/tenants/{tid}/collections/enumerate`

Enumerate collections for a tenant with pagination.

**Auth:** Authenticated

**Request**

```json
{
  "MaxResults": 100,
  "ContinuationToken": null,
  "Ordering": "CreatedDescending"
}
```

**Response `200`**

```json
{
  "Success": true,
  "MaxResults": 100,
  "ContinuationToken": null,
  "EndOfResults": true,
  "TotalRecords": 1,
  "RecordsRemaining": 0,
  "Objects": [
    {
      "Id": "col_01JEXAMPLE",
      "TenantId": "default",
      "Name": "Research Papers",
      "Description": "Embeddings for ML research papers",
      "Dimensionality": 384,
      "Active": true,
      "CreatedUtc": "2025-01-15T12:00:00Z",
      "LastUpdateUtc": "2025-01-15T12:00:00Z"
    }
  ],
  "TotalMs": 1.56
}
```

### `PUT /v1.0/tenants/{tid}/collections`

Create a new collection. This creates the backing document, label, and tag tables with the specified vector dimensionality.

**Auth:** TenantAdmin

**Request**

```json
{
  "TenantId": "default",
  "Name": "Research Papers",
  "Description": "Embeddings for ML research papers",
  "Dimensionality": 384
}
```

`Id` is auto-generated if not provided. When supplied, it must be 1-48 characters of letters, digits, underscore, hyphen, or dot; anything else is rejected with `400`. `Active` defaults to `true`. `Dimensionality` defaults to `384` and must be greater than `0`.

**Response `201`**

```json
{
  "Id": "col_01JEXAMPLE",
  "TenantId": "default",
  "Name": "Research Papers",
  "Description": "Embeddings for ML research papers",
  "Dimensionality": 384,
  "Active": true,
  "CreatedUtc": "2025-01-15T12:00:00Z",
  "LastUpdateUtc": "2025-01-15T12:00:00Z"
}
```

**Response `409`** — A collection with the same name already exists in the tenant. The `Context` names the existing collection's id so a client can read and adopt it instead of retrying.

```json
{
  "Error": "Conflict",
  "StatusCode": 409,
  "Context": "A collection named 'Research Papers' already exists in this tenant (id col_01JEXAMPLE)."
}
```

### `PUT /v1.0/tenants/{tid}/collections/{cid}`

Update an existing collection's metadata. Dimensionality cannot be changed after creation.

**Auth:** Authenticated

**Request** — Full collection object with updated fields.

**Response `200`** — Returns the updated collection object.

### `DELETE /v1.0/tenants/{tid}/collections/{cid}`

Delete a collection and its backing document, label, and tag tables.

**Auth:** TenantAdmin

**Response `204`** — No content.

### `GET /v1.0/tenants/{tid}/collections/{cid}/stats`

Get statistics for a collection.

**Auth:** Authenticated

**Response `200`**

```json
{
  "CollectionId": "col_01JEXAMPLE",
  "DocumentCount": 1500,
  "UniqueDocumentCount": 300,
  "TotalContentLength": 4567890,
  "LabelCount": 2400,
  "TagCount": 3100
}
```

| Field | Type | Description |
|-------|------|-------------|
| `CollectionId` | string | The collection ID |
| `DocumentCount` | long | Total number of document chunks |
| `UniqueDocumentCount` | long | Number of distinct document IDs |
| `TotalContentLength` | long | Sum of content lengths in bytes |
| `LabelCount` | long | Total label records |
| `TagCount` | long | Total tag records |

---

## Documents

Documents are stored within a collection. Each document has a unique `DocumentKey` and can optionally share a `DocumentId` with other chunks of the same source document. The `Embeddings` array must match the collection's `Dimensionality`.

### `GET /v1.0/tenants/{tid}/collections/{cid}/documents`

List all documents in a collection.

**Auth:** Authenticated

**Response `200`**

```json
[
  {
    "Id": 1,
    "DocumentKey": "doc_01JEXAMPLE",
    "DocumentId": "paper-123",
    "ContentLength": 512,
    "Etag": "abc123",
    "Sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
    "Position": 0,
    "ContentType": "Text",
    "Content": "Machine learning is a subset of artificial intelligence...",
    "BinaryData": null,
    "Embeddings": [0.123, -0.456, 0.789, "...384 total floats"],
    "CreatedUtc": "2025-01-15T12:00:00Z",
    "Score": 0,
    "Labels": ["important", "ml"],
    "Tags": { "source": "arxiv", "year": "2024" }
  }
]
```

### `GET /v1.0/tenants/{tid}/collections/{cid}/documents/{docKey}`

Retrieve a document by its unique document key.

**Auth:** Authenticated

**Response `200`**

```json
{
  "Id": 1,
  "DocumentKey": "doc_01JEXAMPLE",
  "DocumentId": "paper-123",
  "ContentLength": 512,
  "Etag": "abc123",
  "Sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
  "Position": 0,
  "ContentType": "Text",
  "Content": "Machine learning is a subset of artificial intelligence...",
  "BinaryData": null,
  "Embeddings": [0.123, -0.456, 0.789],
  "CreatedUtc": "2025-01-15T12:00:00Z",
  "Score": 0,
  "Labels": ["important", "ml"],
  "Tags": { "source": "arxiv", "year": "2024" }
}
```

### `GET /v1.0/tenants/{tid}/collections/{cid}/documents/{docId}/{position}`

Retrieve a specific document chunk by document ID and position index. Useful for navigating chunk lineage within a multi-chunk document.

**Auth:** Authenticated

**Path Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `docId` | string | Document ID (groups related chunks) |
| `position` | int | 0-based chunk position index |

**Response `200`** — Returns a single document record (same structure as above).

**Response `400`** — Position is not a valid integer.

### `HEAD /v1.0/tenants/{tid}/collections/{cid}/documents/{docKey}`

Check if a document exists. Returns `200` if found, `404` if not.

**Auth:** Authenticated

### `POST /v1.0/tenants/{tid}/collections/{cid}/documents/enumerate`

Enumerate documents with pagination and optional filtering.

**Auth:** Authenticated

**Request**

```json
{
  "MaxResults": 10,
  "ContinuationToken": null,
  "Ordering": "CreatedDescending",
  "CreatedBefore": "2025-12-31T23:59:59Z",
  "CreatedAfter": "2025-01-01T00:00:00Z",
  "DocumentIds": ["paper-123"],
  "LabelFilter": {
    "Required": ["important"],
    "Excluded": ["draft"]
  },
  "TagFilter": {
    "Required": [
      { "Key": "source", "Condition": "Equals", "Value": "arxiv" }
    ],
    "Excluded": []
  },
  "Terms": {
    "Required": ["machine learning"],
    "Excluded": ["deprecated"]
  }
}
```

**Response `200`**

```json
{
  "Success": true,
  "MaxResults": 10,
  "ContinuationToken": "10",
  "EndOfResults": false,
  "TotalRecords": 25,
  "RecordsRemaining": 15,
  "Objects": [
    {
      "Id": 1,
      "DocumentKey": "doc_01JEXAMPLE",
      "DocumentId": "paper-123",
      "ContentLength": 512,
      "Etag": "abc123",
      "Sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "Position": 0,
      "ContentType": "Text",
      "Content": "Machine learning is a subset of artificial intelligence...",
      "BinaryData": null,
      "Embeddings": [0.123, -0.456, 0.789],
      "CreatedUtc": "2025-01-15T12:00:00Z",
      "Score": 0,
      "Labels": ["important", "ml"],
      "Tags": { "source": "arxiv", "year": "2024" }
    }
  ],
  "TotalMs": 3.27
}
```

### `PUT /v1.0/tenants/{tid}/collections/{cid}/documents`

Create a new document with content and vector embeddings.

**Auth:** Authenticated

**Request**

```json
{
  "DocumentId": "paper-123",
  "Position": 0,
  "ContentType": "Text",
  "Content": "Machine learning is a subset of artificial intelligence...",
  "Embeddings": [0.123, -0.456, 0.789, "...must match collection dimensionality"]
}
```

`DocumentKey` is auto-generated if not provided. `ContentType` defaults to `Text`. `Position` defaults to `0`.

**Response `201`** — Returns the created document record with server-computed fields (`Id`, `ContentLength`, `Etag`, `Sha256`, `CreatedUtc`).

### `POST /v1.0/tenants/{tid}/collections/{cid}/documents/batch`

Create multiple documents in a single transactional batch.

**Auth:** Authenticated

**Request**

```json
[
  {
    "DocumentId": "paper-123",
    "Position": 0,
    "ContentType": "Text",
    "Content": "First chunk of the paper...",
    "Embeddings": [0.1, 0.2, 0.3]
  },
  {
    "DocumentId": "paper-123",
    "Position": 1,
    "ContentType": "Text",
    "Content": "Second chunk of the paper...",
    "Embeddings": [0.4, 0.5, 0.6]
  }
]
```

Each document must include `Embeddings` matching the collection dimensionality.

**Response `201`** — Returns the list of created document records.

### `PUT /v1.0/tenants/{tid}/collections/{cid}/documents/{docKey}`

Update an existing document by its document key.

**Auth:** Authenticated

**Request** — Full document record with updated fields.

**Response `200`** — Returns the updated document record.

### `DELETE /v1.0/tenants/{tid}/collections/{cid}/documents/{docKey}`

Delete a document by its document key.

**Auth:** Authenticated

**Response `204`** — No content.

### `POST /v1.0/tenants/{tid}/collections/{cid}/documents/batch/delete`

Delete multiple documents by their document keys in a single operation. Associated labels and tags are also deleted.

**Auth:** Authenticated

**Request**

```json
{
  "DocumentKeys": ["doc_01JEXAMPLE1", "doc_01JEXAMPLE2", "doc_01JEXAMPLE3"]
}
```

**Response `204`** — No content.

### `POST /v1.0/tenants/{tid}/collections/{cid}/documents/delete/filter`

Delete all documents matching the specified filter criteria. Uses the same filter model as the enumerate endpoint. Associated labels and tags are also deleted. Pagination fields (`MaxResults`, `ContinuationToken`, `Ordering`) are ignored.

**Auth:** Authenticated

**Request**

```json
{
  "DocumentIds": ["paper-123"],
  "CreatedBefore": "2025-12-31T23:59:59Z",
  "CreatedAfter": "2025-01-01T00:00:00Z",
  "LabelFilter": {
    "Required": ["important"],
    "Excluded": ["draft"]
  },
  "TagFilter": {
    "Required": [
      { "Key": "source", "Condition": "Equals", "Value": "arxiv" }
    ],
    "Excluded": []
  },
  "Terms": {
    "Required": ["machine learning"],
    "Excluded": ["deprecated"]
  }
}
```

**Response `200`**

```json
{
  "DocumentsDeleted": 42
}
```

| Field | Type | Description |
|-------|------|-------------|
| `DocumentsDeleted` | integer | The number of documents deleted by the filter |

### `GET /v1.0/tenants/{tid}/collections/{cid}/documents/stats/{docKey}`

Get statistics for a document. If the document has a `DocumentId`, stats aggregate across all chunks sharing that ID.

**Auth:** Authenticated

**Response `200`**

```json
{
  "DocumentKey": "doc_01JEXAMPLE",
  "DocumentId": "paper-123",
  "ChunkCount": 5,
  "TotalContentLength": 2560,
  "LabelCount": 8,
  "TagCount": 10
}
```

| Field | Type | Description |
|-------|------|-------------|
| `DocumentKey` | string | The requested document key |
| `DocumentId` | string | Document ID (null if the document has no DocumentId) |
| `ChunkCount` | long | Number of chunks for this document |
| `TotalContentLength` | long | Sum of content lengths across chunks |
| `LabelCount` | long | Total label records for this document |
| `TagCount` | long | Total tag records for this document |

---

## Labels

Labels are string tags attached to individual documents (by document key) within a collection. They are used for filtering in search and enumeration queries.

### `GET /v1.0/tenants/{tid}/collections/{cid}/labels`

List all labels in a collection.

**Auth:** Authenticated

**Response `200`**

```json
{
  "Success": true,
  "MaxResults": 100,
  "ContinuationToken": null,
  "EndOfResults": true,
  "TotalRecords": 2,
  "RecordsRemaining": 0,
  "Objects": [
    {
      "Id": "lbl_01JEXAMPLE1",
      "DocumentKey": "doc_01JEXAMPLE",
      "DocumentId": "paper-123",
      "Position": null,
      "Label": "important",
      "CreatedUtc": "2025-01-15T12:00:00Z"
    },
    {
      "Id": "lbl_01JEXAMPLE2",
      "DocumentKey": "doc_01JEXAMPLE",
      "DocumentId": "paper-123",
      "Position": null,
      "Label": "ml",
      "CreatedUtc": "2025-01-15T12:00:00Z"
    }
  ],
  "TotalMs": 1.12
}
```

### `GET /v1.0/tenants/{tid}/collections/{cid}/labels/{id}`

Retrieve a label by ID.

**Auth:** Authenticated

**Response `200`**

```json
{
  "Id": "lbl_01JEXAMPLE",
  "DocumentKey": "doc_01JEXAMPLE",
  "DocumentId": "paper-123",
  "Position": null,
  "Label": "important",
  "CreatedUtc": "2025-01-15T12:00:00Z"
}
```

### `PUT /v1.0/tenants/{tid}/collections/{cid}/labels`

Create a new label on a document.

**Auth:** Authenticated

**Request**

```json
{
  "DocumentKey": "doc_01JEXAMPLE",
  "DocumentId": "paper-123",
  "Position": null,
  "Label": "important"
}
```

`Id` is auto-generated if not provided. `Position` is optional and used for chunk-level labels.

**Response `201`**

```json
{
  "Id": "lbl_01JEXAMPLE",
  "DocumentKey": "doc_01JEXAMPLE",
  "DocumentId": "paper-123",
  "Position": null,
  "Label": "important",
  "CreatedUtc": "2025-01-15T12:00:00Z"
}
```

### `DELETE /v1.0/tenants/{tid}/collections/{cid}/labels/{id}`

Delete a label by ID.

**Auth:** Authenticated

**Response `204`** — No content.

### `GET /v1.0/tenants/{tid}/collections/{cid}/labels/distinct`

Retrieve the set of distinct label values across all documents in a collection.

**Auth:** Authenticated

**Response `200`**

```json
["important", "ml", "reviewed"]
```

---

## Tags

Tags are key-value pairs attached to individual documents within a collection. They support rich conditional filtering in search and enumeration queries.

### `GET /v1.0/tenants/{tid}/collections/{cid}/tags`

List all tags in a collection.

**Auth:** Authenticated

**Response `200`**

```json
{
  "Success": true,
  "MaxResults": 100,
  "ContinuationToken": null,
  "EndOfResults": true,
  "TotalRecords": 2,
  "RecordsRemaining": 0,
  "Objects": [
    {
      "Id": "tag_01JEXAMPLE1",
      "DocumentKey": "doc_01JEXAMPLE",
      "DocumentId": "paper-123",
      "Position": null,
      "Key": "source",
      "Value": "arxiv",
      "CreatedUtc": "2025-01-15T12:00:00Z"
    },
    {
      "Id": "tag_01JEXAMPLE2",
      "DocumentKey": "doc_01JEXAMPLE",
      "DocumentId": "paper-123",
      "Position": null,
      "Key": "year",
      "Value": "2024",
      "CreatedUtc": "2025-01-15T12:00:00Z"
    }
  ],
  "TotalMs": 0.98
}
```

### `GET /v1.0/tenants/{tid}/collections/{cid}/tags/{id}`

Retrieve a tag by ID.

**Auth:** Authenticated

**Response `200`**

```json
{
  "Id": "tag_01JEXAMPLE",
  "DocumentKey": "doc_01JEXAMPLE",
  "DocumentId": "paper-123",
  "Position": null,
  "Key": "source",
  "Value": "arxiv",
  "CreatedUtc": "2025-01-15T12:00:00Z"
}
```

### `PUT /v1.0/tenants/{tid}/collections/{cid}/tags`

Create a new key-value tag on a document.

**Auth:** Authenticated

**Request**

```json
{
  "DocumentKey": "doc_01JEXAMPLE",
  "DocumentId": "paper-123",
  "Position": null,
  "Key": "source",
  "Value": "arxiv"
}
```

`Id` is auto-generated if not provided. `Position` is optional and used for chunk-level tags.

**Response `201`**

```json
{
  "Id": "tag_01JEXAMPLE",
  "DocumentKey": "doc_01JEXAMPLE",
  "DocumentId": "paper-123",
  "Position": null,
  "Key": "source",
  "Value": "arxiv",
  "CreatedUtc": "2025-01-15T12:00:00Z"
}
```

### `DELETE /v1.0/tenants/{tid}/collections/{cid}/tags/{id}`

Delete a tag by ID.

**Auth:** Authenticated

**Response `204`** — No content.

### `GET /v1.0/tenants/{tid}/collections/{cid}/tags/distinct`

Retrieve the set of distinct tag keys across all documents in a collection.

**Auth:** Authenticated

**Response `200`**

```json
["category", "environment", "source"]
```

---

## Search

### `POST /v1.0/tenants/{tid}/collections/{cid}/search`

Perform vector similarity, full-text, or hybrid search within a collection. Supports multiple search types, filtering by labels, tags, date ranges, content terms, and document IDs.

**Auth:** Authenticated

**Request**

```json
{
  "SortOrder": "ScoreDescending",
  "Vector": {
    "SearchType": "CosineSimilarity",
    "Embeddings": [0.1, 0.2, 0.3, "...must match collection dimensionality"],
    "MinimumScore": 0.7,
    "MaximumScore": null,
    "MinimumDistance": null,
    "MaximumDistance": null
  },
  "FullText": {
    "Query": "search terms",
    "MatchMode": "Any",
    "SearchType": "TsRank",
    "Language": "english",
    "Normalization": 32,
    "MinimumScore": 0.01,
    "TextWeight": 0.5
  },
  "Hybrid": {
    "Strategy": "Rrf",
    "RrfK": 60,
    "CandidatePool": null
  },
  "LabelFilter": {
    "Required": ["important"],
    "Excluded": ["draft"]
  },
  "TagFilter": {
    "Required": [
      { "Key": "source", "Condition": "Equals", "Value": "arxiv" },
      { "Key": "year", "Condition": "GreaterThan", "Value": "2023" }
    ],
    "Excluded": [
      { "Key": "status", "Condition": "Equals", "Value": "retracted" }
    ]
  },
  "Terms": {
    "Required": ["machine learning"],
    "Excluded": ["deprecated"]
  },
  "CreatedAfter": "2025-01-01T00:00:00Z",
  "CreatedBefore": "2025-12-31T23:59:59Z",
  "DocumentIds": [],
  "MaxResults": 10,
  "IncludeNeighbors": null,
  "IncludeEmbeddings": false,
  "ContinuationToken": null
}
```

The example above is a hybrid search because it supplies both `Vector.Embeddings` and a non-blank `FullText.Query`. Drop `FullText` for a vector-only search, drop `Vector` for a full-text-only search, and leave out `Hybrid` to take the RRF defaults. [Search Modes](#search-modes) explains how each mode scores documents.

**Response `200`**

```json
{
  "Success": true,
  "MaxResults": 10,
  "ContinuationToken": "10",
  "EndOfResults": false,
  "TotalRecords": 42,
  "RecordsRemaining": 32,
  "Documents": [
    {
      "Id": 1,
      "DocumentKey": "doc_01JEXAMPLE",
      "DocumentId": "paper-123",
      "ContentLength": 512,
      "Etag": "abc123",
      "Sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "Position": 0,
      "ContentType": "Text",
      "Content": "Machine learning is a subset of artificial intelligence...",
      "BinaryData": null,
      "Embeddings": null,
      "CreatedUtc": "2025-01-15T12:00:00Z",
      "Score": 0.9919,
      "VectorScore": 0.91,
      "VectorRank": 1,
      "TextScore": 0.62,
      "TextRank": 2,
      "Labels": ["important", "ml"],
      "Tags": { "source": "arxiv", "year": "2024" },
      "Neighbors": [
        {
          "DocumentKey": "doc_01JPREVIOUS",
          "DocumentId": "paper-123",
          "Position": -1,
          "ContentType": "Text",
          "Content": "Previous chunk content...",
          "Labels": ["important"],
          "Tags": { "source": "arxiv" }
        },
        {
          "DocumentKey": "doc_01JNEXT",
          "DocumentId": "paper-123",
          "Position": 1,
          "ContentType": "Text",
          "Content": "Next chunk content...",
          "Labels": ["important"],
          "Tags": { "source": "arxiv" }
        }
      ]
    }
  ],
  "TotalMs": 12.45
}
```

---

## Search Modes

The server picks a mode from what the request actually contains. A search has a vector leg when `Vector.Embeddings` is non-empty, and a text leg when `FullText.Query` is non-blank. A `FullText` object with a blank `Query` counts as no text query at all: next to a vector it is ignored, and on its own it is rejected with a 400 (`FullText.Query is required for a full-text search.`).

**Vector-only** (a vector, no text query)
- `Score` is the vector similarity (or distance, depending on `Vector.SearchType`)
- `VectorScore` carries the same raw similarity
- `TextScore`, `VectorRank` and `TextRank` are omitted

**Full-text-only** (a text query, no vector)
- `Score` is the PostgreSQL text rank (`ts_rank` or `ts_rank_cd`), and `TextScore` is the same value
- `FullText.MatchMode` decides which documents match. The default, `Any`, returns documents containing any meaningful term in the query and ranks the ones matching more (and rarer) terms higher. Use `All` when every term must be present; that was the only behavior before this release. See `TextMatchModeEnum` under [Enumerations](#enumerations).
- A query made only of stop words (for example `"the and of"`) is not an error. It returns zero results and a `Notice`.

**Hybrid** (both a vector and a text query)

Hybrid runs two legs, vector and text, under the same filters (labels, tags, dates, document IDs, terms) and combines them according to `Hybrid.Strategy`. In every strategy `FullText.TextWeight` (call it `w`) is the text leg's share and the vector leg gets `1 - w`. A weight of `0.0` means vector only, `1.0` means text only, and the default `0.5` weights them equally.

`Rrf`, the default, is weighted Reciprocal Rank Fusion. Each leg retrieves its own top `CandidatePool` documents independently and the result is the union of the two lists. A document does not have to match the text query to come back, so a strong semantic match that shares no keywords with the query still ranks well. With `k = Hybrid.RrfK`:

```
Score = ((1 - w) / (k + VectorRank) + w / (k + TextRank)) * (k + 1)
```

A leg the document is missing from contributes 0. The `k + 1` factor normalizes the score to `[0, 1]`: a document ranked first in both legs scores `1.0`, and one ranked first in a single leg scores `0.5` at the default weight.

`Linear` uses the same union of candidates but blends normalized scores instead of ranks:

```
Score = (1 - w) * vectorNorm + w * textNorm
```

For `CosineSimilarity`, `vectorNorm` is the cosine similarity clamped to `[0, 1]`. For the other vector search types it is the min-max normalized distance within the candidate set, with the best candidate at `1`. `textNorm` is the document's `TextScore` divided by the highest `TextScore` among the candidates, or `0` when the document is not a text match. The result is also in `[0, 1]`.

`Filter` is the old behavior, kept for callers that depend on it. The text query is a required filter, so only text matches come back, ranked by `(1 - w) * vectorScore + w * textScore` on the raw scales (a cosine similarity added to a `ts_rank` value that is usually much smaller). Pair it with `FullText.MatchMode = All` to reproduce what RecallDB returned before the change.

In `Rrf` and `Linear` results, `Score` is the fused score, `VectorScore` is the raw similarity, `TextScore` is the raw text rank, and `VectorRank` and `TextRank` are the 1-based positions in each leg. `VectorRank` is omitted when the document fell outside the vector leg's candidates. `TextRank` and `TextScore` are omitted when it did not match the text query.

**Candidate pool and `TotalRecords`.** `Hybrid.CandidatePool` defaults to `max(MaxResults * 4, 100)`, capped at 1000. For `Rrf` and `Linear`, `TotalRecords` is the size of the fused candidate set, not a count of every document in the collection that matches in some way. It never exceeds `2 * CandidatePool`, and continuation tokens page within that set, so raise `CandidatePool` if you need to page deeper.

**Thresholds.** `SearchQuery.MinimumScore` and `MaximumScore` are applied in SQL to the fused `Score` in hybrid searches and to the text score in full-text searches, which keeps `TotalRecords` and pagination consistent with the pages you get back. `FullText.MinimumScore` excludes documents in full-text-only and `Filter` searches. In `Rrf` and `Linear` it only gates the text leg, so a document below it can still be returned on the strength of its vector rank. Thresholds on vector-only searches work as they did before.

A `Hybrid` object on a search that lacks one of the legs is ignored, and the response's `Notice` says so.

**Notices.** `SearchResult.Notice` is omitted unless the server has something to say about how the search ran:

| Notice | When |
|--------|------|
| `The text query contained no searchable terms.` | Full-text-only search whose query is all stop words. The search succeeds with zero results. |
| `The text query contained no searchable terms; results are ranked by the vector leg only.` | The same situation in a hybrid search. |
| `Full-text language '<name>' is not served by the full-text index; the text match was evaluated without an index.` | `FullText.Language` is something other than `english`. Results are correct, but the text match cannot use the GIN index. |
| `Hybrid options were ignored because the search does not include both a vector query and a text query.` | `Hybrid` was sent with only one leg. |

Notices are informational and their wording may change, so don't branch on the text.

### Search Validation Errors

The search endpoint answers `400 Bad Request` with a message naming the field when any of these rules is broken. Some range checks run while the request body is deserialized, and those can come back in a different body shape from the service-level errors. Rely on the status code and the message, not on a particular error object.

| Field | Rule |
|-------|------|
| request body | Required |
| `Vector.Embeddings` | Every value must be a finite number |
| `FullText.Query` | Must be non-blank when there is no vector (`FullText.Query is required for a full-text search.`) |
| `FullText.MatchMode` | One of `Any`, `All`, `Phrase`, `WebSearch` |
| `FullText.SearchType` | One of `TsRank`, `TsRankCd` |
| `FullText.Language` | A text search configuration installed in PostgreSQL (`pg_ts_config`: `english`, `simple`, `spanish`, `german`, `french` and so on). Case-insensitive. |
| `FullText.Normalization` | 0-63 |
| `FullText.TextWeight` | 0.0-1.0. Out-of-range values are rejected; earlier builds clamped them silently. |
| `Hybrid.Strategy` | One of `Rrf`, `Linear`, `Filter` |
| `Hybrid.RrfK` | 1-100000 |
| `Hybrid.CandidatePool` | 1-10000, or null for the default |

---

## Neighbor Retrieval

When `IncludeNeighbors` is set to `N` in the search query, each matched document in the response will include a `Neighbors` array containing up to `N` chunks before and `N` chunks after the matched chunk's position within the same `DocumentId`. This provides surrounding context for each match, which is useful for RAG pipelines where isolated chunks lose meaning without adjacent content.

**Key behaviors:**

- Neighbors are scoped to the same `DocumentId` as the matched chunk
- Neighbors are ordered by `Position` ascending
- Neighbors do **not** affect scoring, filtering, or pagination
- The matched chunk itself is excluded from the `Neighbors` array
- If two matched chunks are close together in the same document, their neighbor lists may overlap — each match carries its own self-contained context window

**Edge cases:**

- **First/last chunks**: If a matched chunk is at position 0, only neighbors after it are returned (no negative positions)
- **Single-chunk documents**: `Neighbors` will be an empty array
- **`IncludeNeighbors: 0` or `null`**: `Neighbors` is null (not populated)
- **Range 0-10**: Values outside the range are clamped

**Example request:**

```json
{
  "Vector": {
    "SearchType": "CosineSimilarity",
    "Embeddings": [0.1, 0.2, 0.3]
  },
  "MaxResults": 5,
  "IncludeNeighbors": 2
}
```

**Example response (truncated):**

```json
{
  "Documents": [
    {
      "DocumentKey": "doc_chunk_05",
      "DocumentId": "paper-123",
      "Position": 5,
      "Content": "Main matched content...",
      "Score": 0.95,
      "Neighbors": [
        { "DocumentKey": "doc_chunk_03", "DocumentId": "paper-123", "Position": 3, "Content": "Two chunks before..." },
        { "DocumentKey": "doc_chunk_04", "DocumentId": "paper-123", "Position": 4, "Content": "One chunk before..." },
        { "DocumentKey": "doc_chunk_06", "DocumentId": "paper-123", "Position": 6, "Content": "One chunk after..." },
        { "DocumentKey": "doc_chunk_07", "DocumentId": "paper-123", "Position": 7, "Content": "Two chunks after..." }
      ]
    }
  ]
}
```

---

## Reference

### SearchQuery Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `SortOrder` | string | `ScoreDescending` | Result ordering (see SortOrderEnum) |
| `Vector` | VectorQuery | null | Vector search parameters |
| `FullText` | FullTextQuery | null | Full-text search parameters for content relevance scoring |
| `Hybrid` | HybridQuery | null | How the vector and text legs are combined when both are present. Null means `Rrf` with default settings (see HybridQuery Fields) |
| `LabelFilter` | LabelFilter | null | Include/exclude by label |
| `TagFilter` | TagFilterSet | null | Include/exclude by tag conditions |
| `Terms` | TermsFilter | null | Include/exclude by content substring (case-insensitive) |
| `CreatedAfter` | datetime | null | Filter to documents created after this time |
| `CreatedBefore` | datetime | null | Filter to documents created before this time |
| `DocumentIds` | string[] | [] | Restrict search to these document IDs |
| `MinimumScore` | double | null | Minimum score threshold. Applies to the fused score in hybrid searches and to the text score in full-text searches |
| `MaximumScore` | double | null | Maximum score threshold. Same scope as `MinimumScore` |
| `MinimumDistance` | double | null | Minimum distance threshold |
| `MaximumDistance` | double | null | Maximum distance threshold |
| `MaxResults` | int | 10 | Results per page (1-1000) |
| `IncludeNeighbors` | int (nullable) | null | Number of neighboring chunks before and after each matched chunk to include (0-10). When set, each document includes a Neighbors array. |
| `IncludeEmbeddings` | bool | false | Return each hit's stored embedding vector in `Embeddings`. Off by default to keep responses small |
| `ContinuationToken` | string | null | Token for next page of results |

### FullTextQuery Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Query` | string | required | The search text to match against content. Required (non-blank) for a full-text-only search; a blank value next to a vector is ignored |
| `MatchMode` | string | `Any` | Which documents match the query (see TextMatchModeEnum) |
| `SearchType` | string | `TsRank` | Ranking function: TsRank or TsRankCd (see TextSearchTypeEnum) |
| `Language` | string | `english` | PostgreSQL text search configuration. Must exist in `pg_ts_config` (case-insensitive). Only `english` uses the full-text index |
| `Normalization` | int | `32` | ts_rank normalization bitmask (0-63; common values 0, 1, 2, 32) |
| `MinimumScore` | double | null | Minimum text relevance score. Excludes documents in full-text and `Filter` searches; gates only the text leg in `Rrf` and `Linear` |
| `TextWeight` | double | `0.5` | The text leg's share in hybrid search (0.0-1.0); the vector leg gets `1 - TextWeight`. Out-of-range values are rejected |

### HybridQuery Fields

Used only when the search has both a vector query and a text query. Otherwise it is ignored and the response carries a `Notice`.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Strategy` | string | `Rrf` | How the legs are combined (see HybridStrategyEnum) |
| `RrfK` | int | `60` | RRF constant k (1-100000). Larger values flatten the gap between adjacent ranks. Only used by `Rrf` |
| `CandidatePool` | int (nullable) | null | Candidates each leg retrieves before fusion (1-10000). Null means `max(MaxResults * 4, 100)`, capped at 1000. `TotalRecords` is at most `2 * CandidatePool` for `Rrf` and `Linear` |

### SearchResult Fields

| Field | Type | Description |
|-------|------|-------------|
| `Success` | bool | Whether the search ran |
| `MaxResults` | int | Page size that was applied |
| `ContinuationToken` | string | Token for the next page, or null at the end |
| `EndOfResults` | bool | True when there are no more pages |
| `TotalRecords` | long | Total matching records. For hybrid `Rrf` and `Linear`, the size of the fused candidate set (at most `2 * CandidatePool`) |
| `RecordsRemaining` | long | Records left after this page |
| `Documents` | DocumentRecord[] | The hits for this page |
| `Notice` | string | Informational message about how the search ran (see [Search Modes](#search-modes)). Omitted when null |
| `TotalMs` | double | Server-side processing time in milliseconds |

### Search Hit Fields (DocumentRecord)

Each entry in `SearchResult.Documents` is a `DocumentRecord` with these search-specific fields added. The nullable ones are omitted from the JSON when null.

| Field | Type | Set in | Description |
|-------|------|--------|-------------|
| `Score` | double | all modes | Vector similarity (vector-only), text rank (full-text-only), or fused score (hybrid; in [0, 1] for `Rrf` and `Linear`) |
| `VectorScore` | double (nullable) | vector-only, hybrid | Raw similarity in the units of `Vector.SearchType` |
| `VectorRank` | int (nullable) | hybrid `Rrf`, `Linear` | 1-based rank in the vector leg; null when outside the leg's candidates |
| `TextScore` | double (nullable) | full-text, hybrid | Raw `ts_rank` / `ts_rank_cd` value. In hybrid `Rrf` and `Linear` it is null for documents that did not match the text query |
| `TextRank` | int (nullable) | hybrid `Rrf`, `Linear` | 1-based rank in the text leg; null when the document is not a text match |
| `Embeddings` | float[] | when `IncludeEmbeddings` is true | The stored vector. Null by default |
| `Neighbors` | array | when `IncludeNeighbors` is set | Surrounding chunks (see [Neighbor Retrieval](#neighbor-retrieval)) |

### EnumerationQuery Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `MaxResults` | int | 100 | Results per page (1-1000) |
| `ContinuationToken` | string | null | Token for next page of results |
| `Ordering` | string | `CreatedDescending` | Result ordering (see EnumerationOrderEnum) |
| `CreatedAfter` | datetime | null | Filter to records created after this time |
| `CreatedBefore` | datetime | null | Filter to records created before this time |
| `DocumentIds` | string[] | [] | Restrict to these document IDs |
| `LabelFilter` | LabelFilter | null | Include/exclude by label |
| `TagFilter` | TagFilterSet | null | Include/exclude by tag conditions |
| `Terms` | TermsFilter | null | Include/exclude by content substring (case-insensitive) |

### VectorQuery Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `SearchType` | string | `CosineSimilarity` | Search algorithm (see SearchTypeEnum) |
| `Embeddings` | float[] | null | Query vector (must match collection dimensionality) |
| `MinimumScore` | double | null | Minimum score threshold |
| `MaximumScore` | double | null | Maximum score threshold |
| `MinimumDistance` | double | null | Minimum distance threshold |
| `MaximumDistance` | double | null | Maximum distance threshold |

### TagCondition Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Key` | string | null | Tag key to match |
| `Condition` | string | `Equals` | Comparison operator (see TagConditionEnum) |
| `Value` | string | null | Value to compare against |

### Enumerations

**SortOrderEnum** — used in `SearchQuery.SortOrder`:

| Value | Description |
|-------|-------------|
| `ScoreAscending` | Lowest score first |
| `ScoreDescending` | Highest score first |
| `DistanceAscending` | Shortest distance first |
| `DistanceDescending` | Longest distance first |
| `CreatedAscending` | Oldest first |
| `CreatedDescending` | Newest first |
| `TextScoreAscending` | Lowest text relevance score first |
| `TextScoreDescending` | Highest text relevance score first |

**EnumerationOrderEnum** — used in `EnumerationQuery.Ordering`:

| Value | Description |
|-------|-------------|
| `CreatedAscending` | Oldest first |
| `CreatedDescending` | Newest first |

**SearchTypeEnum** — used in `VectorQuery.SearchType`:

| Value | Description |
|-------|-------------|
| `CosineSimilarity` | Cosine similarity (higher = more similar) |
| `CosineDistance` | Cosine distance (lower = more similar) |
| `EuclideanSimilarity` | Euclidean similarity (higher = more similar) |
| `EuclideanDistance` | Euclidean distance (lower = more similar) |
| `InnerProduct` | Inner product |

**TextSearchTypeEnum** (used in `FullTextQuery.SearchType`):

| Value | Description |
|-------|-------------|
| `TsRank` | Standard scoring: term frequency with length normalization |
| `TsRankCd` | Cover density ranking: rewards term proximity |

**TextMatchModeEnum** (used in `FullTextQuery.MatchMode`):

| Value | Description |
|-------|-------------|
| `Any` | Default. Matches documents containing any meaningful term of the query (stemmed, stop words removed). Operators typed into the query are treated as plain text. Documents matching more and rarer terms rank higher |
| `All` | Every term is required (`plainto_tsquery`). The behavior before this release |
| `Phrase` | The terms must appear adjacent and in order (`phraseto_tsquery`) |
| `WebSearch` | Search-box syntax (`websearch_to_tsquery`): `"quoted phrase"`, `or`, and `-exclude` |

**HybridStrategyEnum** (used in `HybridQuery.Strategy`):

| Value | Description |
|-------|-------------|
| `Rrf` | Default. Weighted Reciprocal Rank Fusion over the union of both legs. Scores normalized to [0, 1] |
| `Linear` | Normalized score blend over the union of both legs. Scores in [0, 1] |
| `Filter` | Legacy. The text query is a required filter and the score is the raw `(1 - w) * vector + w * text` blend |

**ContentTypeEnum** — used in `DocumentRecord.ContentType`:

| Value | Description |
|-------|-------------|
| `Text` | Plain text content |
| `List` | List content |
| `Table` | Tabular content |
| `Binary` | Binary data |
| `Image` | Image data |
| `Code` | Source code |
| `Hyperlink` | Hyperlink/URL |
| `Meta` | Metadata |
| `Unknown` | Unknown content type |

**TagConditionEnum** — used in `TagCondition.Condition`:

| Value | Description |
|-------|-------------|
| `Equals` | Exact match |
| `NotEquals` | Not equal |
| `GreaterThan` | Greater than (string comparison) |
| `LessThan` | Less than (string comparison) |
| `Contains` | Value contains substring |
| `ContainsNot` | Value does not contain substring |
| `StartsWith` | Value starts with prefix |
| `EndsWith` | Value ends with suffix |
| `IsNull` | Tag value is null |
| `IsNotNull` | Tag value is not null |

### Pagination

Enumeration and search endpoints support cursor-based pagination using `ContinuationToken`. The flow is:

1. Send an initial request without `ContinuationToken` (or with `null`)
2. If the response has `EndOfResults: false`, use the returned `ContinuationToken` in the next request
3. Repeat until `EndOfResults: true` or `ContinuationToken` is `null`

The `RecordsRemaining` field indicates how many records are left after the current page. The `TotalRecords` field shows the total count matching the query.
