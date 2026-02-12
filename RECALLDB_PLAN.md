# RecallDB Implementation Plan

## Context

RecallDB is a greenfield multi-tenant RESTful vector database service that provides an opinionated layer over PostgreSQL with pgvector. It stores content, metadata/features, and vector embeddings in dynamically-created per-collection tables, with supporting infrastructure tables for tenants, users, credentials, and collections. The project follows the architecture and code conventions established in Conductor, Partio, Verbex, Lattice, and Chronos.

### Key Decisions

| Decision | Choice |
|----------|--------|
| Admin model | AdminApiKeys in settings + User.IsAdmin/IsTenantAdmin flags (no separate Administrator table) |
| Vector implementation | Postgres-only with native pgvector operators (`<=>` cosine, `<->` L2, `<#>` inner product) |
| Embeddings | Store pre-computed embeddings only (caller provides them) |
| .NET version | net10.0 |
| Authentication endpoint | POST /v1.0/authenticate accepts both bearer token validation AND email+password login |
| Dashboard | React SPA (like Conductor) with separate Dockerfile and nginx |
| Batch document creation | Separate endpoint: POST .../documents/batch |

### Code Style

- No `var` or tuples
- `using` statements (not declarations), placed inside namespace blocks
- Private members named `_PascalCase`
- XML code documentation on all public members; include min/max/default values where appropriate
- One entity per code file
- Setter validation with backing fields and sensible default values
- **JSON Serialization:** Use custom `Serializer.cs` helper (System.Text.Json). No SerializationHelper NuGet. No `JsonNamingPolicy`, no `JsonPropertyName` attributes -- everything serializes in PascalCase by default. `WriteIndented = true`, `DefaultIgnoreCondition = WhenWritingNull`.

---

## Directory Structure

```
C:\Code\RecallDB\
├── src\
│   ├── RecallDb.sln
│   ├── RecallDb.Core\
│   │   ├── RecallDb.Core.csproj
│   │   ├── Database\
│   │   │   ├── DatabaseDriverBase.cs
│   │   │   ├── Interfaces\
│   │   │   │   ├── ITenantMethods.cs
│   │   │   │   ├── IUserMethods.cs
│   │   │   │   ├── ICredentialMethods.cs
│   │   │   │   ├── ICollectionMethods.cs
│   │   │   │   ├── IDocumentMethods.cs
│   │   │   │   ├── ILabelMethods.cs
│   │   │   │   ├── ITagMethods.cs
│   │   │   │   └── ISearchMethods.cs
│   │   │   └── Postgresql\
│   │   │       ├── PostgresqlDatabaseDriver.cs
│   │   │       ├── Queries\
│   │   │       │   ├── TableQueries.cs
│   │   │       │   └── DynamicTableQueries.cs
│   │   │       └── Implementations\
│   │   │           ├── TenantMethods.cs
│   │   │           ├── UserMethods.cs
│   │   │           ├── CredentialMethods.cs
│   │   │           ├── CollectionMethods.cs
│   │   │           ├── DocumentMethods.cs
│   │   │           ├── LabelMethods.cs
│   │   │           ├── TagMethods.cs
│   │   │           └── SearchMethods.cs
│   │   ├── Enums\
│   │   │   ├── ContentTypeEnum.cs
│   │   │   ├── SearchTypeEnum.cs
│   │   │   ├── SortOrderEnum.cs
│   │   │   ├── TagConditionEnum.cs
│   │   │   └── EnumerationOrderEnum.cs
│   │   ├── Helpers\
│   │   │   ├── IdGenerator.cs
│   │   │   ├── DataTableHelper.cs
│   │   │   └── Serializer.cs
│   │   ├── Models\
│   │   │   ├── TenantMetadata.cs
│   │   │   ├── UserMaster.cs
│   │   │   ├── Credential.cs
│   │   │   ├── CollectionMetadata.cs
│   │   │   ├── DocumentRecord.cs
│   │   │   ├── LabelRecord.cs
│   │   │   ├── TagRecord.cs
│   │   │   ├── EnumerationQuery.cs
│   │   │   ├── EnumerationResult.cs
│   │   │   ├── SearchQuery.cs
│   │   │   ├── SearchResult.cs
│   │   │   ├── LabelFilter.cs
│   │   │   ├── TagFilterSet.cs
│   │   │   ├── TagCondition.cs
│   │   │   └── VectorQuery.cs
│   │   └── Settings\
│   │       ├── ServerSettings.cs
│   │       ├── WebserverSettings.cs
│   │       ├── DatabaseSettings.cs
│   │       ├── LoggingSettings.cs
│   │       ├── DebugSettings.cs
│   │       └── SyslogServer.cs
│   ├── RecallDb.Server\
│   │   ├── RecallDb.Server.csproj
│   │   ├── RecallDbServer.cs
│   │   ├── recalldb.json
│   │   ├── Dockerfile
│   │   ├── Services\
│   │   │   └── AuthenticationService.cs
│   │   └── Classes\
│   │       ├── AuthenticationResult.cs
│   │       ├── RequestContext.cs
│   │       ├── AuthenticateRequest.cs
│   │       └── AuthenticateResponse.cs
│   └── Test.Automated\
│       ├── Test.Automated.csproj
│       └── Program.cs
├── sdk\
│   ├── csharp\
│   │   ├── RecallDb.Sdk\
│   │   │   ├── RecallDb.Sdk.csproj
│   │   │   ├── RecallDbClient.cs
│   │   │   ├── RecallDbException.cs
│   │   │   └── Models\ (mirrors Core models)
│   │   └── RecallDb.Sdk.TestHarness\
│   │       ├── RecallDb.Sdk.TestHarness.csproj
│   │       └── Program.cs
│   ├── python\
│   │   ├── recalldb_sdk.py
│   │   ├── test_harness.py
│   │   └── requirements.txt
│   └── js\
│       ├── recalldb-sdk.js
│       ├── test-harness.js
│       └── package.json
├── dashboard\
│   ├── package.json
│   ├── vite.config.js
│   ├── index.html
│   ├── nginx.conf
│   ├── Dockerfile
│   └── src\
│       ├── main.jsx
│       ├── App.jsx
│       ├── index.css
│       ├── api\
│       │   └── api.js
│       ├── context\
│       │   └── AuthContext.jsx
│       ├── components\
│       │   ├── Sidebar.jsx
│       │   ├── PageHeader.jsx
│       │   ├── DataTable.jsx
│       │   ├── Modal.jsx
│       │   ├── DeleteConfirmModal.jsx
│       │   ├── StatusIndicator.jsx
│       │   ├── CopyableId.jsx
│       │   └── ErrorBanner.jsx
│       └── views\
│           ├── Login.jsx
│           ├── Dashboard.jsx
│           ├── Tenants.jsx
│           ├── Users.jsx
│           ├── Credentials.jsx
│           ├── Collections.jsx
│           ├── Documents.jsx
│           └── Search.jsx
├── docker\
│   ├── compose.yaml
│   └── recalldb.json
├── build-server.bat
├── build-dashboard.bat
├── README.md
├── REST_API.md
├── CHANGELOG.md
├── LICENSE.md
├── .gitignore
└── .dockerignore
```

---

## Phase 1: Solution Structure, Root Files, and Project Scaffolding

**Dependencies:** None (foundation phase)
**Estimated files:** ~12

