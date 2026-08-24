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
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;
    using RecallDb.Core.Database;
    using RecallDb.Core.Database.Postgresql;
    using RecallDb.Core.Enums;
    using System.Linq;
    using RecallDb.Core.Helpers;
    using RecallDb.Core.Models;
    using RecallDb.Core.Settings;
    using RecallDb.Server.Classes;
    using RecallDb.Server.Mcp;
    using RecallDb.Server.Services;

    /// <summary>
    /// RecallDB server.
    /// </summary>
    public static class RecallDbServer
    {
        #region Private-Members

        private static string _Header = "[RecallDb] ";
        private static string _Version = "0.2.0";
        private static string _SettingsFile = "recalldb.json";
        private static ServerSettings _Settings = new ServerSettings();
        private static LoggingModule _Logging = null;
        private static DatabaseDriverBase _Database = null;
        private static AuthenticationService _AuthService = null;
        private static RecallDbServices _Services = null;
        private static McpServerService _McpServer = null;
        private static Webserver _App = null;
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
            #region CLI-Verbs

            if (args != null && args.Length > 0 && args[0].Equals("mcp", StringComparison.OrdinalIgnoreCase))
            {
                LoadSettingsForCli();
                List<string> subArgs = new List<string>(args);
                subArgs.RemoveAt(0);
                Environment.ExitCode = McpInstaller.Run(subArgs, _Settings.Mcp, _Settings.AdminApiKeys, _Version);
                return;
            }

            #endregion

            #region Load-Settings

            if (File.Exists(_SettingsFile))
            {
                string json = File.ReadAllText(_SettingsFile);
                _Settings = Serializer.DeserializeJson<ServerSettings>(json);
                Console.WriteLine(_Header + "loaded settings from " + _SettingsFile);
            }
            else
            {
                Console.WriteLine(_Header + "settings file " + _SettingsFile + " not found, creating with defaults");
            }

            // Re-write the settings file after load so that any properties added since the last write (for example
            // the Mcp section) are materialized to disk with their default values. Performed before applying
            // environment overrides so ephemeral overrides are not persisted.
            try
            {
                string rewritten = Serializer.SerializeJson(_Settings);
                File.WriteAllText(_SettingsFile, rewritten);
                Console.WriteLine(_Header + "settings file synchronized: " + _SettingsFile);
            }
            catch (Exception e)
            {
                Console.WriteLine(_Header + "unable to write settings file " + _SettingsFile + ": " + e.Message);
            }

            ApplyMcpEnvironmentOverrides();

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
            _Services = new RecallDbServices(_Database, _Logging, _AuthService);

            #endregion

            #region Initialize-Server

            WatsonWebserver.Core.WebserverSettings webserverSettings = new WatsonWebserver.Core.WebserverSettings();
            webserverSettings.Hostname = _Settings.Webserver.Hostname;
            webserverSettings.Port = _Settings.Webserver.Port;
            webserverSettings.Ssl.Enable = _Settings.Webserver.Ssl;

            _App = new Webserver(webserverSettings, DefaultRoute);
            _App.Serializer = new RecallDbSerializationHelper();

            _App.Routes.AuthenticateApiRequest = AuthenticateRequestAsync;

            _App.Routes.Preflight = async (ctx) =>
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.Headers["Access-Control-Allow-Origin"] = "*";
                ctx.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, HEAD, OPTIONS";
                ctx.Response.Headers["Access-Control-Allow-Headers"] = "*, Authorization, Content-Type";
                ctx.Response.ContentType = "text/plain";
                await ctx.Response.Send().ConfigureAwait(false);
            };

            _App.Routes.PreRouting = async (ctx) =>
            {
                ctx.Timestamp.Start = DateTime.UtcNow;
                ctx.Response.ContentType = "application/json";
                ctx.Response.Headers["Access-Control-Allow-Origin"] = "*";
                ctx.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, HEAD, OPTIONS";
                ctx.Response.Headers["Access-Control-Allow-Headers"] = "*, Authorization, Content-Type";
            };

            _App.Routes.PostRouting = async (ctx) =>
            {
                ctx.Timestamp.End = DateTime.UtcNow;
                _Logging.Debug(
                    _Header +
                    ctx.Request.Method + " " +
                    ctx.Request.Url.RawWithQuery + " " +
                    ctx.Response.StatusCode + " " +
                    "(" + ctx.Timestamp.TotalMs.Value.ToString("F2").ToString() + "ms)");

                // Record request history (skip OPTIONS/preflight)
                if (ctx.Request.Method != HttpMethod.OPTIONS)
                {
                    try
                    {
                        RequestHistoryEntry histEntry = new RequestHistoryEntry();
                        histEntry.HttpMethod = ctx.Request.Method.ToString();
                        histEntry.RequestUrl = ctx.Request.Url.RawWithQuery;
                        histEntry.SourceIp = ctx.Request.Source.IpAddress;
                        histEntry.StatusCode = ctx.Response.StatusCode;
                        histEntry.Success = ctx.Response.StatusCode < 400;
                        histEntry.DurationMs = (long)(ctx.Timestamp.TotalMs ?? 0);
                        histEntry.RequestContentType = ctx.Request.ContentType;
                        histEntry.RequestBodyLength = ctx.Request.ContentLength;
                        histEntry.ResponseContentType = ctx.Response.ContentType;

                        try
                        {
                            string reqBody = ctx.Request.DataAsString;
                            if (!string.IsNullOrEmpty(reqBody))
                                histEntry.RequestBody = reqBody.Length > 65536 ? reqBody.Substring(0, 65536) : reqBody;
                        }
                        catch { }

                        try
                        {
                            string respBody = ctx.Response.DataAsString;
                            if (!string.IsNullOrEmpty(respBody))
                            {
                                histEntry.ResponseBody = respBody.Length > 65536 ? respBody.Substring(0, 65536) : respBody;
                                histEntry.ResponseBodyLength = respBody.Length;
                            }
                        }
                        catch { }

                        _ = Task.Run(async () =>
                        {
                            try { await _Database.RequestHistory.InsertAsync(histEntry).ConfigureAwait(false); }
                            catch (Exception ex) { _Logging.Warn(_Header + "failed to record request history: " + ex.Message); }
                        });
                    }
                    catch { }
                }
            };

            _App.UseOpenApi(api =>
            {
                api.Info.Title = "RecallDB API";
                api.Info.Version = _Version;
                api.Info.Description = "Multi-tenant RESTful vector database service built on PostgreSQL with pgvector. Stores content, metadata, and vector embeddings with full-text and similarity search.";
                api.Info.Contact = new OpenApiContact { Name = "RecallDB" };
                api.Info.License = new OpenApiLicense { Name = "MIT" };

                api.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = "http",
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Bearer token authentication. Use an admin API key or a credential bearer token in the Authorization header."
                };
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
            _ = Task.Run(() => _App.Start(_TokenSource.Token), _TokenSource.Token);

            _McpServer = new McpServerService(_Settings.Mcp, _Logging, _Services, _AuthService, _Version, _StartTimeUtc);
            _McpServer.Start(_TokenSource.Token);

            Console.WriteLine("RecallDB v" + _Version + " listening on " + _Settings.Webserver.Hostname + ":" + _Settings.Webserver.Port);
            if (_Settings.Mcp.Enabled)
                Console.WriteLine("RecallDB MCP server listening on http://" + _Settings.Mcp.Hostname + ":" + _Settings.Mcp.Port + "/mcp");
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

            if (_McpServer != null) _McpServer.Dispose();
            if (_Database != null) _Database.Dispose();
            if (_Logging != null) _Logging.Dispose();
            if (_TokenSource != null) _TokenSource.Dispose();

            #endregion
        }

        #endregion

        #region Private-Static-Methods

        private static void LoadSettingsForCli()
        {
            if (File.Exists(_SettingsFile))
            {
                try
                {
                    _Settings = Serializer.DeserializeJson<ServerSettings>(File.ReadAllText(_SettingsFile));
                }
                catch (Exception e)
                {
                    Console.WriteLine(_Header + "unable to read settings file " + _SettingsFile + ": " + e.Message);
                }
            }

            ApplyMcpEnvironmentOverrides();
        }

        private static void ApplyMcpEnvironmentOverrides()
        {
            string enabled = Environment.GetEnvironmentVariable("RECALLDB_MCP_ENABLED");
            if (!string.IsNullOrEmpty(enabled) && bool.TryParse(enabled, out bool enabledValue))
                _Settings.Mcp.Enabled = enabledValue;

            string hostname = Environment.GetEnvironmentVariable("RECALLDB_MCP_HOSTNAME");
            if (!string.IsNullOrEmpty(hostname))
                _Settings.Mcp.Hostname = hostname;

            string port = Environment.GetEnvironmentVariable("RECALLDB_MCP_PORT");
            if (!string.IsNullOrEmpty(port) && int.TryParse(port, out int portValue))
                _Settings.Mcp.Port = portValue;
        }

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
            _App.Get("/", HealthGetRoute,
                openApi => openApi
                    .WithTag("Health")
                    .WithSummary("Health check")
                    .WithDescription("Returns server name, version, and uptime in milliseconds.")
                    .WithOperationId("healthGet")
                    .WithResponse(200, OpenApiResponseMetadata.Create("Server health information")));

            _App.Head("/", HealthHeadRoute,
                openApi => openApi
                    .WithTag("Health")
                    .WithSummary("Health check (HEAD)")
                    .WithDescription("Returns 200 if the server is running.")
                    .WithOperationId("healthHead")
                    .WithResponse(200, OpenApiResponseMetadata.Create("Server is running")));

            _App.Get("/v1.0/mcp", McpInfoRoute,
                openApi => openApi
                    .WithTag("Health")
                    .WithSummary("MCP server info")
                    .WithDescription("Returns MCP server configuration: enabled state, hostname, port, and endpoint.")
                    .WithOperationId("mcpInfo")
                    .WithResponse(200, OpenApiResponseMetadata.Create("MCP server information")));

            // Authentication route
            _App.Post<AuthenticateRequest>("/v1.0/authenticate", AuthenticateRoute,
                openApi => openApi
                    .WithTag("Authentication")
                    .WithSummary("Authenticate")
                    .WithDescription("Authenticate using a bearer token, or using tenant ID, email, and password.")
                    .WithOperationId("authenticate")
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Supply BearerToken or TenantId+Email+Password.", true))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Authentication successful", null))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized()));

            // Tenant routes
            _App.Get("/v1.0/tenants", TenantListRoute,
                openApi => openApi
                    .WithTag("Tenants")
                    .WithSummary("List tenants")
                    .WithDescription("List all tenants. Admins see all tenants; normal users see only their own tenant.")
                    .WithOperationId("tenantList")
                    .WithResponse(200, OpenApiResponseMetadata.Create("List of tenants"))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
                auth: true);

            _App.Get("/v1.0/tenants/{id}", TenantReadRoute,
                openApi => openApi
                    .WithTag("Tenants")
                    .WithSummary("Read tenant")
                    .WithDescription("Retrieve a tenant by ID.")
                    .WithOperationId("tenantRead")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Tenant ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Tenant details", null))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                auth: true);

            _App.Head("/v1.0/tenants/{id}", TenantExistsRoute,
                openApi => openApi
                    .WithTag("Tenants")
                    .WithSummary("Check tenant exists")
                    .WithDescription("Returns 200 if the tenant exists, 404 otherwise.")
                    .WithOperationId("tenantExists")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Tenant ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("Tenant exists"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                auth: true);

            _App.Post<EnumerationQuery>("/v1.0/tenants/enumerate", TenantEnumerateRoute,
                openApi => openApi
                    .WithTag("Tenants")
                    .WithSummary("Enumerate tenants")
                    .WithDescription("Enumerate tenants with pagination. Admin access required.")
                    .WithOperationId("tenantEnumerate")
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Pagination parameters"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("Paginated list of tenants"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                auth: true);

            _App.Put<TenantMetadata>("/v1.0/tenants", TenantCreateRoute,
                openApi => openApi
                    .WithTag("Tenants")
                    .WithSummary("Create tenant")
                    .WithDescription("Create a new tenant. Admin access required.")
                    .WithOperationId("tenantCreate")
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Tenant to create", true))
                    .WithResponse(201, OpenApiResponseMetadata.Json("Tenant created", null))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                auth: true);

            _App.Put<TenantMetadata>("/v1.0/tenants/{id}", TenantUpdateRoute,
                openApi => openApi
                    .WithTag("Tenants")
                    .WithSummary("Update tenant")
                    .WithDescription("Update an existing tenant by ID.")
                    .WithOperationId("tenantUpdate")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Tenant ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Updated tenant data", true))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Tenant updated", null))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                auth: true);

            _App.Delete("/v1.0/tenants/{id}", TenantDeleteRoute,
                openApi => openApi
                    .WithTag("Tenants")
                    .WithSummary("Delete tenant")
                    .WithDescription("Delete a tenant by ID. Use ?force query parameter to also drop collection tables. Admin access required.")
                    .WithOperationId("tenantDelete")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Query("force", "Force delete including collection tables", required: false))
                    .WithResponse(204, OpenApiResponseMetadata.NoContent())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                auth: true);

            // User routes
            _App.Get("/v1.0/tenants/{tid}/users", UserListRoute,
                openApi => openApi
                    .WithTag("Users")
                    .WithSummary("List users")
                    .WithDescription("List all users for a tenant.")
                    .WithOperationId("userList")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("List of users"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                auth: true);

            _App.Get("/v1.0/tenants/{tid}/users/{id}", UserReadRoute,
                openApi => openApi
                    .WithTag("Users")
                    .WithSummary("Read user")
                    .WithDescription("Retrieve a user by ID. Password hash is redacted from the response.")
                    .WithOperationId("userRead")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("id", "User ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Json("User details (password redacted)", null))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                auth: true);

            _App.Head("/v1.0/tenants/{tid}/users/{id}", UserExistsRoute,
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
                auth: true);

            _App.Post<EnumerationQuery>("/v1.0/tenants/{tid}/users/enumerate", UserEnumerateRoute,
                openApi => openApi
                    .WithTag("Users")
                    .WithSummary("Enumerate users")
                    .WithDescription("Enumerate users for a tenant with pagination.")
                    .WithOperationId("userEnumerate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Pagination parameters"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("Paginated list of users"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                auth: true);

            _App.Put<UserMaster>("/v1.0/tenants/{tid}/users", UserCreateRoute,
                openApi => openApi
                    .WithTag("Users")
                    .WithSummary("Create user")
                    .WithDescription("Create a new user for a tenant. Admin or tenant admin access required.")
                    .WithOperationId("userCreate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "User to create", true))
                    .WithResponse(201, OpenApiResponseMetadata.Json("User created (password redacted)", null))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                auth: true);

            _App.Put<UserMaster>("/v1.0/tenants/{tid}/users/{id}", UserUpdateRoute,
                openApi => openApi
                    .WithTag("Users")
                    .WithSummary("Update user")
                    .WithDescription("Update an existing user. Admin or tenant admin access required.")
                    .WithOperationId("userUpdate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("id", "User ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Updated user data", true))
                    .WithResponse(200, OpenApiResponseMetadata.Json("User updated (password redacted)", null))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                auth: true);

            _App.Delete("/v1.0/tenants/{tid}/users/{id}", UserDeleteRoute,
                openApi => openApi
                    .WithTag("Users")
                    .WithSummary("Delete user")
                    .WithDescription("Delete a user. Admin or tenant admin access required.")
                    .WithOperationId("userDelete")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("id", "User ID"))
                    .WithResponse(204, OpenApiResponseMetadata.NoContent())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                auth: true);

            // Credential routes
            _App.Get("/v1.0/tenants/{tid}/credentials", CredentialListRoute,
                openApi => openApi
                    .WithTag("Credentials")
                    .WithSummary("List credentials")
                    .WithDescription("List all credentials for a tenant.")
                    .WithOperationId("credentialList")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("List of credentials"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                auth: true);

            _App.Get("/v1.0/tenants/{tid}/credentials/{id}", CredentialReadRoute,
                openApi => openApi
                    .WithTag("Credentials")
                    .WithSummary("Read credential")
                    .WithDescription("Retrieve a credential by ID.")
                    .WithOperationId("credentialRead")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Credential ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Credential details", null))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                auth: true);

            _App.Head("/v1.0/tenants/{tid}/credentials/{id}", CredentialExistsRoute,
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
                auth: true);

            _App.Post<EnumerationQuery>("/v1.0/tenants/{tid}/credentials/enumerate", CredentialEnumerateRoute,
                openApi => openApi
                    .WithTag("Credentials")
                    .WithSummary("Enumerate credentials")
                    .WithDescription("Enumerate credentials for a tenant with pagination.")
                    .WithOperationId("credentialEnumerate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Pagination parameters"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("Paginated list of credentials"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                auth: true);

            _App.Put<Credential>("/v1.0/tenants/{tid}/credentials", CredentialCreateRoute,
                openApi => openApi
                    .WithTag("Credentials")
                    .WithSummary("Create credential")
                    .WithDescription("Create a new credential for a tenant. Admin or tenant admin access required.")
                    .WithOperationId("credentialCreate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Credential to create", true))
                    .WithResponse(201, OpenApiResponseMetadata.Json("Credential created", null))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                auth: true);

            _App.Put<Credential>("/v1.0/tenants/{tid}/credentials/{id}", CredentialUpdateRoute,
                openApi => openApi
                    .WithTag("Credentials")
                    .WithSummary("Update credential")
                    .WithDescription("Update an existing credential. Admin or tenant admin access required.")
                    .WithOperationId("credentialUpdate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Credential ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Updated credential data", true))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Credential updated", null))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                auth: true);

            _App.Delete("/v1.0/tenants/{tid}/credentials/{id}", CredentialDeleteRoute,
                openApi => openApi
                    .WithTag("Credentials")
                    .WithSummary("Delete credential")
                    .WithDescription("Delete a credential. Admin or tenant admin access required.")
                    .WithOperationId("credentialDelete")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Credential ID"))
                    .WithResponse(204, OpenApiResponseMetadata.NoContent())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                auth: true);

            // Collection routes
            _App.Get("/v1.0/tenants/{tid}/collections", CollectionListRoute,
                openApi => openApi
                    .WithTag("Collections")
                    .WithSummary("List collections")
                    .WithDescription("List all collections for a tenant.")
                    .WithOperationId("collectionList")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("List of collections"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                auth: true);

            _App.Get("/v1.0/tenants/{tid}/collections/{cid}", CollectionReadRoute,
                openApi => openApi
                    .WithTag("Collections")
                    .WithSummary("Read collection")
                    .WithDescription("Retrieve a collection by ID including its dimensionality.")
                    .WithOperationId("collectionRead")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Collection details", null))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                auth: true);

            _App.Head("/v1.0/tenants/{tid}/collections/{cid}", CollectionExistsRoute,
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
                auth: true);

            _App.Post<EnumerationQuery>("/v1.0/tenants/{tid}/collections/enumerate", CollectionEnumerateRoute,
                openApi => openApi
                    .WithTag("Collections")
                    .WithSummary("Enumerate collections")
                    .WithDescription("Enumerate collections for a tenant with pagination.")
                    .WithOperationId("collectionEnumerate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Pagination parameters"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("Paginated list of collections"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                auth: true);

            _App.Put<CollectionMetadata>("/v1.0/tenants/{tid}/collections", CollectionCreateRoute,
                openApi => openApi
                    .WithTag("Collections")
                    .WithSummary("Create collection")
                    .WithDescription("Create a new vector collection. This creates the backing document, label, and tag tables with the specified vector dimensionality. Admin or tenant admin access required.")
                    .WithOperationId("collectionCreate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Collection to create including Dimensionality", true))
                    .WithResponse(201, OpenApiResponseMetadata.Json("Collection created", null))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                auth: true);

            _App.Put<CollectionMetadata>("/v1.0/tenants/{tid}/collections/{cid}", CollectionUpdateRoute,
                openApi => openApi
                    .WithTag("Collections")
                    .WithSummary("Update collection")
                    .WithDescription("Update an existing collection metadata.")
                    .WithOperationId("collectionUpdate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Updated collection data", true))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Collection updated", null))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                auth: true);

            _App.Delete("/v1.0/tenants/{tid}/collections/{cid}", CollectionDeleteRoute,
                openApi => openApi
                    .WithTag("Collections")
                    .WithSummary("Delete collection")
                    .WithDescription("Delete a collection and its backing document, label, and tag tables. Admin or tenant admin access required.")
                    .WithOperationId("collectionDelete")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithResponse(204, OpenApiResponseMetadata.NoContent())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                auth: true);

            _App.Get("/v1.0/tenants/{tid}/collections/{cid}/stats", CollectionStatsRoute,
                openApi => openApi
                    .WithTag("Collections")
                    .WithSummary("Get collection statistics")
                    .WithDescription("Returns document count, unique document count, total content length, label count, and tag count for a collection.")
                    .WithOperationId("collectionStats")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("Collection statistics"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                auth: true);

            // Document routes
            _App.Get("/v1.0/tenants/{tid}/collections/{cid}/documents", DocumentListRoute,
                openApi => openApi
                    .WithTag("Documents")
                    .WithSummary("List documents")
                    .WithDescription("List all documents in a collection.")
                    .WithOperationId("documentList")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("List of documents"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                auth: true);

            _App.Get("/v1.0/tenants/{tid}/collections/{cid}/documents/{docKey}", DocumentReadRoute,
                openApi => openApi
                    .WithTag("Documents")
                    .WithSummary("Read document by key")
                    .WithDescription("Retrieve a document by its unique document key.")
                    .WithOperationId("documentRead")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("docKey", "Document key"))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Document details", null))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                auth: true);

            _App.Get("/v1.0/tenants/{tid}/collections/{cid}/documents/{docId}/{position}", DocumentReadByPositionRoute,
                openApi => openApi
                    .WithTag("Documents")
                    .WithSummary("Read document by ID and position")
                    .WithDescription("Retrieve a specific document chunk by document ID and position index. Useful for chunk lineage.")
                    .WithOperationId("documentReadByPosition")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("docId", "Document ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("position", "Position index (0-based)"))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Document chunk details", null))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                auth: true);

            _App.Head("/v1.0/tenants/{tid}/collections/{cid}/documents/{docKey}", DocumentExistsRoute,
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
                auth: true);

            _App.Post<EnumerationQuery>("/v1.0/tenants/{tid}/collections/{cid}/documents/enumerate", DocumentEnumerateRoute,
                openApi => openApi
                    .WithTag("Documents")
                    .WithSummary("Enumerate documents")
                    .WithDescription("Enumerate documents in a collection with pagination.")
                    .WithOperationId("documentEnumerate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Pagination parameters"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("Paginated list of documents"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                auth: true);

            _App.Post<List<DocumentRecord>>("/v1.0/tenants/{tid}/collections/{cid}/documents/batch", DocumentBatchRoute,
                openApi => openApi
                    .WithTag("Documents")
                    .WithSummary("Batch create documents")
                    .WithDescription("Create multiple documents in a single transactional batch. Each document must include Embeddings matching the collection dimensionality.")
                    .WithOperationId("documentBatchCreate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "List of documents to create", true))
                    .WithResponse(201, OpenApiResponseMetadata.Create("Documents created"))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                auth: true);

            _App.Put<DocumentRecord>("/v1.0/tenants/{tid}/collections/{cid}/documents", DocumentCreateRoute,
                openApi => openApi
                    .WithTag("Documents")
                    .WithSummary("Create document")
                    .WithDescription("Create a new document with content and vector embeddings. Embeddings must match the collection dimensionality.")
                    .WithOperationId("documentCreate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Document to create", true))
                    .WithResponse(201, OpenApiResponseMetadata.Json("Document created", null))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                auth: true);

            _App.Put<DocumentRecord>("/v1.0/tenants/{tid}/collections/{cid}/documents/{docKey}", DocumentUpdateRoute,
                openApi => openApi
                    .WithTag("Documents")
                    .WithSummary("Update document")
                    .WithDescription("Update an existing document by its document key.")
                    .WithOperationId("documentUpdate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("docKey", "Document key"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Updated document data", true))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Document updated", null))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                auth: true);

            _App.Delete("/v1.0/tenants/{tid}/collections/{cid}/documents/{docKey}", DocumentDeleteRoute,
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
                auth: true);

            _App.Post<BatchDeleteRequest>("/v1.0/tenants/{tid}/collections/{cid}/documents/batch/delete", DocumentBatchDeleteRoute,
                openApi => openApi
                    .WithTag("Documents")
                    .WithSummary("Batch delete documents")
                    .WithDescription("Delete multiple documents by their document keys in a single operation. Associated labels and tags are also deleted.")
                    .WithOperationId("documentBatchDelete")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Batch delete request containing document keys", true))
                    .WithResponse(204, OpenApiResponseMetadata.NoContent())
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                auth: true);

            _App.Post<EnumerationQuery>("/v1.0/tenants/{tid}/collections/{cid}/documents/delete/filter", DocumentDeleteByFilterRoute,
                openApi => openApi
                    .WithTag("Documents")
                    .WithSummary("Delete documents by filter")
                    .WithDescription("Delete all documents matching the specified filter criteria. Uses the same filter model as the enumerate endpoint. Pagination fields (MaxResults, ContinuationToken, Ordering) are ignored. Associated labels and tags are also deleted.")
                    .WithOperationId("documentDeleteByFilter")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Filter criteria for documents to delete", true))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Delete result with count of deleted documents", null))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                auth: true);

            _App.Get("/v1.0/tenants/{tid}/collections/{cid}/documents/stats/{docKey}", DocumentStatsRoute,
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
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                auth: true);

            // Label routes
            _App.Get("/v1.0/tenants/{tid}/collections/{cid}/labels", LabelListRoute,
                openApi => openApi
                    .WithTag("Labels")
                    .WithSummary("List labels")
                    .WithDescription("List all labels in a collection.")
                    .WithOperationId("labelList")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("List of labels"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                auth: true);

            _App.Get("/v1.0/tenants/{tid}/collections/{cid}/labels/distinct", LabelDistinctRoute,
                openApi => openApi
                    .WithTag("Labels")
                    .WithSummary("Distinct labels")
                    .WithDescription("Retrieve distinct label values in a collection.")
                    .WithOperationId("labelDistinct")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("List of distinct labels"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                auth: true);

            _App.Get("/v1.0/tenants/{tid}/collections/{cid}/labels/{id}", LabelReadRoute,
                openApi => openApi
                    .WithTag("Labels")
                    .WithSummary("Read label")
                    .WithDescription("Retrieve a label by ID.")
                    .WithOperationId("labelRead")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Label ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Label details", null))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                auth: true);

            _App.Put<LabelRecord>("/v1.0/tenants/{tid}/collections/{cid}/labels", LabelCreateRoute,
                openApi => openApi
                    .WithTag("Labels")
                    .WithSummary("Create label")
                    .WithDescription("Create a new label associated with a document in a collection.")
                    .WithOperationId("labelCreate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Label to create", true))
                    .WithResponse(201, OpenApiResponseMetadata.Json("Label created", null))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                auth: true);

            _App.Delete("/v1.0/tenants/{tid}/collections/{cid}/labels/{id}", LabelDeleteRoute,
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
                auth: true);

            // Tag routes
            _App.Get("/v1.0/tenants/{tid}/collections/{cid}/tags", TagListRoute,
                openApi => openApi
                    .WithTag("Tags")
                    .WithSummary("List tags")
                    .WithDescription("List all tags in a collection.")
                    .WithOperationId("tagList")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("List of tags"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                auth: true);

            _App.Get("/v1.0/tenants/{tid}/collections/{cid}/tags/distinct", TagDistinctRoute,
                openApi => openApi
                    .WithTag("Tags")
                    .WithSummary("Distinct tag keys")
                    .WithDescription("Retrieve distinct tag keys in a collection.")
                    .WithOperationId("tagDistinctKeys")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Create("List of distinct tag keys"))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                auth: true);

            _App.Get("/v1.0/tenants/{tid}/collections/{cid}/tags/{id}", TagReadRoute,
                openApi => openApi
                    .WithTag("Tags")
                    .WithSummary("Read tag")
                    .WithDescription("Retrieve a tag by ID.")
                    .WithOperationId("tagRead")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Tag ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Tag details", null))
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                auth: true);

            _App.Put<TagRecord>("/v1.0/tenants/{tid}/collections/{cid}/tags", TagCreateRoute,
                openApi => openApi
                    .WithTag("Tags")
                    .WithSummary("Create tag")
                    .WithDescription("Create a new key-value tag associated with a document in a collection.")
                    .WithOperationId("tagCreate")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Tag to create", true))
                    .WithResponse(201, OpenApiResponseMetadata.Json("Tag created", null))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
                auth: true);

            _App.Delete("/v1.0/tenants/{tid}/collections/{cid}/tags/{id}", TagDeleteRoute,
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
                auth: true);

            // Search route
            _App.Post<SearchQuery>("/v1.0/tenants/{tid}/collections/{cid}/search", SearchRoute,
                openApi => openApi
                    .WithTag("Search")
                    .WithSummary("Search")
                    .WithDescription("Search within a collection using vector similarity, full-text relevance, or hybrid (combined) search. Vector search supports cosine similarity, cosine distance, euclidean similarity, euclidean distance, and inner product. Full-text search uses PostgreSQL ts_rank scoring with stemming and stop word removal. Hybrid search blends vector and text scores with configurable weighting. Filter results by labels, tags, date ranges, terms, and document IDs.")
                    .WithOperationId("search")
                    .WithParameter(OpenApiParameterMetadata.Path("tid", "Tenant ID"))
                    .WithParameter(OpenApiParameterMetadata.Path("cid", "Collection ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Search parameters including vector, filters, and pagination", true))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Search results with scored documents", null))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                auth: true);

            // Request History Routes
            _App.Get("/v1.0/requesthistory", RequestHistoryListRoute,
                openApi => openApi
                    .WithTag("Request History")
                    .WithSummary("List request history")
                    .WithDescription("List request history entries with optional filters and pagination.")
                    .WithOperationId("requestHistoryList")
                    .WithResponse(200, OpenApiResponseMetadata.Json("Request history entries", null)),
                auth: true);

            _App.Get("/v1.0/requesthistory/summary", RequestHistorySummaryRoute,
                openApi => openApi
                    .WithTag("Request History")
                    .WithSummary("Request history summary")
                    .WithDescription("Get aggregated request history buckets for charts. Supports intervals: minute, 15minute, hour, 6hour, day.")
                    .WithOperationId("requestHistorySummary")
                    .WithResponse(200, OpenApiResponseMetadata.Json("Summary buckets with success/failure counts", null)),
                auth: true);

            _App.Get("/v1.0/requesthistory/{guid}", RequestHistoryReadRoute,
                openApi => openApi
                    .WithTag("Request History")
                    .WithSummary("Get request history entry")
                    .WithDescription("Get a single request history entry by GUID.")
                    .WithOperationId("requestHistoryRead")
                    .WithParameter(OpenApiParameterMetadata.Path("guid", "Request history GUID"))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Request history entry", null))
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                auth: true);

            _App.Delete("/v1.0/requesthistory/{guid}", RequestHistoryDeleteRoute,
                openApi => openApi
                    .WithTag("Request History")
                    .WithSummary("Delete request history entry")
                    .WithDescription("Delete a single request history entry by GUID.")
                    .WithOperationId("requestHistoryDelete")
                    .WithParameter(OpenApiParameterMetadata.Path("guid", "Request history GUID"))
                    .WithResponse(204, OpenApiResponseMetadata.NoContent())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()),
                auth: true);
        }

        #endregion

        #region Default-Route

        private static async Task DefaultRoute(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 404;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.Send(Serializer.SerializeJson(new { Error = "Not found", StatusCode = 404 })).ConfigureAwait(false);
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
            result.AuthorizationResult = authResult.IsAuthenticated
                ? AuthorizationResultEnum.Permitted
                : AuthorizationResultEnum.DeniedImplicit;
            return result;
        }

        #endregion

        #region Route-Helpers

        private static AuthenticationResult GetAuthResult(ApiRequest req)
        {
            return req.Http.Metadata as AuthenticationResult;
        }

        private static RequestContext BuildContext(ApiRequest req, string requestType)
        {
            RequestContext ctx = new RequestContext();
            ctx.Origin = RequestOriginEnum.Rest;
            ctx.Auth = GetAuthResult(req);
            ctx.RequestType = requestType;
            OperationScope scope = OperationScopeMap.Resolve(requestType);
            ctx.ResourceType = scope.ResourceType;
            ctx.Operation = scope.Operation;
            return ctx;
        }

        private static object MapResult(ApiRequest req, ServiceResult result)
        {
            req.Http.Response.StatusCode = result.StatusCode;
            if (!result.Success)
                return new { Error = result.Error, StatusCode = result.StatusCode, Context = result.Context };
            if (result.StatusCode == 204) return null;
            return result.Data;
        }

        private static object MapExists(ApiRequest req, ServiceResult result)
        {
            req.Http.Response.StatusCode = result.StatusCode;
            return null;
        }

        #endregion

        #region Health-Routes

        private static async Task<object> HealthGetRoute(ApiRequest req)
        {
            double uptimeMs = (DateTime.UtcNow - _StartTimeUtc).TotalMilliseconds;
            return new
            {
                Name = "RecallDB",
                Version = _Version,
                UptimeMs = uptimeMs
            };
        }

        private static async Task<object> HealthHeadRoute(ApiRequest req)
        {
            req.Http.Response.StatusCode = 200;
            return null;
        }

        private static async Task<object> McpInfoRoute(ApiRequest req)
        {
            McpServerInfo info = new McpServerInfo();
            info.Version = _Version;
            info.UptimeMs = (DateTime.UtcNow - _StartTimeUtc).TotalMilliseconds;
            info.Enabled = _Settings.Mcp.Enabled;
            info.Hostname = _Settings.Mcp.Hostname;
            info.Port = _Settings.Mcp.Port;
            return info;
        }

        #endregion

        #region Authenticate-Route

        private static async Task<object> AuthenticateRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, OperationScopeMap.AuthAuthenticate);
            ctx.Payload = req.Data as AuthenticateRequest;
            ServiceResult result = await _Services.Auth.AuthenticateAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        #endregion

        #region Tenant-Routes

        private static async Task<object> TenantListRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "tenant/enumerate");
            ServiceResult result = await _Services.Tenants.ListAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> TenantReadRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "tenant/read");
            ctx.TenantId = req.Parameters["id"];
            ServiceResult result = await _Services.Tenants.ReadAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> TenantExistsRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "tenant/exists");
            ctx.TenantId = req.Parameters["id"];
            ServiceResult result = await _Services.Tenants.ExistsAsync(ctx).ConfigureAwait(false);
            return MapExists(req, result);
        }

        private static async Task<object> TenantEnumerateRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "tenant/enumerate");
            ctx.Query = req.Data as EnumerationQuery;
            ServiceResult result = await _Services.Tenants.EnumerateAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> TenantCreateRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "tenant/create");
            ctx.Payload = req.Data as TenantMetadata;
            ServiceResult result = await _Services.Tenants.CreateAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> TenantUpdateRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "tenant/update");
            ctx.TenantId = req.Parameters["id"];
            ctx.Payload = req.Data as TenantMetadata;
            ServiceResult result = await _Services.Tenants.UpdateAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> TenantDeleteRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "tenant/delete");
            ctx.TenantId = req.Parameters["id"];
            ServiceResult result = await _Services.Tenants.DeleteAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        #endregion

        #region User-Routes

        private static async Task<object> UserListRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "user/enumerate");
            ctx.TenantId = req.Parameters["tid"];
            ServiceResult result = await _Services.Users.ListAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> UserReadRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "user/read");
            ctx.TenantId = req.Parameters["tid"];
            ctx.UserId = req.Parameters["id"];
            ServiceResult result = await _Services.Users.ReadAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> UserExistsRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "user/exists");
            ctx.TenantId = req.Parameters["tid"];
            ctx.UserId = req.Parameters["id"];
            ServiceResult result = await _Services.Users.ExistsAsync(ctx).ConfigureAwait(false);
            return MapExists(req, result);
        }

        private static async Task<object> UserEnumerateRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "user/enumerate");
            ctx.TenantId = req.Parameters["tid"];
            ctx.Query = req.Data as EnumerationQuery;
            ServiceResult result = await _Services.Users.EnumerateAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> UserCreateRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "user/create");
            ctx.TenantId = req.Parameters["tid"];
            ctx.Payload = req.Data as UserMaster;
            ServiceResult result = await _Services.Users.CreateAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> UserUpdateRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "user/update");
            ctx.TenantId = req.Parameters["tid"];
            ctx.UserId = req.Parameters["id"];
            ctx.Payload = req.Data as UserMaster;
            ServiceResult result = await _Services.Users.UpdateAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> UserDeleteRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "user/delete");
            ctx.TenantId = req.Parameters["tid"];
            ctx.UserId = req.Parameters["id"];
            ServiceResult result = await _Services.Users.DeleteAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        #endregion

        #region Credential-Routes

        private static async Task<object> CredentialListRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "credential/enumerate");
            ctx.TenantId = req.Parameters["tid"];
            ServiceResult result = await _Services.Credentials.ListAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> CredentialReadRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "credential/read");
            ctx.TenantId = req.Parameters["tid"];
            ctx.ResourceId = req.Parameters["id"];
            ServiceResult result = await _Services.Credentials.ReadAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> CredentialExistsRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "credential/exists");
            ctx.TenantId = req.Parameters["tid"];
            ctx.ResourceId = req.Parameters["id"];
            ServiceResult result = await _Services.Credentials.ExistsAsync(ctx).ConfigureAwait(false);
            return MapExists(req, result);
        }

        private static async Task<object> CredentialEnumerateRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "credential/enumerate");
            ctx.TenantId = req.Parameters["tid"];
            ctx.Query = req.Data as EnumerationQuery;
            ServiceResult result = await _Services.Credentials.EnumerateAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> CredentialCreateRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "credential/create");
            ctx.TenantId = req.Parameters["tid"];
            ctx.Payload = req.Data as Credential;
            ServiceResult result = await _Services.Credentials.CreateAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> CredentialUpdateRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "credential/update");
            ctx.TenantId = req.Parameters["tid"];
            ctx.ResourceId = req.Parameters["id"];
            ctx.Payload = req.Data as Credential;
            ServiceResult result = await _Services.Credentials.UpdateAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> CredentialDeleteRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "credential/delete");
            ctx.TenantId = req.Parameters["tid"];
            ctx.ResourceId = req.Parameters["id"];
            ServiceResult result = await _Services.Credentials.DeleteAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        #endregion

        #region Collection-Routes

        private static async Task<object> CollectionListRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "collection/enumerate");
            ctx.TenantId = req.Parameters["tid"];
            ServiceResult result = await _Services.Collections.ListAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> CollectionReadRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "collection/read");
            ctx.TenantId = req.Parameters["tid"];
            ctx.CollectionId = req.Parameters["cid"];
            ServiceResult result = await _Services.Collections.ReadAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> CollectionExistsRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "collection/exists");
            ctx.TenantId = req.Parameters["tid"];
            ctx.CollectionId = req.Parameters["cid"];
            ServiceResult result = await _Services.Collections.ExistsAsync(ctx).ConfigureAwait(false);
            return MapExists(req, result);
        }

        private static async Task<object> CollectionEnumerateRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "collection/enumerate");
            ctx.TenantId = req.Parameters["tid"];
            ctx.Query = req.Data as EnumerationQuery;
            ServiceResult result = await _Services.Collections.EnumerateAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> CollectionCreateRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "collection/create");
            ctx.TenantId = req.Parameters["tid"];
            ctx.Payload = req.Data as CollectionMetadata;
            ServiceResult result = await _Services.Collections.CreateAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> CollectionUpdateRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "collection/update");
            ctx.TenantId = req.Parameters["tid"];
            ctx.CollectionId = req.Parameters["cid"];
            ctx.Payload = req.Data as CollectionMetadata;
            ServiceResult result = await _Services.Collections.UpdateAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> CollectionDeleteRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "collection/delete");
            ctx.TenantId = req.Parameters["tid"];
            ctx.CollectionId = req.Parameters["cid"];
            ServiceResult result = await _Services.Collections.DeleteAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> CollectionStatsRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "collection/stats");
            ctx.TenantId = req.Parameters["tid"];
            ctx.CollectionId = req.Parameters["cid"];
            ServiceResult result = await _Services.Collections.StatsAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        #endregion

        #region Document-Routes

        private static async Task<object> DocumentListRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "document/enumerate");
            ctx.TenantId = req.Parameters["tid"];
            ctx.CollectionId = req.Parameters["cid"];
            ServiceResult result = await _Services.Documents.ListAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> DocumentReadRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "document/read");
            ctx.TenantId = req.Parameters["tid"];
            ctx.CollectionId = req.Parameters["cid"];
            ctx.DocumentKey = req.Parameters["docKey"];
            ServiceResult result = await _Services.Documents.ReadAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> DocumentReadByPositionRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "document/readByPosition");
            ctx.TenantId = req.Parameters["tid"];
            ctx.CollectionId = req.Parameters["cid"];
            ctx.DocumentId = req.Parameters["docId"];
            if (int.TryParse(req.Parameters["position"], out int position)) ctx.Position = position;
            ServiceResult result = await _Services.Documents.ReadByPositionAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> DocumentExistsRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "document/exists");
            ctx.TenantId = req.Parameters["tid"];
            ctx.CollectionId = req.Parameters["cid"];
            ctx.DocumentKey = req.Parameters["docKey"];
            ServiceResult result = await _Services.Documents.ExistsAsync(ctx).ConfigureAwait(false);
            return MapExists(req, result);
        }

        private static async Task<object> DocumentEnumerateRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "document/enumerate");
            ctx.TenantId = req.Parameters["tid"];
            ctx.CollectionId = req.Parameters["cid"];
            ctx.Query = req.Data as EnumerationQuery;
            ServiceResult result = await _Services.Documents.EnumerateAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> DocumentCreateRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "document/create");
            ctx.TenantId = req.Parameters["tid"];
            ctx.CollectionId = req.Parameters["cid"];
            ctx.Payload = req.Data as DocumentRecord;
            ServiceResult result = await _Services.Documents.CreateAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> DocumentUpdateRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "document/update");
            ctx.TenantId = req.Parameters["tid"];
            ctx.CollectionId = req.Parameters["cid"];
            ctx.DocumentKey = req.Parameters["docKey"];
            ctx.Payload = req.Data as DocumentRecord;
            ServiceResult result = await _Services.Documents.UpdateAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> DocumentDeleteRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "document/delete");
            ctx.TenantId = req.Parameters["tid"];
            ctx.CollectionId = req.Parameters["cid"];
            ctx.DocumentKey = req.Parameters["docKey"];
            ServiceResult result = await _Services.Documents.DeleteAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> DocumentBatchDeleteRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "document/batchDelete");
            ctx.TenantId = req.Parameters["tid"];
            ctx.CollectionId = req.Parameters["cid"];
            ctx.Payload = req.Data as BatchDeleteRequest;
            ServiceResult result = await _Services.Documents.BatchDeleteAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> DocumentDeleteByFilterRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "document/deleteByFilter");
            ctx.TenantId = req.Parameters["tid"];
            ctx.CollectionId = req.Parameters["cid"];
            ctx.Query = req.Data as EnumerationQuery;
            ServiceResult result = await _Services.Documents.DeleteByFilterAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> DocumentBatchRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "document/batchCreate");
            ctx.TenantId = req.Parameters["tid"];
            ctx.CollectionId = req.Parameters["cid"];
            ctx.Payload = req.Data as List<DocumentRecord>;
            ServiceResult result = await _Services.Documents.BatchCreateAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> DocumentStatsRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "document/stats");
            ctx.TenantId = req.Parameters["tid"];
            ctx.CollectionId = req.Parameters["cid"];
            ctx.DocumentKey = req.Parameters["docKey"];
            ServiceResult result = await _Services.Documents.StatsAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        #endregion

        #region Label-Routes

        private static async Task<object> LabelListRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "label/enumerate");
            ctx.TenantId = req.Parameters["tid"];
            ctx.CollectionId = req.Parameters["cid"];
            ServiceResult result = await _Services.Labels.ListAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> LabelReadRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "label/read");
            ctx.TenantId = req.Parameters["tid"];
            ctx.CollectionId = req.Parameters["cid"];
            ctx.ResourceId = req.Parameters["id"];
            ServiceResult result = await _Services.Labels.ReadAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> LabelCreateRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "label/create");
            ctx.TenantId = req.Parameters["tid"];
            ctx.CollectionId = req.Parameters["cid"];
            ctx.Payload = req.Data as LabelRecord;
            ServiceResult result = await _Services.Labels.CreateAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> LabelDeleteRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "label/delete");
            ctx.TenantId = req.Parameters["tid"];
            ctx.CollectionId = req.Parameters["cid"];
            ctx.ResourceId = req.Parameters["id"];
            ServiceResult result = await _Services.Labels.DeleteAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> LabelDistinctRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "label/read");
            ctx.TenantId = req.Parameters["tid"];
            ctx.CollectionId = req.Parameters["cid"];
            ServiceResult result = await _Services.Labels.DistinctAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        #endregion

        #region Tag-Routes

        private static async Task<object> TagListRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "tag/enumerate");
            ctx.TenantId = req.Parameters["tid"];
            ctx.CollectionId = req.Parameters["cid"];
            ServiceResult result = await _Services.Tags.ListAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> TagReadRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "tag/read");
            ctx.TenantId = req.Parameters["tid"];
            ctx.CollectionId = req.Parameters["cid"];
            ctx.ResourceId = req.Parameters["id"];
            ServiceResult result = await _Services.Tags.ReadAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> TagCreateRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "tag/create");
            ctx.TenantId = req.Parameters["tid"];
            ctx.CollectionId = req.Parameters["cid"];
            ctx.Payload = req.Data as TagRecord;
            ServiceResult result = await _Services.Tags.CreateAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> TagDeleteRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "tag/delete");
            ctx.TenantId = req.Parameters["tid"];
            ctx.CollectionId = req.Parameters["cid"];
            ctx.ResourceId = req.Parameters["id"];
            ServiceResult result = await _Services.Tags.DeleteAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> TagDistinctRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "tag/read");
            ctx.TenantId = req.Parameters["tid"];
            ctx.CollectionId = req.Parameters["cid"];
            ServiceResult result = await _Services.Tags.DistinctAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        #endregion

        #region Search-Route

        private static async Task<object> SearchRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "search/query");
            ctx.TenantId = req.Parameters["tid"];
            ctx.CollectionId = req.Parameters["cid"];
            ctx.Search = req.Data as SearchQuery;
            ServiceResult result = await _Services.Search.SearchAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        #endregion

        #region Request-History-Routes

        private static async Task<object> RequestHistoryListRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "requestHistory/enumerate");

            RequestHistoryFilter filter = new RequestHistoryFilter();
            filter.Method = req.Query["method"];
            filter.SourceIp = req.Query["sourceIp"];

            if (!string.IsNullOrEmpty(req.Query["statusCode"]) && int.TryParse(req.Query["statusCode"], out int sc)) filter.StatusCode = sc;
            if (!string.IsNullOrEmpty(req.Query["startUtc"]) && DateTime.TryParse(req.Query["startUtc"], null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime su)) filter.StartUtc = su;
            if (!string.IsNullOrEmpty(req.Query["endUtc"]) && DateTime.TryParse(req.Query["endUtc"], null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime eu)) filter.EndUtc = eu;
            if (!string.IsNullOrEmpty(req.Query["maxResults"]) && int.TryParse(req.Query["maxResults"], out int mr)) filter.MaxResults = mr;
            if (!string.IsNullOrEmpty(req.Query["offset"]) && int.TryParse(req.Query["offset"], out int o)) filter.Offset = o;

            ctx.Payload = filter;
            ServiceResult result = await _Services.RequestHistory.EnumerateAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> RequestHistorySummaryRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "requestHistory/summary");

            RequestHistoryFilter filter = new RequestHistoryFilter();
            if (!string.IsNullOrEmpty(req.Query["startUtc"]) && DateTime.TryParse(req.Query["startUtc"], null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime su)) filter.StartUtc = su;
            if (!string.IsNullOrEmpty(req.Query["endUtc"]) && DateTime.TryParse(req.Query["endUtc"], null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime eu)) filter.EndUtc = eu;
            filter.Interval = req.Query["interval"];

            ctx.Payload = filter;
            ServiceResult result = await _Services.RequestHistory.SummaryAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> RequestHistoryReadRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "requestHistory/read");
            ctx.ResourceId = req.Parameters["guid"];
            ServiceResult result = await _Services.RequestHistory.ReadAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        private static async Task<object> RequestHistoryDeleteRoute(ApiRequest req)
        {
            RequestContext ctx = BuildContext(req, "requestHistory/delete");
            ctx.ResourceId = req.Parameters["guid"];
            ServiceResult result = await _Services.RequestHistory.DeleteAsync(ctx).ConfigureAwait(false);
            return MapResult(req, result);
        }

        #endregion
    }

    /// <summary>
    /// Extension methods bridging SwiftStack fluent API to Watson 7.
    /// </summary>
    internal static class OpenApiRouteMetadataExtensions
    {
        public static OpenApiRouteMetadata WithSummary(this OpenApiRouteMetadata metadata, string summary)
        {
            metadata.Summary = summary;
            return metadata;
        }

        public static OpenApiRouteMetadata WithOperationId(this OpenApiRouteMetadata metadata, string operationId)
        {
            metadata.OperationId = operationId;
            return metadata;
        }
    }
}
