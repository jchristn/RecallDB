namespace RecallDb.Core.Database.Postgresql.Queries
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// SQL queries for creating and managing dynamic per-collection tables.
    /// </summary>
    public static class DynamicTableQueries
    {
        /// <summary>
        /// Maximum length of the per-collection index identifier. PostgreSQL truncates identifiers to 63 bytes
        /// (NAMEDATALEN - 1); the longest framing built around the identifier is "idx_col_" (8) + identifier +
        /// "_l_dkey"/"_t_dkey" (7), so the identifier may be up to 48 characters and stay within the limit.
        /// </summary>
        public const int MaxIndexIdentifierLength = 48;

        private static readonly Regex _ValidResourceId = new Regex("^[A-Za-z0-9_]+$", RegexOptions.Compiled);
        private static readonly Regex _CreateIndexNameAndTable = new Regex(
            @"IF NOT EXISTS (?<name>\S+) ON (?<table>\S+)", RegexOptions.Compiled);
        /// <summary>
        /// Get the SQL to create a collection documents table.
        /// </summary>
        /// <param name="collectionId">Collection ID (used as table name suffix).</param>
        /// <param name="dimensionality">Vector dimensionality.</param>
        /// <returns>SQL query string.</returns>
        public static string GetCreateCollectionTable(string collectionId, int dimensionality)
        {
            string tableName = SanitizeTableName(collectionId);
            return
                "CREATE TABLE IF NOT EXISTS collection_" + tableName + " (" +
                "id BIGSERIAL PRIMARY KEY, " +
                "document_key VARCHAR(256) NOT NULL, " +
                "document_id VARCHAR(256), " +
                "content_length BIGINT NOT NULL DEFAULT 0, " +
                "etag VARCHAR(64), " +
                "sha256 VARCHAR(64), " +
                "position INTEGER NOT NULL DEFAULT 0, " +
                "content_type VARCHAR(32) NOT NULL DEFAULT 'Text', " +
                "content TEXT, " +
                "binary_data BYTEA, " +
                "embeddings vector(" + dimensionality + "), " +
                "created_utc TIMESTAMPTZ(6) NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'), " +
                GetStoredTsVectorColumnDefinition() +
                ");";
        }

        /// <summary>
        /// Get the SQL to create indexes on a collection documents table.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <returns>Array of SQL query strings.</returns>
        public static string[] GetCreateCollectionIndexes(string collectionId)
        {
            string tableName = SanitizeTableName(collectionId);
            string ixId = GetIndexIdentifier(collectionId);
            return new string[]
            {
                "CREATE UNIQUE INDEX IF NOT EXISTS idx_col_" + ixId + "_dkey ON collection_" + tableName + " (document_key);",
                "CREATE INDEX IF NOT EXISTS idx_col_" + ixId + "_did ON collection_" + tableName + " (document_id);",
                "CREATE INDEX IF NOT EXISTS idx_col_" + ixId + "_didp ON collection_" + tableName + " (document_id, position);",
                "CREATE INDEX IF NOT EXISTS idx_col_" + ixId + "_crt ON collection_" + tableName + " (created_utc);",
                "CREATE INDEX IF NOT EXISTS idx_col_" + ixId + "_hnsw ON collection_" + tableName + " USING hnsw (embeddings vector_cosine_ops) WITH (m = 16, ef_construction = 64);",
                "CREATE INDEX IF NOT EXISTS idx_col_" + ixId + "_trgm ON collection_" + tableName + " USING gin (content gin_trgm_ops);"
            };
        }

        /// <summary>
        /// Get the SQL to add the stored, generated content_tsv column to an existing collection documents table.
        /// Idempotent (ADD COLUMN IF NOT EXISTS). On a table that lacks the column this rewrites the table while
        /// holding an ACCESS EXCLUSIVE lock.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <returns>SQL query string.</returns>
        public static string GetAddStoredTsVectorColumn(string collectionId)
        {
            string tableName = SanitizeTableName(collectionId);
            return "ALTER TABLE collection_" + tableName + " ADD COLUMN IF NOT EXISTS " + GetStoredTsVectorColumnDefinition() + ";";
        }

        /// <summary>
        /// Get the SQL to create the GIN index on the stored content_tsv column.
        /// The column must exist.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <returns>SQL query string.</returns>
        public static string GetCreateStoredTsVectorIndex(string collectionId)
        {
            string tableName = SanitizeTableName(collectionId);
            string ixId = GetIndexIdentifier(collectionId);
            return "CREATE INDEX IF NOT EXISTS idx_col_" + ixId + "_tsv ON collection_" + tableName + " USING gin (content_tsv);";
        }

        /// <summary>
        /// Get the SQL to create the legacy GIN expression index on to_tsvector('english', content).
        /// Used only for collections that do not yet have the stored content_tsv column (when the startup
        /// migration is disabled), so full-text search keeps an index.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <returns>SQL query string.</returns>
        public static string GetCreateLegacyFullTextIndex(string collectionId)
        {
            string tableName = SanitizeTableName(collectionId);
            string ixId = GetIndexIdentifier(collectionId);
            return "CREATE INDEX IF NOT EXISTS idx_col_" + ixId + "_fts ON collection_" + tableName + " USING gin (to_tsvector('english', COALESCE(content, '')));";
        }

        /// <summary>
        /// Get the SQL to drop the legacy GIN expression index, which the content_tsv index supersedes.
        /// Run only after the content_tsv index exists, so there is never a window without a text index.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <returns>SQL query string.</returns>
        public static string GetDropLegacyFullTextIndex(string collectionId)
        {
            string ixId = GetIndexIdentifier(collectionId);
            return "DROP INDEX IF EXISTS idx_col_" + ixId + "_fts;";
        }

        /// <summary>
        /// Get the SQL that returns one row when the collection documents table has the content_tsv column.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <returns>SQL query string.</returns>
        public static string GetStoredTsVectorColumnExists(string collectionId)
        {
            string tableName = SanitizeTableName(collectionId);
            return
                "SELECT 1 AS present FROM information_schema.columns " +
                "WHERE table_schema = current_schema() " +
                "AND table_name = 'collection_" + tableName.ToLowerInvariant() + "' " +
                "AND column_name = 'content_tsv';";
        }

        /// <summary>
        /// Get the SQL that returns the planner's row estimate for a collection documents table
        /// (pg_class.reltuples; cheap, approximate, and -1 or 0 for a table never analyzed).
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <returns>SQL query string.</returns>
        public static string GetEstimatedRowCount(string collectionId)
        {
            string tableName = SanitizeTableName(collectionId);
            return
                "SELECT reltuples::bigint AS estimate FROM pg_class " +
                "WHERE oid = to_regclass('collection_" + tableName + "');";
        }

        /// <summary>
        /// Get the SQL to create a collection labels table.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <returns>SQL query string.</returns>
        public static string GetCreateLabelsTable(string collectionId)
        {
            string tableName = SanitizeTableName(collectionId);
            return
                "CREATE TABLE IF NOT EXISTS collection_" + tableName + "_labels (" +
                "id VARCHAR(48) NOT NULL PRIMARY KEY, " +
                "document_key VARCHAR(256) NOT NULL, " +
                "document_id VARCHAR(256), " +
                "position INTEGER, " +
                "label VARCHAR(256) NOT NULL, " +
                "created_utc TIMESTAMPTZ(6) NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC')" +
                ");";
        }

        /// <summary>
        /// Get the SQL to create indexes on a collection labels table.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <returns>Array of SQL query strings.</returns>
        public static string[] GetCreateLabelsIndexes(string collectionId)
        {
            string tableName = SanitizeTableName(collectionId);
            string ixId = GetIndexIdentifier(collectionId);
            return new string[]
            {
                "CREATE INDEX IF NOT EXISTS idx_col_" + ixId + "_l_dkey ON collection_" + tableName + "_labels (document_key);",
                "CREATE INDEX IF NOT EXISTS idx_col_" + ixId + "_l_did ON collection_" + tableName + "_labels (document_id);",
                "CREATE INDEX IF NOT EXISTS idx_col_" + ixId + "_l_lbl ON collection_" + tableName + "_labels (label);",
                "CREATE INDEX IF NOT EXISTS idx_col_" + ixId + "_l_crt ON collection_" + tableName + "_labels (created_utc);"
            };
        }

        /// <summary>
        /// Get the SQL to create a collection tags table.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <returns>SQL query string.</returns>
        public static string GetCreateTagsTable(string collectionId)
        {
            string tableName = SanitizeTableName(collectionId);
            return
                "CREATE TABLE IF NOT EXISTS collection_" + tableName + "_tags (" +
                "id VARCHAR(48) NOT NULL PRIMARY KEY, " +
                "document_key VARCHAR(256) NOT NULL, " +
                "document_id VARCHAR(256), " +
                "position INTEGER, " +
                "key VARCHAR(256) NOT NULL, " +
                "value TEXT, " +
                "created_utc TIMESTAMPTZ(6) NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC')" +
                ");";
        }

        /// <summary>
        /// Get the SQL to create indexes on a collection tags table.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <returns>Array of SQL query strings.</returns>
        public static string[] GetCreateTagsIndexes(string collectionId)
        {
            string tableName = SanitizeTableName(collectionId);
            string ixId = GetIndexIdentifier(collectionId);
            return new string[]
            {
                "CREATE INDEX IF NOT EXISTS idx_col_" + ixId + "_t_dkey ON collection_" + tableName + "_tags (document_key);",
                "CREATE INDEX IF NOT EXISTS idx_col_" + ixId + "_t_did ON collection_" + tableName + "_tags (document_id);",
                "CREATE INDEX IF NOT EXISTS idx_col_" + ixId + "_t_key ON collection_" + tableName + "_tags (key);",
                "CREATE INDEX IF NOT EXISTS idx_col_" + ixId + "_t_kv ON collection_" + tableName + "_tags (key, value);",
                "CREATE INDEX IF NOT EXISTS idx_col_" + ixId + "_t_crt ON collection_" + tableName + "_tags (created_utc);"
            };
        }

        /// <summary>
        /// Get the SQL to drop all tables for a collection.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <returns>Array of SQL query strings.</returns>
        public static string[] GetDropCollectionTables(string collectionId)
        {
            string tableName = SanitizeTableName(collectionId);
            return new string[]
            {
                "DROP TABLE IF EXISTS collection_" + tableName + "_tags CASCADE;",
                "DROP TABLE IF EXISTS collection_" + tableName + "_labels CASCADE;",
                "DROP TABLE IF EXISTS collection_" + tableName + " CASCADE;"
            };
        }

        #region Private-Methods

        private static string GetStoredTsVectorColumnDefinition()
        {
            // The index language is fixed to english; other text search configurations are evaluated through
            // the unindexed to_tsvector expression path at query time.
            return "content_tsv tsvector GENERATED ALWAYS AS (to_tsvector('english', COALESCE(content, ''))) STORED";
        }

        /// <summary>
        /// Sanitize a collection id into the suffix used to build its table names. Hyphens and dots map to
        /// underscores; any other character means the id could break out of an identifier in a DDL/DML statement,
        /// so it is rejected rather than silently altered (silent alteration could also collapse two distinct ids
        /// onto one table name). Valid server-generated and client-supplied ids contain only [A-Za-z0-9_].
        /// </summary>
        /// <param name="collectionId">Collection id.</param>
        /// <returns>Table-name-safe suffix.</returns>
        /// <exception cref="ArgumentException">Thrown when the id contains characters outside [A-Za-z0-9_-.].</exception>
        public static string SanitizeTableName(string collectionId)
        {
            if (string.IsNullOrEmpty(collectionId)) return "unknown";
            string sanitized = collectionId.Replace("-", "_").Replace(".", "_");
            if (!_ValidResourceId.IsMatch(sanitized))
                throw new ArgumentException("Collection id contains characters that are not allowed in an identifier: " + collectionId, nameof(collectionId));
            return sanitized;
        }

        /// <summary>
        /// Whether a resource id is safe to embed in an identifier and to route through a URL: 1 to 48 characters
        /// of [A-Za-z0-9_] (hyphen and dot are also accepted because they are mapped to underscore for table names).
        /// </summary>
        /// <param name="id">Resource id.</param>
        /// <returns>True when the id is valid.</returns>
        public static bool IsValidResourceId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (id.Length > MaxIndexIdentifierLength) return false;
            return _ValidResourceId.IsMatch(id.Replace("-", "_").Replace(".", "_"));
        }

        /// <summary>
        /// Derive the per-collection index identifier from the whole collection id, so two collections created in
        /// the same millisecond (whose ids share a timestamp component) never produce colliding index names.
        /// The full sanitized, lower-cased id is used when it fits within <see cref="MaxIndexIdentifierLength"/>;
        /// a longer id (client-supplied, or a future longer format) falls back to a deterministic short hash that
        /// keeps every generated name within PostgreSQL's 63-byte limit. Lower-casing matches the table names,
        /// which are already folded to lower case by unquoted-identifier rules.
        /// </summary>
        /// <param name="collectionId">Collection id.</param>
        /// <returns>Collision-free index identifier.</returns>
        public static string GetIndexIdentifier(string collectionId)
        {
            string sanitized = SanitizeTableName(collectionId).ToLowerInvariant();
            if (sanitized.Length <= MaxIndexIdentifierLength) return sanitized;

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(collectionId));
            return "h" + Convert.ToHexString(hash, 0, 12).ToLowerInvariant();
        }

        #endregion

        #region Index-Specs

        /// <summary>
        /// Describes a single per-collection index: its name under the current (full-id) scheme, the table it is on,
        /// the suffix that identifies its role, the CREATE statement, and whether it is unique.
        /// </summary>
        public sealed class CollectionIndexSpec
        {
            /// <summary>Index name.</summary>
            public string Name { get; set; }
            /// <summary>Table the index is on.</summary>
            public string Table { get; set; }
            /// <summary>Suffix identifying the index's role (e.g. dkey, hnsw, l_dkey, t_kv).</summary>
            public string Suffix { get; set; }
            /// <summary>CREATE statement (idempotent, IF NOT EXISTS).</summary>
            public string CreateSql { get; set; }
            /// <summary>Whether the index is unique.</summary>
            public bool IsUnique { get; set; }
        }

        /// <summary>
        /// The full set of indexes a healthy collection should have, under the current (full-id) naming scheme.
        /// The CREATE statements are the same ones used at creation, so this is the single source of truth for the
        /// startup repair (which renames or builds missing indexes and drops leftovers).
        /// </summary>
        /// <param name="collectionId">Collection id.</param>
        /// <param name="includeTsv">Include the stored content_tsv GIN index.</param>
        /// <param name="includeLegacyFts">Include the legacy expression full-text index (used when there is no content_tsv column).</param>
        /// <returns>Index specifications.</returns>
        public static List<CollectionIndexSpec> GetExpectedIndexes(string collectionId, bool includeTsv, bool includeLegacyFts)
        {
            string ixId = GetIndexIdentifier(collectionId);
            string prefix = "idx_col_" + ixId + "_";

            List<string> statements = new List<string>();
            statements.AddRange(GetCreateCollectionIndexes(collectionId));
            if (includeTsv) statements.Add(GetCreateStoredTsVectorIndex(collectionId));
            if (includeLegacyFts) statements.Add(GetCreateLegacyFullTextIndex(collectionId));
            statements.AddRange(GetCreateLabelsIndexes(collectionId));
            statements.AddRange(GetCreateTagsIndexes(collectionId));

            List<CollectionIndexSpec> specs = new List<CollectionIndexSpec>();
            foreach (string statement in statements)
            {
                Match match = _CreateIndexNameAndTable.Match(statement);
                if (!match.Success) continue;
                string name = match.Groups["name"].Value;
                // PostgreSQL folds unquoted identifiers to lower case, so the catalog stores the table name in lower
                // case; use that canonical form so catalog lookups by table name match.
                string table = match.Groups["table"].Value.ToLowerInvariant();
                string suffix = name.StartsWith(prefix, StringComparison.Ordinal) ? name.Substring(prefix.Length) : name;
                specs.Add(new CollectionIndexSpec
                {
                    Name = name,
                    Table = table,
                    Suffix = suffix,
                    CreateSql = statement,
                    IsUnique = statement.IndexOf("UNIQUE", StringComparison.Ordinal) >= 0
                });
            }
            return specs;
        }

        #endregion
    }
}
