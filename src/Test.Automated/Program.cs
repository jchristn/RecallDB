namespace Test.Automated
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    public class Program
    {
        #region Private-Members

        private static string _Endpoint = "http://localhost:8600";
        private static string _ApiKey = "recalldbadmin";
        private static HttpClient _AdminClient = null;
        private static HttpClient _UserClient = null;
        private static int _Passed = 0;
        private static int _Failed = 0;
        private static List<string> _FailedTests = new List<string>();
        private static Stopwatch _TotalTimer = new Stopwatch();

        private static JsonSerializerOptions _JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        // Test data IDs to track for cleanup
        private static string _TestTenantId = null;
        private static string _TestUserId = null;
        private static string _TestCredentialId = null;
        private static string _TestUserBearerToken = null;
        private static string _TestCollectionId = null;
        private static string _TestDocumentKey = null;
        private static List<string> _TestBatchDocumentKeys = new List<string>();
        private static string _TestLabelId = null;
        private static string _TestTagId = null;
        private static List<string> _PaginationTenantIds = new List<string>();

        #endregion

        #region Entry-Point

        public static async Task Main(string[] args)
        {
            if (args != null && args.Length > 0) _Endpoint = args[0];
            if (args != null && args.Length > 1) _ApiKey = args[1];

            _Endpoint = _Endpoint.TrimEnd('/');

            Console.WriteLine("=========================================");
            Console.WriteLine("  RecallDB Integration Test Harness");
            Console.WriteLine("=========================================");
            Console.WriteLine("  Endpoint : " + _Endpoint);
            Console.WriteLine("  API Key  : " + _ApiKey);
            Console.WriteLine("=========================================");
            Console.WriteLine("");

            _AdminClient = CreateHttpClient(_ApiKey);

            _TotalTimer.Start();

            // 1. Connectivity
            await RunTest("Connectivity: GET /", TestConnectivityGet);
            await RunTest("Connectivity: HEAD /", TestConnectivityHead);

            // 2. Authentication
            await RunTest("Authentication: POST /v1.0/authenticate with bearer token", TestAuthenticateBearer);
            await RunTest("Authentication: POST /v1.0/authenticate with email+password", TestAuthenticateEmailPassword);

            // 3. Tenant CRUD
            await RunTest("Tenant: PUT create", TestTenantCreate);
            await RunTest("Tenant: GET read", TestTenantRead);
            await RunTest("Tenant: PUT update", TestTenantUpdate);
            await RunTest("Tenant: POST enumerate", TestTenantEnumerate);
            await RunTest("Tenant: HEAD exists", TestTenantExists);

            // 4. User CRUD
            await RunTest("User: PUT create", TestUserCreate);
            await RunTest("User: GET read", TestUserRead);
            await RunTest("User: PUT update", TestUserUpdate);
            await RunTest("User: POST enumerate", TestUserEnumerate);
            await RunTest("User: HEAD exists", TestUserExists);

            // 5. Credential CRUD
            await RunTest("Credential: PUT create", TestCredentialCreate);
            await RunTest("Credential: GET read", TestCredentialRead);
            await RunTest("Credential: POST enumerate", TestCredentialEnumerate);
            await RunTest("Credential: HEAD exists", TestCredentialExists);

            // 6. Collection CRUD
            await RunTest("Collection: PUT create", TestCollectionCreate);
            await RunTest("Collection: GET read", TestCollectionRead);
            await RunTest("Collection: PUT update", TestCollectionUpdate);
            await RunTest("Collection: POST enumerate", TestCollectionEnumerate);
            await RunTest("Collection: HEAD exists", TestCollectionExists);

            // 7. Document CRUD
            await RunTest("Document: PUT create", TestDocumentCreate);
            await RunTest("Document: GET read", TestDocumentRead);
            await RunTest("Document: PUT update", TestDocumentUpdate);

            // 8. Document Batch
            await RunTest("Document: POST batch create", TestDocumentBatchCreate);

            // 9. Label CRUD
            await RunTest("Label: PUT create", TestLabelCreate);
            await RunTest("Label: GET list", TestLabelList);

            // 10. Tag CRUD
            await RunTest("Tag: PUT create", TestTagCreate);
            await RunTest("Tag: GET list", TestTagList);

            // 11. Vector Search
            await RunTest("Search: cosine similarity", TestSearchCosineSimilarity);
            await RunTest("Search: euclidean distance", TestSearchEuclideanDistance);
            await RunTest("Search: inner product", TestSearchInnerProduct);

            // 12. Search Filters
            await RunTest("Search: label filter", TestSearchLabelFilter);
            await RunTest("Search: tag filter", TestSearchTagFilter);
            await RunTest("Search: date range filter", TestSearchDateRangeFilter);

            // 13. Enumeration Pagination
            await RunTest("Enumeration: pagination", TestEnumerationPagination);

            // 14. Authorization
            await RunTest("Authorization: non-admin cannot create tenant", TestAuthorizationNonAdmin);

            // 15. Cleanup
            await RunTest("Cleanup: delete labels", TestCleanupLabels);
            await RunTest("Cleanup: delete tags", TestCleanupTags);
            await RunTest("Cleanup: delete documents", TestCleanupDocuments);
            await RunTest("Cleanup: delete collection", TestCleanupCollection);
            await RunTest("Cleanup: delete credential", TestCleanupCredential);
            await RunTest("Cleanup: delete user", TestCleanupUser);
            await RunTest("Cleanup: delete tenant", TestCleanupTenant);
            await RunTest("Cleanup: delete pagination tenants", TestCleanupPaginationTenants);

            _TotalTimer.Stop();

            Console.WriteLine("");
            Console.WriteLine("=========================================");
            Console.WriteLine("  Test Summary");
            Console.WriteLine("=========================================");
            Console.WriteLine("  Total    : " + (_Passed + _Failed));
            Console.WriteLine("  Passed   : " + _Passed);
            Console.WriteLine("  Failed   : " + _Failed);
            Console.WriteLine("  Runtime  : " + _TotalTimer.ElapsedMilliseconds + " ms");
            Console.WriteLine("  Result   : " + (_Failed == 0 ? "PASS" : "FAIL"));

            if (_FailedTests.Count > 0)
            {
                Console.WriteLine("");
                Console.WriteLine("  Failed Tests:");
                foreach (string name in _FailedTests)
                {
                    Console.WriteLine("    - " + name);
                }
            }

            Console.WriteLine("=========================================");

            if (_AdminClient != null) _AdminClient.Dispose();
            if (_UserClient != null) _UserClient.Dispose();

            Environment.ExitCode = _Failed == 0 ? 0 : 1;
        }

        #endregion

        #region Test-Runner

        private static async Task RunTest(string name, Func<Task> test)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            try
            {
                await test().ConfigureAwait(false);
                sw.Stop();
                _Passed++;
                Console.WriteLine("  [PASS] " + name + " (" + sw.ElapsedMilliseconds + " ms)");
            }
            catch (Exception e)
            {
                sw.Stop();
                _Failed++;
                _FailedTests.Add(name);
                Console.WriteLine("  [FAIL] " + name + " (" + sw.ElapsedMilliseconds + " ms)");
                Console.WriteLine("         " + e.GetType().Name + ": " + e.Message);
            }
        }

        #endregion

        #region HTTP-Helpers

        private static HttpClient CreateHttpClient(string bearerToken)
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(_Endpoint);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string path)
        {
            HttpResponseMessage response = await client.GetAsync(path).ConfigureAwait(false);
            return response;
        }

        private static async Task<HttpResponseMessage> PutAsync(HttpClient client, string path, object body)
        {
            string json = JsonSerializer.Serialize(body, _JsonOptions);
            StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PutAsync(path, content).ConfigureAwait(false);
            return response;
        }

        private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body)
        {
            string json = JsonSerializer.Serialize(body, _JsonOptions);
            StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync(path, content).ConfigureAwait(false);
            return response;
        }

        private static async Task<HttpResponseMessage> DeleteAsync(HttpClient client, string path)
        {
            HttpResponseMessage response = await client.DeleteAsync(path).ConfigureAwait(false);
            return response;
        }

        private static async Task<HttpResponseMessage> HeadAsync(HttpClient client, string path)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, path);
            HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
            return response;
        }

        private static async Task<T> ReadResponse<T>(HttpResponseMessage response)
        {
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (string.IsNullOrEmpty(body)) return default;
            T result = JsonSerializer.Deserialize<T>(body, _JsonOptions);
            return result;
        }

        private static void AssertStatusCode(HttpResponseMessage response, HttpStatusCode expected)
        {
            if (response.StatusCode != expected)
            {
                throw new Exception(
                    "Expected status " + (int)expected + " " + expected +
                    " but got " + (int)response.StatusCode + " " + response.StatusCode);
            }
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition) throw new Exception("Assertion failed: " + message);
        }

        private static void AssertNotNull(object value, string name)
        {
            if (value == null) throw new Exception("Expected non-null value for " + name);
        }

        private static void AssertNotNullOrEmpty(string value, string name)
        {
            if (string.IsNullOrEmpty(value)) throw new Exception("Expected non-null/non-empty value for " + name);
        }

        private static void AssertEqual(string expected, string actual, string name)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                throw new Exception(
                    "Expected '" + expected + "' but got '" + actual + "' for " + name);
            }
        }

        private static void AssertEqual(int expected, int actual, string name)
        {
            if (expected != actual)
            {
                throw new Exception(
                    "Expected " + expected + " but got " + actual + " for " + name);
            }
        }

        #endregion

        #region Test-1-Connectivity

        private static async Task TestConnectivityGet()
        {
            HttpResponseMessage response = await GetAsync(_AdminClient, "/").ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Name", out JsonElement nameElem), "Response should contain Name");
            AssertEqual("RecallDB", nameElem.GetString(), "Name");
        }

        private static async Task TestConnectivityHead()
        {
            HttpResponseMessage response = await HeadAsync(_AdminClient, "/").ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);
        }

        #endregion

        #region Test-2-Authentication

        private static async Task TestAuthenticateBearer()
        {
            object body = new { BearerToken = "default" };
            HttpResponseMessage response = await PostAsync(_AdminClient, "/v1.0/authenticate", body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Success", out JsonElement successElem), "Response should contain Success");
            AssertTrue(successElem.GetBoolean(), "Authentication should succeed");
        }

        private static async Task TestAuthenticateEmailPassword()
        {
            object body = new { TenantId = "default", Email = "admin@recall", Password = "password" };
            HttpResponseMessage response = await PostAsync(_AdminClient, "/v1.0/authenticate", body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Success", out JsonElement successElem), "Response should contain Success");
            AssertTrue(successElem.GetBoolean(), "Authentication should succeed");
        }

        #endregion

        #region Test-3-Tenant-CRUD

        private static async Task TestTenantCreate()
        {
            object body = new { Name = "IntegrationTestTenant" };
            HttpResponseMessage response = await PutAsync(_AdminClient, "/v1.0/tenants", body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.Created);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Id", out JsonElement idElem), "Response should contain Id");
            _TestTenantId = idElem.GetString();
            AssertNotNullOrEmpty(_TestTenantId, "TenantId");

            AssertTrue(json.TryGetProperty("Name", out JsonElement nameElem), "Response should contain Name");
            AssertEqual("IntegrationTestTenant", nameElem.GetString(), "Name");
        }

        private static async Task TestTenantRead()
        {
            HttpResponseMessage response = await GetAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Id", out JsonElement idElem), "Response should contain Id");
            AssertEqual(_TestTenantId, idElem.GetString(), "Id");
        }

        private static async Task TestTenantUpdate()
        {
            object body = new { Name = "IntegrationTestTenantUpdated" };
            HttpResponseMessage response = await PutAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId, body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Name", out JsonElement nameElem), "Response should contain Name");
            AssertEqual("IntegrationTestTenantUpdated", nameElem.GetString(), "Name");
        }

        private static async Task TestTenantEnumerate()
        {
            object body = new { MaxResults = 100 };
            HttpResponseMessage response = await PostAsync(_AdminClient, "/v1.0/tenants/enumerate", body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Success", out JsonElement successElem), "Response should contain Success");
            AssertTrue(successElem.GetBoolean(), "Enumeration should succeed");
            AssertTrue(json.TryGetProperty("TotalRecords", out JsonElement totalElem), "Response should contain TotalRecords");
            AssertTrue(totalElem.GetInt64() >= 1, "TotalRecords should be >= 1");
        }

        private static async Task TestTenantExists()
        {
            HttpResponseMessage response = await HeadAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);
        }

        #endregion

        #region Test-4-User-CRUD

        private static async Task TestUserCreate()
        {
            object body = new
            {
                Email = "testuser@integrationtest.local",
                FirstName = "Test",
                LastName = "User",
                PasswordSha256 = "5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8", // password
                IsAdmin = false,
                IsTenantAdmin = false,
                Active = true
            };
            HttpResponseMessage response = await PutAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId + "/users", body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.Created);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Id", out JsonElement idElem), "Response should contain Id");
            _TestUserId = idElem.GetString();
            AssertNotNullOrEmpty(_TestUserId, "UserId");
        }

        private static async Task TestUserRead()
        {
            HttpResponseMessage response = await GetAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId + "/users/" + _TestUserId).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Id", out JsonElement idElem), "Response should contain Id");
            AssertEqual(_TestUserId, idElem.GetString(), "Id");
            AssertTrue(json.TryGetProperty("Email", out JsonElement emailElem), "Response should contain Email");
            AssertEqual("testuser@integrationtest.local", emailElem.GetString(), "Email");
        }

        private static async Task TestUserUpdate()
        {
            object body = new
            {
                Email = "testuser@integrationtest.local",
                FirstName = "TestUpdated",
                LastName = "UserUpdated"
            };
            HttpResponseMessage response = await PutAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId + "/users/" + _TestUserId, body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("FirstName", out JsonElement fnElem), "Response should contain FirstName");
            AssertEqual("TestUpdated", fnElem.GetString(), "FirstName");
        }

        private static async Task TestUserEnumerate()
        {
            object body = new { MaxResults = 100 };
            HttpResponseMessage response = await PostAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId + "/users/enumerate", body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("TotalRecords", out JsonElement totalElem), "Response should contain TotalRecords");
            AssertTrue(totalElem.GetInt64() >= 1, "TotalRecords should be >= 1");
        }

        private static async Task TestUserExists()
        {
            HttpResponseMessage response = await HeadAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId + "/users/" + _TestUserId).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);
        }

        #endregion

        #region Test-5-Credential-CRUD

        private static async Task TestCredentialCreate()
        {
            object body = new
            {
                UserId = _TestUserId,
                Name = "IntegrationTestCredential",
                Active = true
            };
            HttpResponseMessage response = await PutAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId + "/credentials", body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.Created);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Id", out JsonElement idElem), "Response should contain Id");
            _TestCredentialId = idElem.GetString();
            AssertNotNullOrEmpty(_TestCredentialId, "CredentialId");

            AssertTrue(json.TryGetProperty("BearerToken", out JsonElement tokenElem), "Response should contain BearerToken");
            _TestUserBearerToken = tokenElem.GetString();
            AssertNotNullOrEmpty(_TestUserBearerToken, "BearerToken");

            // Create the user-level HttpClient for authorization tests later
            _UserClient = CreateHttpClient(_TestUserBearerToken);
        }

        private static async Task TestCredentialRead()
        {
            HttpResponseMessage response = await GetAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId + "/credentials/" + _TestCredentialId).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Id", out JsonElement idElem), "Response should contain Id");
            AssertEqual(_TestCredentialId, idElem.GetString(), "Id");
        }

        private static async Task TestCredentialEnumerate()
        {
            object body = new { MaxResults = 100 };
            HttpResponseMessage response = await PostAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId + "/credentials/enumerate", body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("TotalRecords", out JsonElement totalElem), "Response should contain TotalRecords");
            AssertTrue(totalElem.GetInt64() >= 1, "TotalRecords should be >= 1");
        }

        private static async Task TestCredentialExists()
        {
            HttpResponseMessage response = await HeadAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId + "/credentials/" + _TestCredentialId).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);
        }

        #endregion

        #region Test-6-Collection-CRUD

        private static async Task TestCollectionCreate()
        {
            object body = new
            {
                Name = "IntegrationTestCollection",
                Description = "Test collection for integration tests",
                Dimensionality = 3
            };
            HttpResponseMessage response = await PutAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId + "/collections", body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.Created);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Id", out JsonElement idElem), "Response should contain Id");
            _TestCollectionId = idElem.GetString();
            AssertNotNullOrEmpty(_TestCollectionId, "CollectionId");

            AssertTrue(json.TryGetProperty("Dimensionality", out JsonElement dimElem), "Response should contain Dimensionality");
            AssertEqual(3, dimElem.GetInt32(), "Dimensionality");
        }

        private static async Task TestCollectionRead()
        {
            HttpResponseMessage response = await GetAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Id", out JsonElement idElem), "Response should contain Id");
            AssertEqual(_TestCollectionId, idElem.GetString(), "Id");
        }

        private static async Task TestCollectionUpdate()
        {
            object body = new
            {
                Name = "IntegrationTestCollectionUpdated",
                Description = "Updated description",
                Dimensionality = 3
            };
            HttpResponseMessage response = await PutAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId, body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Name", out JsonElement nameElem), "Response should contain Name");
            AssertEqual("IntegrationTestCollectionUpdated", nameElem.GetString(), "Name");
        }

        private static async Task TestCollectionEnumerate()
        {
            object body = new { MaxResults = 100 };
            HttpResponseMessage response = await PostAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId + "/collections/enumerate", body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("TotalRecords", out JsonElement totalElem), "Response should contain TotalRecords");
            AssertTrue(totalElem.GetInt64() >= 1, "TotalRecords should be >= 1");
        }

        private static async Task TestCollectionExists()
        {
            HttpResponseMessage response = await HeadAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);
        }

        #endregion

        #region Test-7-Document-CRUD

        private static async Task TestDocumentCreate()
        {
            string docKey = "testdoc-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            object body = new
            {
                DocumentKey = docKey,
                DocumentId = "testdocid-001",
                Content = "This is a test document for integration testing.",
                ContentType = "Text",
                Position = 0,
                Embeddings = new List<float> { 0.1f, 0.2f, 0.3f }
            };

            string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/documents";
            HttpResponseMessage response = await PutAsync(_AdminClient, path, body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.Created);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("DocumentKey", out JsonElement keyElem), "Response should contain DocumentKey");
            _TestDocumentKey = keyElem.GetString();
            AssertNotNullOrEmpty(_TestDocumentKey, "DocumentKey");
        }

        private static async Task TestDocumentRead()
        {
            string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/documents/" + _TestDocumentKey;
            HttpResponseMessage response = await GetAsync(_AdminClient, path).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("DocumentKey", out JsonElement keyElem), "Response should contain DocumentKey");
            AssertEqual(_TestDocumentKey, keyElem.GetString(), "DocumentKey");
            AssertTrue(json.TryGetProperty("Content", out JsonElement contentElem), "Response should contain Content");
            AssertEqual("This is a test document for integration testing.", contentElem.GetString(), "Content");
        }

        private static async Task TestDocumentUpdate()
        {
            object body = new
            {
                DocumentKey = _TestDocumentKey,
                DocumentId = "testdocid-001",
                Content = "This is an updated test document.",
                ContentType = "Text",
                Position = 0,
                Embeddings = new List<float> { 0.1f, 0.2f, 0.3f }
            };

            string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/documents/" + _TestDocumentKey;
            HttpResponseMessage response = await PutAsync(_AdminClient, path, body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Content", out JsonElement contentElem), "Response should contain Content");
            AssertEqual("This is an updated test document.", contentElem.GetString(), "Content");
        }

        #endregion

        #region Test-8-Document-Batch

        private static async Task TestDocumentBatchCreate()
        {
            List<object> docs = new List<object>();

            for (int i = 0; i < 3; i++)
            {
                string batchKey = "batchdoc-" + i + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                _TestBatchDocumentKeys.Add(batchKey);

                // Use different embeddings for each document for search ordering tests
                float baseVal = (i + 1) * 0.1f;
                docs.Add(new
                {
                    DocumentKey = batchKey,
                    DocumentId = "batchdocid-" + i,
                    Content = "Batch document number " + i,
                    ContentType = "Text",
                    Position = 0,
                    Embeddings = new List<float> { baseVal, baseVal + 0.1f, baseVal + 0.2f }
                });
            }

            string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/documents/batch";
            HttpResponseMessage response = await PostAsync(_AdminClient, path, docs).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.Created);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.ValueKind == JsonValueKind.Array, "Response should be an array");
            AssertEqual(3, json.GetArrayLength(), "Batch should create 3 documents");

            // Verify all documents were created by reading each one
            foreach (string batchKey in _TestBatchDocumentKeys)
            {
                string readPath = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/documents/" + batchKey;
                HttpResponseMessage readResp = await GetAsync(_AdminClient, readPath).ConfigureAwait(false);
                AssertStatusCode(readResp, HttpStatusCode.OK);
            }
        }

        #endregion

        #region Test-9-Label-CRUD

        private static async Task TestLabelCreate()
        {
            object body = new
            {
                DocumentKey = _TestDocumentKey,
                Label = "integration-test-label"
            };

            string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/labels";
            HttpResponseMessage response = await PutAsync(_AdminClient, path, body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.Created);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Id", out JsonElement idElem), "Response should contain Id");
            _TestLabelId = idElem.GetString();
            AssertNotNullOrEmpty(_TestLabelId, "LabelId");

            AssertTrue(json.TryGetProperty("Label", out JsonElement labelElem), "Response should contain Label");
            AssertEqual("integration-test-label", labelElem.GetString(), "Label");
        }

        private static async Task TestLabelList()
        {
            string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/labels";
            HttpResponseMessage response = await GetAsync(_AdminClient, path).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("TotalRecords", out JsonElement totalElem), "Response should contain TotalRecords");
            AssertTrue(totalElem.GetInt64() >= 1, "TotalRecords should be >= 1");
        }

        #endregion

        #region Test-10-Tag-CRUD

        private static async Task TestTagCreate()
        {
            object body = new
            {
                DocumentKey = _TestDocumentKey,
                Key = "environment",
                Value = "integration-test"
            };

            string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/tags";
            HttpResponseMessage response = await PutAsync(_AdminClient, path, body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.Created);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Id", out JsonElement idElem), "Response should contain Id");
            _TestTagId = idElem.GetString();
            AssertNotNullOrEmpty(_TestTagId, "TagId");

            AssertTrue(json.TryGetProperty("Key", out JsonElement keyElem), "Response should contain Key");
            AssertEqual("environment", keyElem.GetString(), "Key");
        }

        private static async Task TestTagList()
        {
            string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/tags";
            HttpResponseMessage response = await GetAsync(_AdminClient, path).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("TotalRecords", out JsonElement totalElem), "Response should contain TotalRecords");
            AssertTrue(totalElem.GetInt64() >= 1, "TotalRecords should be >= 1");
        }

        #endregion

        #region Test-11-Vector-Search

        private static async Task TestSearchCosineSimilarity()
        {
            object body = new
            {
                Vector = new
                {
                    SearchType = "CosineSimilarity",
                    Embeddings = new List<float> { 0.1f, 0.2f, 0.3f }
                },
                MaxResults = 10
            };

            string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/search";
            HttpResponseMessage response = await PostAsync(_AdminClient, path, body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Success", out JsonElement successElem), "Response should contain Success");
            AssertTrue(successElem.GetBoolean(), "Search should succeed");
            AssertTrue(json.TryGetProperty("Documents", out JsonElement docsElem), "Response should contain Documents");
            AssertTrue(docsElem.GetArrayLength() > 0, "Search should return results");

            // Verify results are ordered by score descending (cosine similarity)
            double previousScore = double.MaxValue;
            foreach (JsonElement doc in docsElem.EnumerateArray())
            {
                double score = doc.GetProperty("Score").GetDouble();
                AssertTrue(score <= previousScore, "Cosine similarity results should be ordered by score descending");
                previousScore = score;
            }
        }

        private static async Task TestSearchEuclideanDistance()
        {
            object body = new
            {
                Vector = new
                {
                    SearchType = "EuclideanDistance",
                    Embeddings = new List<float> { 0.1f, 0.2f, 0.3f }
                },
                SortOrder = "DistanceAscending",
                MaxResults = 10
            };

            string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/search";
            HttpResponseMessage response = await PostAsync(_AdminClient, path, body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Success", out JsonElement successElem), "Response should contain Success");
            AssertTrue(successElem.GetBoolean(), "Search should succeed");
            AssertTrue(json.TryGetProperty("Documents", out JsonElement docsElem), "Response should contain Documents");
            AssertTrue(docsElem.GetArrayLength() > 0, "Search should return results");
        }

        private static async Task TestSearchInnerProduct()
        {
            object body = new
            {
                Vector = new
                {
                    SearchType = "InnerProduct",
                    Embeddings = new List<float> { 0.1f, 0.2f, 0.3f }
                },
                MaxResults = 10
            };

            string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/search";
            HttpResponseMessage response = await PostAsync(_AdminClient, path, body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Success", out JsonElement successElem), "Response should contain Success");
            AssertTrue(successElem.GetBoolean(), "Search should succeed");
            AssertTrue(json.TryGetProperty("Documents", out JsonElement docsElem), "Response should contain Documents");
            AssertTrue(docsElem.GetArrayLength() > 0, "Search should return results");
        }

        #endregion

        #region Test-12-Search-Filters

        private static async Task TestSearchLabelFilter()
        {
            object body = new
            {
                Vector = new
                {
                    SearchType = "CosineSimilarity",
                    Embeddings = new List<float> { 0.1f, 0.2f, 0.3f }
                },
                LabelFilter = new
                {
                    Required = new List<string> { "integration-test-label" }
                },
                MaxResults = 10
            };

            string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/search";
            HttpResponseMessage response = await PostAsync(_AdminClient, path, body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Success", out JsonElement successElem), "Response should contain Success");
            AssertTrue(successElem.GetBoolean(), "Search should succeed");
            AssertTrue(json.TryGetProperty("Documents", out JsonElement docsElem), "Response should contain Documents");
            // The labeled document should appear in results
            AssertTrue(docsElem.GetArrayLength() >= 1, "Label filter search should return at least 1 result");
        }

        private static async Task TestSearchTagFilter()
        {
            object body = new
            {
                Vector = new
                {
                    SearchType = "CosineSimilarity",
                    Embeddings = new List<float> { 0.1f, 0.2f, 0.3f }
                },
                TagFilter = new
                {
                    Required = new List<object>
                    {
                        new
                        {
                            Key = "environment",
                            Condition = "Equals",
                            Value = "integration-test"
                        }
                    }
                },
                MaxResults = 10
            };

            string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/search";
            HttpResponseMessage response = await PostAsync(_AdminClient, path, body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Success", out JsonElement successElem), "Response should contain Success");
            AssertTrue(successElem.GetBoolean(), "Search should succeed");
            AssertTrue(json.TryGetProperty("Documents", out JsonElement docsElem), "Response should contain Documents");
            AssertTrue(docsElem.GetArrayLength() >= 1, "Tag filter search should return at least 1 result");
        }

        private static async Task TestSearchDateRangeFilter()
        {
            DateTime now = DateTime.UtcNow;
            string createdAfter = now.AddHours(-1).ToString("o");
            string createdBefore = now.AddHours(1).ToString("o");

            object body = new
            {
                Vector = new
                {
                    SearchType = "CosineSimilarity",
                    Embeddings = new List<float> { 0.1f, 0.2f, 0.3f }
                },
                CreatedAfter = createdAfter,
                CreatedBefore = createdBefore,
                MaxResults = 10
            };

            string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/search";
            HttpResponseMessage response = await PostAsync(_AdminClient, path, body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Success", out JsonElement successElem), "Response should contain Success");
            AssertTrue(successElem.GetBoolean(), "Search should succeed");
            AssertTrue(json.TryGetProperty("Documents", out JsonElement docsElem), "Response should contain Documents");
            // Documents were created just now, so they should fall within the date range
            AssertTrue(docsElem.GetArrayLength() >= 1, "Date range search should return at least 1 result");
        }

        #endregion

        #region Test-13-Enumeration-Pagination

        private static async Task TestEnumerationPagination()
        {
            int totalToCreate = 5;

            // Create N tenants for pagination testing
            for (int i = 0; i < totalToCreate; i++)
            {
                object body = new { Name = "PaginationTestTenant_" + i };
                HttpResponseMessage createResp = await PutAsync(_AdminClient, "/v1.0/tenants", body).ConfigureAwait(false);
                AssertStatusCode(createResp, HttpStatusCode.Created);

                JsonElement createJson = await ReadResponse<JsonElement>(createResp).ConfigureAwait(false);
                string tenantId = createJson.GetProperty("Id").GetString();
                _PaginationTenantIds.Add(tenantId);
            }

            // Now enumerate with a small page size
            int pageSize = 2;
            int totalFetched = 0;
            string continuationToken = null;
            bool endOfResults = false;
            long reportedTotal = 0;

            while (!endOfResults)
            {
                object enumBody;
                if (continuationToken != null)
                {
                    enumBody = new
                    {
                        MaxResults = pageSize,
                        ContinuationToken = continuationToken,
                        Ordering = "CreatedAscending"
                    };
                }
                else
                {
                    enumBody = new
                    {
                        MaxResults = pageSize,
                        Ordering = "CreatedAscending"
                    };
                }

                HttpResponseMessage enumResp = await PostAsync(_AdminClient, "/v1.0/tenants/enumerate", enumBody).ConfigureAwait(false);
                AssertStatusCode(enumResp, HttpStatusCode.OK);

                JsonElement enumJson = await ReadResponse<JsonElement>(enumResp).ConfigureAwait(false);
                endOfResults = enumJson.GetProperty("EndOfResults").GetBoolean();
                reportedTotal = enumJson.GetProperty("TotalRecords").GetInt64();

                JsonElement objects = enumJson.GetProperty("Objects");
                totalFetched += objects.GetArrayLength();

                if (!endOfResults)
                {
                    AssertTrue(enumJson.TryGetProperty("ContinuationToken", out JsonElement ctElem), "Non-terminal page should have ContinuationToken");
                    continuationToken = ctElem.GetString();
                    AssertNotNullOrEmpty(continuationToken, "ContinuationToken");
                }
            }

            // reportedTotal should account for existing + created tenants
            AssertTrue(reportedTotal >= totalToCreate, "TotalRecords should be >= " + totalToCreate);
            AssertTrue(totalFetched >= totalToCreate, "Total fetched records should be >= " + totalToCreate);
            AssertTrue((long)totalFetched <= reportedTotal, "Total fetched should not exceed TotalRecords");
        }

        #endregion

        #region Test-14-Authorization

        private static async Task TestAuthorizationNonAdmin()
        {
            AssertNotNull(_UserClient, "UserClient should be initialized");

            object body = new { Name = "UnauthorizedTenantAttempt" };
            HttpResponseMessage response = await PutAsync(_UserClient, "/v1.0/tenants", body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.Forbidden);
        }

        #endregion

        #region Test-15-Cleanup

        private static async Task TestCleanupLabels()
        {
            if (string.IsNullOrEmpty(_TestLabelId) || string.IsNullOrEmpty(_TestCollectionId)) return;

            string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/labels/" + _TestLabelId;
            HttpResponseMessage response = await DeleteAsync(_AdminClient, path).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.NoContent);
        }

        private static async Task TestCleanupTags()
        {
            if (string.IsNullOrEmpty(_TestTagId) || string.IsNullOrEmpty(_TestCollectionId)) return;

            string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/tags/" + _TestTagId;
            HttpResponseMessage response = await DeleteAsync(_AdminClient, path).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.NoContent);
        }

        private static async Task TestCleanupDocuments()
        {
            if (string.IsNullOrEmpty(_TestCollectionId)) return;

            // Delete the single test document
            if (!string.IsNullOrEmpty(_TestDocumentKey))
            {
                string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/documents/" + _TestDocumentKey;
                HttpResponseMessage response = await DeleteAsync(_AdminClient, path).ConfigureAwait(false);
                AssertStatusCode(response, HttpStatusCode.NoContent);
            }

            // Delete batch documents
            foreach (string batchKey in _TestBatchDocumentKeys)
            {
                string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/documents/" + batchKey;
                HttpResponseMessage response = await DeleteAsync(_AdminClient, path).ConfigureAwait(false);
                AssertStatusCode(response, HttpStatusCode.NoContent);
            }
        }

        private static async Task TestCleanupCollection()
        {
            if (string.IsNullOrEmpty(_TestCollectionId)) return;

            string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId;
            HttpResponseMessage response = await DeleteAsync(_AdminClient, path).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.NoContent);
        }

        private static async Task TestCleanupCredential()
        {
            if (string.IsNullOrEmpty(_TestCredentialId)) return;

            string path = "/v1.0/tenants/" + _TestTenantId + "/credentials/" + _TestCredentialId;
            HttpResponseMessage response = await DeleteAsync(_AdminClient, path).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.NoContent);
        }

        private static async Task TestCleanupUser()
        {
            if (string.IsNullOrEmpty(_TestUserId)) return;

            string path = "/v1.0/tenants/" + _TestTenantId + "/users/" + _TestUserId;
            HttpResponseMessage response = await DeleteAsync(_AdminClient, path).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.NoContent);
        }

        private static async Task TestCleanupTenant()
        {
            if (string.IsNullOrEmpty(_TestTenantId)) return;

            string path = "/v1.0/tenants/" + _TestTenantId + "?force";
            HttpResponseMessage response = await DeleteAsync(_AdminClient, path).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.NoContent);
        }

        private static async Task TestCleanupPaginationTenants()
        {
            foreach (string tenantId in _PaginationTenantIds)
            {
                string path = "/v1.0/tenants/" + tenantId + "?force";
                HttpResponseMessage response = await DeleteAsync(_AdminClient, path).ConfigureAwait(false);
                AssertStatusCode(response, HttpStatusCode.NoContent);
            }
        }

        #endregion
    }
}