| Step | Task | Status | Notes |
|------|------|--------|-------|
| 1.1 | Create directory structure: `src/`, `sdk/csharp/`, `sdk/python/`, `sdk/js/`, `dashboard/`, `docker/` | [ ] | |
| 1.2 | Create `src/RecallDb.sln` with projects: RecallDb.Core (classlib), RecallDb.Server (console), Test.Automated (console) | [ ] | |
| 1.3 | Create `src/RecallDb.Core/RecallDb.Core.csproj` -- net10.0, ImplicitUsings=disable, Nullable=disable, GenerateDocumentationFile=true. NuGet: Npgsql (latest), PrettyId (2.0.0), SyslogLogging, Timestamps. Uses System.Text.Json (built-in) for serialization with default PascalCase -- no SerializationHelper, no JsonNamingPolicy, no JsonPropertyName attributes. | [ ] | |
| 1.4 | Create `src/RecallDb.Server/RecallDb.Server.csproj` -- net10.0. NuGet: SwiftStack (latest), SyslogLogging, Inputty. ProjectReference to RecallDb.Core. No SerializationHelper. | [ ] | |
| 1.5 | Create `src/Test.Automated/Test.Automated.csproj` -- net10.0. ProjectReference to RecallDb.Core | [ ] | |
| 1.6 | Create root files: `README.md` (placeholder), `REST_API.md` (placeholder), `CHANGELOG.md`, `LICENSE.md` (MIT), `.gitignore` (ref: `c:\code\conductor\Conductor\.gitignore`), `.dockerignore` | [ ] | |
| 1.7 | Add C# SDK to solution: `sdk/csharp/RecallDb.Sdk/RecallDb.Sdk.csproj` (classlib, net10.0) and `sdk/csharp/RecallDb.Sdk.TestHarness/RecallDb.Sdk.TestHarness.csproj` (console, net10.0) | [ ] | |

**Verification:** `dotnet restore src/RecallDb.sln && dotnet build src/RecallDb.sln` succeeds with no errors.

---

## Phase 2: Core Enums and Settings

**Dependencies:** Phase 1
**Estimated files:** ~11

| Step | Task | Status | Notes |
|------|------|--------|-------|
| 2.1a | Create `src/RecallDb.Core/Enums/ContentTypeEnum.cs` -- Text, List, Table, Binary, Image, Code, Hyperlink, Meta, Unknown. Ref: `c:\code\partio\Partio\src\Partio.Core\Enums\AtomTypeEnum.cs` | [ ] | |
| 2.1b | Create `src/RecallDb.Core/Enums/SearchTypeEnum.cs` -- CosineSimilarity, CosineDistance, EuclideanSimilarity, EuclideanDistance, InnerProduct | [ ] | |
| 2.1c | Create `src/RecallDb.Core/Enums/SortOrderEnum.cs` -- ScoreAscending, ScoreDescending, DistanceAscending, DistanceDescending, CreatedAscending, CreatedDescending | [ ] | |
| 2.1d | Create `src/RecallDb.Core/Enums/TagConditionEnum.cs` -- Equals, NotEquals, GreaterThan, LessThan, Contains, ContainsNot, StartsWith, EndsWith, IsNull, IsNotNull | [ ] | |
| 2.1e | Create `src/RecallDb.Core/Enums/EnumerationOrderEnum.cs` -- CreatedAscending, CreatedDescending | [ ] | |
| 2.2a | Create `src/RecallDb.Core/Settings/ServerSettings.cs` -- Root object: Webserver, Database, Logging, Debug, AdminApiKeys (default `["recalldbadmin"]`). Backing fields `_PascalCase`, setter validation. Ref: `c:\code\conductor\Conductor\src\Conductor.Core\Settings\ServerSettings.cs` | [ ] | |
| 2.2b | Create `src/RecallDb.Core/Settings/WebserverSettings.cs` -- Hostname (default `localhost`), Port (default 8600), Ssl (default false) | [ ] | |
| 2.2c | Create `src/RecallDb.Core/Settings/DatabaseSettings.cs` -- Hostname (localhost), Port (5432), DatabaseName (recalldb), Username (recalldb), Password (recalldb), RequireEncryption (false), LogQueries (false). Method: `GetConnectionString()`. Env var overrides: RECALLDB_DB_HOST, RECALLDB_DB_PORT, RECALLDB_DB_NAME, RECALLDB_DB_USER, RECALLDB_DB_PASS | [ ] | |
| 2.2d | Create `src/RecallDb.Core/Settings/LoggingSettings.cs` -- ConsoleLogging (true), EnableColors (true), MinimumSeverity (0=Debug), LogDirectory (logs/), LogFilename (recalldb.log), Servers (List\<SyslogServer\>) | [ ] | |
| 2.2e | Create `src/RecallDb.Core/Settings/DebugSettings.cs` -- Authentication (false), Exceptions (true), Requests (false), DatabaseQueries (false) | [ ] | |
| 2.2f | Create `src/RecallDb.Core/Settings/SyslogServer.cs` -- Hostname, Port | [ ] | |
| 2.3 | Create `src/RecallDb.Server/recalldb.json` -- default config file with sensible defaults | [ ] | |

**Verification:** Settings round-trip serialize/deserialize to PascalCase JSON (using custom Serializer helper) matching the recalldb.json format.

---

## Phase 3: Core Helpers

**Dependencies:** Phase 2
**Estimated files:** 3

| Step | Task | Status | Notes |
|------|------|--------|-------|
| 3.1 | Create `src/RecallDb.Core/Helpers/IdGenerator.cs` using PrettyId. Static methods: `NewTenantId()` (prefix `ten_`, max 40), `NewUserId()` (`usr_`, 40), `NewCredentialId()` (`cred_`, 40), `NewCollectionId()` (`col_`, 40), `NewLabelId()` (`lbl_`, 40), `NewTagId()` (`tag_`, 40), `NewBearerToken()` (64 chars, cryptographically random alphanumeric). Uses `new PrettyId.IdGenerator().GenerateKSortable(prefix, maxLen)`. Ref: PrettyId source at `c:\code\misc\prettyid\src\PrettyId\PrettyId.cs` | [ ] | |
| 3.2 | Create `src/RecallDb.Core/Helpers/DataTableHelper.cs` -- Static methods: `GetStringValue(DataRow, string)`, `GetBooleanValue`, `GetIntValue`, `GetLongValue`, `GetDateTimeValue`, `GetNullableIntValue`, `GetNullableLongValue`, `GetByteArrayValue` (for BYTEA), `GetFloatArrayValue` (for pgvector vector type parsing). Handle DBNull gracefully. | [ ] | |
| 3.3 | Create `src/RecallDb.Core/Helpers/Serializer.cs` -- Static JSON serialization helper using `System.Text.Json`. Methods: `SerializeJson(object)`, `DeserializeJson<T>(string)`, `SerializeJsonBytes(object)`, `DeserializeJson<T>(byte[])`. Use default `JsonSerializerOptions` with **no** `JsonNamingPolicy` (PascalCase by default), **no** `JsonPropertyName` attributes anywhere in the codebase. Options: `WriteIndented = true`, `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull`. This replaces SerializationHelper throughout. | [ ] | |

**Verification:** `IdGenerator.NewTenantId()` returns string like `ten_1lx4f0h_kL9mT3pR7` of length 40. All DataTableHelper methods handle null/DBNull. `Serializer.SerializeJson(obj)` produces PascalCase JSON with no naming policy overrides.

---

## Phase 4: Core Models

**Dependencies:** Phase 3
**Estimated files:** 7

All models follow the exact pattern from `c:\code\conductor\Conductor\src\Conductor.Core\Models\TenantMetadata.cs`: private backing fields `_PascalCase`, setter validation with `ArgumentNullException`/`ArgumentException`, `FromDataRow(DataRow)`/`FromDataTable(DataTable)` static factory methods, XML documentation on all public members.

