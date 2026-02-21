-- ============================================================================
-- RecallDB Factory Database Initialization Script
-- Creates schema, indices, and default records matching a clean first run.
-- ============================================================================

-- Extensions
CREATE EXTENSION IF NOT EXISTS vector;
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- ============================================================================
-- Fixed tables
-- ============================================================================

-- Tenants
CREATE TABLE IF NOT EXISTS tenants (
    id VARCHAR(48) NOT NULL PRIMARY KEY,
    name VARCHAR(256) NOT NULL,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    labels_json TEXT,
    tags_json TEXT,
    created_utc TIMESTAMPTZ(6) NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
    last_update_utc TIMESTAMPTZ(6) NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC')
);
CREATE INDEX IF NOT EXISTS idx_tenants_name ON tenants (name);
CREATE INDEX IF NOT EXISTS idx_tenants_created_utc ON tenants (created_utc);

-- Users
CREATE TABLE IF NOT EXISTS users (
    id VARCHAR(48) NOT NULL PRIMARY KEY,
    tenant_id VARCHAR(48) NOT NULL,
    email VARCHAR(256) NOT NULL,
    password_sha256 VARCHAR(64),
    first_name VARCHAR(128),
    last_name VARCHAR(128),
    is_admin BOOLEAN NOT NULL DEFAULT FALSE,
    is_tenant_admin BOOLEAN NOT NULL DEFAULT FALSE,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    created_utc TIMESTAMPTZ(6) NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
    last_update_utc TIMESTAMPTZ(6) NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC')
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_users_tenant_email ON users (tenant_id, email);
CREATE INDEX IF NOT EXISTS idx_users_tenant_id ON users (tenant_id);
CREATE INDEX IF NOT EXISTS idx_users_created_utc ON users (created_utc);

-- Credentials
CREATE TABLE IF NOT EXISTS credentials (
    id VARCHAR(48) NOT NULL PRIMARY KEY,
    tenant_id VARCHAR(48) NOT NULL,
    user_id VARCHAR(48) NOT NULL,
    bearer_token VARCHAR(64) NOT NULL,
    name VARCHAR(256),
    active BOOLEAN NOT NULL DEFAULT TRUE,
    created_utc TIMESTAMPTZ(6) NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
    last_update_utc TIMESTAMPTZ(6) NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC')
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_credentials_bearer_token ON credentials (bearer_token);
CREATE INDEX IF NOT EXISTS idx_credentials_tenant_user ON credentials (tenant_id, user_id);
CREATE INDEX IF NOT EXISTS idx_credentials_tenant_id ON credentials (tenant_id);
CREATE INDEX IF NOT EXISTS idx_credentials_created_utc ON credentials (created_utc);

-- Collections
CREATE TABLE IF NOT EXISTS collections (
    id VARCHAR(48) NOT NULL PRIMARY KEY,
    tenant_id VARCHAR(48) NOT NULL,
    name VARCHAR(256) NOT NULL,
    description TEXT,
    dimensionality INTEGER NOT NULL,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    created_utc TIMESTAMPTZ(6) NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
    last_update_utc TIMESTAMPTZ(6) NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC')
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_collections_tenant_name ON collections (tenant_id, name);
CREATE INDEX IF NOT EXISTS idx_collections_tenant_id ON collections (tenant_id);
CREATE INDEX IF NOT EXISTS idx_collections_created_utc ON collections (created_utc);

-- ============================================================================
-- Dynamic per-collection tables for the default collection
-- ============================================================================

-- Documents table (collection_default)
CREATE TABLE IF NOT EXISTS collection_default (
    id BIGSERIAL PRIMARY KEY,
    document_key VARCHAR(256) NOT NULL,
    document_id VARCHAR(256),
    content_length BIGINT NOT NULL DEFAULT 0,
    etag VARCHAR(64),
    sha256 VARCHAR(64),
    position INTEGER NOT NULL DEFAULT 0,
    content_type VARCHAR(32) NOT NULL DEFAULT 'Text',
    content TEXT,
    binary_data BYTEA,
    embeddings vector(384),
    created_utc TIMESTAMPTZ(6) NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC')
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_col_default_dkey ON collection_default (document_key);
CREATE INDEX IF NOT EXISTS idx_col_default_did ON collection_default (document_id);
CREATE INDEX IF NOT EXISTS idx_col_default_didp ON collection_default (document_id, position);
CREATE INDEX IF NOT EXISTS idx_col_default_crt ON collection_default (created_utc);
CREATE INDEX IF NOT EXISTS idx_col_default_hnsw ON collection_default USING hnsw (embeddings vector_cosine_ops) WITH (m = 16, ef_construction = 64);
CREATE INDEX IF NOT EXISTS idx_col_default_trgm ON collection_default USING gin (content gin_trgm_ops);

-- Labels table (collection_default_labels)
CREATE TABLE IF NOT EXISTS collection_default_labels (
    id VARCHAR(48) NOT NULL PRIMARY KEY,
    document_key VARCHAR(256) NOT NULL,
    document_id VARCHAR(256),
    position INTEGER,
    label VARCHAR(256) NOT NULL,
    created_utc TIMESTAMPTZ(6) NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC')
);
CREATE INDEX IF NOT EXISTS idx_col_default_l_dkey ON collection_default_labels (document_key);
CREATE INDEX IF NOT EXISTS idx_col_default_l_did ON collection_default_labels (document_id);
CREATE INDEX IF NOT EXISTS idx_col_default_l_lbl ON collection_default_labels (label);
CREATE INDEX IF NOT EXISTS idx_col_default_l_crt ON collection_default_labels (created_utc);

-- Tags table (collection_default_tags)
CREATE TABLE IF NOT EXISTS collection_default_tags (
    id VARCHAR(48) NOT NULL PRIMARY KEY,
    document_key VARCHAR(256) NOT NULL,
    document_id VARCHAR(256),
    position INTEGER,
    key VARCHAR(256) NOT NULL,
    value TEXT,
    created_utc TIMESTAMPTZ(6) NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC')
);
CREATE INDEX IF NOT EXISTS idx_col_default_t_dkey ON collection_default_tags (document_key);
CREATE INDEX IF NOT EXISTS idx_col_default_t_did ON collection_default_tags (document_id);
CREATE INDEX IF NOT EXISTS idx_col_default_t_key ON collection_default_tags (key);
CREATE INDEX IF NOT EXISTS idx_col_default_t_kv ON collection_default_tags (key, value);
CREATE INDEX IF NOT EXISTS idx_col_default_t_crt ON collection_default_tags (created_utc);

-- ============================================================================
-- Default records (matching InitializeFirstRunAsync)
-- ============================================================================

-- Default tenant
INSERT INTO tenants (id, name, active)
VALUES ('default', 'Default Tenant', TRUE)
ON CONFLICT (id) DO NOTHING;

-- Default user (password: "password", SHA256: 5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8)
INSERT INTO users (id, tenant_id, email, password_sha256, first_name, last_name, is_admin, is_tenant_admin, active)
VALUES ('default', 'default', 'admin@recall', '5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8', 'Admin', 'User', TRUE, TRUE, TRUE)
ON CONFLICT (id) DO NOTHING;

-- Default credential (bearer token: "default")
INSERT INTO credentials (id, tenant_id, user_id, bearer_token, name, active)
VALUES ('default', 'default', 'default', 'default', 'Default API Key', TRUE)
ON CONFLICT (id) DO NOTHING;

-- Default collection (384 dimensions)
INSERT INTO collections (id, tenant_id, name, dimensionality, active)
VALUES ('default', 'default', 'Default Collection', 384, TRUE)
ON CONFLICT (id) DO NOTHING;
