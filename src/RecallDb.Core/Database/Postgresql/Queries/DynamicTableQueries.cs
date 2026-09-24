namespace RecallDb.Core.Database.Postgresql.Queries
{
    /// <summary>
    /// SQL queries for creating and managing dynamic per-collection tables.
    /// </summary>
    public static class DynamicTableQueries
    {
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

        private static string SanitizeTableName(string collectionId)
        {
            if (string.IsNullOrEmpty(collectionId)) return "unknown";
            return collectionId.Replace("-", "_").Replace(".", "_");
        }

        private static string GetIndexIdentifier(string collectionId)
        {
            string sanitized = SanitizeTableName(collectionId);

            if (sanitized.StartsWith("col_") && sanitized.Length > 4)
            {
                int nextUnderscore = sanitized.IndexOf('_', 4);
                if (nextUnderscore > 4)
                {
                    return sanitized.Substring(4, nextUnderscore - 4);
                }
            }

            if (sanitized.Length <= 8) return sanitized;
            return sanitized.Substring(sanitized.Length - 8);
        }

        #endregion
    }
}
