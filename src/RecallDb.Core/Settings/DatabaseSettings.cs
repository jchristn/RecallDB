namespace RecallDb.Core.Settings
{
    using System;

    /// <summary>
    /// Database settings.
    /// </summary>
    public class DatabaseSettings
    {
        #region Public-Members

        /// <summary>
        /// Hostname of the PostgreSQL server.
        /// Default: localhost.
        /// </summary>
        public string Hostname
        {
            get
            {
                return _Hostname;
            }
            set
            {
                if (string.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Hostname));
                _Hostname = value;
            }
        }

        /// <summary>
        /// Port of the PostgreSQL server.
        /// Default: 5432.  Minimum: 1.  Maximum: 65535.
        /// </summary>
        public int Port
        {
            get
            {
                return _Port;
            }
            set
            {
                if (value < 1 || value > 65535) throw new ArgumentOutOfRangeException(nameof(Port));
                _Port = value;
            }
        }

        /// <summary>
        /// Database name.
        /// Default: recalldb.
        /// </summary>
        public string DatabaseName
        {
            get
            {
                return _DatabaseName;
            }
            set
            {
                if (string.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(DatabaseName));
                _DatabaseName = value;
            }
        }

        /// <summary>
        /// Database username.
        /// Default: recalldb.
        /// </summary>
        public string Username
        {
            get
            {
                return _Username;
            }
            set
            {
                if (string.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Username));
                _Username = value;
            }
        }

        /// <summary>
        /// Database password.
        /// Default: recalldb.
        /// </summary>
        public string Password
        {
            get
            {
                return _Password;
            }
            set
            {
                _Password = value;
            }
        }

        /// <summary>
        /// Schema name.
        /// Default: public.
        /// </summary>
        public string Schema
        {
            get
            {
                return _Schema;
            }
            set
            {
                if (string.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Schema));
                _Schema = value;
            }
        }

        /// <summary>
        /// Require encryption for the database connection.
        /// Default: false.
        /// </summary>
        public bool RequireEncryption
        {
            get
            {
                return _RequireEncryption;
            }
            set
            {
                _RequireEncryption = value;
            }
        }

        /// <summary>
        /// Log database queries.
        /// Default: false.
        /// </summary>
        public bool LogQueries
        {
            get
            {
                return _LogQueries;
            }
            set
            {
                _LogQueries = value;
            }
        }

        /// <summary>
        /// Add the stored, GIN-indexed content_tsv column to existing collections at startup.
        /// Adding a stored generated column rewrites the table while holding an ACCESS EXCLUSIVE lock, so on a
        /// very large collection startup can block for seconds to minutes. Set to false to skip the rewrite and
        /// run it in a maintenance window instead (restart with true). While a collection lacks the column,
        /// full-text search stays correct through the legacy expression index; only ranking speed is affected.
        /// New collections always get the column. Overridden by the RECALLDB_DB_MIGRATE_FTS_COLUMN environment
        /// variable (true or false).
        /// Default: true.
        /// </summary>
        public bool MigrateFullTextColumn
        {
            get
            {
                return _MigrateFullTextColumn;
            }
            set
            {
                _MigrateFullTextColumn = value;
            }
        }

        /// <summary>
        /// Command timeout, in seconds, for schema maintenance statements run when collections are created and by
        /// the startup schema pass: adding the content_tsv column (a full table rewrite that also rebuilds every
        /// index, including HNSW) and building indexes. These can take minutes on large collections, far longer
        /// than the 30-second default used for ordinary queries.
        /// Default: 0 (no limit). Minimum: 0. Maximum: 86400 (one day).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is outside 0-86400.</exception>
        public int SchemaCommandTimeoutSeconds
        {
            get
            {
                return _SchemaCommandTimeoutSeconds;
            }
            set
            {
                if (value < 0 || value > 86400) throw new ArgumentOutOfRangeException(nameof(SchemaCommandTimeoutSeconds), "SchemaCommandTimeoutSeconds must be between 0 (no limit) and 86400.");
                _SchemaCommandTimeoutSeconds = value;
            }
        }

        #endregion

        #region Private-Members

        private string _Hostname = "localhost";
        private int _Port = 5432;
        private string _DatabaseName = "recalldb";
        private string _Username = "recalldb";
        private string _Password = "recalldb";
        private string _Schema = "public";
        private bool _RequireEncryption = false;
        private bool _LogQueries = false;
        private bool _MigrateFullTextColumn = true;
        private int _SchemaCommandTimeoutSeconds = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public DatabaseSettings()
        {
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Get the PostgreSQL connection string.
        /// Environment variables override settings values:
        /// RECALLDB_DB_HOST, RECALLDB_DB_PORT, RECALLDB_DB_NAME, RECALLDB_DB_USER, RECALLDB_DB_PASS.
        /// </summary>
        /// <returns>Connection string.</returns>
        public string GetConnectionString()
        {
            string host = Environment.GetEnvironmentVariable("RECALLDB_DB_HOST");
            string port = Environment.GetEnvironmentVariable("RECALLDB_DB_PORT");
            string dbName = Environment.GetEnvironmentVariable("RECALLDB_DB_NAME");
            string user = Environment.GetEnvironmentVariable("RECALLDB_DB_USER");
            string pass = Environment.GetEnvironmentVariable("RECALLDB_DB_PASS");
            string schema = Environment.GetEnvironmentVariable("RECALLDB_DB_SCHEMA");

            string connStr =
                "Host=" + (!string.IsNullOrEmpty(host) ? host : _Hostname) + ";" +
                "Port=" + (!string.IsNullOrEmpty(port) ? port : _Port.ToString()) + ";" +
                "Database=" + (!string.IsNullOrEmpty(dbName) ? dbName : _DatabaseName) + ";" +
                "Username=" + (!string.IsNullOrEmpty(user) ? user : _Username) + ";" +
                "Password=" + (!string.IsNullOrEmpty(pass) ? pass : _Password) + ";" +
                "Search Path=" + (!string.IsNullOrEmpty(schema) ? schema : _Schema) + ";";

            if (_RequireEncryption)
                connStr += "SSL Mode=Require;";

            return connStr;
        }

        #endregion
    }
}
