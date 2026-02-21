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
                "created_utc TIMESTAMPTZ(6) NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC')" +
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
                "CREATE INDEX IF NOT EXISTS idx_col_" + ixId + "_trgm ON collection_" + tableName + " USING gin (content gin_trgm_ops);",
                "CREATE INDEX IF NOT EXISTS idx_col_" + ixId + "_fts ON collection_" + tableName + " USING gin (to_tsvector('english', COALESCE(content, '')));"
            };
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