| Step | Task | Status | Notes |
|------|------|--------|-------|
| 4.1 | Create `src/RecallDb.Core/Models/TenantMetadata.cs` -- Id (default `IdGenerator.NewTenantId()`), Name (string, required), Active (bool, default true), Labels (List\<string\>), Tags (Dictionary\<string,string\>), CreatedUtc (DateTime), LastUpdateUtc (DateTime). Include `[JsonIgnore]` LabelsJson/TagsJson bridge properties for database serialization. | [ ] | |
| 4.2 | Create `src/RecallDb.Core/Models/UserMaster.cs` -- Id, TenantId, Email, PasswordSha256, FirstName, LastName, IsAdmin (default false), IsTenantAdmin (default false), Active (default true), CreatedUtc, LastUpdateUtc. Methods: `SetPassword(string plaintext)` (SHA256 hash), `VerifyPassword(string plaintext)`. Redact method to mask password. | [ ] | |
| 4.3 | Create `src/RecallDb.Core/Models/Credential.cs` -- Id, TenantId, UserId, BearerToken (default `IdGenerator.NewBearerToken()`), Name, Active (default true), CreatedUtc, LastUpdateUtc | [ ] | |
| 4.4 | Create `src/RecallDb.Core/Models/CollectionMetadata.cs` -- Id, TenantId, Name, Description, Dimensionality (int, validated > 0), Active (default true), CreatedUtc, LastUpdateUtc | [ ] | |
| 4.5 | Create `src/RecallDb.Core/Models/DocumentRecord.cs` -- Id (long, assigned by DB auto-increment), DocumentKey (string, required), DocumentId (string, required), ContentLength (int), Etag (string), Sha256 (string), Position (int, default 0), ContentType (ContentTypeEnum), Content (string, nullable), BinaryData (byte[], nullable), Embeddings (List\<float\>, nullable), CreatedUtc. Transient property: Score (double, for search results, not stored). | [ ] | |
| 4.6 | Create `src/RecallDb.Core/Models/LabelRecord.cs` -- Id (default `IdGenerator.NewLabelId()`), DocumentKey (string, required), DocumentId (string, nullable), Position (int?, nullable), Label (string, required), CreatedUtc | [ ] | |
| 4.7 | Create `src/RecallDb.Core/Models/TagRecord.cs` -- Id (default `IdGenerator.NewTagId()`), DocumentKey (string, required), DocumentId (string, nullable), Position (int?, nullable), Key (string, required), Value (string, nullable), CreatedUtc | [ ] | |

**Verification:** All models compile, serialize/deserialize to JSON, FromDataRow handles DBNull.

---

## Phase 5: Database Interfaces, Base Class, and Query/Result Models

**Dependencies:** Phase 4
**Estimated files:** ~17

### 5A: Database Interfaces

| Step | Task | Status | Notes |
|------|------|--------|-------|
| 5.1 | Create `src/RecallDb.Core/Database/Interfaces/ITenantMethods.cs` -- `Task<TenantMetadata> CreateAsync(TenantMetadata, CancellationToken)`, `Task<TenantMetadata> ReadAsync(string id, CancellationToken)`, `Task<TenantMetadata> UpdateAsync(TenantMetadata, CancellationToken)`, `Task DeleteAsync(string id, CancellationToken)`, `Task<bool> ExistsAsync(string id, CancellationToken)`, `Task<EnumerationResult<TenantMetadata>> EnumerateAsync(EnumerationQuery, CancellationToken)`, `Task<long> GetCountAsync(CancellationToken)` | [ ] | |
| 5.2 | Create `src/RecallDb.Core/Database/Interfaces/IUserMethods.cs` -- Same pattern + `ReadByEmailAsync(string tenantId, string email, CancellationToken)`. All methods scoped by tenantId. | [ ] | |
| 5.3 | Create `src/RecallDb.Core/Database/Interfaces/ICredentialMethods.cs` -- Same pattern + `ReadByBearerTokenAsync(string bearerToken, CancellationToken)` (cross-tenant lookup for auth). Other methods scoped by tenantId. | [ ] | |
| 5.4 | Create `src/RecallDb.Core/Database/Interfaces/ICollectionMethods.cs` -- Same pattern + `ReadByNameAsync(string tenantId, string name, CancellationToken)`. Scoped by tenantId. | [ ] | |
| 5.5 | Create `src/RecallDb.Core/Database/Interfaces/IDocumentMethods.cs` -- `CreateAsync(string collectionId, DocumentRecord)`, `CreateBatchAsync(string collectionId, List<DocumentRecord>)`, `ReadAsync(string collectionId, long id)`, `ReadByDocumentIdAsync(string collectionId, string documentId)` (returns list of all chunks/positions for that documentId), `ReadByDocumentIdAndPositionAsync(string collectionId, string documentId, int position)` (returns single record for chunk lineage), `UpdateAsync(string collectionId, DocumentRecord)`, `DeleteAsync(string collectionId, long id)`, `DeleteByDocumentKeyAsync(string collectionId, string documentKey)`, `ExistsAsync(string collectionId, long id)`, `EnumerateAsync(string collectionId, EnumerationQuery)` | [ ] | |
| 5.6 | Create `src/RecallDb.Core/Database/Interfaces/ILabelMethods.cs` -- `CreateAsync(string collectionId, LabelRecord)`, `ReadAsync(string collectionId, string id)`, `DeleteAsync(string collectionId, string id)`, `EnumerateAsync(string collectionId, EnumerationQuery)`, `EnumerateByDocumentKeyAsync(string collectionId, string documentKey, EnumerationQuery)` | [ ] | |
| 5.7 | Create `src/RecallDb.Core/Database/Interfaces/ITagMethods.cs` -- Same pattern as ILabelMethods | [ ] | |
| 5.8 | Create `src/RecallDb.Core/Database/Interfaces/ISearchMethods.cs` -- `Task<SearchResult> SearchAsync(string collectionId, int dimensionality, SearchQuery query, CancellationToken)` | [ ] | |

### 5B: Database Driver Base

| Step | Task | Status | Notes |
|------|------|--------|-------|
| 5.9 | Create `src/RecallDb.Core/Database/DatabaseDriverBase.cs` -- Abstract class. Properties: `ITenantMethods Tenants`, `IUserMethods Users`, `ICredentialMethods Credentials`, `ICollectionMethods Collections`, `IDocumentMethods Documents`, `ILabelMethods Labels`, `ITagMethods Tags`, `ISearchMethods Search`. Abstract methods: `InitializeAsync(CancellationToken)`, `CreateCollectionTablesAsync(string collectionId, int dimensionality, CancellationToken)`, `DropCollectionTablesAsync(string collectionId, CancellationToken)`, `ExecuteQueryAsync(string, bool, CancellationToken)`, `ExecuteQueriesAsync(IEnumerable<string>, bool, CancellationToken)`, `Sanitize(string)`, `FormatBoolean(bool)`, `FormatDateTime(DateTime)`, `FormatNullableString(string)` | [ ] | |

### 5C: Query and Result Models

