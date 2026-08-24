namespace RecallDb.Server.Services
{
    using System;

    using SyslogLogging;

    using RecallDb.Core.Database;
    using RecallDb.Server.Classes;

    /// <summary>
    /// Base class for transport-agnostic services. Holds shared dependencies and the authorization helpers that
    /// mirror the checks previously embedded in the REST handlers, so REST and MCP enforce access identically.
    /// </summary>
    public abstract class ServiceBase
    {
        #region Protected-Members

        /// <summary>
        /// Database driver.
        /// </summary>
        protected readonly DatabaseDriverBase _Database;

        /// <summary>
        /// Logging module.
        /// </summary>
        protected readonly LoggingModule _Logging;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="logging">Logging module.</param>
        protected ServiceBase(DatabaseDriverBase database, LoggingModule logging)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (logging == null) throw new ArgumentNullException(nameof(logging));

            _Database = database;
            _Logging = logging;
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Whether the authenticated principal may access the given tenant. Admins may access any tenant; other
        /// principals may access only the tenant they authenticated against.
        /// </summary>
        /// <param name="auth">Authentication result.</param>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <returns>True if access is permitted.</returns>
        protected static bool ValidateTenantAccess(AuthenticationResult auth, string tenantId)
        {
            if (auth == null) return false;
            if (auth.IsAdmin) return true;
            if (auth.Tenant != null && auth.Tenant.Id == tenantId) return true;
            return false;
        }

        #endregion
    }
}
