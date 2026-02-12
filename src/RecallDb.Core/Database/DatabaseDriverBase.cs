namespace RecallDb.Core.Database
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using RecallDb.Core.Database.Interfaces;

    /// <summary>
    /// Abstract base class for database driver implementations.
    /// </summary>
    public abstract class DatabaseDriverBase
    {
        #region Public-Members

        /// <summary>
        /// Tenant data access methods.
        /// </summary>
        public ITenantMethods Tenants { get; protected set; }

        /// <summary>
        /// User data access methods.
        /// </summary>
        public IUserMethods Users { get; protected set; }

        /// <summary>
        /// Credential data access methods.
        /// </summary>
        public ICredentialMethods Credentials { get; protected set; }

        /// <summary>
        /// Collection data access methods.
        /// </summary>
        public ICollectionMethods Collections { get; protected set; }

        /// <summary>
        /// Document data access methods.
        /// </summary>
        public IDocumentMethods Documents { get; protected set; }

        /// <summary>
        /// Label data access methods.
        /// </summary>
        public ILabelMethods Labels { get; protected set; }

        /// <summary>
        /// Tag data access methods.
        /// </summary>
        public ITagMethods Tags { get; protected set; }

        /// <summary>
        /// Search data access methods.
        /// </summary>
        public ISearchMethods Search { get; protected set; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public DatabaseDriverBase()
        {
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Initialize the database.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public abstract Task InitializeAsync(CancellationToken token = default);

        /// <summary>
        /// Create collection tables.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="dimensionality">Vector dimensionality.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public abstract Task CreateCollectionTablesAsync(string collectionId, int dimensionality, CancellationToken token = default);

        /// <summary>
        /// Drop collection tables.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public abstract Task DropCollectionTablesAsync(string collectionId, CancellationToken token = default);

        /// <summary>
        /// Execute a SQL query.
        /// </summary>
        /// <param name="query">SQL query.</param>
        /// <param name="isTransaction">Whether to use a transaction.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>DataTable.</returns>
        public abstract Task<DataTable> ExecuteQueryAsync(string query, bool isTransaction = false, CancellationToken token = default);

        /// <summary>
        /// Execute multiple SQL queries.
        /// </summary>
        /// <param name="queries">SQL queries.</param>
        /// <param name="isTransaction">Whether to use a transaction.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public abstract Task ExecuteQueriesAsync(IEnumerable<string> queries, bool isTransaction = false, CancellationToken token = default);

        /// <summary>
        /// Sanitize a string for SQL.
        /// </summary>
        /// <param name="value">Input string.</param>
        /// <returns>Sanitized string.</returns>
        public virtual string Sanitize(string value)
        {
            if (value == null) return null;
            return value.Replace("'", "''");
        }

        /// <summary>
        /// Format a boolean for SQL.
        /// </summary>
        /// <param name="value">Boolean value.</param>
        /// <returns>SQL boolean string.</returns>
        public virtual string FormatBoolean(bool value)
        {
            return value ? "true" : "false";
        }

        /// <summary>
        /// Format a DateTime for SQL.
        /// </summary>
        /// <param name="value">DateTime value.</param>
        /// <returns>Formatted string.</returns>
        public virtual string FormatDateTime(DateTime value)
        {
            return value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Format a nullable string for SQL.
        /// </summary>
        /// <param name="value">Input string.</param>
        /// <returns>SQL value or NULL.</returns>
        public virtual string FormatNullableString(string value)
        {
            if (string.IsNullOrEmpty(value)) return "NULL";
            return "'" + Sanitize(value) + "'";
        }

        #endregion
    }
}