| Step | Task | Status | Notes |
|------|------|--------|-------|
| 5.10 | Create `src/RecallDb.Core/Models/EnumerationQuery.cs` -- MaxResults (int, 1-1000, default 100), ContinuationToken (string), Ordering (EnumerationOrderEnum, default CreatedDescending). Static method: `Parse(string maxResults, string skip, string continuationToken, string ordering)` for querystring parsing. Validation method. Ref: `c:\code\verbex\Verbex\src\Verbex.Server\Classes\EnumerationQuery.cs` | [ ] | |
| 5.11 | Create `src/RecallDb.Core/Models/EnumerationResult.cs` -- Generic `EnumerationResult<T>`. Properties: Success (bool), MaxResults (int), ContinuationToken (string), EndOfResults (bool), TotalRecords (long), RecordsRemaining (long), Objects (List\<T\>). Constructor auto-calculates RecordsRemaining, EndOfResults, ContinuationToken from query+data+totalCount. Ref: `c:\code\verbex\Verbex\src\Verbex.Server\Classes\EnumerationResult.cs` | [ ] | |
| 5.12 | Create `src/RecallDb.Core/Models/SearchQuery.cs` -- SortOrder (SortOrderEnum, default ScoreDescending), CreatedBefore (DateTime?), CreatedAfter (DateTime?), DocumentIds (List\<string\>), LabelFilter (LabelFilter), TagFilter (TagFilterSet), Vector (VectorQuery), MaxResults (int, 1-10000, default 100), ContinuationToken (string). Validation: return 400 if no filters supplied. | [ ] | |
| 5.13 | Create `src/RecallDb.Core/Models/LabelFilter.cs` -- Required (List\<string\>), Excluded (List\<string\>) | [ ] | |
| 5.14 | Create `src/RecallDb.Core/Models/TagFilterSet.cs` -- Required (List\<TagCondition\>), Excluded (List\<TagCondition\>) | [ ] | |
| 5.15 | Create `src/RecallDb.Core/Models/TagCondition.cs` -- Key (string, required), Condition (TagConditionEnum), Value (string) | [ ] | |
| 5.16 | Create `src/RecallDb.Core/Models/VectorQuery.cs` -- SearchType (SearchTypeEnum, required), Embeddings (List\<float\>, required), MinimumScore (double?), MaximumScore (double?), MinimumDistance (double?), MaximumDistance (double?) | [ ] | |
| 5.17 | Create `src/RecallDb.Core/Models/SearchResult.cs` -- Similar to EnumerationResult: Success, MaxResults, ContinuationToken, EndOfResults, TotalRecords, RecordsRemaining, Documents (List\<DocumentRecord\> with Score populated) | [ ] | |

**Verification:** All interfaces, base class, and models compile. `dotnet build` succeeds.

---

## Phase 6: PostgreSQL Implementation -- Fixed Tables

**Dependencies:** Phase 5
**Estimated files:** 6

| Step | Task | Status | Notes |
|------|------|--------|-------|
| 6.1 | Create `src/RecallDb.Core/Database/Postgresql/Queries/TableQueries.cs` -- Static class with SQL strings: `CreateExtension` (`CREATE EXTENSION IF NOT EXISTS vector`), `CreateTenantsTable`, `CreateUsersTable`, `CreateCredentialsTable`, `CreateCollectionsTable`. All use `TIMESTAMPTZ(6)`, `VARCHAR(48)` for IDs. Include all indexes: `tenants(created_utc)`, `users(tenant_id, email) UNIQUE`, `users(tenant_id)`, `users(created_utc)`, `credentials(bearer_token) UNIQUE`, `credentials(tenant_id, user_id)`, `credentials(tenant_id)`, `credentials(created_utc)`, `collections(tenant_id, name) UNIQUE`, `collections(tenant_id)`, `collections(created_utc)` | [ ] | |
| 6.2 | Create `src/RecallDb.Core/Database/Postgresql/PostgresqlDatabaseDriver.cs` -- Extends DatabaseDriverBase. Constructor takes DatabaseSettings, builds NpgsqlConnectionString. `InitializeAsync`: creates pgvector extension, then all fixed tables. `ExecuteQueryAsync`/`ExecuteQueriesAsync` using NpgsqlConnection + NpgsqlCommand + NpgsqlDataAdapter. `Sanitize`: escape single quotes. `FormatBoolean`: `TRUE`/`FALSE`. `FormatDateTime`: ISO 8601. Instantiates all implementation classes in constructor. | [ ] | |
| 6.3 | Create `src/RecallDb.Core/Database/Postgresql/Implementations/TenantMethods.cs` -- Full CRUD. Enumerate with `LIMIT`/`OFFSET`, continuation token = base64-encoded skip value. `ORDER BY created_utc DESC` by default. `GetCountAsync` via `SELECT COUNT(*)`. | [ ] | |
| 6.4 | Create `src/RecallDb.Core/Database/Postgresql/Implementations/UserMethods.cs` -- All queries scoped by `tenant_id`. `ReadByEmailAsync`: SELECT WHERE tenant_id = ? AND email = ?. Password stored as SHA256 hex. | [ ] | |
| 6.5 | Create `src/RecallDb.Core/Database/Postgresql/Implementations/CredentialMethods.cs` -- `ReadByBearerTokenAsync`: SELECT WHERE bearer_token = ? (cross-tenant, no tenant_id filter). Other CRUD scoped by tenant_id. | [ ] | |
| 6.6 | Create `src/RecallDb.Core/Database/Postgresql/Implementations/CollectionMethods.cs` -- Standard CRUD scoped by tenant_id. `CreateAsync` additionally calls `_Driver.CreateCollectionTablesAsync(collection.Id, collection.Dimensionality)`. `DeleteAsync` additionally calls `_Driver.DropCollectionTablesAsync(collection.Id)`. | [ ] | |

**Verification:** Against a running pgvector instance: `InitializeAsync` creates pgvector extension + all fixed tables. CRUD on tenants, users, credentials, collections works. Collection creation creates dynamic tables; deletion drops them.

---

## Phase 7: PostgreSQL Implementation -- Dynamic Tables and Search

**Dependencies:** Phase 6
**Estimated files:** 5

| Step | Task | Status | Notes |
|------|------|--------|-------|
| 7.1 | Create `src/RecallDb.Core/Database/Postgresql/Queries/DynamicTableQueries.cs` -- Methods returning SQL strings: `GetCreateCollectionTable(collectionId, dimensionality)` (creates `collection_{id}` with `BIGSERIAL PRIMARY KEY`, all columns including `embeddings vector({dim})`, and HNSW index: `CREATE INDEX ... USING hnsw (embeddings vector_cosine_ops)`), `GetCreateLabelsTable(collectionId)`, `GetCreateTagsTable(collectionId)`, `GetDropCollectionTables(collectionId)` (drops all three). Dynamic table indexes: `collection_{id}(document_key)`, `collection_{id}(document_id)`, `collection_{id}(document_key, document_id)`, `collection_{id}(document_key, position)`, `collection_{id}(content_type)`, `collection_{id}(created_utc)`. Labels: `(document_key, document_id, position, label)`, `(document_key)`, `(label)`, `(created_utc)`. Tags: `(document_key, document_id, position, key)`, `(document_key)`, `(key)`, `(key, value)`, `(created_utc)`. | [ ] | |
| 7.2 | Create `src/RecallDb.Core/Database/Postgresql/Implementations/DocumentMethods.cs` -- `CreateAsync`: INSERT with vector literal formatting `'[0.1,0.2,...]'::vector`. Handle BYTEA for binary_data with parameterized queries or proper escaping. `CreateBatchAsync`: wrap multiple INSERTs in a transaction. `ReadByDocumentIdAsync`: returns List\<DocumentRecord\> (all chunks/positions for that document_id). `ReadByDocumentIdAndPositionAsync`: SELECT WHERE document_id = ? AND position = ? (returns single record for chunk lineage). Enumerate with LIMIT/OFFSET. | [ ] | |
| 7.3 | Create `src/RecallDb.Core/Database/Postgresql/Implementations/LabelMethods.cs` -- CRUD against `collection_{collectionId}_labels`. EnumerateByDocumentKeyAsync filters by document_key. | [ ] | |
| 7.4 | Create `src/RecallDb.Core/Database/Postgresql/Implementations/TagMethods.cs` -- Same pattern as LabelMethods against `collection_{collectionId}_tags`. | [ ] | |
| 7.5 | Create `src/RecallDb.Core/Database/Postgresql/Implementations/SearchMethods.cs` -- The core search engine. Builds dynamic SQL: (1) Base SELECT with distance/score calculation using pgvector operators based on `SearchType`: CosineSimilarity/CosineDistance → `<=>`, EuclideanSimilarity/EuclideanDistance → `<->`, InnerProduct → `<#>`. (2) WHERE clause from: CreatedBefore/After, DocumentIds (IN clause), label filters via EXISTS subquery on labels table (Required = all must exist, Excluded = none may exist), tag filters via EXISTS subquery on tags table with condition evaluation (Equals, GreaterThan, Contains, IsNull, etc.). (3) Score/distance filtering (MinimumScore, MaximumScore, MinimumDistance, MaximumDistance). (4) ORDER BY based on SortOrder enum. (5) LIMIT MaxResults OFFSET from ContinuationToken. Returns 400 if no filters supplied (SearchQuery validation). | [ ] | |

