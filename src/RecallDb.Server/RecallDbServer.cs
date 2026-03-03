namespace RecallDb.Server
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using SwiftStack;
    using SwiftStack.Rest;
    using SwiftStack.Rest.OpenApi;
    using WatsonWebserver.Core;
    using RecallDb.Core.Database;
    using RecallDb.Core.Database.Postgresql;
    using RecallDb.Core.Enums;
    using System.Linq;
    using RecallDb.Core.Helpers;
    using RecallDb.Core.Models;
    using RecallDb.Core.Settings;
    using RecallDb.Server.Classes;
    using RecallDb.Server.Services;

    /// <summary>
    /// RecallDB server.
    /// </summary>
    public static class RecallDbServer
    {
        #region Private-Members

        private static string _Header = "[RecallDb] ";
        private static string _Version = "1.0.0";
        private static string _SettingsFile = "recalldb.json";
        private static ServerSettings _Settings = new ServerSettings();
        private static LoggingModule _Logging = null;
        private static DatabaseDriverBase _Database = null;
        private static AuthenticationService _AuthService = null;
        private static SwiftStackApp _App = null;
        private static DateTime _StartTimeUtc = DateTime.UtcNow;
        private static CancellationTokenSource _TokenSource = new CancellationTokenSource();

        #endregion

        #region Entry-Point

        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static async Task Main(string[] args)
        {
            #region Load-Settings

            if (File.Exists(_SettingsFile))
            {
                string json = File.ReadAllText(_SettingsFile);
                _Settings = Serializer.DeserializeJson<ServerSettings>(json);
                Console.WriteLine(_Header + "loaded settings from " + _SettingsFile);
            }
            else
            {
                string json = Serializer.SerializeJson(_Settings);
                File.WriteAllText(_SettingsFile, json);
                Console.WriteLine(_Header + "created default settings file " + _SettingsFile);
            }

            #endregion

            #region Initialize-Logging

            List<SyslogLogging.SyslogServer> syslogServers = new List<SyslogLogging.SyslogServer>();
            if (_Settings.Logging.Servers != null)
            {
                foreach (RecallDb.Core.Settings.SyslogServer server in _Settings.Logging.Servers)
                {
                    syslogServers.Add(new SyslogLogging.SyslogServer(server.Hostname, server.Port));
                }
            }

            if (syslogServers.Count > 0)
                _Logging = new LoggingModule(syslogServers);
            else
                _Logging = new LoggingModule();

            _Logging.Settings.EnableConsole = _Settings.Logging.ConsoleLogging;
            _Logging.Settings.EnableColors = _Settings.Logging.EnableColors;
            _Logging.Settings.MinimumSeverity = (Severity)_Settings.Logging.MinimumSeverity;

            if (_Settings.Logging.FileLogging
                && !String.IsNullOrEmpty(_Settings.Logging.LogDirectory)
                && !String.IsNullOrEmpty(_Settings.Logging.LogFilename))
            {
                if (!Directory.Exists(_Settings.Logging.LogDirectory))
                    Directory.CreateDirectory(_Settings.Logging.LogDirectory);

                _Logging.Settings.LogFilename = Path.Combine(_Settings.Logging.LogDirectory, _Settings.Logging.LogFilename);

                if (_Settings.Logging.IncludeDateInFilename)
                    _Logging.Settings.FileLogging = FileLoggingMode.FileWithDate;
                else
                    _Logging.Settings.FileLogging = FileLoggingMode.SingleLogFile;
            }

            _Logging.Info(_Header + "RecallDB v" + _Version + " starting");

            #endregion

            #region Initialize-Database

            _Database = new PostgresqlDatabaseDriver(_Settings.Database, _Logging);
            await _Database.InitializeAsync().ConfigureAwait(false);
            _Logging.Info(_Header + "database initialized");

            #endregion

            #region First-Run

            await InitializeFirstRunAsync().ConfigureAwait(false);

            #endregion

            #region Initialize-Services

            _AuthService = new AuthenticationService(_Settings, _Database, _Logging);

            #endregion

            #region Initialize-Server

            _App = new SwiftStackApp("RecallDB Server");
            _App.Rest.WebserverSettings.Hostname = _Settings.Webserver.Hostname;
            _App.Rest.WebserverSettings.Port = _Settings.Webserver.Port;
            _App.Rest.WebserverSettings.Ssl.Enable = _Settings.Webserver.Ssl;

            _App.Rest.AuthenticationRoute = AuthenticateRequestAsync;

            _App.Rest.PreRoutingRoute = async (ctx) =>
            {
                ctx.Timestamp.Start = DateTime.UtcNow;
                ctx.Response.ContentType = "application/json";
            };

            _App.Rest.PostRoutingRoute = async (ctx) =>
            {
                ctx.Timestamp.End = DateTime.UtcNow;
                _Logging.Debug(
                    _Header + 
                    ctx.Request.Method + " " + 
                    ctx.Request.Url.RawWithQuery + " " + 
                    ctx.Response.StatusCode + " " +
                    "(" + ctx.Timestamp.TotalMs.Value.ToString("F2").ToString() + "ms)");
            };

            _App.Rest.UseOpenApi(api =>
            {
                api.Info.Title = "RecallDB API";
                api.Info.Version = _Version;
                api.Info.Description = "Multi-tenant RESTful vector database service built on PostgreSQL with pgvector. Stores content, metadata, and vector embeddings with full-text and similarity search.";
                api.Info.Contact = new OpenApiContact { Name = "RecallDB" };
                api.Info.License = new OpenApiLicense { Name = "MIT" };

                api.SecuritySchemes["Bearer"] = OpenApiSecurityScheme.Bearer(
                    "JWT",
                    "Bearer token authentication. Use an admin API key or a credential bearer token in the Authorization header.");
                api.Security.Add(new Dictionary<string, List<string>> { { "Bearer", new List<string>() } });

                api.Tags.Add(new OpenApiTag { Name = "Health", Description = "Health check endpoints" });
                api.Tags.Add(new OpenApiTag { Name = "Authentication", Description = "Authentication endpoints" });
                api.Tags.Add(new OpenApiTag { Name = "Tenants", Description = "Tenant management" });
                api.Tags.Add(new OpenApiTag { Name = "Users", Description = "User management scoped by tenant" });
                api.Tags.Add(new OpenApiTag { Name = "Credentials", Description = "Credential and API key management scoped by tenant" });
                api.Tags.Add(new OpenApiTag { Name = "Collections", Description = "Vector collection management scoped by tenant" });
                api.Tags.Add(new OpenApiTag { Name = "Documents", Description = "Document and embedding management within collections" });
                api.Tags.Add(new OpenApiTag { Name = "Labels", Description = "Label management within collections" });
                api.Tags.Add(new OpenApiTag { Name = "Tags", Description = "Tag key-value management within collections" });
                api.Tags.Add(new OpenApiTag { Name = "Search", Description = "Vector similarity search within collections" });
            });

            #endregion

            #region Register-Routes

            RegisterRoutes();

            #endregion

            #region Start

            Console.WriteLine("");
            Console.WriteLine(RecallDb.Core.Constants.Logo);

            _Logging.Info(_Header + "starting on " + _Settings.Webserver.Hostname + ":" + _Settings.Webserver.Port);
            _ = Task.Run(() => _App.Rest.Run(_TokenSource.Token), _TokenSource.Token);

            Console.WriteLine("RecallDB v" + _Version + " listening on " + _Settings.Webserver.Hostname + ":" + _Settings.Webserver.Port);
            Console.WriteLine("Press CTRL+C to exit");
            Console.WriteLine("");

            using (ManualResetEvent waitHandle = new ManualResetEvent(false))
            {
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    _TokenSource.Cancel();
                    waitHandle.Set();
                };
                waitHandle.WaitOne();
            }

            _Logging.Info(_Header + "shutting down");

            if (_Database != null) _Database.Dispose();
            if (_Logging != null) _Logging.Dispose();
            if (_TokenSource != null) _TokenSource.Dispose();

            #endregion
        }

        #endregion

        #region Private-Static-Methods

        private static async Task InitializeFirstRunAsync()
        {
            long tenantCount = await _Database.Tenants.GetCountAsync().ConfigureAwait(false);
            if (tenantCount > 0) return;

            _Logging.Info(_Header + "first run detected, creating default records");

            TenantMetadata tenant = new TenantMetadata();
            tenant.Id = "default";
            tenant.Name = "Default Tenant";
            await _Database.Tenants.CreateAsync(tenant).ConfigureAwait(false);

            UserMaster user = new UserMaster();
            user.Id = "default";
            user.TenantId = "default";
            user.Email = "admin@recall";
            user.SetPassword("password");
            user.FirstName = "Admin";
            user.LastName = "User";
            user.IsAdmin = true;
            user.IsTenantAdmin = true;
            await _Database.Users.CreateAsync(user).ConfigureAwait(false);

            Credential credential = new Credential();
            credential.Id = "default";
            credential.TenantId = "default";
            credential.UserId = "default";
            credential.Name = "Default API Key";
            credential.BearerToken = "default";
            await _Database.Credentials.CreateAsync(credential).ConfigureAwait(false);

            CollectionMetadata collection = new CollectionMetadata();
            collection.Id = "default";
            collection.TenantId = "default";
            collection.Name = "Default Collection";
            await _Database.Collections.CreateAsync(collection).ConfigureAwait(false);

            Console.WriteLine("");
            Console.WriteLine("===== FIRST RUN =====");
            Console.WriteLine("Tenant    : Default Tenant (ID: default)");
            Console.WriteLine("User      : admin@recall / password (ID: default)");
            Console.WriteLine("Credential: Bearer token: default");
            Console.WriteLine("Collection: Default Collection (ID: default)");
            Console.WriteLine("Admin keys: " + string.Join(", ", _Settings.AdminApiKeys));
            Console.WriteLine("=====================");
            Console.WriteLine("");
        }

        private static void RegisterRoutes()
        {
            // Health routes
            _App.Rest.Get("/", HealthGetRoute,
                openApi => openApi
                    .WithTag("Health")
                    .WithSummary("Health check")
                    .WithDescription("Returns server name, version, and uptime in milliseconds.")
                    .WithOperationId("healthGet")
                    .WithResponse(200, OpenApiResponseMetadata.Create("Server health information")));

            _App.Rest.Head("/", HealthHeadRoute,
                openApi => openApi
                    .WithTag("Health")
                    .WithSummary("Health check (HEAD)")
                    .WithDescription("Returns 200 if the server is running.")
                    .WithOperationId("healthHead")
                    .WithResponse(200, OpenApiResponseMetadata.Create("Server is running")));

            // Authentication route
            _App.Rest.Post<AuthenticateRequest>("/v1.0/authenticate", AuthenticateRoute,
                openApi => openApi
                    .WithTag("Authentication")
                    .WithSummary("Authenticate")
                    .WithDescription("Authenticate using a bearer token, or using tenant ID, email, and password.")
                    .WithOperationId("authenticate")
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<AuthenticateRequest>("Supply BearerToken or TenantId+Email+Password.", required: true))
                    .WithResponse(200, OpenApiResponseMetadata.Json<AuthenticateResponse>("Authentication successful"))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest("Invalid request body"))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized("Authentication failed")));

            // Tenant routes
            _App.Rest.Get("/v1.0/tenants", TenantListRoute,
                openApi => openApi
                    .WithTag("Tenants")
                    .WithSummary("List tenants")
                    .WithDescription("List all tenants. Admins see all tenants; normal users see only their own tenant.")
                    .WithOperationId("tenantList")
                    .WithResponse(200, OpenApiResponseMetadata.Create("List of tenants"))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
                requireAuthentication: true);

            _App.Rest.Get("/v1.0/tenants/{id}", TenantReadRoute,
                openApi => openApi
                    .WithTag("Tenants")
                    .WithSummary("Read tenant")
                    .WithDescription("Retrieve a tenant by ID.")
                    .WithOperationId("tenantRead")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Tenant ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Json<TenantMetadata>("Tenant details"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound("Tenant not found")),
                requireAuthentication: true);

            _App.Rest.Head("/v1.0/tenants/{id}", TenantExistsRoute,
                openApi => openApi
                    .WithTag("Tenants")
                    .WithSummary("Check tenant exists")
                    .WithDescription("Returns 200 if the tenant exists, 404 otherwise.")
                    .WithOperationId("tenantExists")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Tenant ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("Tenant exists"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                requireAuthentication: true);

            _App.Rest.Post<EnumerationQuery>("/v1.0/tenants/enumerate", TenantEnumerateRoute,
                openApi => openApi
                    .WithTag("Tenants")
                    .WithSummary("Enumerate tenants")
                    .WithDescription("Enumerate tenants with pagination. Admin access required.")
                    .WithOperationId("tenantEnumerate")
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<EnumerationQuery>("Pagination parameters"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("Paginated list of tenants"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            _App.Rest.Put<TenantMetadata>("/v1.0/tenants", TenantCreateRoute,
                openApi => openApi
                    .WithTag("Tenants")
                    .WithSummary("Create tenant")
                    .WithDescription("Create a new tenant. Admin access required.")
                    .WithOperationId("tenantCreate")
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<TenantMetadata>("Tenant to create", required: true))
                    .WithResponse(201, OpenApiResponseMetadata.Json<TenantMetadata>("Tenant created"))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            _App.Rest.Put<TenantMetadata>("/v1.0/tenants/{id}", TenantUpdateRoute,
                openApi => openApi
                    .WithTag("Tenants")
                    .WithSummary("Update tenant")
                    .WithDescription("Update an existing tenant by ID.")
                    .WithOperationId("tenantUpdate")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Tenant ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<TenantMetadata>("Updated tenant data", required: true))
                    .WithResponse(200, OpenApiResponseMetadata.Json<TenantMetadata>("Tenant updated"))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound("Tenant not found")),
                requireAuthentication: true);

            _App.Rest.Delete("/v1.0/tenants/{id}", TenantDeleteRoute,
                openApi => openApi
                    .WithTag("Tenants")
                    .WithSummary("Delete tenant")
                    .WithDescription("Delete a tenant by ID. Use ?force query parameter to also drop collection tables. Admin access required.")
                    .WithOperationId("tenantDelete")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Query("force", "Force delete including collection tables", required: false))
                    .WithResponse(204, OpenApiResponseMetadata.NoContent())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            // User routes
            _App.Rest.Get("/v1.0/tenants/{tid}/users", UserListRoute,
                openApi => openApi
                    .WithTag("Users")
                    .WithSummary("List users")
                    .WithDescription("List all users for a tenant.")
                    .WithOperationId("userList")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("List of users"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            _App.Rest.Get("/v1.0/tenants/{tid}/users/{id}", UserReadRoute,
                openApi => openApi
                    .WithTag("Users")
                    .WithSummary("Read user")
                    .WithDescription("Retrieve a user by ID. Password hash is redacted from the response.")
                    .WithOperationId("userRead")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("id", "User ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Json<UserMaster>("User details (password redacted)"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound("User not found")),
                requireAuthentication: true);

            _App.Rest.Head("/v1.0/tenants/{tid}/users/{id}", UserExistsRoute,
                openApi => openApi
                    .WithTag("Users")
                    .WithSummary("Check user exists")
                    .WithDescription("Returns 200 if the user exists, 404 otherwise.")
                    .WithOperationId("userExists")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("id", "User ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("User exists"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                requireAuthentication: true);

            _App.Rest.Post<EnumerationQuery>("/v1.0/tenants/{tid}/users/enumerate", UserEnumerateRoute,
                openApi => openApi
                    .WithTag("Users")
                    .WithSummary("Enumerate users")
                    .WithDescription("Enumerate users for a tenant with pagination.")
                    .WithOperationId("userEnumerate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<EnumerationQuery>("Pagination parameters"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("Paginated list of users"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            _App.Rest.Put<UserMaster>("/v1.0/tenants/{tid}/users", UserCreateRoute,
                openApi => openApi
                    .WithTag("Users")
                    .WithSummary("Create user")
                    .WithDescription("Create a new user for a tenant. Admin or tenant admin access required.")
                    .WithOperationId("userCreate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<UserMaster>("User to create", required: true))
                    .WithResponse(201, OpenApiResponseMetadata.Json<UserMaster>("User created (password redacted)"))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            _App.Rest.Put<UserMaster>("/v1.0/tenants/{tid}/users/{id}", UserUpdateRoute,
                openApi => openApi
                    .WithTag("Users")
                    .WithSummary("Update user")
                    .WithDescription("Update an existing user. Admin or tenant admin access required.")
                    .WithOperationId("userUpdate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("id", "User ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<UserMaster>("Updated user data", required: true))
                    .WithResponse(200, OpenApiResponseMetadata.Json<UserMaster>("User updated (password redacted)"))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound("User not found")),
                requireAuthentication: true);

            _App.Rest.Delete("/v1.0/tenants/{tid}/users/{id}", UserDeleteRoute,
                openApi => openApi
                    .WithTag("Users")
                    .WithSummary("Delete user")
                    .WithDescription("Delete a user. Admin or tenant admin access required.")
                    .WithOperationId("userDelete")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("id", "User ID"))
                    .WithResponse(204, OpenApiResponseMetadata.NoContent())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            // Credential routes
            _App.Rest.Get("/v1.0/tenants/{tid}/credentials", CredentialListRoute,
                openApi => openApi
                    .WithTag("Credentials")
                    .WithSummary("List credentials")
                    .WithDescription("List all credentials for a tenant.")
                    .WithOperationId("credentialList")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("List of credentials"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            _App.Rest.Get("/v1.0/tenants/{tid}/credentials/{id}", CredentialReadRoute,
                openApi => openApi
                    .WithTag("Credentials")
                    .WithSummary("Read credential")
                    .WithDescription("Retrieve a credential by ID.")
                    .WithOperationId("credentialRead")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Credential ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Json<Credential>("Credential details"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound("Credential not found")),
                requireAuthentication: true);

            _App.Rest.Head("/v1.0/tenants/{tid}/credentials/{id}", CredentialExistsRoute,
                openApi => openApi
                    .WithTag("Credentials")
                    .WithSummary("Check credential exists")
                    .WithDescription("Returns 200 if the credential exists, 404 otherwise.")
                    .WithOperationId("credentialExists")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Credential ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("Credential exists"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                requireAuthentication: true);

            _App.Rest.Post<EnumerationQuery>("/v1.0/tenants/{tid}/credentials/enumerate", CredentialEnumerateRoute,
                openApi => openApi
                    .WithTag("Credentials")
                    .WithSummary("Enumerate credentials")
                    .WithDescription("Enumerate credentials for a tenant with pagination.")
                    .WithOperationId("credentialEnumerate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<EnumerationQuery>("Pagination parameters"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("Paginated list of credentials"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            _App.Rest.Put<Credential>("/v1.0/tenants/{tid}/credentials", CredentialCreateRoute,
                openApi => openApi
                    .WithTag("Credentials")
                    .WithSummary("Create credential")
                    .WithDescription("Create a new credential for a tenant. Admin or tenant admin access required.")
                    .WithOperationId("credentialCreate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<Credential>("Credential to create", required: true))
                    .WithResponse(201, OpenApiResponseMetadata.Json<Credential>("Credential created"))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            _App.Rest.Put<Credential>("/v1.0/tenants/{tid}/credentials/{id}", CredentialUpdateRoute,
                openApi => openApi
                    .WithTag("Credentials")
                    .WithSummary("Update credential")
                    .WithDescription("Update an existing credential. Admin or tenant admin access required.")
                    .WithOperationId("credentialUpdate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Credential ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<Credential>("Updated credential data", required: true))
                    .WithResponse(200, OpenApiResponseMetadata.Json<Credential>("Credential updated"))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound("Credential not found")),
                requireAuthentication: true);

            _App.Rest.Delete("/v1.0/tenants/{tid}/credentials/{id}", CredentialDeleteRoute,
                openApi => openApi
                    .WithTag("Credentials")
                    .WithSummary("Delete credential")
                    .WithDescription("Delete a credential. Admin or tenant admin access required.")
                    .WithOperationId("credentialDelete")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Credential ID"))
                    .WithResponse(204, OpenApiResponseMetadata.NoContent())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            // Collection routes
            _App.Rest.Get("/v1.0/tenants/{tid}/collections", CollectionListRoute,
                openApi => openApi
                    .WithTag("Collections")
                    .WithSummary("List collections")
                    .WithDescription("List all collections for a tenant.")
                    .WithOperationId("collectionList")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("List of collections"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            _App.Rest.Get("/v1.0/tenants/{tid}/collections/{cid}", CollectionReadRoute,
                openApi => openApi
                    .WithTag("Collections")
                    .WithSummary("Read collection")
                    .WithDescription("Retrieve a collection by ID including its dimensionality.")
                    .WithOperationId("collectionRead")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Json<CollectionMetadata>("Collection details"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound("Collection not found")),
                requireAuthentication: true);

            _App.Rest.Head("/v1.0/tenants/{tid}/collections/{cid}", CollectionExistsRoute,
                openApi => openApi
                    .WithTag("Collections")
                    .WithSummary("Check collection exists")
                    .WithDescription("Returns 200 if the collection exists, 404 otherwise.")
                    .WithOperationId("collectionExists")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("Collection exists"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                requireAuthentication: true);

            _App.Rest.Post<EnumerationQuery>("/v1.0/tenants/{tid}/collections/enumerate", CollectionEnumerateRoute,
                openApi => openApi
                    .WithTag("Collections")
                    .WithSummary("Enumerate collections")
                    .WithDescription("Enumerate collections for a tenant with pagination.")
                    .WithOperationId("collectionEnumerate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<EnumerationQuery>("Pagination parameters"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("Paginated list of collections"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            _App.Rest.Put<CollectionMetadata>("/v1.0/tenants/{tid}/collections", CollectionCreateRoute,
                openApi => openApi
                    .WithTag("Collections")
                    .WithSummary("Create collection")
                    .WithDescription("Create a new vector collection. This creates the backing document, label, and tag tables with the specified vector dimensionality. Admin or tenant admin access required.")
                    .WithOperationId("collectionCreate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<CollectionMetadata>("Collection to create including Dimensionality", required: true))
                    .WithResponse(201, OpenApiResponseMetadata.Json<CollectionMetadata>("Collection created"))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            _App.Rest.Put<CollectionMetadata>("/v1.0/tenants/{tid}/collections/{cid}", CollectionUpdateRoute,
                openApi => openApi
                    .WithTag("Collections")
                    .WithSummary("Update collection")
                    .WithDescription("Update an existing collection metadata.")
                    .WithOperationId("collectionUpdate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<CollectionMetadata>("Updated collection data", required: true))
                    .WithResponse(200, OpenApiResponseMetadata.Json<CollectionMetadata>("Collection updated"))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound("Collection not found")),
                requireAuthentication: true);

            _App.Rest.Delete("/v1.0/tenants/{tid}/collections/{cid}", CollectionDeleteRoute,
                openApi => openApi
                    .WithTag("Collections")
                    .WithSummary("Delete collection")
                    .WithDescription("Delete a collection and its backing document, label, and tag tables. Admin or tenant admin access required.")
                    .WithOperationId("collectionDelete")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithResponse(204, OpenApiResponseMetadata.NoContent())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            _App.Rest.Get("/v1.0/tenants/{tid}/collections/{cid}/stats", CollectionStatsRoute,
                openApi => openApi
                    .WithTag("Collections")
                    .WithSummary("Get collection statistics")
                    .WithDescription("Returns document count, unique document count, total content length, label count, and tag count for a collection.")
                    .WithOperationId("collectionStats")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("Collection statistics"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound("Collection not found")),
                requireAuthentication: true);

            // Document routes
            _App.Rest.Get("/v1.0/tenants/{tid}/collections/{cid}/documents", DocumentListRoute,
                openApi => openApi
                    .WithTag("Documents")
                    .WithSummary("List documents")
                    .WithDescription("List all documents in a collection.")
                    .WithOperationId("documentList")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("List of documents"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            _App.Rest.Get("/v1.0/tenants/{tid}/collections/{cid}/documents/{docKey}", DocumentReadRoute,
                openApi => openApi
                    .WithTag("Documents")
                    .WithSummary("Read document by key")
                    .WithDescription("Retrieve a document by its unique document key.")
                    .WithOperationId("documentRead")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("docKey", "Document key"))
                    .WithResponse(200, OpenApiResponseMetadata.Json<DocumentRecord>("Document details"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound("Document not found")),
                requireAuthentication: true);

            _App.Rest.Get("/v1.0/tenants/{tid}/collections/{cid}/documents/{docId}/{position}", DocumentReadByPositionRoute,
                openApi => openApi
                    .WithTag("Documents")
                    .WithSummary("Read document by ID and position")
                    .WithDescription("Retrieve a specific document chunk by document ID and position index. Useful for chunk lineage.")
                    .WithOperationId("documentReadByPosition")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("docId", "Document ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("position", "Position index (0-based)"))
                    .WithResponse(200, OpenApiResponseMetadata.Json<DocumentRecord>("Document chunk details"))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest("Position must be an integer"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound("Document chunk not found")),
                requireAuthentication: true);

            _App.Rest.Head("/v1.0/tenants/{tid}/collections/{cid}/documents/{docKey}", DocumentExistsRoute,
                openApi => openApi
                    .WithTag("Documents")
                    .WithSummary("Check document exists")
                    .WithDescription("Returns 200 if the document exists, 404 otherwise.")
                    .WithOperationId("documentExists")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("docKey", "Document key"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("Document exists"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                requireAuthentication: true);

            _App.Rest.Post<EnumerationQuery>("/v1.0/tenants/{tid}/collections/{cid}/documents/enumerate", DocumentEnumerateRoute,
                openApi => openApi
                    .WithTag("Documents")
                    .WithSummary("Enumerate documents")
                    .WithDescription("Enumerate documents in a collection with pagination.")
                    .WithOperationId("documentEnumerate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<EnumerationQuery>("Pagination parameters"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("Paginated list of documents"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            _App.Rest.Post<List<DocumentRecord>>("/v1.0/tenants/{tid}/collections/{cid}/documents/batch", DocumentBatchRoute,
                openApi => openApi
                    .WithTag("Documents")
                    .WithSummary("Batch create documents")
                    .WithDescription("Create multiple documents in a single transactional batch. Each document must include Embeddings matching the collection dimensionality.")
                    .WithOperationId("documentBatchCreate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<List<DocumentRecord>>("List of documents to create", required: true))
                    .WithResponse(201, OpenApiResponseMetadata.Create("Documents created"))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            _App.Rest.Put<DocumentRecord>("/v1.0/tenants/{tid}/collections/{cid}/documents", DocumentCreateRoute,
                openApi => openApi
                    .WithTag("Documents")
                    .WithSummary("Create document")
                    .WithDescription("Create a new document with content and vector embeddings. Embeddings must match the collection dimensionality.")
                    .WithOperationId("documentCreate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<DocumentRecord>("Document to create", required: true))
                    .WithResponse(201, OpenApiResponseMetadata.Json<DocumentRecord>("Document created"))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            _App.Rest.Put<DocumentRecord>("/v1.0/tenants/{tid}/collections/{cid}/documents/{docKey}", DocumentUpdateRoute,
                openApi => openApi
                    .WithTag("Documents")
                    .WithSummary("Update document")
                    .WithDescription("Update an existing document by its document key.")
                    .WithOperationId("documentUpdate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("docKey", "Document key"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<DocumentRecord>("Updated document data", required: true))
                    .WithResponse(200, OpenApiResponseMetadata.Json<DocumentRecord>("Document updated"))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound("Document not found")),
                requireAuthentication: true);

            _App.Rest.Delete("/v1.0/tenants/{tid}/collections/{cid}/documents/{docKey}", DocumentDeleteRoute,
                openApi => openApi
                    .WithTag("Documents")
                    .WithSummary("Delete document")
                    .WithDescription("Delete a document by its document key.")
                    .WithOperationId("documentDelete")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("docKey", "Document key"))
                    .WithResponse(204, OpenApiResponseMetadata.NoContent())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            _App.Rest.Post<BatchDeleteRequest>("/v1.0/tenants/{tid}/collections/{cid}/documents/batch/delete", DocumentBatchDeleteRoute,
                openApi => openApi
                    .WithTag("Documents")
                    .WithSummary("Batch delete documents")
                    .WithDescription("Delete multiple documents by their document keys in a single operation. Associated labels and tags are also deleted.")
                    .WithOperationId("documentBatchDelete")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<BatchDeleteRequest>("Batch delete request containing document keys", required: true))
                    .WithResponse(204, OpenApiResponseMetadata.NoContent())
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            _App.Rest.Post<EnumerationQuery>("/v1.0/tenants/{tid}/collections/{cid}/documents/delete/filter", DocumentDeleteByFilterRoute,
                openApi => openApi
                    .WithTag("Documents")
                    .WithSummary("Delete documents by filter")
                    .WithDescription("Delete all documents matching the specified filter criteria. Uses the same filter model as the enumerate endpoint. Pagination fields (MaxResults, ContinuationToken, Ordering) are ignored. Associated labels and tags are also deleted.")
                    .WithOperationId("documentDeleteByFilter")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<EnumerationQuery>("Filter criteria for documents to delete", required: true))
                    .WithResponse(200, OpenApiResponseMetadata.Json<DeleteResult>("Delete result with count of deleted documents"))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            _App.Rest.Get("/v1.0/tenants/{tid}/collections/{cid}/documents/stats/{docKey}", DocumentStatsRoute,
                openApi => openApi
                    .WithTag("Documents")
                    .WithSummary("Get document statistics")
                    .WithDescription("Returns chunk count, total content length, label count, and tag count for a document. If the document has a DocumentId, stats aggregate across all chunks sharing that ID.")
                    .WithOperationId("documentStats")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("docKey", "Document key"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("Document statistics"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound("Document not found")),
                requireAuthentication: true);

            // Label routes
            _App.Rest.Get("/v1.0/tenants/{tid}/collections/{cid}/labels", LabelListRoute,
                openApi => openApi
                    .WithTag("Labels")
                    .WithSummary("List labels")
                    .WithDescription("List all labels in a collection.")
                    .WithOperationId("labelList")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("List of labels"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            _App.Rest.Get("/v1.0/tenants/{tid}/collections/{cid}/labels/distinct", LabelDistinctRoute,
                openApi => openApi
                    .WithTag("Labels")
                    .WithSummary("Distinct labels")
                    .WithDescription("Retrieve distinct label values in a collection.")
                    .WithOperationId("labelDistinct")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("List of distinct labels"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            _App.Rest.Get("/v1.0/tenants/{tid}/collections/{cid}/labels/{id}", LabelReadRoute,
                openApi => openApi
                    .WithTag("Labels")
                    .WithSummary("Read label")
                    .WithDescription("Retrieve a label by ID.")
                    .WithOperationId("labelRead")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Label ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Json<LabelRecord>("Label details"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound("Label not found")),
                requireAuthentication: true);

            _App.Rest.Put<LabelRecord>("/v1.0/tenants/{tid}/collections/{cid}/labels", LabelCreateRoute,
                openApi => openApi
                    .WithTag("Labels")
                    .WithSummary("Create label")
                    .WithDescription("Create a new label associated with a document in a collection.")
                    .WithOperationId("labelCreate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<LabelRecord>("Label to create", required: true))
                    .WithResponse(201, OpenApiResponseMetadata.Json<LabelRecord>("Label created"))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            _App.Rest.Delete("/v1.0/tenants/{tid}/collections/{cid}/labels/{id}", LabelDeleteRoute,
                openApi => openApi
                    .WithTag("Labels")
                    .WithSummary("Delete label")
                    .WithDescription("Delete a label by ID.")
                    .WithOperationId("labelDelete")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Label ID"))
                    .WithResponse(204, OpenApiResponseMetadata.NoContent())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            // Tag routes
            _App.Rest.Get("/v1.0/tenants/{tid}/collections/{cid}/tags", TagListRoute,
                openApi => openApi
                    .WithTag("Tags")
                    .WithSummary("List tags")
                    .WithDescription("List all tags in a collection.")
                    .WithOperationId("tagList")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("List of tags"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            _App.Rest.Get("/v1.0/tenants/{tid}/collections/{cid}/tags/distinct", TagDistinctRoute,
                openApi => openApi
                    .WithTag("Tags")
                    .WithSummary("Distinct tag keys")
                    .WithDescription("Retrieve distinct tag keys in a collection.")
                    .WithOperationId("tagDistinctKeys")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("List of distinct tag keys"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            _App.Rest.Get("/v1.0/tenants/{tid}/collections/{cid}/tags/{id}", TagReadRoute,
                openApi => openApi
                    .WithTag("Tags")
                    .WithSummary("Read tag")
                    .WithDescription("Retrieve a tag by ID.")
                    .WithOperationId("tagRead")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Tag ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Json<TagRecord>("Tag details"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound("Tag not found")),
                requireAuthentication: true);

            _App.Rest.Put<TagRecord>("/v1.0/tenants/{tid}/collections/{cid}/tags", TagCreateRoute,
                openApi => openApi
                    .WithTag("Tags")
                    .WithSummary("Create tag")
                    .WithDescription("Create a new key-value tag associated with a document in a collection.")
                    .WithOperationId("tagCreate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<TagRecord>("Tag to create", required: true))
                    .WithResponse(201, OpenApiResponseMetadata.Json<TagRecord>("Tag created"))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            _App.Rest.Delete("/v1.0/tenants/{tid}/collections/{cid}/tags/{id}", TagDeleteRoute,
                openApi => openApi
                    .WithTag("Tags")
                    .WithSummary("Delete tag")
                    .WithDescription("Delete a tag by ID.")
                    .WithOperationId("tagDelete")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Tag ID"))
                    .WithResponse(204, OpenApiResponseMetadata.NoContent())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                requireAuthentication: true);

            // Search route
            _App.Rest.Post<SearchQuery>("/v1.0/tenants/{tid}/collections/{cid}/search", SearchRoute,
                openApi => openApi
                    .WithTag("Search")
                    .WithSummary("Search")
                    .WithDescription("Search within a collection using vector similarity, full-text relevance, or hybrid (combined) search. Vector search supports cosine similarity, cosine distance, euclidean similarity, euclidean distance, and inner product. Full-text search uses PostgreSQL ts_rank scoring with stemming and stop word removal. Hybrid search blends vector and text scores with configurable weighting. Filter results by labels, tags, date ranges, terms, and document IDs.")
                    .WithOperationId("search")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<SearchQuery>("Search parameters including vector, filters, and pagination", required: true))
                    .WithResponse(200, OpenApiResponseMetadata.Json<SearchResult>("Search results with scored documents"))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest("Invalid search query"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound("Collection not found")),
                requireAuthentication: true);
        }

        #endregion

        #region Authentication-Callback

        private static async Task<AuthResult> AuthenticateRequestAsync(HttpContextBase ctx)
        {
            string token = null;

            if (ctx.Request.Headers != null)
            {
                string authHeader = ctx.Request.Headers["Authorization"];
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    token = authHeader.Substring(7).Trim();
            }

            AuthenticationResult authResult = await _AuthService.AuthenticateBearerAsync(token ?? string.Empty).ConfigureAwait(false);
            ctx.Metadata = authResult;

            AuthResult result = new AuthResult();
            result.AuthenticationResult = authResult.IsAuthenticated
                ? AuthenticationResultEnum.Success
                : AuthenticationResultEnum.Invalid;
            return result;
        }

        #endregion

        #region Route-Helpers

        private static AuthenticationResult GetAuthResult(AppRequest req)
        {
            return req.Http.Metadata as AuthenticationResult;
        }

        private static object MakeError(AppRequest req, int statusCode, string error, string context = null)
        {
            req.Http.Response.StatusCode = statusCode;
            return new { Error = error, StatusCode = statusCode, Context = context };
        }

        private static bool ValidateTenantAccess(AuthenticationResult auth, string tenantId)
        {
            if (auth.IsAdmin) return true;
            if (auth.Tenant != null && auth.Tenant.Id == tenantId) return true;
            return false;
        }

        #endregion

        #region Health-Routes

        private static async Task<object> HealthGetRoute(AppRequest req)
        {
            double uptimeMs = (DateTime.UtcNow - _StartTimeUtc).TotalMilliseconds;
            return new
            {
                Name = "RecallDB",
                Version = _Version,
                UptimeMs = uptimeMs
            };
        }

        private static async Task<object> HealthHeadRoute(AppRequest req)
        {
            req.Http.Response.StatusCode = 200;
            return null;
        }

        #endregion

        #region Authenticate-Route

        private static async Task<object> AuthenticateRoute(AppRequest req)
        {
            AuthenticateRequest body = req.Data as AuthenticateRequest;
            if (body == null)
                return MakeError(req, 400, "Bad request", "Request body is required.");

            AuthenticationResult authResult = null;

            if (!string.IsNullOrEmpty(body.BearerToken))
            {
                authResult = await _AuthService.AuthenticateBearerAsync(body.BearerToken).ConfigureAwait(false);
            }
            else if (!string.IsNullOrEmpty(body.Email) && !string.IsNullOrEmpty(body.Password) && !string.IsNullOrEmpty(body.TenantId))
            {
                authResult = await _AuthService.AuthenticateEmailPasswordAsync(body.TenantId, body.Email, body.Password).ConfigureAwait(false);
            }
            else
            {
                return MakeError(req, 400, "Bad request", "Supply BearerToken or TenantId+Email+Password.");
            }

            AuthenticateResponse resp = new AuthenticateResponse();
            resp.Success = authResult.IsAuthenticated;
            resp.Tenant = authResult.Tenant;
            resp.User = authResult.User != null ? UserMaster.Redact(authResult.User) : null;
            resp.Credential = authResult.Credential;
            resp.ErrorMessage = authResult.ErrorMessage;

            if (!authResult.IsAuthenticated) req.Http.Response.StatusCode = 401;
            return resp;
        }

        #endregion

        #region Tenant-Routes

        private static async Task<object> TenantListRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            EnumerationQuery query = new EnumerationQuery();

            Stopwatch sw = Stopwatch.StartNew();

            if (auth.IsAdmin)
            {
                EnumerationResult<TenantMetadata> result = await _Database.Tenants.EnumerateAsync(query).ConfigureAwait(false);
                result.TotalMs = sw.Elapsed.TotalMilliseconds;
                return result;
            }
            else
            {
                List<TenantMetadata> list = new List<TenantMetadata>();
                if (auth.Tenant != null) list.Add(auth.Tenant);
                EnumerationResult<TenantMetadata> result = new EnumerationResult<TenantMetadata>();
                result.Success = true;
                result.MaxResults = 1;
                result.EndOfResults = true;
                result.TotalRecords = list.Count;
                result.RecordsRemaining = 0;
                result.Objects = list;
                result.TotalMs = sw.Elapsed.TotalMilliseconds;
                return result;
            }
        }

        private static async Task<object> TenantReadRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string id = req.Parameters["id"];

            if (!auth.IsAdmin && !ValidateTenantAccess(auth, id))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            TenantMetadata tenant = await _Database.Tenants.ReadAsync(id).ConfigureAwait(false);
            if (tenant == null)
                return MakeError(req, 404, "Not found", "Tenant not found.");

            return tenant;
        }

        private static async Task<object> TenantExistsRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string id = req.Parameters["id"];

            if (!auth.IsAdmin && !ValidateTenantAccess(auth, id))
            {
                req.Http.Response.StatusCode = 403;
                return null;
            }

            bool exists = await _Database.Tenants.ExistsAsync(id).ConfigureAwait(false);
            req.Http.Response.StatusCode = exists ? 200 : 404;
            return null;
        }

        private static async Task<object> TenantEnumerateRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);

            if (!auth.IsAdmin)
                return MakeError(req, 403, "Forbidden", "Admin access required.");

            EnumerationQuery query = req.Data as EnumerationQuery;
            if (query == null) query = new EnumerationQuery();

            Stopwatch sw = Stopwatch.StartNew();
            EnumerationResult<TenantMetadata> result = await _Database.Tenants.EnumerateAsync(query).ConfigureAwait(false);
            result.TotalMs = sw.Elapsed.TotalMilliseconds;
            return result;
        }

        private static async Task<object> TenantCreateRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);

            if (!auth.IsAdmin)
                return MakeError(req, 403, "Forbidden", "Admin access required.");

            TenantMetadata tenant = req.Data as TenantMetadata;
            if (tenant == null)
                return MakeError(req, 400, "Bad request", "Request body is required.");

            tenant = await _Database.Tenants.CreateAsync(tenant).ConfigureAwait(false);
            req.Http.Response.StatusCode = 201;
            return tenant;
        }

        private static async Task<object> TenantUpdateRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string id = req.Parameters["id"];

            if (!auth.IsAdmin && !ValidateTenantAccess(auth, id))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            TenantMetadata tenant = req.Data as TenantMetadata;
            if (tenant == null)
                return MakeError(req, 400, "Bad request", "Request body is required.");

            tenant.Id = id;
            tenant = await _Database.Tenants.UpdateAsync(tenant).ConfigureAwait(false);
            if (tenant == null)
                return MakeError(req, 404, "Not found", "Tenant not found.");

            return tenant;
        }

        private static async Task<object> TenantDeleteRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);

            if (!auth.IsAdmin)
                return MakeError(req, 403, "Forbidden", "Admin access required.");

            string id = req.Parameters["id"];

            await _Database.Collections.DeleteByTenantIdAsync(id).ConfigureAwait(false);
            await _Database.Credentials.DeleteByTenantIdAsync(id).ConfigureAwait(false);
            await _Database.Users.DeleteByTenantIdAsync(id).ConfigureAwait(false);
            await _Database.Tenants.DeleteAsync(id).ConfigureAwait(false);
            req.Http.Response.StatusCode = 204;
            return null;
        }

        #endregion

        #region User-Routes

        private static async Task<object> UserListRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            Stopwatch sw = Stopwatch.StartNew();
            EnumerationQuery query = new EnumerationQuery();
            EnumerationResult<UserMaster> result = await _Database.Users.EnumerateAsync(tid, query).ConfigureAwait(false);
            result.TotalMs = sw.Elapsed.TotalMilliseconds;
            return result;
        }

        private static async Task<object> UserReadRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string id = req.Parameters["id"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            UserMaster user = await _Database.Users.ReadAsync(tid, id).ConfigureAwait(false);
            if (user == null)
                return MakeError(req, 404, "Not found", "User not found.");

            return UserMaster.Redact(user);
        }

        private static async Task<object> UserExistsRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string id = req.Parameters["id"];

            if (!ValidateTenantAccess(auth, tid))
            {
                req.Http.Response.StatusCode = 403;
                return null;
            }

            bool exists = await _Database.Users.ExistsAsync(tid, id).ConfigureAwait(false);
            req.Http.Response.StatusCode = exists ? 200 : 404;
            return null;
        }

        private static async Task<object> UserEnumerateRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            EnumerationQuery query = req.Data as EnumerationQuery;
            if (query == null) query = new EnumerationQuery();

            Stopwatch sw = Stopwatch.StartNew();
            EnumerationResult<UserMaster> result = await _Database.Users.EnumerateAsync(tid, query).ConfigureAwait(false);
            result.TotalMs = sw.Elapsed.TotalMilliseconds;
            return result;
        }

        private static async Task<object> UserCreateRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];

            if (!auth.IsAdmin && !auth.IsTenantAdmin)
                return MakeError(req, 403, "Forbidden", "Admin or tenant admin required.");

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            UserMaster user = req.Data as UserMaster;
            if (user == null)
                return MakeError(req, 400, "Bad request", "Request body is required.");

            user.TenantId = tid;
            user = await _Database.Users.CreateAsync(user).ConfigureAwait(false);
            req.Http.Response.StatusCode = 201;
            return UserMaster.Redact(user);
        }

        private static async Task<object> UserUpdateRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string id = req.Parameters["id"];

            if (!auth.IsAdmin && !auth.IsTenantAdmin)
                return MakeError(req, 403, "Forbidden", "Admin or tenant admin required.");

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            UserMaster user = req.Data as UserMaster;
            if (user == null)
                return MakeError(req, 400, "Bad request", "Request body is required.");

            user.Id = id;
            user.TenantId = tid;
            user = await _Database.Users.UpdateAsync(user).ConfigureAwait(false);
            if (user == null)
                return MakeError(req, 404, "Not found", "User not found.");

            return UserMaster.Redact(user);
        }

        private static async Task<object> UserDeleteRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string id = req.Parameters["id"];

            if (!auth.IsAdmin && !auth.IsTenantAdmin)
                return MakeError(req, 403, "Forbidden", "Admin or tenant admin required.");

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            await _Database.Credentials.DeleteByUserIdAsync(tid, id).ConfigureAwait(false);
            await _Database.Users.DeleteAsync(tid, id).ConfigureAwait(false);
            req.Http.Response.StatusCode = 204;
            return null;
        }

        #endregion

        #region Credential-Routes

        private static async Task<object> CredentialListRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            Stopwatch sw = Stopwatch.StartNew();
            EnumerationQuery query = new EnumerationQuery();
            EnumerationResult<Credential> result = await _Database.Credentials.EnumerateAsync(tid, query).ConfigureAwait(false);
            result.TotalMs = sw.Elapsed.TotalMilliseconds;
            return result;
        }

        private static async Task<object> CredentialReadRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string id = req.Parameters["id"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            Credential cred = await _Database.Credentials.ReadAsync(tid, id).ConfigureAwait(false);
            if (cred == null)
                return MakeError(req, 404, "Not found", "Credential not found.");

            return cred;
        }

        private static async Task<object> CredentialExistsRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string id = req.Parameters["id"];

            if (!ValidateTenantAccess(auth, tid))
            {
                req.Http.Response.StatusCode = 403;
                return null;
            }

            bool exists = await _Database.Credentials.ExistsAsync(tid, id).ConfigureAwait(false);
            req.Http.Response.StatusCode = exists ? 200 : 404;
            return null;
        }

        private static async Task<object> CredentialEnumerateRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            EnumerationQuery query = req.Data as EnumerationQuery;
            if (query == null) query = new EnumerationQuery();

            Stopwatch sw = Stopwatch.StartNew();
            EnumerationResult<Credential> result = await _Database.Credentials.EnumerateAsync(tid, query).ConfigureAwait(false);
            result.TotalMs = sw.Elapsed.TotalMilliseconds;
            return result;
        }

        private static async Task<object> CredentialCreateRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];

            if (!auth.IsAdmin && !auth.IsTenantAdmin)
                return MakeError(req, 403, "Forbidden", "Admin or tenant admin required.");

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            Credential cred = req.Data as Credential;
            if (cred == null)
                return MakeError(req, 400, "Bad request", "Request body is required.");

            cred.TenantId = tid;
            cred = await _Database.Credentials.CreateAsync(cred).ConfigureAwait(false);
            req.Http.Response.StatusCode = 201;
            return cred;
        }

        private static async Task<object> CredentialUpdateRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string id = req.Parameters["id"];

            if (!auth.IsAdmin && !auth.IsTenantAdmin)
                return MakeError(req, 403, "Forbidden", "Admin or tenant admin required.");

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            Credential cred = req.Data as Credential;
            if (cred == null)
                return MakeError(req, 400, "Bad request", "Request body is required.");

            cred.Id = id;
            cred.TenantId = tid;
            cred = await _Database.Credentials.UpdateAsync(cred).ConfigureAwait(false);
            if (cred == null)
                return MakeError(req, 404, "Not found", "Credential not found.");

            return cred;
        }

        private static async Task<object> CredentialDeleteRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string id = req.Parameters["id"];

            if (!auth.IsAdmin && !auth.IsTenantAdmin)
                return MakeError(req, 403, "Forbidden", "Admin or tenant admin required.");

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            await _Database.Credentials.DeleteAsync(tid, id).ConfigureAwait(false);
            req.Http.Response.StatusCode = 204;
            return null;
        }

        #endregion

        #region Collection-Routes

        private static async Task<object> CollectionListRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            Stopwatch sw = Stopwatch.StartNew();
            EnumerationQuery query = new EnumerationQuery();
            EnumerationResult<CollectionMetadata> result = await _Database.Collections.EnumerateAsync(tid, query).ConfigureAwait(false);
            result.TotalMs = sw.Elapsed.TotalMilliseconds;
            return result;
        }

        private static async Task<object> CollectionReadRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string cid = req.Parameters["cid"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            CollectionMetadata col = await _Database.Collections.ReadAsync(tid, cid).ConfigureAwait(false);
            if (col == null)
                return MakeError(req, 404, "Not found", "Collection not found.");

            return col;
        }

        private static async Task<object> CollectionExistsRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string cid = req.Parameters["cid"];

            if (!ValidateTenantAccess(auth, tid))
            {
                req.Http.Response.StatusCode = 403;
                return null;
            }

            bool exists = await _Database.Collections.ExistsAsync(tid, cid).ConfigureAwait(false);
            req.Http.Response.StatusCode = exists ? 200 : 404;
            return null;
        }

        private static async Task<object> CollectionEnumerateRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            EnumerationQuery query = req.Data as EnumerationQuery;
            if (query == null) query = new EnumerationQuery();

            Stopwatch sw = Stopwatch.StartNew();
            EnumerationResult<CollectionMetadata> result = await _Database.Collections.EnumerateAsync(tid, query).ConfigureAwait(false);
            result.TotalMs = sw.Elapsed.TotalMilliseconds;
            return result;
        }

        private static async Task<object> CollectionCreateRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];

            if (!auth.IsAdmin && !auth.IsTenantAdmin)
                return MakeError(req, 403, "Forbidden", "Admin or tenant admin required.");

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            CollectionMetadata col = req.Data as CollectionMetadata;
            if (col == null)
                return MakeError(req, 400, "Bad request", "Request body is required.");

            col.TenantId = tid;
            col = await _Database.Collections.CreateAsync(col).ConfigureAwait(false);
            req.Http.Response.StatusCode = 201;
            return col;
        }

        private static async Task<object> CollectionUpdateRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string cid = req.Parameters["cid"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            CollectionMetadata col = req.Data as CollectionMetadata;
            if (col == null)
                return MakeError(req, 400, "Bad request", "Request body is required.");

            col.Id = cid;
            col.TenantId = tid;
            col = await _Database.Collections.UpdateAsync(col).ConfigureAwait(false);
            if (col == null)
                return MakeError(req, 404, "Not found", "Collection not found.");

            return col;
        }

        private static async Task<object> CollectionDeleteRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string cid = req.Parameters["cid"];

            if (!auth.IsAdmin && !auth.IsTenantAdmin)
                return MakeError(req, 403, "Forbidden", "Admin or tenant admin required.");

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            await _Database.Collections.DeleteAsync(tid, cid).ConfigureAwait(false);
            req.Http.Response.StatusCode = 204;
            return null;
        }

        private static async Task<object> CollectionStatsRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string cid = req.Parameters["cid"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            CollectionMetadata col = await _Database.Collections.ReadAsync(tid, cid).ConfigureAwait(false);
            if (col == null)
                return MakeError(req, 404, "Not found", "Collection not found.");

            string tableName = cid.Replace("-", "_").Replace(".", "_");
            string docsTable = "collection_" + tableName;
            string labelsTable = "collection_" + tableName + "_labels";
            string tagsTable = "collection_" + tableName + "_tags";

            string query =
                "SELECT " +
                "(SELECT COUNT(*) FROM " + docsTable + ") AS document_count, " +
                "(SELECT COUNT(DISTINCT document_id) FROM " + docsTable + " WHERE document_id IS NOT NULL) AS unique_document_count, " +
                "(SELECT COALESCE(SUM(content_length), 0) FROM " + docsTable + ") AS total_content_length, " +
                "(SELECT COUNT(*) FROM " + labelsTable + ") AS label_count, " +
                "(SELECT COUNT(*) FROM " + tagsTable + ") AS tag_count;";

            System.Data.DataTable dt = await _Database.ExecuteQueryAsync(query).ConfigureAwait(false);

            long documentCount = 0;
            long uniqueDocumentCount = 0;
            long totalContentLength = 0;
            long labelCount = 0;
            long tagCount = 0;

            if (dt != null && dt.Rows.Count > 0)
            {
                System.Data.DataRow row = dt.Rows[0];
                documentCount = Convert.ToInt64(row["document_count"]);
                uniqueDocumentCount = Convert.ToInt64(row["unique_document_count"]);
                totalContentLength = Convert.ToInt64(row["total_content_length"]);
                labelCount = Convert.ToInt64(row["label_count"]);
                tagCount = Convert.ToInt64(row["tag_count"]);
            }

            return new
            {
                CollectionId = cid,
                DocumentCount = documentCount,
                UniqueDocumentCount = uniqueDocumentCount,
                TotalContentLength = totalContentLength,
                LabelCount = labelCount,
                TagCount = tagCount
            };
        }

        #endregion

        #region Labels-Tags-Helpers

        private static async Task AttachLabelsAndTagsAsync(string collectionId, List<DocumentRecord> docs)
        {
            if (docs == null || docs.Count == 0) return;

            List<string> documentKeys = docs.Select(d => d.DocumentKey).ToList();

            Dictionary<string, List<string>> labelsMap = await _Database.Labels.GetByDocumentKeysAsync(collectionId, documentKeys).ConfigureAwait(false);
            Dictionary<string, Dictionary<string, string>> tagsMap = await _Database.Tags.GetByDocumentKeysAsync(collectionId, documentKeys).ConfigureAwait(false);

            foreach (DocumentRecord doc in docs)
            {
                doc.Labels = labelsMap.ContainsKey(doc.DocumentKey) ? labelsMap[doc.DocumentKey] : new List<string>();
                doc.Tags = tagsMap.ContainsKey(doc.DocumentKey) ? tagsMap[doc.DocumentKey] : new Dictionary<string, string>();
            }
        }

        private static async Task AttachLabelsAndTagsAsync(string collectionId, DocumentRecord doc)
        {
            if (doc == null) return;
            await AttachLabelsAndTagsAsync(collectionId, new List<DocumentRecord> { doc }).ConfigureAwait(false);
        }

        private static async Task PersistLabelsAndTagsAsync(string collectionId, DocumentRecord doc)
        {
            if (doc.Labels != null && doc.Labels.Count > 0)
            {
                foreach (string label in doc.Labels)
                {
                    LabelRecord lr = new LabelRecord();
                    lr.DocumentKey = doc.DocumentKey;
                    lr.DocumentId = doc.DocumentId;
                    lr.Position = doc.Position;
                    lr.Label = label;
                    await _Database.Labels.CreateAsync(collectionId, lr).ConfigureAwait(false);
                }
            }

            if (doc.Tags != null && doc.Tags.Count > 0)
            {
                foreach (KeyValuePair<string, string> tag in doc.Tags)
                {
                    TagRecord tr = new TagRecord();
                    tr.DocumentKey = doc.DocumentKey;
                    tr.DocumentId = doc.DocumentId;
                    tr.Position = doc.Position;
                    tr.Key = tag.Key;
                    tr.Value = tag.Value;
                    await _Database.Tags.CreateAsync(collectionId, tr).ConfigureAwait(false);
                }
            }
        }

        #endregion

        #region Document-Routes

        private static async Task<object> DocumentListRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string cid = req.Parameters["cid"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            Stopwatch sw = Stopwatch.StartNew();
            EnumerationQuery query = new EnumerationQuery();
            EnumerationResult<DocumentRecord> result = await _Database.Documents.EnumerateAsync(cid, query).ConfigureAwait(false);
            await AttachLabelsAndTagsAsync(cid, result.Objects).ConfigureAwait(false);
            result.TotalMs = sw.Elapsed.TotalMilliseconds;
            return result;
        }

        private static async Task<object> DocumentReadRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string cid = req.Parameters["cid"];
            string docKey = req.Parameters["docKey"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            DocumentRecord doc = await _Database.Documents.ReadAsync(cid, docKey).ConfigureAwait(false);
            if (doc == null)
                return MakeError(req, 404, "Not found", "Document not found.");

            await AttachLabelsAndTagsAsync(cid, doc).ConfigureAwait(false);
            return doc;
        }

        private static async Task<object> DocumentReadByPositionRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string cid = req.Parameters["cid"];
            string docId = req.Parameters["docId"];
            string posStr = req.Parameters["position"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            if (!int.TryParse(posStr, out int position))
                return MakeError(req, 400, "Bad request", "Position must be an integer.");

            DocumentRecord doc = await _Database.Documents.ReadByDocumentIdAndPositionAsync(cid, docId, position).ConfigureAwait(false);
            if (doc == null)
                return MakeError(req, 404, "Not found", "Document chunk not found.");

            await AttachLabelsAndTagsAsync(cid, doc).ConfigureAwait(false);
            return doc;
        }

        private static async Task<object> DocumentExistsRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string cid = req.Parameters["cid"];
            string docKey = req.Parameters["docKey"];

            if (!ValidateTenantAccess(auth, tid))
            {
                req.Http.Response.StatusCode = 403;
                return null;
            }

            bool exists = await _Database.Documents.ExistsAsync(cid, docKey).ConfigureAwait(false);
            req.Http.Response.StatusCode = exists ? 200 : 404;
            return null;
        }

        private static async Task<object> DocumentEnumerateRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string cid = req.Parameters["cid"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            EnumerationQuery query = req.Data as EnumerationQuery;
            if (query == null) query = new EnumerationQuery();

            Stopwatch sw = Stopwatch.StartNew();
            EnumerationResult<DocumentRecord> result = await _Database.Documents.EnumerateAsync(cid, query).ConfigureAwait(false);
            await AttachLabelsAndTagsAsync(cid, result.Objects).ConfigureAwait(false);
            result.TotalMs = sw.Elapsed.TotalMilliseconds;
            return result;
        }

        private static async Task<object> DocumentCreateRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string cid = req.Parameters["cid"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            DocumentRecord doc = req.Data as DocumentRecord;
            if (doc == null)
                return MakeError(req, 400, "Bad request", "Request body is required.");

            CollectionMetadata col = await _Database.Collections.ReadAsync(tid, cid).ConfigureAwait(false);
            if (col == null)
                return MakeError(req, 404, "Not found", "Collection not found.");

            if (doc.Embeddings != null && doc.Embeddings.Count != col.Dimensionality)
                return MakeError(req, 400, "Bad request", "Embeddings dimensionality mismatch. Expected " + col.Dimensionality + " dimensions, but received " + doc.Embeddings.Count + ".");

            List<string> reqLabels = doc.Labels;
            Dictionary<string, string> reqTags = doc.Tags;

            doc = await _Database.Documents.CreateAsync(cid, doc).ConfigureAwait(false);

            doc.Labels = reqLabels;
            doc.Tags = reqTags;
            await PersistLabelsAndTagsAsync(cid, doc).ConfigureAwait(false);
            await AttachLabelsAndTagsAsync(cid, doc).ConfigureAwait(false);

            req.Http.Response.StatusCode = 201;
            return doc;
        }

        private static async Task<object> DocumentUpdateRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string cid = req.Parameters["cid"];
            string docKey = req.Parameters["docKey"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            DocumentRecord doc = req.Data as DocumentRecord;
            if (doc == null)
                return MakeError(req, 400, "Bad request", "Request body is required.");

            List<string> reqLabels = doc.Labels;
            Dictionary<string, string> reqTags = doc.Tags;

            doc.DocumentKey = docKey;
            doc = await _Database.Documents.UpdateAsync(cid, doc).ConfigureAwait(false);
            if (doc == null)
                return MakeError(req, 404, "Not found", "Document not found.");

            if (reqLabels != null)
            {
                await _Database.Labels.DeleteByDocumentKeyAsync(cid, docKey).ConfigureAwait(false);
                doc.Labels = reqLabels;
            }

            if (reqTags != null)
            {
                await _Database.Tags.DeleteByDocumentKeyAsync(cid, docKey).ConfigureAwait(false);
                doc.Tags = reqTags;
            }

            await PersistLabelsAndTagsAsync(cid, doc).ConfigureAwait(false);
            await AttachLabelsAndTagsAsync(cid, doc).ConfigureAwait(false);

            return doc;
        }

        private static async Task<object> DocumentDeleteRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string cid = req.Parameters["cid"];
            string docKey = req.Parameters["docKey"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            await _Database.Labels.DeleteByDocumentKeyAsync(cid, docKey).ConfigureAwait(false);
            await _Database.Tags.DeleteByDocumentKeyAsync(cid, docKey).ConfigureAwait(false);
            await _Database.Documents.DeleteAsync(cid, docKey).ConfigureAwait(false);
            req.Http.Response.StatusCode = 204;
            return null;
        }

        private static async Task<object> DocumentBatchDeleteRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string cid = req.Parameters["cid"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            BatchDeleteRequest batchReq = req.Data as BatchDeleteRequest;
            if (batchReq == null || batchReq.DocumentKeys == null || batchReq.DocumentKeys.Count == 0)
                return MakeError(req, 400, "Bad request", "Request body must contain a non-empty list of document keys.");

            await _Database.Labels.DeleteByDocumentKeysAsync(cid, batchReq.DocumentKeys).ConfigureAwait(false);
            await _Database.Tags.DeleteByDocumentKeysAsync(cid, batchReq.DocumentKeys).ConfigureAwait(false);
            await _Database.Documents.DeleteBatchAsync(cid, batchReq.DocumentKeys).ConfigureAwait(false);
            req.Http.Response.StatusCode = 204;
            return null;
        }

        private static async Task<object> DocumentDeleteByFilterRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string cid = req.Parameters["cid"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            EnumerationQuery query = req.Data as EnumerationQuery;
            if (query == null) query = new EnumerationQuery();

            // Collect all matching document keys by paginating through results
            List<string> allKeys = new List<string>();
            query.MaxResults = 1000;
            query.ContinuationToken = null;

            while (true)
            {
                EnumerationResult<DocumentRecord> result = await _Database.Documents.EnumerateAsync(cid, query).ConfigureAwait(false);

                if (result.Objects != null)
                {
                    foreach (DocumentRecord doc in result.Objects)
                    {
                        allKeys.Add(doc.DocumentKey);
                    }
                }

                if (result.EndOfResults || result.ContinuationToken == null)
                    break;

                query.ContinuationToken = result.ContinuationToken;
            }

            if (allKeys.Count > 0)
            {
                await _Database.Labels.DeleteByDocumentKeysAsync(cid, allKeys).ConfigureAwait(false);
                await _Database.Tags.DeleteByDocumentKeysAsync(cid, allKeys).ConfigureAwait(false);
                await _Database.Documents.DeleteBatchAsync(cid, allKeys).ConfigureAwait(false);
            }

            DeleteResult deleteResult = new DeleteResult();
            deleteResult.DocumentsDeleted = allKeys.Count;
            return deleteResult;
        }

        private static async Task<object> DocumentBatchRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string cid = req.Parameters["cid"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            List<DocumentRecord> docs = req.Data as List<DocumentRecord>;
            if (docs == null || docs.Count == 0)
                return MakeError(req, 400, "Bad request", "Request body must contain a list of documents.");

            CollectionMetadata col = await _Database.Collections.ReadAsync(tid, cid).ConfigureAwait(false);
            if (col == null)
                return MakeError(req, 404, "Not found", "Collection not found.");

            foreach (DocumentRecord d in docs)
            {
                if (d.Embeddings != null && d.Embeddings.Count != col.Dimensionality)
                    return MakeError(req, 400, "Bad request", "Embeddings dimensionality mismatch for document '" + d.DocumentKey + "'. Expected " + col.Dimensionality + " dimensions, but received " + d.Embeddings.Count + ".");
            }

            Dictionary<string, List<string>> reqLabelsMap = new Dictionary<string, List<string>>();
            Dictionary<string, Dictionary<string, string>> reqTagsMap = new Dictionary<string, Dictionary<string, string>>();
            foreach (DocumentRecord d in docs)
            {
                if (d.Labels != null) reqLabelsMap[d.DocumentKey] = d.Labels;
                if (d.Tags != null) reqTagsMap[d.DocumentKey] = d.Tags;
            }

            List<DocumentRecord> created = await _Database.Documents.CreateBatchAsync(cid, docs).ConfigureAwait(false);

            foreach (DocumentRecord d in created)
            {
                if (reqLabelsMap.ContainsKey(d.DocumentKey)) d.Labels = reqLabelsMap[d.DocumentKey];
                if (reqTagsMap.ContainsKey(d.DocumentKey)) d.Tags = reqTagsMap[d.DocumentKey];
                await PersistLabelsAndTagsAsync(cid, d).ConfigureAwait(false);
            }

            await AttachLabelsAndTagsAsync(cid, created).ConfigureAwait(false);

            req.Http.Response.StatusCode = 201;
            return created;
        }

        private static async Task<object> DocumentStatsRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string cid = req.Parameters["cid"];
            string docKey = req.Parameters["docKey"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            CollectionMetadata col = await _Database.Collections.ReadAsync(tid, cid).ConfigureAwait(false);
            if (col == null)
                return MakeError(req, 404, "Not found", "Collection not found.");

            DocumentRecord doc = await _Database.Documents.ReadAsync(cid, docKey).ConfigureAwait(false);
            if (doc == null)
                return MakeError(req, 404, "Not found", "Document not found.");

            string tableName = cid.Replace("-", "_").Replace(".", "_");
            string docsTable = "collection_" + tableName;
            string labelsTable = "collection_" + tableName + "_labels";
            string tagsTable = "collection_" + tableName + "_tags";

            string docFilter;
            string documentId;

            if (!string.IsNullOrEmpty(doc.DocumentId))
            {
                documentId = doc.DocumentId;
                string safeDocId = _Database.Sanitize(doc.DocumentId);
                docFilter = "document_id = '" + safeDocId + "'";
            }
            else
            {
                documentId = null;
                string safeDocKey = _Database.Sanitize(docKey);
                docFilter = "document_key = '" + safeDocKey + "'";
            }

            string query =
                "SELECT " +
                "(SELECT COUNT(*) FROM " + docsTable + " WHERE " + docFilter + ") AS chunk_count, " +
                "(SELECT COALESCE(SUM(content_length), 0) FROM " + docsTable + " WHERE " + docFilter + ") AS total_content_length, " +
                "(SELECT COUNT(*) FROM " + labelsTable + " WHERE " + docFilter + ") AS label_count, " +
                "(SELECT COUNT(*) FROM " + tagsTable + " WHERE " + docFilter + ") AS tag_count;";

            System.Data.DataTable dt = await _Database.ExecuteQueryAsync(query).ConfigureAwait(false);

            long chunkCount = 0;
            long totalContentLength = 0;
            long labelCount = 0;
            long tagCount = 0;

            if (dt != null && dt.Rows.Count > 0)
            {
                System.Data.DataRow row = dt.Rows[0];
                chunkCount = Convert.ToInt64(row["chunk_count"]);
                totalContentLength = Convert.ToInt64(row["total_content_length"]);
                labelCount = Convert.ToInt64(row["label_count"]);
                tagCount = Convert.ToInt64(row["tag_count"]);
            }

            return new
            {
                DocumentKey = docKey,
                DocumentId = documentId,
                ChunkCount = chunkCount,
                TotalContentLength = totalContentLength,
                LabelCount = labelCount,
                TagCount = tagCount
            };
        }

        #endregion

        #region Label-Routes

        private static async Task<object> LabelListRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string cid = req.Parameters["cid"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            Stopwatch sw = Stopwatch.StartNew();
            EnumerationQuery query = new EnumerationQuery();
            EnumerationResult<LabelRecord> result = await _Database.Labels.EnumerateAsync(cid, query).ConfigureAwait(false);
            result.TotalMs = sw.Elapsed.TotalMilliseconds;
            return result;
        }

        private static async Task<object> LabelReadRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string cid = req.Parameters["cid"];
            string id = req.Parameters["id"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            LabelRecord label = await _Database.Labels.ReadAsync(cid, id).ConfigureAwait(false);
            if (label == null)
                return MakeError(req, 404, "Not found", "Label not found.");

            return label;
        }

        private static async Task<object> LabelCreateRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string cid = req.Parameters["cid"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            LabelRecord label = req.Data as LabelRecord;
            if (label == null)
                return MakeError(req, 400, "Bad request", "Request body is required.");

            label = await _Database.Labels.CreateAsync(cid, label).ConfigureAwait(false);
            req.Http.Response.StatusCode = 201;
            return label;
        }

        private static async Task<object> LabelDeleteRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string cid = req.Parameters["cid"];
            string id = req.Parameters["id"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            await _Database.Labels.DeleteAsync(cid, id).ConfigureAwait(false);
            req.Http.Response.StatusCode = 204;
            return null;
        }

        private static async Task<object> LabelDistinctRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string cid = req.Parameters["cid"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            List<string> labels = await _Database.Labels.DistinctAsync(cid).ConfigureAwait(false);
            return labels;
        }

        #endregion

        #region Tag-Routes

        private static async Task<object> TagListRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string cid = req.Parameters["cid"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            Stopwatch sw = Stopwatch.StartNew();
            EnumerationQuery query = new EnumerationQuery();
            EnumerationResult<TagRecord> result = await _Database.Tags.EnumerateAsync(cid, query).ConfigureAwait(false);
            result.TotalMs = sw.Elapsed.TotalMilliseconds;
            return result;
        }

        private static async Task<object> TagReadRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string cid = req.Parameters["cid"];
            string id = req.Parameters["id"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            TagRecord tag = await _Database.Tags.ReadAsync(cid, id).ConfigureAwait(false);
            if (tag == null)
                return MakeError(req, 404, "Not found", "Tag not found.");

            return tag;
        }

        private static async Task<object> TagCreateRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string cid = req.Parameters["cid"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            TagRecord tag = req.Data as TagRecord;
            if (tag == null)
                return MakeError(req, 400, "Bad request", "Request body is required.");

            tag = await _Database.Tags.CreateAsync(cid, tag).ConfigureAwait(false);
            req.Http.Response.StatusCode = 201;
            return tag;
        }

        private static async Task<object> TagDeleteRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string cid = req.Parameters["cid"];
            string id = req.Parameters["id"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            await _Database.Tags.DeleteAsync(cid, id).ConfigureAwait(false);
            req.Http.Response.StatusCode = 204;
            return null;
        }

        private static async Task<object> TagDistinctRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string cid = req.Parameters["cid"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            List<string> keys = await _Database.Tags.DistinctKeysAsync(cid).ConfigureAwait(false);
            return keys;
        }

        #endregion

        #region Search-Route

        private static async Task<object> SearchRoute(AppRequest req)
        {
            AuthenticationResult auth = GetAuthResult(req);
            string tid = req.Parameters["tid"];
            string cid = req.Parameters["cid"];

            if (!ValidateTenantAccess(auth, tid))
                return MakeError(req, 403, "Forbidden", "Access denied.");

            SearchQuery query = req.Data as SearchQuery;
            if (query == null)
                return MakeError(req, 400, "Bad request", "Request body is required.");

            CollectionMetadata col = await _Database.Collections.ReadAsync(tid, cid).ConfigureAwait(false);
            if (col == null)
                return MakeError(req, 404, "Not found", "Collection not found.");

            Stopwatch sw = Stopwatch.StartNew();
            SearchResult result = await _Database.Search.SearchAsync(cid, col.Dimensionality, query).ConfigureAwait(false);
            await AttachLabelsAndTagsAsync(cid, result.Documents).ConfigureAwait(false);

            // Neighbor enrichment
            if (query.IncludeNeighbors.HasValue && query.IncludeNeighbors.Value > 0 && result.Documents != null && result.Documents.Count > 0)
            {
                int n = query.IncludeNeighbors.Value;

                // Group matched documents by DocumentId, skipping those without a DocumentId
                var groupedByDocId = new Dictionary<string, List<DocumentRecord>>();
                foreach (DocumentRecord doc in result.Documents)
                {
                    if (string.IsNullOrEmpty(doc.DocumentId)) continue;
                    if (!groupedByDocId.ContainsKey(doc.DocumentId))
                        groupedByDocId[doc.DocumentId] = new List<DocumentRecord>();
                    groupedByDocId[doc.DocumentId].Add(doc);
                }

                // For each unique DocumentId, merge overlapping position ranges and fetch neighbors
                List<DocumentRecord> allNeighborDocs = new List<DocumentRecord>();

                foreach (var kvp in groupedByDocId)
                {
                    string documentId = kvp.Key;
                    List<DocumentRecord> matchedDocs = kvp.Value;

                    // Compute merged position ranges
                    var ranges = new List<(int Min, int Max)>();
                    foreach (DocumentRecord doc in matchedDocs)
                    {
                        int minPos = Math.Max(0, doc.Position - n);
                        int maxPos = doc.Position + n;
                        ranges.Add((minPos, maxPos));
                    }

                    // Sort and merge overlapping ranges
                    ranges.Sort((a, b) => a.Min.CompareTo(b.Min));
                    var merged = new List<(int Min, int Max)>();
                    merged.Add(ranges[0]);
                    for (int i = 1; i < ranges.Count; i++)
                    {
                        var last = merged[merged.Count - 1];
                        if (ranges[i].Min <= last.Max + 1)
                        {
                            merged[merged.Count - 1] = (last.Min, Math.Max(last.Max, ranges[i].Max));
                        }
                        else
                        {
                            merged.Add(ranges[i]);
                        }
                    }

                    // Fetch chunks for each merged range
                    List<DocumentRecord> fetchedChunks = new List<DocumentRecord>();
                    foreach (var range in merged)
                    {
                        List<DocumentRecord> chunks = await _Database.Documents.ReadByDocumentIdAndPositionRangeAsync(
                            cid, documentId, range.Min, range.Max).ConfigureAwait(false);
                        fetchedChunks.AddRange(chunks);
                    }

                    // Assign neighbors to each matched document
                    foreach (DocumentRecord doc in matchedDocs)
                    {
                        int minPos = Math.Max(0, doc.Position - n);
                        int maxPos = doc.Position + n;
                        doc.Neighbors = new List<DocumentRecord>();
                        foreach (DocumentRecord chunk in fetchedChunks)
                        {
                            if (chunk.Position >= minPos && chunk.Position <= maxPos && chunk.Position != doc.Position)
                            {
                                doc.Neighbors.Add(chunk);
                            }
                        }
                    }

                    allNeighborDocs.AddRange(fetchedChunks);
                }

                // Attach labels and tags to neighbor documents
                if (allNeighborDocs.Count > 0)
                {
                    await AttachLabelsAndTagsAsync(cid, allNeighborDocs).ConfigureAwait(false);
                }
            }

            result.TotalMs = sw.Elapsed.TotalMilliseconds;
            return result;
        }

        #endregion
    }
}
