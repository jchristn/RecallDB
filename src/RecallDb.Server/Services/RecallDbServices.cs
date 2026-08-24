namespace RecallDb.Server.Services
{
    using System;

    using SyslogLogging;

    using RecallDb.Core.Database;

    /// <summary>
    /// Aggregates the transport-agnostic service layer. A single instance is shared by the REST host and the MCP
    /// host so both feed identical business logic through the same <see cref="RecallDb.Server.Classes.RequestContext"/>.
    /// </summary>
    public class RecallDbServices
    {
        #region Public-Members

        /// <summary>
        /// Tenant service.
        /// </summary>
        public TenantService Tenants { get; }

        /// <summary>
        /// User service.
        /// </summary>
        public UserService Users { get; }

        /// <summary>
        /// Credential service.
        /// </summary>
        public CredentialService Credentials { get; }

        /// <summary>
        /// Collection service.
        /// </summary>
        public CollectionService Collections { get; }

        /// <summary>
        /// Document service.
        /// </summary>
        public DocumentService Documents { get; }

        /// <summary>
        /// Label service.
        /// </summary>
        public LabelService Labels { get; }

        /// <summary>
        /// Tag service.
        /// </summary>
        public TagService Tags { get; }

        /// <summary>
        /// Search service.
        /// </summary>
        public SearchService Search { get; }

        /// <summary>
        /// Authentication (login) service.
        /// </summary>
        public AuthService Auth { get; }

        /// <summary>
        /// Request-history service.
        /// </summary>
        public RequestHistoryService RequestHistory { get; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="authenticationService">Authentication service used by the login service.</param>
        public RecallDbServices(DatabaseDriverBase database, LoggingModule logging, AuthenticationService authenticationService)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (logging == null) throw new ArgumentNullException(nameof(logging));
            if (authenticationService == null) throw new ArgumentNullException(nameof(authenticationService));

            Tenants = new TenantService(database, logging);
            Users = new UserService(database, logging);
            Credentials = new CredentialService(database, logging);
            Collections = new CollectionService(database, logging);
            Documents = new DocumentService(database, logging);
            Labels = new LabelService(database, logging);
            Tags = new TagService(database, logging);
            Search = new SearchService(database, logging, Documents);
            Auth = new AuthService(authenticationService);
            RequestHistory = new RequestHistoryService(database, logging);
        }

        #endregion
    }
}