**Verification:** Document CRUD with vector storage works. Batch insert creates all records. Vector search returns results ordered by similarity. All three distance metrics work. Label/tag filtering works in conjunction with vector search. Date range filtering works. MinimumScore filtering works.

---

## Phase 8: Authentication Service and Server Classes

**Dependencies:** Phase 6
**Estimated files:** 5

| Step | Task | Status | Notes |
|------|------|--------|-------|
| 8.1 | Create `src/RecallDb.Server/Services/AuthenticationService.cs` -- Constructor takes DatabaseDriverBase + List\<string\> adminApiKeys. Method: `AuthenticateAsync(HttpContextBase ctx, CancellationToken)`. Flow: (1) Extract `Authorization: Bearer {token}` header. (2) Check if token is in AdminApiKeys → return result with IsAdmin=true (no tenant/user/credential needed). (3) Look up credential by bearer_token → get user → get tenant → validate all active. (4) Build AuthenticationResult. Ref: `c:\code\conductor\Conductor\src\Conductor.Server\Services\AuthenticationService.cs` | [ ] | |
| 8.2 | Create `src/RecallDb.Server/Classes/AuthenticationResult.cs` -- IsAuthenticated (bool), Tenant (TenantMetadata), User (UserMaster), Credential, AuthMethod (string), ErrorMessage (string). Computed properties: `IsAdmin` (AdminApiKey match OR User?.IsAdmin), `IsTenantAdmin` (User?.IsTenantAdmin), `CanManageUsers` (IsAdmin OR IsTenantAdmin), `HasCrossTenantAccess` (IsAdmin) | [ ] | |
| 8.3 | Create `src/RecallDb.Server/Classes/RequestContext.cs` -- RequestId (Guid), ReceivedUtc (DateTime), TenantId, UserId, HttpMethod, OriginalUrl, Path, ClientIpAddress, Data (byte[]). Ref: `c:\code\conductor\Conductor\src\Conductor.Core\Models\RequestContext.cs` | [ ] | |
| 8.4 | Create `src/RecallDb.Server/Classes/AuthenticateRequest.cs` -- BearerToken (string, nullable), TenantId (string, nullable), Email (string, nullable), Password (string, nullable). Both authentication paths in one request object. | [ ] | |
| 8.5 | Create `src/RecallDb.Server/Classes/AuthenticateResponse.cs` -- Success (bool), Tenant (TenantMetadata), User (UserMaster with password redacted), Credential, ErrorMessage | [ ] | |

**Verification:** AdminApiKey auth works. Bearer token auth chain works. Inactive tenant/user/credential rejected. IsAdmin/IsTenantAdmin flags correctly propagated.

---

## Phase 9: REST API Server -- Entry Point and All Routes

**Dependencies:** Phase 7, Phase 8
**Estimated files:** 2 (large files)

| Step | Task | Status | Notes |
|------|------|--------|-------|
| 9.1 | Create `src/RecallDb.Server/RecallDbServer.cs` -- Main entry point. Startup sequence: (1) Welcome banner. (2) Load/create `recalldb.json` with env var overrides (RECALLDB_DB_HOST, etc.). (3) Initialize LoggingModule (SyslogLogging). (4) Initialize PostgresqlDatabaseDriver with DatabaseSettings. (5) Call `InitializeAsync()`. (6) First-run init: if no tenants exist, create default tenant (id=`"default"`, name=`"Default Tenant"`), default user (id=`"default"`, tenant_id=`"default"`, email=`"admin@recall"`, IsAdmin=true, IsTenantAdmin=true, password=hashed "default"), default credential (id=`"default"`, bearer_token=`"default"`). (7) Initialize AuthenticationService with AdminApiKeys. (8) Create SwiftStackApp. (9) Set AuthenticationRoute, PreRoutingRoute (set JSON content type), PostRoutingRoute (log timing), ExceptionRoute (structured error JSON). (10) Register all routes (see 9.2). (11) Start, wait for Ctrl+C. Ref: `c:\code\conductor\Conductor\src\Conductor.Server\ConductorServer.cs` | [ ] | |
| 9.2 | Register all API routes using SwiftStack. See the complete route table below. Each handler: extract AuthenticationResult from `ctx.Metadata`, validate authorization, parse request body/params, call database methods, return JSON response. | [ ] | |

### Complete Route Table

**Unauthenticated:**

| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | Health: returns JSON `{ServerName, Version, UpTimeMs}` |
| HEAD | `/` | Health: no response body, 200 OK |

**Authentication:**

| Method | Path | Description |
|--------|------|-------------|
| POST | `/v1.0/authenticate` | Accepts `AuthenticateRequest` body. If BearerToken provided: validate and return tenant/user/credential. If TenantId+Email+Password provided: authenticate and return. Returns `AuthenticateResponse`. |

**Admin-only (requires AdminApiKey or User.IsAdmin):**

| Method | Path | Description |
|--------|------|-------------|
| GET | `/v1.0/tenants` | Enumerate all tenants (querystring: maxResults, continuationToken, ordering) |
| GET | `/v1.0/tenants/{id}` | Read tenant by ID |
| HEAD | `/v1.0/tenants/{id}` | Check tenant existence |
| POST | `/v1.0/tenants/enumerate` | POST body EnumerationQuery, returns EnumerationResult |
| PUT | `/v1.0/tenants` | Create tenant (body: TenantMetadata). Also creates first user + credential. |
| PUT | `/v1.0/tenants/{id}` | Update tenant |
| DELETE | `/v1.0/tenants/{id}` | Delete tenant + users + credentials (preserves collection tables) |
| DELETE | `/v1.0/tenants/{id}?force` | Force delete: also drops all collection tables |
| GET | `/v1.0/tenants/{tid}/users` | Enumerate users in tenant |
| GET | `/v1.0/tenants/{tid}/users/{uid}` | Read user |
| HEAD | `/v1.0/tenants/{tid}/users/{uid}` | Check user existence |
| POST | `/v1.0/tenants/{tid}/users/enumerate` | POST body EnumerationQuery |
| PUT | `/v1.0/tenants/{tid}/users` | Create user |
| PUT | `/v1.0/tenants/{tid}/users/{uid}` | Update user |
| DELETE | `/v1.0/tenants/{tid}/users/{uid}` | Delete user |
| GET | `/v1.0/tenants/{tid}/credentials` | Enumerate credentials in tenant |
| GET | `/v1.0/tenants/{tid}/credentials/{cid}` | Read credential |
| HEAD | `/v1.0/tenants/{tid}/credentials/{cid}` | Check credential existence |
| POST | `/v1.0/tenants/{tid}/credentials/enumerate` | POST body EnumerationQuery |
| PUT | `/v1.0/tenants/{tid}/credentials` | Create credential |
| PUT | `/v1.0/tenants/{tid}/credentials/{cid}` | Update credential |
| DELETE | `/v1.0/tenants/{tid}/credentials/{cid}` | Delete credential |

