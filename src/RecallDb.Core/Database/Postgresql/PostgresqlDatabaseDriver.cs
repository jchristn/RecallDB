namespace RecallDb.Core.Database.Postgresql
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Npgsql;
    using SyslogLogging;
    using RecallDb.Core.Database.Interfaces;
    using RecallDb.Core.Database.Postgresql.Implementations;
    using RecallDb.Core.Database.Postgresql.Queries;
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
            List<string> queries = new List<string>();
            queries.Add(DynamicTableQueries.GetCreateCollectionTable(collectionId, dimensionality));
            queries.AddRange(DynamicTableQueries.GetCreateCollectionIndexes(collectionId));
            queries.Add(DynamicTableQueries.GetCreateLabelsTable(collectionId));
            queries.AddRange(DynamicTableQueries.GetCreateLabelsIndexes(collectionId));
            queries.Add(DynamicTableQueries.GetCreateTagsTable(collectionId));
            queries.AddRange(DynamicTableQueries.GetCreateTagsIndexes(collectionId));

            await ExecuteQueriesAsync(queries, false, token).ConfigureAwait(false);
            if (_Logging != null) _Logging.Info(_Header + "created collection tables for " + collectionId);
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
            if (_Logging != null) _Logging.Info(_Header + "dropped collection tables for " + collectionId);
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
            if (string.IsNullOrEmpty(query)) throw new ArgumentNullException(nameof(query));

            if (_Settings.LogQueries && _Logging != null)
                _Logging.Debug(_Header + "query: " + query);

            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                NpgsqlTransaction transaction = null;

                try
                {
                    if (isTransaction)
                        transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);

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

        /// <summary>
        /// Execute multiple SQL queries.
        /// </summary>
        /// <param name="queries">SQL queries.</param>
        /// <param name="isTransaction">Whether to wrap in a transaction.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public override async Task ExecuteQueriesAsync(IEnumerable<string> queries, bool isTransaction = false, CancellationToken token = default)
        {
            if (queries == null) throw new ArgumentNullException(nameof(queries));

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

                            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
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
