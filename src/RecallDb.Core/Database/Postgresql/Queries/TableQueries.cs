namespace RecallDb.Core.Database.Postgresql.Queries
{
    /// <summary>
    /// SQL queries for creating and managing fixed database tables.
    /// </summary>
    public static class TableQueries
    {
        /// <summary>
        /// Create the pgvector extension.
        /// </summary>
        public static readonly string CreateVectorExtension =
            "CREATE EXTENSION IF NOT EXISTS vector;";

        /// <summary>
        /// Create the pg_trgm extension for trigram indexing.
        /// </summary>
        public static readonly string CreateTrgmExtension =
            "CREATE EXTENSION IF NOT EXISTS pg_trgm;";

        /// <summary>
        /// Create the tenants table.
        /// </summary>
        public static readonly string CreateTenantsTable =
            "CREATE TABLE IF NOT EXISTS tenants (" +
            "id VARCHAR(48) NOT NULL PRIMARY KEY, " +
            "name VARCHAR(256) NOT NULL, " +
            "active BOOLEAN NOT NULL DEFAULT TRUE, " +
            "labels_json TEXT, " +
            "tags_json TEXT, " +
            "created_utc TIMESTAMPTZ(6) NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'), " +
            "last_update_utc TIMESTAMPTZ(6) NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC')" +
            ");";

        /// <summary>
        /// Create the tenants name index.
        /// </summary>
        public static readonly string CreateTenantsNameIndex =
            "CREATE INDEX IF NOT EXISTS idx_tenants_name ON tenants (name);";

        /// <summary>
        /// Create the tenants created index.
        /// </summary>
        public static readonly string CreateTenantsCreatedIndex =
            "CREATE INDEX IF NOT EXISTS idx_tenants_created_utc ON tenants (created_utc);";

        /// <summary>
        /// Create the users table.
        /// </summary>
        public static readonly string CreateUsersTable =
            "CREATE TABLE IF NOT EXISTS users (" +
            "id VARCHAR(48) NOT NULL PRIMARY KEY, " +
            "tenant_id VARCHAR(48) NOT NULL, " +
            "email VARCHAR(256) NOT NULL, " +
            "password_sha256 VARCHAR(64), " +
            "first_name VARCHAR(128), " +
            "last_name VARCHAR(128), " +
            "is_admin BOOLEAN NOT NULL DEFAULT FALSE, " +
            "is_tenant_admin BOOLEAN NOT NULL DEFAULT FALSE, " +
            "active BOOLEAN NOT NULL DEFAULT TRUE, " +
            "created_utc TIMESTAMPTZ(6) NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'), " +
            "last_update_utc TIMESTAMPTZ(6) NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC')" +
            ");";

        /// <summary>
        /// Create the users tenant_id + email unique index.
        /// </summary>
        public static readonly string CreateUsersTenantEmailIndex =
            "CREATE UNIQUE INDEX IF NOT EXISTS idx_users_tenant_email ON users (tenant_id, email);";

        /// <summary>
        /// Create the users tenant_id index.
        /// </summary>
        public static readonly string CreateUsersTenantIndex =
            "CREATE INDEX IF NOT EXISTS idx_users_tenant_id ON users (tenant_id);";

        /// <summary>
        /// Create the users created index.
        /// </summary>
        public static readonly string CreateUsersCreatedIndex =
            "CREATE INDEX IF NOT EXISTS idx_users_created_utc ON users (created_utc);";

        /// <summary>
        /// Create the credentials table.
        /// </summary>
        public static readonly string CreateCredentialsTable =
            "CREATE TABLE IF NOT EXISTS credentials (" +
            "id VARCHAR(48) NOT NULL PRIMARY KEY, " +
            "tenant_id VARCHAR(48) NOT NULL, " +
            "user_id VARCHAR(48) NOT NULL, " +
            "bearer_token VARCHAR(64) NOT NULL, " +
            "name VARCHAR(256), " +
            "active BOOLEAN NOT NULL DEFAULT TRUE, " +
            "created_utc TIMESTAMPTZ(6) NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'), " +
            "last_update_utc TIMESTAMPTZ(6) NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC')" +
            ");";

        /// <summary>
        /// Create the credentials bearer_token unique index.
        /// </summary>
        public static readonly string CreateCredentialsBearerTokenIndex =
            "CREATE UNIQUE INDEX IF NOT EXISTS idx_credentials_bearer_token ON credentials (bearer_token);";

        /// <summary>
        /// Create the credentials tenant_id + user_id index.
        /// </summary>
        public static readonly string CreateCredentialsTenantUserIndex =
            "CREATE INDEX IF NOT EXISTS idx_credentials_tenant_user ON credentials (tenant_id, user_id);";

        /// <summary>
        /// Create the credentials tenant_id index.
        /// </summary>
        public static readonly string CreateCredentialsTenantIndex =
            "CREATE INDEX IF NOT EXISTS idx_credentials_tenant_id ON credentials (tenant_id);";

        /// <summary>
        /// Create the credentials created index.
        /// </summary>
        public static readonly string CreateCredentialsCreatedIndex =
            "CREATE INDEX IF NOT EXISTS idx_credentials_created_utc ON credentials (created_utc);";

        /// <summary>
        /// Create the collections table.
        /// </summary>
        public static readonly string CreateCollectionsTable =
            "CREATE TABLE IF NOT EXISTS collections (" +
            "id VARCHAR(48) NOT NULL PRIMARY KEY, " +
            "tenant_id VARCHAR(48) NOT NULL, " +
            "name VARCHAR(256) NOT NULL, " +
            "description TEXT, " +
            "dimensionality INTEGER NOT NULL, " +
            "active BOOLEAN NOT NULL DEFAULT TRUE, " +
            "created_utc TIMESTAMPTZ(6) NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'), " +
            "last_update_utc TIMESTAMPTZ(6) NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC')" +
            ");";

        /// <summary>
        /// Create the collections tenant_id + name unique index.
        /// </summary>
        public static readonly string CreateCollectionsTenantNameIndex =
            "CREATE UNIQUE INDEX IF NOT EXISTS idx_collections_tenant_name ON collections (tenant_id, name);";

        /// <summary>
        /// Create the collections tenant_id index.
        /// </summary>
        public static readonly string CreateCollectionsTenantIndex =
            "CREATE INDEX IF NOT EXISTS idx_collections_tenant_id ON collections (tenant_id);";

        /// <summary>
        /// Create the collections created index.
        /// </summary>
        public static readonly string CreateCollectionsCreatedIndex =
            "CREATE INDEX IF NOT EXISTS idx_collections_created_utc ON collections (created_utc);";
    }
}