**Authenticated (normal user, scoped to own tenant):**

Non-admin users can access the same tenant/user/credential endpoints above but:
- Tenant GET/HEAD/PUT: only their own tenant
- Tenant enumerate: returns only their own tenant
- User/Credential CUD: requires IsTenantAdmin
- User/Credential read/enumerate: within own tenant only

| Method | Path | Description |
|--------|------|-------------|
| PUT | `/v1.0/tenants/{tid}/collections` | Create collection (IsTenantAdmin required). Creates dynamic tables. |
| GET | `/v1.0/tenants/{tid}/collections` | Enumerate collections in tenant |
| GET | `/v1.0/tenants/{tid}/collections/{cid}` | Read collection |
| HEAD | `/v1.0/tenants/{tid}/collections/{cid}` | Check collection existence |
| POST | `/v1.0/tenants/{tid}/collections/enumerate` | POST body EnumerationQuery |
| PUT | `/v1.0/tenants/{tid}/collections/{cid}` | Update collection (IsTenantAdmin; cannot change dimensionality) |
| DELETE | `/v1.0/tenants/{tid}/collections/{cid}` | Delete collection + drop tables (IsTenantAdmin) |
| PUT | `/v1.0/tenants/{tid}/collections/{cid}/documents` | Create single document |
| POST | `/v1.0/tenants/{tid}/collections/{cid}/documents/batch` | Create batch of documents |
| GET | `/v1.0/tenants/{tid}/collections/{cid}/documents` | Enumerate documents |
| GET | `/v1.0/tenants/{tid}/collections/{cid}/documents/{docId}` | Read document(s) by document_id (returns list of all chunks/positions) |
| GET | `/v1.0/tenants/{tid}/collections/{cid}/documents/{docId}/{position}` | Read single document by document_id + position (chunk lineage) |
| HEAD | `/v1.0/tenants/{tid}/collections/{cid}/documents/{docId}` | Check document existence |
| HEAD | `/v1.0/tenants/{tid}/collections/{cid}/documents/{docId}/{position}` | Check specific chunk existence by document_id + position |
| POST | `/v1.0/tenants/{tid}/collections/{cid}/documents/enumerate` | POST body EnumerationQuery |
| PUT | `/v1.0/tenants/{tid}/collections/{cid}/documents/{id}` | Update document (by internal id) |
| DELETE | `/v1.0/tenants/{tid}/collections/{cid}/documents/{id}` | Delete document (by internal id) |
| PUT | `/v1.0/tenants/{tid}/collections/{cid}/labels` | Create label |
| GET | `/v1.0/tenants/{tid}/collections/{cid}/labels` | Enumerate labels |
| GET | `/v1.0/tenants/{tid}/collections/{cid}/labels/{id}` | Read label |
| DELETE | `/v1.0/tenants/{tid}/collections/{cid}/labels/{id}` | Delete label |
| PUT | `/v1.0/tenants/{tid}/collections/{cid}/tags` | Create tag |
| GET | `/v1.0/tenants/{tid}/collections/{cid}/tags` | Enumerate tags |
| GET | `/v1.0/tenants/{tid}/collections/{cid}/tags/{id}` | Read tag |
| DELETE | `/v1.0/tenants/{tid}/collections/{cid}/tags/{id}` | Delete tag |
| POST | `/v1.0/tenants/{tid}/collections/{cid}/search` | Search collection (body: SearchQuery, returns SearchResult) |

**Verification:** Server starts, loads settings, initializes database, creates defaults on first run. Health endpoints respond. Auth works. All CRUD routes function correctly. Search returns scored results. Authorization enforcement works (admin-only, tenant-admin-only, own-tenant-only).

---

## Phase 10: Docker and Deployment

**Dependencies:** Phase 9
**Estimated files:** 7

| Step | Task | Status | Notes |
|------|------|--------|-------|
| 10.1 | Create `src/RecallDb.Server/Dockerfile` -- Multi-stage: build stage `mcr.microsoft.com/dotnet/sdk:10.0` with `$BUILDPLATFORM`/`$TARGETARCH`, runtime stage `mcr.microsoft.com/dotnet/aspnet:10.0`. Install: `iputils-ping traceroute wget curl dnsutils net-tools netcat-openbsd vim-tiny iproute2 procps libkrb5-3` (libkrb5-3 for Npgsql). Create /app/data and /app/logs. Expose 8600. ENTRYPOINT `["dotnet", "RecallDb.Server.dll"]`. Ref: `c:\code\conductor\Conductor\src\Conductor.Server\Dockerfile` | [ ] | |
| 10.2 | Create `docker/compose.yaml` -- Three services: (1) `recalldb-server` (image: jchristn77/recalldb-server, port 8600, env vars inline: DOTNET_RUNNING_IN_CONTAINER, RECALLDB_DB_HOST=pgvector, RECALLDB_DB_PORT=5432, RECALLDB_DB_NAME=recalldb, RECALLDB_DB_USER=recalldb, RECALLDB_DB_PASS=recalldb, **volumes: `./recalldb.json:/app/recalldb.json:ro`** (host-mounted settings file), depends on pgvector healthy, healthcheck curl). (2) `pgvector` (image: ankane/pgvector:latest, port 5432, env vars inline: POSTGRES_DB, POSTGRES_USER, POSTGRES_PASSWORD, volume pgvector-data, healthcheck pg_isready). (3) `recalldb-dashboard` (image: jchristn77/recalldb-dashboard, port 8601, depends on recalldb-server healthy). No .env files. | [ ] | |
| 10.2a | Create `docker/recalldb.json` -- Default settings file for host-mounted use with Docker Compose. Same structure as `src/RecallDb.Server/recalldb.json` but with database hostname set to `pgvector` (container name) and any other Docker-appropriate defaults. This file is mounted read-only into the container at `/app/recalldb.json`. | [ ] | |
| 10.3 | Create `build-server.bat` -- Takes image tag as CLI arg. Uses `docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 -t jchristn77/recalldb-server:%TAG% -t jchristn77/recalldb-server:latest -f src/RecallDb.Server/Dockerfile --push .` Ref: `c:\code\conductor\Conductor\build-server.bat` | [ ] | |
| 10.4 | Create `build-dashboard.bat` -- Same pattern: `jchristn77/recalldb-dashboard`, from dashboard/ directory. | [ ] | |

**Verification:** `docker compose up` from docker/ brings up pgvector + recalldb-server + dashboard. API reachable at localhost:8600. Healthchecks pass.

---

## Phase 11: Test.Automated

**Dependencies:** Phase 9
**Estimated files:** 1 (large)

| Step | Task | Status | Notes |
|------|------|--------|-------|
| 11.1 | Create `src/Test.Automated/Program.cs` -- Console app accepting CLI args: endpoint URL and API key. Test framework with pass/fail/timing per test. | [ ] | |

**Test categories and cases:**

