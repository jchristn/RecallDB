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
  "Version": "1.0.0",
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

`Id` is auto-generated if not provided. `Active` defaults to `true`. `Dimensionality` defaults to `384` and must be greater than `0`.

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

---

## Search

### `POST /v1.0/tenants/{tid}/collections/{cid}/search`

Perform vector similarity search within a collection. Supports multiple search types, filtering by labels, tags, date ranges, content terms, and document IDs.

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
  "ContinuationToken": null
}
```

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
      "Embeddings": [0.123, -0.456, 0.789],
      "CreatedUtc": "2025-01-15T12:00:00Z",
      "Score": 0.95,
      "Labels": ["important", "ml"],
      "Tags": { "source": "arxiv", "year": "2024" }
    }
  ],
  "TotalMs": 12.45
}
```

---

## Reference

### SearchQuery Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `SortOrder` | string | `ScoreDescending` | Result ordering (see SortOrderEnum) |
| `Vector` | VectorQuery | null | Vector search parameters |
| `LabelFilter` | LabelFilter | null | Include/exclude by label |
| `TagFilter` | TagFilterSet | null | Include/exclude by tag conditions |
| `Terms` | TermsFilter | null | Include/exclude by content substring (case-insensitive) |
| `CreatedAfter` | datetime | null | Filter to documents created after this time |
| `CreatedBefore` | datetime | null | Filter to documents created before this time |
| `DocumentIds` | string[] | [] | Restrict search to these document IDs |
| `MinimumScore` | double | null | Minimum score threshold |
| `MaximumScore` | double | null | Maximum score threshold |
| `MinimumDistance` | double | null | Minimum distance threshold |
| `MaximumDistance` | double | null | Maximum distance threshold |
| `MaxResults` | int | 10 | Results per page (1-1000) |
| `ContinuationToken` | string | null | Token for next page of results |

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
