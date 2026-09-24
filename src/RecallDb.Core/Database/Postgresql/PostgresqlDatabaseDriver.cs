namespace RecallDb.Core.Database.Postgresql
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Npgsql;
    using SyslogLogging;
    using RecallDb.Core.Database.Interfaces;
    using RecallDb.Core.Database.Postgresql.Implementations;
    using RecallDb.Core.Database.Postgresql.Queries;
    using RecallDb.Core.Observability;
    using RecallDb.Core.Settings;

    /// <summary>
    /// PostgreSQL database driver with pgvector support.
    /// </summary>
    public class PostgresqlDatabaseDriver : DatabaseDriverBase
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private readonly string _ConnectionString;
        private readonly DatabaseSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[PostgresqlDriver] ";
        private readonly ConcurrentDictionary<string, bool> _StoredTsVectorCache = new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);
        private List<string> _TextSearchConfigurations = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <param name="logging">Logging module.</param>
        public PostgresqlDatabaseDriver(DatabaseSettings settings, LoggingModule logging)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _Settings = settings;
            _Logging = logging;
            _ConnectionString = settings.GetConnectionString();

            Tenants = new TenantMethods(this, _Logging);
            Users = new UserMethods(this, _Logging);
            Credentials = new CredentialMethods(this, _Logging);
            Collections = new CollectionMethods(this, _Logging);
            Documents = new DocumentMethods(this, _Logging);
            Labels = new LabelMethods(this, _Logging);
            Tags = new TagMethods(this, _Logging);
            Search = new SearchMethods(this, _Logging);
            RequestHistory = new RequestHistoryMethods(this, _Logging);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Initialize the database.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public override async Task InitializeAsync(CancellationToken token = default)
        {
            List<string> queries = new List<string>
            {
                TableQueries.CreateVectorExtension,
                TableQueries.CreateTrgmExtension,
                TableQueries.CreateTenantsTable,
                TableQueries.CreateTenantsNameIndex,
                TableQueries.CreateTenantsCreatedIndex,
                TableQueries.CreateUsersTable,
                TableQueries.CreateUsersTenantEmailIndex,
                TableQueries.CreateUsersTenantIndex,
                TableQueries.CreateUsersCreatedIndex,
                TableQueries.CreateCredentialsTable,
                TableQueries.CreateCredentialsBearerTokenIndex,
                TableQueries.CreateCredentialsTenantUserIndex,
                TableQueries.CreateCredentialsTenantIndex,
                TableQueries.CreateCredentialsCreatedIndex,
                TableQueries.CreateCollectionsTable,
                TableQueries.CreateCollectionsTenantNameIndex,
                TableQueries.CreateCollectionsTenantIndex,
                TableQueries.CreateCollectionsCreatedIndex,
                TableQueries.CreateRequestHistoryTable,
                TableQueries.CreateRequestHistoryGuidIndex,
                TableQueries.CreateRequestHistoryCreatedIndex,
                TableQueries.CreateRequestHistoryMethodCreatedIndex,
                TableQueries.CreateRequestHistoryStatusCreatedIndex,
                TableQueries.CreateRequestHistorySuccessCreatedIndex,
                // Migrations
                TableQueries.MigrateAddRequestBody,
                TableQueries.MigrateAddResponseBody
            };

            await ExecuteQueriesAsync(queries, false, token).ConfigureAwait(false);
            await ListTextSearchConfigurationsAsync(token).ConfigureAwait(false);
            if (_Logging != null) _Logging.Info(_Header + "database initialized");
        }

        /// <summary>
        /// Create dynamic tables for a collection.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="dimensionality">Vector dimensionality.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public override async Task CreateCollectionTablesAsync(string collectionId, int dimensionality, CancellationToken token = default)
        {
            await CreateCollectionTablesInternalAsync(collectionId, dimensionality, true, token).ConfigureAwait(false);
            if (_Logging != null) _Logging.Info(_Header + "created collection tables for " + collectionId);
        }

        /// <summary>
        /// Ensure the tables and indexes for every existing collection are present. Re-runs the
        /// idempotent CREATE TABLE / CREATE INDEX IF NOT EXISTS statements for each collection so
        /// that collections created before an index was introduced (e.g. the HNSW vector index and
        /// GIN full-text index) acquire it without a manual migration. When
        /// DatabaseSettings.MigrateFullTextColumn is true, collections without the stored content_tsv
        /// column gain it (a table rewrite; progress and timing are logged per collection), its GIN index is
        /// built, and the superseded expression index is dropped. Best-effort: a failure on one
        /// collection is logged and does not abort the others or server startup.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public override async Task EnsureAllCollectionSchemasAsync(CancellationToken token = default)
        {
            DataTable result = await ExecuteQueryAsync("SELECT id, dimensionality FROM collections", false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count == 0) return;

            int ensured = 0;

            foreach (DataRow row in result.Rows)
            {
                if (row["id"] == null || row["id"] == DBNull.Value) continue;
                string collectionId = row["id"].ToString();
                int dimensionality = row["dimensionality"] == DBNull.Value ? 0 : Convert.ToInt32(row["dimensionality"]);
                if (string.IsNullOrEmpty(collectionId) || dimensionality <= 0) continue;

                try
                {
                    await CreateCollectionTablesInternalAsync(collectionId, dimensionality, _Settings.MigrateFullTextColumn, token).ConfigureAwait(false);
                    ensured++;
                }
                catch (Exception e)
                {
                    if (_Logging != null) _Logging.Warn(_Header + "unable to ensure schema for collection " + collectionId + ": " + e.Message);
                }
            }

            if (_Logging != null) _Logging.Info(_Header + "ensured schema for " + ensured + " collection(s)");
        }

        /// <summary>
        /// Drop dynamic tables for a collection.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public override async Task DropCollectionTablesAsync(string collectionId, CancellationToken token = default)
        {
            string[] dropQueries = DynamicTableQueries.GetDropCollectionTables(collectionId);
            List<string> queries = new List<string>(dropQueries);
            await ExecuteQueriesAsync(queries, false, token).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(collectionId)) _StoredTsVectorCache.TryRemove(collectionId, out bool _);
            if (_Logging != null) _Logging.Info(_Header + "dropped collection tables for " + collectionId);
        }

        /// <summary>
        /// List the text search configurations installed in the database (pg_ts_config), used as the
        /// allowlist for FullTextQuery.Language. Loaded once and cached for the lifetime of the driver;
        /// thread-safe (concurrent first calls may each query, and the last result wins).
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Lower-case configuration names; never null.</returns>
        public override async Task<List<string>> ListTextSearchConfigurationsAsync(CancellationToken token = default)
        {
            List<string> cached = Volatile.Read(ref _TextSearchConfigurations);
            if (cached != null) return new List<string>(cached);

            DataTable result = await ExecuteQueryAsync("SELECT cfgname FROM pg_ts_config ORDER BY cfgname", false, token).ConfigureAwait(false);
            List<string> names = new List<string>();
            if (result != null)
            {
                foreach (DataRow row in result.Rows)
                {
                    if (row["cfgname"] == null || row["cfgname"] == DBNull.Value) continue;
                    string name = row["cfgname"].ToString();
                    if (!string.IsNullOrEmpty(name)) names.Add(name.ToLowerInvariant());
                }
            }

            names = names.Distinct().ToList();
            Volatile.Write(ref _TextSearchConfigurations, names);
            return new List<string>(names);
        }

        /// <summary>
        /// Determine whether a collection's documents table has the stored, indexed content_tsv column.
        /// The answer is cached per collection; the cache is refreshed by the startup schema pass and by
        /// collection creation, and cleared when a collection's tables are dropped. Thread-safe.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when the column exists.</returns>
        /// <exception cref="ArgumentNullException">Thrown when collectionId is null or empty.</exception>
        public override async Task<bool> HasStoredTsVectorAsync(string collectionId, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (_StoredTsVectorCache.TryGetValue(collectionId, out bool cached)) return cached;
            return await RefreshStoredTsVectorAsync(collectionId, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Execute a SQL query.
        /// </summary>
        /// <param name="query">SQL query.</param>
        /// <param name="isTransaction">Whether to wrap in a transaction.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>DataTable with results.</returns>
        public override async Task<DataTable> ExecuteQueryAsync(string query, bool isTransaction = false, CancellationToken token = default)
        {
            return await ExecuteQueryAsync(query, null, isTransaction, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Execute a SQL query after running setup statements on the same connection and transaction.
        /// Intended for transaction-scoped settings such as "SET LOCAL hnsw.ef_search = 200" that must apply to
        /// the query and nothing else. When setup statements are supplied the query always runs in a transaction,
        /// so SET LOCAL values are discarded when it ends.
        /// </summary>
        /// <param name="query">SQL query.</param>
        /// <param name="setupStatements">Statements executed, in order, before the query. Null or empty for none.</param>
        /// <param name="isTransaction">Whether to wrap in a transaction (forced to true when setup statements are supplied).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>DataTable with results.</returns>
        /// <exception cref="ArgumentNullException">Thrown when query is null or empty.</exception>
        public async Task<DataTable> ExecuteQueryAsync(string query, IList<string> setupStatements, bool isTransaction, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(query)) throw new ArgumentNullException(nameof(query));

            bool hasSetup = setupStatements != null && setupStatements.Any(st => !string.IsNullOrEmpty(st));
            if (hasSetup) isTransaction = true;

            if (_Settings.LogQueries && _Logging != null)
            {
                if (hasSetup) _Logging.Debug(_Header + "setup: " + string.Join(" ", setupStatements));
                _Logging.Debug(_Header + "query: " + query);
            }

            string dbOperation = RecallDbTelemetry.DeriveSqlOperation(query);
            long telemetryStart = Stopwatch.GetTimestamp();
            string telemetryOutcome = "success";
            int telemetryRows = -1;
            RecallDbTelemetry.DbActiveQueries.Add(1, new TagList { { RecallDbTelemetry.TagDbOperation, dbOperation } });

            using Activity dbActivity = RecallDbTelemetry.ActivitySource.StartActivity("db " + dbOperation, ActivityKind.Client);
            if (dbActivity != null)
            {
                dbActivity.SetTag("db.system", "postgresql");
                dbActivity.SetTag(RecallDbTelemetry.TagDbOperation, dbOperation);
                dbActivity.SetTag(RecallDbTelemetry.TagTransaction, isTransaction);
            }

            try
            {
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                NpgsqlTransaction transaction = null;

                try
                {
                    if (isTransaction)
                        transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);

                    if (hasSetup)
                    {
                        foreach (string setup in setupStatements)
                        {
                            if (string.IsNullOrEmpty(setup)) continue;
                            using (NpgsqlCommand setupCommand = new NpgsqlCommand(setup, connection))
                            {
                                setupCommand.Transaction = transaction;
                                await setupCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                            }
                        }
                    }

                    using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                    {
                        if (transaction != null)
                            command.Transaction = transaction;

                        DataTable result = new DataTable();

                        using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                        {
                            // Build columns manually to handle types unsupported by DataTable.Load
                            // (e.g. pgvector 'vector' columns cannot be read as System.Object)
                            bool[] isUnsupportedType = new bool[reader.FieldCount];
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string colName = reader.GetName(i);
                                Type colType;
                                try
                                {
                                    colType = reader.GetFieldType(i);
                                }
                                catch (InvalidCastException)
                                {
                                    colType = typeof(string);
                                    isUnsupportedType[i] = true;
                                }
                                result.Columns.Add(colName, colType);
                            }

                            while (await reader.ReadAsync(token).ConfigureAwait(false))
                            {
                                DataRow row = result.NewRow();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    if (reader.IsDBNull(i))
                                    {
                                        row[i] = DBNull.Value;
                                    }
                                    else if (isUnsupportedType[i])
                                    {
                                        row[i] = DBNull.Value;
                                    }
                                    else
                                    {
                                        row[i] = reader.GetValue(i);
                                    }
                                }
                                result.Rows.Add(row);
                            }
                        }

                        if (transaction != null)
                            await transaction.CommitAsync(token).ConfigureAwait(false);

                        telemetryRows = result.Rows.Count;
                        return result;
                    }
                }
                catch
                {
                    if (transaction != null)
                        await transaction.RollbackAsync(token).ConfigureAwait(false);
                    throw;
                }
                finally
                {
                    if (transaction != null)
                        transaction.Dispose();
                }
            }
            }
            catch (Exception telemetryException)
            {
                telemetryOutcome = "error";
                RecallDbTelemetry.RecordException(dbActivity, telemetryException);
                throw;
            }
            finally
            {
                double seconds = Stopwatch.GetElapsedTime(telemetryStart).TotalSeconds;
                TagList tags = new TagList { { RecallDbTelemetry.TagDbOperation, dbOperation }, { RecallDbTelemetry.TagOutcome, telemetryOutcome } };
                RecallDbTelemetry.DbQueryDuration.Record(seconds, tags);
                RecallDbTelemetry.DbQueries.Add(1, tags);
                RecallDbTelemetry.DbActiveQueries.Add(-1, new TagList { { RecallDbTelemetry.TagDbOperation, dbOperation } });
                if (telemetryRows >= 0)
                    RecallDbTelemetry.DbRowsReturned.Record(telemetryRows, new TagList { { RecallDbTelemetry.TagDbOperation, dbOperation } });
            }
        }

        /// <summary>
        /// Execute multiple SQL queries.
        /// </summary>
        /// <param name="queries">SQL queries.</param>
        /// <param name="isTransaction">Whether to wrap in a transaction.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public override async Task ExecuteQueriesAsync(IEnumerable<string> queries, bool isTransaction = false, CancellationToken token = default)
        {
            await ExecuteQueriesAsync(queries, isTransaction, null, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Execute multiple SQL queries with an explicit per-command timeout. Used for schema maintenance
        /// (table rewrites and index builds on large collections) that can legitimately run far longer than the
        /// default command timeout.
        /// </summary>
        /// <param name="queries">SQL queries.</param>
        /// <param name="isTransaction">Whether to wrap in a transaction.</param>
        /// <param name="commandTimeoutSeconds">Per-command timeout in seconds; 0 means no limit; null keeps the Npgsql default (30 seconds).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">Thrown when queries is null.</exception>
        public async Task ExecuteQueriesAsync(IEnumerable<string> queries, bool isTransaction, int? commandTimeoutSeconds, CancellationToken token = default)
        {
            if (queries == null) throw new ArgumentNullException(nameof(queries));

            long telemetryStart = Stopwatch.GetTimestamp();
            string telemetryOutcome = "success";
            int telemetryCount = 0;
            RecallDbTelemetry.DbActiveQueries.Add(1, new TagList { { RecallDbTelemetry.TagDbOperation, "batch" } });

            using Activity dbActivity = RecallDbTelemetry.ActivitySource.StartActivity("db batch", ActivityKind.Client);
            if (dbActivity != null)
            {
                dbActivity.SetTag("db.system", "postgresql");
                dbActivity.SetTag(RecallDbTelemetry.TagDbOperation, "batch");
                dbActivity.SetTag(RecallDbTelemetry.TagTransaction, isTransaction);
            }

            try
            {
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                NpgsqlTransaction transaction = null;

                try
                {
                    if (isTransaction)
                        transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);

                    foreach (string query in queries)
                    {
                        if (string.IsNullOrEmpty(query)) continue;

                        if (_Settings.LogQueries && _Logging != null)
                            _Logging.Debug(_Header + "query: " + query);

                        using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                        {
                            if (transaction != null)
                                command.Transaction = transaction;

                            if (commandTimeoutSeconds.HasValue)
                                command.CommandTimeout = commandTimeoutSeconds.Value;

                            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                            telemetryCount++;
                        }
                    }

                    if (transaction != null)
                        await transaction.CommitAsync(token).ConfigureAwait(false);
                }
                catch
                {
                    if (transaction != null)
                        await transaction.RollbackAsync(token).ConfigureAwait(false);
                    throw;
                }
                finally
                {
                    if (transaction != null)
                        transaction.Dispose();
                }
            }
            }
            catch (Exception telemetryException)
            {
                telemetryOutcome = "error";
                RecallDbTelemetry.RecordException(dbActivity, telemetryException);
                throw;
            }
            finally
            {
                double seconds = Stopwatch.GetElapsedTime(telemetryStart).TotalSeconds;
                if (dbActivity != null) dbActivity.SetTag("db.batch.size", telemetryCount);
                TagList tags = new TagList { { RecallDbTelemetry.TagDbOperation, "batch" }, { RecallDbTelemetry.TagOutcome, telemetryOutcome } };
                RecallDbTelemetry.DbQueryDuration.Record(seconds, tags);
                RecallDbTelemetry.DbQueries.Add(telemetryCount > 0 ? telemetryCount : 1, tags);
                RecallDbTelemetry.DbActiveQueries.Add(-1, new TagList { { RecallDbTelemetry.TagDbOperation, "batch" } });
            }
        }

        /// <summary>
        /// Sanitize a string for SQL.
        /// </summary>
        /// <param name="value">Input string.</param>
        /// <returns>Sanitized string.</returns>
        public override string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Replace("'", "''");
        }

        /// <summary>
        /// Format a boolean for PostgreSQL.
        /// </summary>
        /// <param name="value">Boolean value.</param>
        /// <returns>SQL boolean string.</returns>
        public override string FormatBoolean(bool value)
        {
            return value ? "TRUE" : "FALSE";
        }

        /// <summary>
        /// Format a DateTime for PostgreSQL.
        /// </summary>
        /// <param name="value">DateTime value.</param>
        /// <returns>ISO 8601 formatted string.</returns>
        public override string FormatDateTime(DateTime value)
        {
            return value.ToString("yyyy-MM-dd HH:mm:ss.ffffff");
        }

        /// <summary>
        /// Format a nullable string for SQL.
        /// </summary>
        /// <param name="value">Input string.</param>
        /// <returns>SQL value or NULL.</returns>
        public override string FormatNullableString(string value)
        {
            if (value == null) return "NULL";
            return "'" + Sanitize(value) + "'";
        }

        #endregion

        #region Private-Methods

        private async Task CreateCollectionTablesInternalAsync(string collectionId, int dimensionality, bool migrateFullTextColumn, CancellationToken token)
        {
            List<string> queries = new List<string>();
            queries.Add(DynamicTableQueries.GetCreateCollectionTable(collectionId, dimensionality));
            queries.AddRange(DynamicTableQueries.GetCreateCollectionIndexes(collectionId));
            queries.Add(DynamicTableQueries.GetCreateLabelsTable(collectionId));
            queries.AddRange(DynamicTableQueries.GetCreateLabelsIndexes(collectionId));
            queries.Add(DynamicTableQueries.GetCreateTagsTable(collectionId));
            queries.AddRange(DynamicTableQueries.GetCreateTagsIndexes(collectionId));

            await ExecuteQueriesAsync(queries, false, _Settings.SchemaCommandTimeoutSeconds, token).ConfigureAwait(false);
            await EnsureFullTextSchemaAsync(collectionId, migrateFullTextColumn, token).ConfigureAwait(false);
        }

        private async Task EnsureFullTextSchemaAsync(string collectionId, bool migrateFullTextColumn, CancellationToken token)
        {
            bool hasColumn = await RefreshStoredTsVectorAsync(collectionId, token).ConfigureAwait(false);

            if (!hasColumn && migrateFullTextColumn)
            {
                long estimatedRows = await GetEstimatedRowCountAsync(collectionId, token).ConfigureAwait(false);
                if (_Logging != null)
                    _Logging.Info(_Header + "adding stored content_tsv column to collection " + collectionId
                        + " (about " + Math.Max(0, estimatedRows) + " rows); the table is locked until this completes");

                Stopwatch sw = Stopwatch.StartNew();
                await ExecuteQueriesAsync(new List<string> { DynamicTableQueries.GetAddStoredTsVectorColumn(collectionId) }, false, _Settings.SchemaCommandTimeoutSeconds, token).ConfigureAwait(false);
                sw.Stop();

                if (_Logging != null)
                    _Logging.Info(_Header + "added content_tsv to collection " + collectionId + " in " + sw.ElapsedMilliseconds + "ms");

                hasColumn = await RefreshStoredTsVectorAsync(collectionId, token).ConfigureAwait(false);
            }

            if (hasColumn)
            {
                // Build the new index before dropping the old one so there is never a window without a text index.
                Stopwatch sw = Stopwatch.StartNew();
                await ExecuteQueriesAsync(new List<string> { DynamicTableQueries.GetCreateStoredTsVectorIndex(collectionId) }, false, _Settings.SchemaCommandTimeoutSeconds, token).ConfigureAwait(false);
                await ExecuteQueriesAsync(new List<string> { DynamicTableQueries.GetDropLegacyFullTextIndex(collectionId) }, false, _Settings.SchemaCommandTimeoutSeconds, token).ConfigureAwait(false);
                sw.Stop();
                if (_Logging != null && sw.ElapsedMilliseconds > 1000)
                    _Logging.Info(_Header + "ensured content_tsv index for collection " + collectionId + " in " + sw.ElapsedMilliseconds + "ms");
            }
            else
            {
                // Migration skipped: keep the legacy expression index so english full-text search stays indexed.
                await ExecuteQueriesAsync(new List<string> { DynamicTableQueries.GetCreateLegacyFullTextIndex(collectionId) }, false, _Settings.SchemaCommandTimeoutSeconds, token).ConfigureAwait(false);
                if (_Logging != null)
                    _Logging.Warn(_Header + "collection " + collectionId + " has no content_tsv column (Database.MigrateFullTextColumn is false); "
                        + "full-text search uses the legacy expression index");
            }
        }

        private async Task<bool> RefreshStoredTsVectorAsync(string collectionId, CancellationToken token)
        {
            DataTable result = await ExecuteQueryAsync(DynamicTableQueries.GetStoredTsVectorColumnExists(collectionId), false, token).ConfigureAwait(false);
            bool exists = result != null && result.Rows.Count > 0;
            _StoredTsVectorCache[collectionId] = exists;
            return exists;
        }

        private async Task<long> GetEstimatedRowCountAsync(string collectionId, CancellationToken token)
        {
            try
            {
                DataTable result = await ExecuteQueryAsync(DynamicTableQueries.GetEstimatedRowCount(collectionId), false, token).ConfigureAwait(false);
                if (result == null || result.Rows.Count < 1) return 0;
                if (result.Rows[0]["estimate"] == null || result.Rows[0]["estimate"] == DBNull.Value) return 0;
                return Convert.ToInt64(result.Rows[0]["estimate"]);
            }
            catch (NpgsqlException)
            {
                return 0;
            }
        }

        #endregion

        #region IDisposable

        private bool _Disposed = false;

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        /// <param name="disposing">Whether managed resources should be disposed.</param>
        protected override void Dispose(bool disposing)
        {
            if (!_Disposed)
            {
                _Disposed = true;
            }

            base.Dispose(disposing);
        }

        #endregion
    }
}