```
1. CONNECTIVITY
   - GET / returns health JSON with server name, version, uptime
   - HEAD / returns 200 with no body

2. AUTHENTICATION
   - POST /v1.0/authenticate with bearer token → success
   - POST /v1.0/authenticate with email+password → success
   - POST /v1.0/authenticate with invalid token → failure

3. TENANT CRUD
   - PUT /v1.0/tenants → create tenant (returns tenant + user + credential)
   - GET /v1.0/tenants → enumerate (includes new tenant)
   - GET /v1.0/tenants/{id} → read
   - HEAD /v1.0/tenants/{id} → exists (200)
   - HEAD /v1.0/tenants/{nonexistent} → not found (404)
   - PUT /v1.0/tenants/{id} → update name
   - POST /v1.0/tenants/enumerate → POST enumeration
   - DELETE /v1.0/tenants/{id} → delete (preserves collection tables)
   - DELETE /v1.0/tenants/{id}?force → force delete

4. USER CRUD
   - PUT /v1.0/tenants/{tid}/users → create
   - GET /v1.0/tenants/{tid}/users → enumerate
   - GET /v1.0/tenants/{tid}/users/{uid} → read
   - PUT /v1.0/tenants/{tid}/users/{uid} → update
   - DELETE /v1.0/tenants/{tid}/users/{uid} → delete

5. CREDENTIAL CRUD
   - PUT /v1.0/tenants/{tid}/credentials → create
   - GET /v1.0/tenants/{tid}/credentials → enumerate
   - GET /v1.0/tenants/{tid}/credentials/{cid} → read
   - DELETE /v1.0/tenants/{tid}/credentials/{cid} → delete

6. COLLECTION CRUD
   - PUT /v1.0/tenants/{tid}/collections → create (dimensionality=384)
   - GET /v1.0/tenants/{tid}/collections → enumerate
   - GET /v1.0/tenants/{tid}/collections/{cid} → read
   - HEAD /v1.0/tenants/{tid}/collections/{cid} → exists
   - PUT /v1.0/tenants/{tid}/collections/{cid} → update
   - DELETE /v1.0/tenants/{tid}/collections/{cid} → delete (drops tables)

7. DOCUMENT CRUD (SINGLETON)
   - PUT .../documents → create with embeddings (384-dim)
   - GET .../documents/{docId} → read (verify embeddings returned, returns list of chunks)
   - GET .../documents/{docId}/{position} → read specific chunk by document_id + position (chunk lineage)
   - HEAD .../documents/{docId}/{position} → check specific chunk existence
   - PUT .../documents/{id} → update content
   - DELETE .../documents/{id} → delete

8. DOCUMENT CRUD (BATCH)
   - POST .../documents/batch → create 10 documents
   - GET .../documents → enumerate (verify count)

9. LABEL CRUD
   - PUT .../labels → create label on document
   - GET .../labels → enumerate
   - DELETE .../labels/{id} → delete

10. TAG CRUD
    - PUT .../tags → create tag on document
    - GET .../tags → enumerate
    - DELETE .../tags/{id} → delete

11. VECTOR SEARCH
    - POST .../search with CosineSimilarity → verify ordering
    - POST .../search with EuclideanDistance → verify ordering
    - POST .../search with InnerProduct → verify ordering

12. SEARCH WITH FILTERS
    - Search with label Required filter
    - Search with label Excluded filter
    - Search with tag Equals condition
    - Search with tag Contains condition
    - Search with CreatedBefore/After
    - Search with DocumentIds filter
    - Search with MinimumScore
    - Search with no filters → verify 400

13. ENUMERATION PAGINATION
    - Create 25 records, paginate with MaxResults=10
    - Verify ContinuationToken, HasMore, TotalRecords across pages

14. AUTHORIZATION
    - Non-admin user cannot create tenant → 401/403
    - Non-tenant-admin cannot create collection → 401/403

15. CLEANUP
    - Delete all test data created during tests
```

**Output format:**
```
=== RecallDB Automated Tests ===
Endpoint: http://localhost:8600
API Key:  recalldbadmin

--- CONNECTIVITY ---
  [PASS] GET / health check (12ms)
  [PASS] HEAD / health check (8ms)

--- AUTHENTICATION ---
  [PASS] Authenticate with bearer token (45ms)
  ...

=== TEST SUMMARY ===
  Total:  52
  Passed: 51
  Failed: 1
  Runtime: 3.2s

  FAILED TESTS:
  - Search with InnerProduct: Expected score > 0, got -0.5

  OVERALL: FAIL
```

**Verification:** All tests pass against a running server with pgvector.

---

## Phase 12: SDKs

**Dependencies:** Phase 9 (API must be finalized)
**Estimated files:** ~17

### 12A: C# SDK

| Step | Task | Status | Notes |
|------|------|--------|-------|
| 12.1 | Create `sdk/csharp/RecallDb.Sdk/RecallDb.Sdk.csproj` -- classlib, net10.0. NuGet: System.Text.Json | [ ] | |
| 12.2 | Create `sdk/csharp/RecallDb.Sdk/RecallDbClient.cs` -- Constructor(endpoint, bearerToken), IDisposable (owns HttpClient). Methods: Authenticate, Tenant CRUD, User CRUD, Credential CRUD, Collection CRUD, Document CRUD + batch, Label CRUD, Tag CRUD, Search. Each method builds HTTP request, sends, deserializes response. | [ ] | |
| 12.3 | Create `sdk/csharp/RecallDb.Sdk/RecallDbException.cs` -- Custom exception with StatusCode, ErrorMessage | [ ] | |
| 12.4 | Create SDK model classes mirroring Core models (simplified, no FromDataRow) | [ ] | |
| 12.5 | Create `sdk/csharp/RecallDb.Sdk.TestHarness/Program.cs` -- Console app accepting endpoint + API key as CLI args. Runs through all SDK operations with PASS/FAIL output. | [ ] | |

### 12B: Python SDK

| Step | Task | Status | Notes |
|------|------|--------|-------|
| 12.6 | Create `sdk/python/recalldb_sdk.py` -- RecallDbClient class using `requests`. Full API coverage. | [ ] | |
| 12.7 | Create `sdk/python/test_harness.py` -- Accepts `--endpoint` and `--token` CLI args. Runs all operations. | [ ] | |
| 12.8 | Create `sdk/python/requirements.txt` -- `requests` | [ ] | |

### 12C: JavaScript SDK

| Step | Task | Status | Notes |
|------|------|--------|-------|
| 12.9 | Create `sdk/js/recalldb-sdk.js` -- RecallDbClient class using fetch. Full API coverage. | [ ] | |
| 12.10 | Create `sdk/js/test-harness.js` -- Accepts endpoint and token from CLI. Runs all operations. | [ ] | |
| 12.11 | Create `sdk/js/package.json` -- name: recalldb-sdk | [ ] | |

**Verification:** Each SDK test harness passes against a running server.

---

## Phase 13: React Dashboard

**Dependencies:** Phase 9
**Estimated files:** ~20+

| Step | Task | Status | Notes |
|------|------|--------|-------|
| 13.1 | Create `dashboard/package.json` -- React 19, react-router-dom 7, Vite 6 | [ ] | |
| 13.2 | Create `dashboard/vite.config.js` and `dashboard/index.html` | [ ] | |
| 13.3 | Create `dashboard/nginx.conf` -- Listen port 8601, SPA routing (`try_files $uri $uri/ /index.html`), API proxy `/v1.0/` → `http://recalldb-server:8600`, security headers, static asset caching. Ref: `c:\code\conductor\Conductor\dashboard\nginx.conf` | [ ] | |
| 13.4 | Create `dashboard/Dockerfile` -- Build: `node:20-alpine`, npm ci, npm run build. Runtime: `nginx:alpine`, install troubleshooting tools (iputils, bind-tools, curl, wget, vim, net-tools), copy dist + nginx.conf, expose 8601. Ref: `c:\code\conductor\Conductor\dashboard\Dockerfile` | [ ] | |
| 13.5 | Create `dashboard/src/main.jsx`, `src/App.jsx` (router setup), `src/index.css` (global styles) | [ ] | |
| 13.6 | Create `dashboard/src/api/api.js` -- fetch wrapper that injects Authorization: Bearer header, handles JSON parse, error mapping | [ ] | |
| 13.7 | Create `dashboard/src/context/AuthContext.jsx` -- React context for auth state (bearerToken, tenant, user), login/logout functions | [ ] | |
| 13.8 | Create shared components: Sidebar, PageHeader, DataTable (sortable, paginated), Modal, DeleteConfirmModal, StatusIndicator (active/inactive badge), CopyableId (click-to-copy), ErrorBanner | [ ] | |
| 13.9 | Create views: Login (bearer token or email/password), Dashboard (overview cards with tenant/user/collection/document counts), Tenants (list + create/edit/delete), Users (list + create/edit/delete), Credentials (list + create/delete, show bearer token once), Collections (list + create/edit/delete, show dimensionality), Documents (list within collection, create/edit/delete, show embeddings preview), Search (search form with filters, results table with scores) | [ ] | |

**Verification:** `npm run dev` starts dashboard. Login with bearer token or email+password works. All CRUD views render and interact with the API. Search view submits queries and displays scored results.

---

## Phase 14: Documentation

**Dependencies:** All previous phases
**Estimated files:** 3 (updates to existing placeholders)

| Step | Task | Status | Notes |
|------|------|--------|-------|
| 14.1 | Complete `REST_API.md` -- Full API documentation: all endpoints with HTTP methods, request/response JSON examples, authentication header format (`Authorization: Bearer {token}`), error response format, SearchQuery JSON examples with all filter types, EnumerationQuery/EnumerationResult JSON examples | [ ] | |
| 14.2 | Complete `README.md` -- Project description, quick start with Docker Compose, configuration reference (recalldb.json structure), environment variable overrides, SDK quick start examples (C#, Python, JS), API overview | [ ] | |
| 14.3 | Complete `CHANGELOG.md` -- v0.1.0 initial release notes | [ ] | |

**Verification:** Documentation accurately reflects the running system. Docker Compose quick start works from a clean checkout.

---

## Phase 15: Post-Build Index Review

**Dependencies:** All previous phases (code complete)
**Estimated files:** 0-2 (modifications only)

| Step | Task | Status | Notes |
|------|------|--------|-------|
| 15.1 | Review all SQL queries in `SearchMethods.cs` -- map each WHERE clause and JOIN to existing indexes, identify any missing compound indexes | [ ] | |
| 15.2 | Review all SQL queries in enumerate methods across TenantMethods, UserMethods, CredentialMethods, CollectionMethods, DocumentMethods, LabelMethods, TagMethods -- verify indexes cover ORDER BY + WHERE patterns | [ ] | |
| 15.3 | Review HNSW index parameters -- evaluate whether `ef_construction` and `m` parameters should be tunable per collection, or if defaults are sufficient | [ ] | |
| 15.4 | Add any missing indexes identified from the query pattern review to `TableQueries.cs` and `DynamicTableQueries.cs` | [ ] | |
| 15.5 | Test query plans using `EXPLAIN ANALYZE` on representative queries against a populated database to confirm index usage | [ ] | |

**Verification:** All SearchMethods queries use indexes (confirmed via EXPLAIN ANALYZE). No sequential scans on large tables.

---

## Critical Reference Files

| Pattern | Reference File |
|---------|---------------|
| Server entry point, SwiftStack routes, first-run init | `c:\code\conductor\Conductor\src\Conductor.Server\ConductorServer.cs` |
| ServerSettings with AdminApiKeys, backing fields | `c:\code\conductor\Conductor\src\Conductor.Core\Settings\ServerSettings.cs` |
| Auth service (AdminApiKeys + bearer token chain) | `c:\code\conductor\Conductor\src\Conductor.Server\Services\AuthenticationService.cs` |
| RequestContext / UrlContext | `c:\code\conductor\Conductor\src\Conductor.Core\Models\RequestContext.cs` |
| Model pattern (backing fields, FromDataRow, XML docs) | `c:\code\conductor\Conductor\src\Conductor.Core\Models\TenantMetadata.cs` |
| PostgreSQL driver pattern (InitializeAsync, ExecuteQuery) | Conductor `Database\PostgreSql\PostgreSqlDatabaseDriver.cs` |
| EnumerationQuery (parse from querystring, validation) | `c:\code\verbex\Verbex\src\Verbex.Server\Classes\EnumerationQuery.cs` |
| EnumerationResult (auto-calc continuation token, records remaining) | `c:\code\verbex\Verbex\src\Verbex.Server\Classes\EnumerationResult.cs` |
| Content types (atom types) | `c:\code\partio\Partio\src\Partio.Core\Enums\AtomTypeEnum.cs` |
| PrettyId K-sortable generation | `c:\code\misc\prettyid\src\PrettyId\PrettyId.cs` -- `GenerateKSortable(prefix, maxLen)` |
| SwiftStack route registration, auth hooks, AppRequest | `c:\code\swiftstack\src\SwiftStack\Rest\RestApp.cs` |
| SwiftStack AuthResult/AuthenticationResultEnum | `c:\code\swiftstack\src\SwiftStack\Rest\AuthResult.cs` |
| Dockerfile with troubleshooting tools (apt-get) | `c:\code\conductor\Conductor\src\Conductor.Server\Dockerfile` |
| Docker compose pattern (healthchecks, depends_on) | `c:\code\conductor\Conductor\docker\compose.yaml` |
| Build scripts (buildx cloud-jchristn77-jchristn77, multi-arch) | `c:\code\conductor\Conductor\build-server.bat` |
| Dashboard (React SPA + nginx + Dockerfile) | `c:\code\conductor\Conductor\dashboard\` |
| SDK structure (C#, Python, JS) | `c:\code\verbex\Verbex\sdk\` |
| Test harness pattern (PASS/FAIL, timing, summary) | `c:\code\verbex\Verbex\sdk\csharp\Verbex.Sdk.TestHarness\` |
| .gitignore patterns | `c:\code\conductor\Conductor\.gitignore` |

---

## Estimated Totals

| Phase | Description | Files | Depends On |
|-------|-------------|-------|------------|
| 1 | Solution Structure & Scaffolding | ~12 | None |
| 2 | Core Enums & Settings | ~11 | Phase 1 |
| 3 | Core Helpers | 3 | Phase 2 |
| 4 | Core Models | 7 | Phase 3 |
| 5 | Database Interfaces & Base Class & Query Models | ~17 | Phase 4 |
| 6 | PostgreSQL -- Fixed Tables | 6 | Phase 5 |
| 7 | PostgreSQL -- Dynamic Tables & Search | 5 | Phase 6 |
| 8 | Authentication Service & Server Classes | 5 | Phase 6 |
| 9 | REST API Server & All Routes | 2 | Phase 7, 8 |
| 10 | Docker & Deployment | 7 | Phase 9 |
| 11 | Test.Automated | 1 | Phase 9 |
| 12 | SDKs (C#, Python, JS) | ~17 | Phase 9 |
| 13 | React Dashboard | ~20+ | Phase 9 |
| 14 | Documentation | 3 | All |
| 15 | Post-Build Index Review | 0-2 | All |
| **Total** | | **~115-120** | |

**Parallelization:** Phases 10, 11, 12, 13 can all be worked in parallel once Phase 9 is complete. Within Phases 1-9, the sequence is strictly linear.
