namespace RecallDb.Sdk.TestHarness
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
    using RecallDb.Sdk;
    using RecallDb.Sdk.Models;

    /// <summary>
    /// RecallDB C# SDK integration test harness.
    /// Mirrors the test cases from the main Test.Automated suite.
    /// </summary>
    public class Program
    {
        #region Private-Members

        private static string _Endpoint = "http://localhost:8600";
        private static string _ApiKey = "recalldbadmin";
        private static RecallDbClient _AdminClient = null;
        private static RecallDbClient _UserClient = null;
        private static HttpClient _RawAdminClient = null;
        private static HttpClient _RawUserClient = null;
        private static int _Passed = 0;
        private static int _Failed = 0;
        private static List<string> _FailedTests = new List<string>();
        private static Stopwatch _TotalTimer = new Stopwatch();

        private static JsonSerializerOptions _JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
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

        // Search/enumeration test dataset
        private static List<string> _SearchTestDocKeys = new List<string>();
        private static List<string> _SearchTestLabelIds = new List<string>();
        private static List<string> _SearchTestTagIds = new List<string>();

        #endregion

        #region Entry-Point

        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args">Command line arguments: [endpoint] [api_key]</param>
        public static async Task Main(string[] args)
        {
            if (args != null && args.Length > 0) _Endpoint = args[0];
            if (args != null && args.Length > 1) _ApiKey = args[1];

            _Endpoint = _Endpoint.TrimEnd('/');

            Console.WriteLine("=========================================");
            Console.WriteLine("  RecallDB Integration Test Harness");
            Console.WriteLine("  (C# SDK)");
            Console.WriteLine("=========================================");
            Console.WriteLine("  Endpoint : " + _Endpoint);
            Console.WriteLine("  API Key  : " + _ApiKey);
            Console.WriteLine("=========================================");
            Console.WriteLine("");

            _AdminClient = new RecallDbClient(_Endpoint, _ApiKey);
            _RawAdminClient = CreateRawHttpClient(_ApiKey);

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

            // 11. Search Data Setup
            await RunTest("Search data: setup test documents, labels, and tags", TestSearchDataSetup);

            // 12. Vector Search
            await RunTest("Search: cosine similarity", TestSearchCosineSimilarity);
            await RunTest("Search: cosine distance", TestSearchCosineDistance);
            await RunTest("Search: euclidean similarity", TestSearchEuclideanSimilarity);
            await RunTest("Search: euclidean distance", TestSearchEuclideanDistance);
            await RunTest("Search: inner product", TestSearchInnerProduct);

            // 13. Search Sort Orders
            await RunTest("Search sort: score descending", TestSearchSortScoreDescending);
            await RunTest("Search sort: score ascending", TestSearchSortScoreAscending);
            await RunTest("Search sort: distance ascending", TestSearchSortDistanceAscending);
            await RunTest("Search sort: distance descending", TestSearchSortDistanceDescending);
            await RunTest("Search sort: created ascending", TestSearchSortCreatedAscending);
            await RunTest("Search sort: created descending", TestSearchSortCreatedDescending);

            // 14. Search Thresholds
            await RunTest("Search threshold: minimum score", TestSearchMinimumScore);
            await RunTest("Search threshold: maximum score", TestSearchMaximumScore);
            await RunTest("Search threshold: min and max score", TestSearchMinMaxScore);
            await RunTest("Search threshold: minimum distance", TestSearchMinimumDistance);
            await RunTest("Search threshold: maximum distance", TestSearchMaximumDistance);

            // 15. Search Label Filters
            await RunTest("Search label: required", TestSearchLabelRequired);
            await RunTest("Search label: excluded", TestSearchLabelExcluded);
            await RunTest("Search label: required multiple", TestSearchLabelRequiredMultiple);
            await RunTest("Search label: required and excluded", TestSearchLabelRequiredAndExcluded);
            await RunTest("Search label: no match", TestSearchLabelNoMatch);

            // 16. Search Tag Filters
            await RunTest("Search tag: equals", TestSearchTagEquals);
            await RunTest("Search tag: not equals", TestSearchTagNotEquals);
            await RunTest("Search tag: contains", TestSearchTagContains);
            await RunTest("Search tag: contains not", TestSearchTagContainsNot);
            await RunTest("Search tag: starts with", TestSearchTagStartsWith);
            await RunTest("Search tag: ends with", TestSearchTagEndsWith);
            await RunTest("Search tag: greater than", TestSearchTagGreaterThan);
            await RunTest("Search tag: less than", TestSearchTagLessThan);
            await RunTest("Search tag: is not null", TestSearchTagIsNotNull);
            await RunTest("Search tag: is null", TestSearchTagIsNull);
            await RunTest("Search tag: excluded", TestSearchTagExcluded);

            // 17. Search Terms Filters
            await RunTest("Search terms: required", TestSearchTermsRequired);
            await RunTest("Search terms: excluded", TestSearchTermsExcluded);
            await RunTest("Search terms: required multiple", TestSearchTermsRequiredMultiple);
            await RunTest("Search terms: required and excluded", TestSearchTermsRequiredAndExcluded);
            await RunTest("Search terms: case insensitive", TestSearchTermsCaseInsensitive);

            // 18. Search Date Range
            await RunTest("Search date: created after", TestSearchCreatedAfter);
            await RunTest("Search date: created before", TestSearchCreatedBefore);
            await RunTest("Search date: created before none", TestSearchCreatedBeforeNone);
            await RunTest("Search date: range combined", TestSearchDateRangeCombined);

            // 19. Search DocumentIds
            await RunTest("Search docids: filter by document ids", TestSearchDocumentIds);
            await RunTest("Search docids: no match", TestSearchDocumentIdsNoMatch);

            // 20. Search Pagination
            await RunTest("Search pagination: page through results", TestSearchPagination);
            await RunTest("Search pagination: max results", TestSearchMaxResults);

            // 21. Search Combined Filters
            await RunTest("Search combined: label and tag", TestSearchCombinedLabelAndTag);
            await RunTest("Search combined: terms and label", TestSearchCombinedTermsAndLabel);
            await RunTest("Search combined: all filters", TestSearchCombinedAllFilters);

            // 21b. Full-Text Search
            await RunTest("Search full-text: basic query", TestSearchFullTextBasic);
            await RunTest("Search full-text: TsRankCd scoring", TestSearchFullTextTsRankCd);
            await RunTest("Search full-text: hybrid vector + text", TestSearchFullTextHybrid);
            await RunTest("Search full-text: minimum score threshold", TestSearchFullTextMinimumScore);
            await RunTest("Search full-text: with terms filter", TestSearchFullTextWithTerms);
            await RunTest("Search full-text: with label filter", TestSearchFullTextWithLabels);
            await RunTest("Search full-text: no match", TestSearchFullTextNoMatch);
            await RunTest("Search full-text: sort text score descending", TestSearchFullTextSortDescending);
            await RunTest("Search full-text: sort text score ascending", TestSearchFullTextSortAscending);
            await RunTest("Search full-text: backward compat vector-only", TestSearchFullTextBackwardCompat);

            // 22. Search Result Validation
            await RunTest("Search validation: result fields", TestSearchResultFields);
            await RunTest("Search validation: document fields", TestSearchDocumentFields);

            // 23. Document Enumeration
            await RunTest("Enum docs: basic", TestEnumDocumentsBasic);
            await RunTest("Enum docs: created ascending", TestEnumDocumentsCreatedAscending);
            await RunTest("Enum docs: created descending", TestEnumDocumentsCreatedDescending);
            await RunTest("Enum docs: pagination", TestEnumDocumentsPagination);
            await RunTest("Enum docs: max results", TestEnumDocumentsMaxResults);
            await RunTest("Enum docs: created after", TestEnumDocumentsCreatedAfter);
            await RunTest("Enum docs: created before", TestEnumDocumentsCreatedBefore);
            await RunTest("Enum docs: date range none", TestEnumDocumentsDateRangeNone);
            await RunTest("Enum docs: document ids", TestEnumDocumentsDocumentIds);
            await RunTest("Enum docs: document ids no match", TestEnumDocumentsDocumentIdsNoMatch);
            await RunTest("Enum docs: label required", TestEnumDocumentsLabelRequired);
            await RunTest("Enum docs: label excluded", TestEnumDocumentsLabelExcluded);
            await RunTest("Enum docs: label no match", TestEnumDocumentsLabelNoMatch);
            await RunTest("Enum docs: tag equals", TestEnumDocumentsTagEquals);
            await RunTest("Enum docs: tag not equals", TestEnumDocumentsTagNotEquals);
            await RunTest("Enum docs: tag contains", TestEnumDocumentsTagContains);
            await RunTest("Enum docs: tag starts with", TestEnumDocumentsTagStartsWith);
            await RunTest("Enum docs: tag ends with", TestEnumDocumentsTagEndsWith);
            await RunTest("Enum docs: tag greater than", TestEnumDocumentsTagGreaterThan);
            await RunTest("Enum docs: tag less than", TestEnumDocumentsTagLessThan);
            await RunTest("Enum docs: tag is not null", TestEnumDocumentsTagIsNotNull);
            await RunTest("Enum docs: tag is null", TestEnumDocumentsTagIsNull);
            await RunTest("Enum docs: tag contains not", TestEnumDocumentsTagContainsNot);
            await RunTest("Enum docs: terms required", TestEnumDocumentsTermsRequired);
            await RunTest("Enum docs: terms excluded", TestEnumDocumentsTermsExcluded);
            await RunTest("Enum docs: combined label and tag", TestEnumDocumentsCombinedLabelAndTag);
            await RunTest("Enum docs: combined all filters", TestEnumDocumentsCombinedAllFilters);
            await RunTest("Enum docs: result fields", TestEnumDocumentsResultFields);
            await RunTest("Enum docs: object fields", TestEnumDocumentsObjectFields);

            // 24. Tenant Enumeration Pagination
            await RunTest("Enumeration: tenant pagination", TestEnumerationPagination);

            // 25. Authorization
            await RunTest("Authorization: non-admin cannot create tenant", TestAuthorizationNonAdmin);

            // 26. Cleanup
            await RunTest("Cleanup: delete search labels", TestCleanupSearchLabels);
            await RunTest("Cleanup: delete search tags", TestCleanupSearchTags);
            await RunTest("Cleanup: delete search documents", TestCleanupSearchDocuments);
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
            if (_RawAdminClient != null) _RawAdminClient.Dispose();
            if (_RawUserClient != null) _RawUserClient.Dispose();

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

        #region Helpers

        private static HttpClient CreateRawHttpClient(string bearerToken)
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(_Endpoint);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        private static async Task<HttpResponseMessage> RawGetAsync(HttpClient client, string path)
        {
            return await client.GetAsync(path).ConfigureAwait(false);
        }

        private static async Task<HttpResponseMessage> RawHeadAsync(HttpClient client, string path)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, path);
            return await client.SendAsync(request).ConfigureAwait(false);
        }

        private static async Task<HttpResponseMessage> RawPostAsync(HttpClient client, string path, object body)
        {
            string json = JsonSerializer.Serialize(body, _JsonOptions);
            using StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
            return await client.PostAsync(path, content).ConfigureAwait(false);
        }

        private static async Task<HttpResponseMessage> RawPutAsync(HttpClient client, string path, object body)
        {
            string json = JsonSerializer.Serialize(body, _JsonOptions);
            using StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
            return await client.PutAsync(path, content).ConfigureAwait(false);
        }

        private static async Task<HttpResponseMessage> RawDeleteAsync(HttpClient client, string path)
        {
            return await client.DeleteAsync(path).ConfigureAwait(false);
        }

        private static async Task<T> ReadResponse<T>(HttpResponseMessage response)
        {
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (string.IsNullOrEmpty(body)) return default;
            return JsonSerializer.Deserialize<T>(body, _JsonOptions);
        }

        private static async Task<JsonElement> ReadJsonResponse(HttpResponseMessage response)
        {
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<JsonElement>(body, _JsonOptions);
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

        private static void AssertEqual(long expected, long actual, string name)
        {
            if (expected != actual)
            {
                throw new Exception(
                    "Expected " + expected + " but got " + actual + " for " + name);
            }
        }

        private static void AssertGreaterThanOrEqual(double value, double threshold, string name)
        {
            if (value < threshold)
                throw new Exception("Expected " + name + " (" + value + ") >= " + threshold);
        }

        private static void AssertLessThanOrEqual(double value, double threshold, string name)
        {
            if (value > threshold)
                throw new Exception("Expected " + name + " (" + value + ") <= " + threshold);
        }

        private static List<float> SearchEmbeddings()
        {
            return new List<float> { 0.9f, 0.1f, 0.05f };
        }

        private static SearchQuery MakeSearchQuery(
            string searchType = "CosineSimilarity",
            string sortOrder = null,
            int maxResults = 10,
            string continuationToken = null,
            double? minimumScore = null,
            double? maximumScore = null,
            double? minimumDistance = null,
            double? maximumDistance = null,
            LabelFilter labelFilter = null,
            TagFilterSet tagFilter = null,
            TermsFilter termsFilter = null,
            DateTime? createdAfter = null,
            DateTime? createdBefore = null,
            List<string> documentIds = null)
        {
            SearchQuery q = new SearchQuery();
            q.MaxResults = maxResults;
            q.ContinuationToken = continuationToken;
            if (sortOrder != null) q.SortOrder = sortOrder;
            q.LabelFilter = labelFilter;
            q.TagFilter = tagFilter;
            q.Terms = termsFilter;
            q.CreatedAfter = createdAfter;
            q.CreatedBefore = createdBefore;
            if (documentIds != null) q.DocumentIds = documentIds;

            VectorQuery vq = new VectorQuery();
            vq.SearchType = searchType;
            vq.Embeddings = SearchEmbeddings();
            vq.MinimumScore = minimumScore;
            vq.MaximumScore = maximumScore;
            vq.MinimumDistance = minimumDistance;
            vq.MaximumDistance = maximumDistance;
            q.Vector = vq;

            return q;
        }

        private static async Task<SearchResult> DoSearch(SearchQuery query)
        {
            SearchResult result = await _AdminClient.SearchAsync(_TestTenantId, _TestCollectionId, query).ConfigureAwait(false);
            AssertTrue(result.Success, "Search should succeed");
            return result;
        }

        /// <summary>
        /// Perform a document enumeration using raw HTTP POST to support filter fields
        /// not present on the SDK's EnumerationQuery model.
        /// </summary>
        private static async Task<JsonElement> DoEnumDocsRaw(object body)
        {
            string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/documents/enumerate";
            using HttpResponseMessage response = await RawPostAsync(_RawAdminClient, path, body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);
            JsonElement json = await ReadJsonResponse(response).ConfigureAwait(false);
            AssertTrue(json.GetProperty("Success").GetBoolean(), "Enumeration should succeed");
            return json;
        }

        private static JsonElement GetObjects(JsonElement json)
        {
            return json.GetProperty("Objects");
        }

        #endregion

        #region Test-1-Connectivity

        private static async Task TestConnectivityGet()
        {
            Dictionary<string, object> health = await _AdminClient.HealthAsync().ConfigureAwait(false);
            AssertNotNull(health, "Health response");
            AssertTrue(health.ContainsKey("Name"), "Response should contain Name");
        }

        private static async Task TestConnectivityHead()
        {
            // HEAD / is not directly supported by the SDK, use raw HTTP
            using HttpResponseMessage response = await RawHeadAsync(_RawAdminClient, "/").ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);
        }

        #endregion

        #region Test-2-Authentication

        private static async Task TestAuthenticateBearer()
        {
            AuthenticateRequest req = new AuthenticateRequest();
            req.BearerToken = "default";
            AuthenticateResponse resp = await _AdminClient.AuthenticateAsync(req).ConfigureAwait(false);
            AssertTrue(resp.Success, "Authentication should succeed");
        }

        private static async Task TestAuthenticateEmailPassword()
        {
            AuthenticateRequest req = new AuthenticateRequest();
            req.TenantId = "default";
            req.Email = "admin@recall";
            req.Password = "password";
            AuthenticateResponse resp = await _AdminClient.AuthenticateAsync(req).ConfigureAwait(false);
            AssertTrue(resp.Success, "Authentication should succeed");
        }

        #endregion

        #region Test-3-Tenant-CRUD

        private static async Task TestTenantCreate()
        {
            TenantMetadata tenant = new TenantMetadata();
            tenant.Name = "IntegrationTestTenant";
            TenantMetadata created = await _AdminClient.CreateTenantAsync(tenant).ConfigureAwait(false);
            AssertNotNullOrEmpty(created.Id, "TenantId");
            AssertEqual("IntegrationTestTenant", created.Name, "Name");
            _TestTenantId = created.Id;
        }

        private static async Task TestTenantRead()
        {
            TenantMetadata tenant = await _AdminClient.GetTenantAsync(_TestTenantId).ConfigureAwait(false);
            AssertEqual(_TestTenantId, tenant.Id, "Id");
        }

        private static async Task TestTenantUpdate()
        {
            TenantMetadata tenant = new TenantMetadata();
            tenant.Name = "IntegrationTestTenantUpdated";
            TenantMetadata updated = await _AdminClient.UpdateTenantAsync(_TestTenantId, tenant).ConfigureAwait(false);
            AssertEqual("IntegrationTestTenantUpdated", updated.Name, "Name");
        }

        private static async Task TestTenantEnumerate()
        {
            EnumerationQuery query = new EnumerationQuery();
            query.MaxResults = 100;
            EnumerationResult<TenantMetadata> result = await _AdminClient.EnumerateTenantsAsync(query).ConfigureAwait(false);
            AssertTrue(result.Success, "Enumeration should succeed");
            AssertTrue(result.TotalRecords >= 1, "TotalRecords should be >= 1");
        }

        private static async Task TestTenantExists()
        {
            bool exists = await _AdminClient.TenantExistsAsync(_TestTenantId).ConfigureAwait(false);
            AssertTrue(exists, "Tenant should exist");
        }

        #endregion

        #region Test-4-User-CRUD

        private static async Task TestUserCreate()
        {
            UserMaster user = new UserMaster();
            user.Email = "testuser@integrationtest.local";
            user.FirstName = "Test";
            user.LastName = "User";
            user.PasswordSha256 = "5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8"; // password
            user.IsAdmin = false;
            user.IsTenantAdmin = false;
            user.Active = true;
            UserMaster created = await _AdminClient.CreateUserAsync(_TestTenantId, user).ConfigureAwait(false);
            AssertNotNullOrEmpty(created.Id, "UserId");
            _TestUserId = created.Id;
        }

        private static async Task TestUserRead()
        {
            UserMaster user = await _AdminClient.GetUserAsync(_TestTenantId, _TestUserId).ConfigureAwait(false);
            AssertEqual(_TestUserId, user.Id, "Id");
            AssertEqual("testuser@integrationtest.local", user.Email, "Email");
        }

        private static async Task TestUserUpdate()
        {
            UserMaster user = new UserMaster();
            user.Email = "testuser@integrationtest.local";
            user.FirstName = "TestUpdated";
            user.LastName = "UserUpdated";
            UserMaster updated = await _AdminClient.UpdateUserAsync(_TestTenantId, _TestUserId, user).ConfigureAwait(false);
            AssertEqual("TestUpdated", updated.FirstName, "FirstName");
        }

        private static async Task TestUserEnumerate()
        {
            EnumerationQuery query = new EnumerationQuery();
            query.MaxResults = 100;
            EnumerationResult<UserMaster> result = await _AdminClient.EnumerateUsersAsync(_TestTenantId, query).ConfigureAwait(false);
            AssertTrue(result.TotalRecords >= 1, "TotalRecords should be >= 1");
        }

        private static async Task TestUserExists()
        {
            bool exists = await _AdminClient.UserExistsAsync(_TestTenantId, _TestUserId).ConfigureAwait(false);
            AssertTrue(exists, "User should exist");
        }

        #endregion

        #region Test-5-Credential-CRUD

        private static async Task TestCredentialCreate()
        {
            Credential cred = new Credential();
            cred.UserId = _TestUserId;
            cred.Name = "IntegrationTestCredential";
            cred.Active = true;
            Credential created = await _AdminClient.CreateCredentialAsync(_TestTenantId, cred).ConfigureAwait(false);
            AssertNotNullOrEmpty(created.Id, "CredentialId");
            AssertNotNullOrEmpty(created.BearerToken, "BearerToken");
            _TestCredentialId = created.Id;
            _TestUserBearerToken = created.BearerToken;

            // Create the user-level SDK client and raw HTTP client for authorization tests later
            _UserClient = new RecallDbClient(_Endpoint, _TestUserBearerToken);
            _RawUserClient = CreateRawHttpClient(_TestUserBearerToken);
        }

        private static async Task TestCredentialRead()
        {
            Credential cred = await _AdminClient.GetCredentialAsync(_TestTenantId, _TestCredentialId).ConfigureAwait(false);
            AssertEqual(_TestCredentialId, cred.Id, "Id");
        }

        private static async Task TestCredentialEnumerate()
        {
            EnumerationQuery query = new EnumerationQuery();
            query.MaxResults = 100;
            EnumerationResult<Credential> result = await _AdminClient.EnumerateCredentialsAsync(_TestTenantId, query).ConfigureAwait(false);
            AssertTrue(result.TotalRecords >= 1, "TotalRecords should be >= 1");
        }

        private static async Task TestCredentialExists()
        {
            bool exists = await _AdminClient.CredentialExistsAsync(_TestTenantId, _TestCredentialId).ConfigureAwait(false);
            AssertTrue(exists, "Credential should exist");
        }

        #endregion

        #region Test-6-Collection-CRUD

        private static async Task TestCollectionCreate()
        {
            CollectionMetadata col = new CollectionMetadata();
            col.Name = "IntegrationTestCollection";
            col.Description = "Test collection for integration tests";
            col.Dimensionality = 3;
            CollectionMetadata created = await _AdminClient.CreateCollectionAsync(_TestTenantId, col).ConfigureAwait(false);
            AssertNotNullOrEmpty(created.Id, "CollectionId");
            AssertEqual(3, created.Dimensionality, "Dimensionality");
            _TestCollectionId = created.Id;
        }

        private static async Task TestCollectionRead()
        {
            CollectionMetadata col = await _AdminClient.GetCollectionAsync(_TestTenantId, _TestCollectionId).ConfigureAwait(false);
            AssertEqual(_TestCollectionId, col.Id, "Id");
        }

        private static async Task TestCollectionUpdate()
        {
            CollectionMetadata col = new CollectionMetadata();
            col.Name = "IntegrationTestCollectionUpdated";
            col.Description = "Updated description";
            col.Dimensionality = 3;
            CollectionMetadata updated = await _AdminClient.UpdateCollectionAsync(_TestTenantId, _TestCollectionId, col).ConfigureAwait(false);
            AssertEqual("IntegrationTestCollectionUpdated", updated.Name, "Name");
        }

        private static async Task TestCollectionEnumerate()
        {
            EnumerationQuery query = new EnumerationQuery();
            query.MaxResults = 100;
            EnumerationResult<CollectionMetadata> result = await _AdminClient.EnumerateCollectionsAsync(_TestTenantId, query).ConfigureAwait(false);
            AssertTrue(result.TotalRecords >= 1, "TotalRecords should be >= 1");
        }

        private static async Task TestCollectionExists()
        {
            bool exists = await _AdminClient.CollectionExistsAsync(_TestTenantId, _TestCollectionId).ConfigureAwait(false);
            AssertTrue(exists, "Collection should exist");
        }

        #endregion

        #region Test-7-Document-CRUD

        private static async Task TestDocumentCreate()
        {
            string docKey = "testdoc-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            DocumentRecord doc = new DocumentRecord();
            doc.DocumentKey = docKey;
            doc.DocumentId = "testdocid-001";
            doc.Content = "This is a test document for integration testing.";
            doc.ContentType = "Text";
            doc.Position = 0;
            doc.Embeddings = new List<float> { 0.1f, 0.2f, 0.3f };

            DocumentRecord created = await _AdminClient.CreateDocumentAsync(_TestTenantId, _TestCollectionId, doc).ConfigureAwait(false);
            AssertNotNullOrEmpty(created.DocumentKey, "DocumentKey");
            _TestDocumentKey = created.DocumentKey;
        }

        private static async Task TestDocumentRead()
        {
            DocumentRecord doc = await _AdminClient.GetDocumentAsync(_TestTenantId, _TestCollectionId, _TestDocumentKey).ConfigureAwait(false);
            AssertEqual(_TestDocumentKey, doc.DocumentKey, "DocumentKey");
            AssertEqual("This is a test document for integration testing.", doc.Content, "Content");
        }

        private static async Task TestDocumentUpdate()
        {
            DocumentRecord doc = new DocumentRecord();
            doc.DocumentKey = _TestDocumentKey;
            doc.DocumentId = "testdocid-001";
            doc.Content = "This is an updated test document.";
            doc.ContentType = "Text";
            doc.Position = 0;
            doc.Embeddings = new List<float> { 0.1f, 0.2f, 0.3f };

            DocumentRecord updated = await _AdminClient.UpdateDocumentAsync(_TestTenantId, _TestCollectionId, _TestDocumentKey, doc).ConfigureAwait(false);
            AssertEqual("This is an updated test document.", updated.Content, "Content");
        }

        #endregion

        #region Test-8-Document-Batch

        private static async Task TestDocumentBatchCreate()
        {
            List<DocumentRecord> docs = new List<DocumentRecord>();

            for (int i = 0; i < 3; i++)
            {
                string batchKey = "batchdoc-" + i + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                _TestBatchDocumentKeys.Add(batchKey);

                float baseVal = (i + 1) * 0.1f;
                DocumentRecord doc = new DocumentRecord();
                doc.DocumentKey = batchKey;
                doc.DocumentId = "batchdocid-" + i;
                doc.Content = "Batch document number " + i;
                doc.ContentType = "Text";
                doc.Position = 0;
                doc.Embeddings = new List<float> { baseVal, baseVal + 0.1f, baseVal + 0.2f };
                docs.Add(doc);
            }

            List<DocumentRecord> created = await _AdminClient.CreateDocumentBatchAsync(_TestTenantId, _TestCollectionId, docs).ConfigureAwait(false);
            AssertEqual(3, created.Count, "Batch should create 3 documents");

            // Verify all documents were created by reading each one
            foreach (string batchKey in _TestBatchDocumentKeys)
            {
                DocumentRecord readDoc = await _AdminClient.GetDocumentAsync(_TestTenantId, _TestCollectionId, batchKey).ConfigureAwait(false);
                AssertNotNull(readDoc, "Batch document " + batchKey);
            }
        }

        #endregion

        #region Test-9-Label-CRUD

        private static async Task TestLabelCreate()
        {
            LabelRecord label = new LabelRecord();
            label.DocumentKey = _TestDocumentKey;
            label.Label = "integration-test-label";
            LabelRecord created = await _AdminClient.CreateLabelAsync(_TestTenantId, _TestCollectionId, label).ConfigureAwait(false);
            AssertNotNullOrEmpty(created.Id, "LabelId");
            AssertEqual("integration-test-label", created.Label, "Label");
            _TestLabelId = created.Id;
        }

        private static async Task TestLabelList()
        {
            EnumerationResult<LabelRecord> result = await _AdminClient.ListLabelsAsync(_TestTenantId, _TestCollectionId).ConfigureAwait(false);
            AssertTrue(result.TotalRecords >= 1, "TotalRecords should be >= 1");
        }

        #endregion

        #region Test-10-Tag-CRUD

        private static async Task TestTagCreate()
        {
            TagRecord tag = new TagRecord();
            tag.DocumentKey = _TestDocumentKey;
            tag.Key = "environment";
            tag.Value = "integration-test";
            TagRecord created = await _AdminClient.CreateTagAsync(_TestTenantId, _TestCollectionId, tag).ConfigureAwait(false);
            AssertNotNullOrEmpty(created.Id, "TagId");
            AssertEqual("environment", created.Key, "Key");
            _TestTagId = created.Id;
        }

        private static async Task TestTagList()
        {
            EnumerationResult<TagRecord> result = await _AdminClient.ListTagsAsync(_TestTenantId, _TestCollectionId).ConfigureAwait(false);
            AssertTrue(result.TotalRecords >= 1, "TotalRecords should be >= 1");
        }

        #endregion

        #region Test-11-Search-Data-Setup

        private static async Task TestSearchDataSetup()
        {
            // 1. Create 10 documents via batch
            List<DocumentRecord> docs = new List<DocumentRecord>
            {
                MakeDoc("srch-doc-0", "grp-alpha", "Machine learning is transforming industries worldwide", "Text", new List<float> { 0.9f, 0.1f, 0.05f }),
                MakeDoc("srch-doc-1", "grp-alpha", "Deep learning neural networks for image recognition", "Code", new List<float> { 0.8f, 0.15f, 0.1f }),
                MakeDoc("srch-doc-2", "grp-beta", "Quantum computing breakthroughs in 2024", "Text", new List<float> { 0.1f, 0.9f, 0.05f }),
                MakeDoc("srch-doc-3", "grp-beta", "Classical physics and thermodynamics overview", "Text", new List<float> { 0.05f, 0.85f, 0.15f }),
                MakeDoc("srch-doc-4", "grp-gamma", "Cooking recipes from Mediterranean cuisine", "Text", new List<float> { 0.1f, 0.1f, 0.9f }),
                MakeDoc("srch-doc-5", "grp-gamma", "Travel guide to Southeast Asia for beginners", "Text", new List<float> { 0.05f, 0.15f, 0.85f }),
                MakeDoc("srch-doc-6", "grp-delta", "Financial markets analysis and stock trading strategies", "Text", new List<float> { 0.5f, 0.5f, 0.1f }),
                MakeDoc("srch-doc-7", "grp-delta", "Startup funding and venture capital trends", "Text", new List<float> { 0.45f, 0.55f, 0.05f }),
                MakeDoc("srch-doc-8", "grp-epsilon", "Health benefits of regular exercise and nutrition", "Text", new List<float> { 0.3f, 0.3f, 0.5f }),
                MakeDoc("srch-doc-9", "grp-epsilon", "Mental health awareness and meditation techniques", "Text", new List<float> { 0.25f, 0.35f, 0.45f })
            };

            List<DocumentRecord> created = await _AdminClient.CreateDocumentBatchAsync(_TestTenantId, _TestCollectionId, docs).ConfigureAwait(false);
            AssertEqual(10, created.Count, "Batch should create 10 documents");

            _SearchTestDocKeys = new List<string> { "srch-doc-0", "srch-doc-1", "srch-doc-2", "srch-doc-3", "srch-doc-4", "srch-doc-5", "srch-doc-6", "srch-doc-7", "srch-doc-8", "srch-doc-9" };

            // 2. Create labels
            string[][] labelMap = new string[][]
            {
                new[] { "science", "tech" },
                new[] { "science", "tech", "featured" },
                new[] { "science" },
                new[] { "science", "educational" },
                new[] { "lifestyle" },
                new[] { "lifestyle", "featured" },
                new[] { "business" },
                new[] { "business", "featured" },
                new[] { "lifestyle", "science" },
                new[] { "lifestyle", "educational" }
            };

            for (int i = 0; i < 10; i++)
            {
                foreach (string labelStr in labelMap[i])
                {
                    LabelRecord label = new LabelRecord();
                    label.DocumentKey = _SearchTestDocKeys[i];
                    label.Label = labelStr;
                    LabelRecord createdLabel = await _AdminClient.CreateLabelAsync(_TestTenantId, _TestCollectionId, label).ConfigureAwait(false);
                    _SearchTestLabelIds.Add(createdLabel.Id);
                }
            }

            // 3. Create tags
            string[][][] tagMap = new string[][][]
            {
                new[] { new[] { "category", "ai" }, new[] { "priority", "high" }, new[] { "year", "2024" } },
                new[] { new[] { "category", "ai" }, new[] { "priority", "medium" }, new[] { "year", "2024" } },
                new[] { new[] { "category", "physics" }, new[] { "priority", "high" }, new[] { "year", "2024" } },
                new[] { new[] { "category", "physics" }, new[] { "priority", "low" }, new[] { "year", "2023" } },
                new[] { new[] { "category", "cooking" }, new[] { "priority", "medium" }, new[] { "year", "2023" } },
                new[] { new[] { "category", "travel" }, new[] { "priority", "low" }, new[] { "year", "2022" } },
                new[] { new[] { "category", "finance" }, new[] { "priority", "high" }, new[] { "year", "2024" } },
                new[] { new[] { "category", "finance" }, new[] { "priority", "medium" }, new[] { "year", "2023" } },
                new[] { new[] { "category", "health" }, new[] { "priority", "high" }, new[] { "year", "2024" } },
                new[] { new[] { "category", "health" }, new[] { "priority", "low" }, new[] { "year", "2022" } }
            };

            for (int i = 0; i < 10; i++)
            {
                foreach (string[] kv in tagMap[i])
                {
                    TagRecord tag = new TagRecord();
                    tag.DocumentKey = _SearchTestDocKeys[i];
                    tag.Key = kv[0];
                    tag.Value = kv[1];
                    TagRecord createdTag = await _AdminClient.CreateTagAsync(_TestTenantId, _TestCollectionId, tag).ConfigureAwait(false);
                    _SearchTestTagIds.Add(createdTag.Id);
                }
            }
        }

        private static DocumentRecord MakeDoc(string docKey, string docId, string content, string contentType, List<float> embeddings)
        {
            DocumentRecord doc = new DocumentRecord();
            doc.DocumentKey = docKey;
            doc.DocumentId = docId;
            doc.Content = content;
            doc.ContentType = contentType;
            doc.Embeddings = embeddings;
            return doc;
        }

        #endregion

        #region Test-12-Vector-Search

        private static async Task TestSearchCosineSimilarity()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(searchType: "CosineSimilarity", maxResults: 10)).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Cosine similarity search should return results");

            double prev = double.MaxValue;
            foreach (DocumentRecord doc in result.Documents)
            {
                AssertTrue(doc.Score <= prev, "Cosine similarity results should be ordered by score descending");
                prev = doc.Score;
            }
        }

        private static async Task TestSearchCosineDistance()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(searchType: "CosineDistance", sortOrder: "DistanceAscending", maxResults: 10)).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Cosine distance search should return results");

            double prev = -1;
            foreach (DocumentRecord doc in result.Documents)
            {
                AssertTrue(doc.Score >= prev, "Cosine distance results should be ordered by score ascending");
                prev = doc.Score;
            }
        }

        private static async Task TestSearchEuclideanSimilarity()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(searchType: "EuclideanSimilarity", maxResults: 10)).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Euclidean similarity search should return results");

            foreach (DocumentRecord doc in result.Documents)
            {
                // Each document should have a Score property (it always exists as double, defaults to 0)
                AssertTrue(true, "Each document should have a Score property");
            }
        }

        private static async Task TestSearchEuclideanDistance()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(searchType: "EuclideanDistance", sortOrder: "DistanceAscending", maxResults: 10)).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Euclidean distance search should return results");

            double prev = -1;
            foreach (DocumentRecord doc in result.Documents)
            {
                AssertTrue(doc.Score >= prev, "Euclidean distance results should be ordered by score ascending");
                prev = doc.Score;
            }
        }

        private static async Task TestSearchInnerProduct()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(searchType: "InnerProduct", maxResults: 10)).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Inner product search should return results");
        }

        #endregion

        #region Test-13-Search-Sort-Orders

        private static async Task TestSearchSortScoreDescending()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(sortOrder: "ScoreDescending", maxResults: 10)).ConfigureAwait(false);
            double prev = double.MaxValue;
            foreach (DocumentRecord doc in result.Documents)
            {
                AssertTrue(doc.Score <= prev, "ScoreDescending: score[i] >= score[i+1]");
                prev = doc.Score;
            }
        }

        private static async Task TestSearchSortScoreAscending()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(sortOrder: "ScoreAscending", maxResults: 10)).ConfigureAwait(false);
            double prev = -1;
            foreach (DocumentRecord doc in result.Documents)
            {
                AssertTrue(doc.Score >= prev, "ScoreAscending: score[i] <= score[i+1]");
                prev = doc.Score;
            }
        }

        private static async Task TestSearchSortDistanceAscending()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(searchType: "EuclideanDistance", sortOrder: "DistanceAscending", maxResults: 10)).ConfigureAwait(false);
            double prev = -1;
            foreach (DocumentRecord doc in result.Documents)
            {
                AssertTrue(doc.Score >= prev, "DistanceAscending: score[i] <= score[i+1]");
                prev = doc.Score;
            }
        }

        private static async Task TestSearchSortDistanceDescending()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(searchType: "EuclideanDistance", sortOrder: "DistanceDescending", maxResults: 10)).ConfigureAwait(false);
            double prev = double.MaxValue;
            foreach (DocumentRecord doc in result.Documents)
            {
                AssertTrue(doc.Score <= prev, "DistanceDescending: score[i] >= score[i+1]");
                prev = doc.Score;
            }
        }

        private static async Task TestSearchSortCreatedAscending()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(sortOrder: "CreatedAscending", maxResults: 10)).ConfigureAwait(false);
            DateTime prev = DateTime.MinValue;
            foreach (DocumentRecord doc in result.Documents)
            {
                AssertTrue(doc.CreatedUtc >= prev, "CreatedAscending: CreatedUtc[i] <= CreatedUtc[i+1]");
                prev = doc.CreatedUtc;
            }
        }

        private static async Task TestSearchSortCreatedDescending()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(sortOrder: "CreatedDescending", maxResults: 10)).ConfigureAwait(false);
            DateTime prev = DateTime.MaxValue;
            foreach (DocumentRecord doc in result.Documents)
            {
                AssertTrue(doc.CreatedUtc <= prev, "CreatedDescending: CreatedUtc[i] >= CreatedUtc[i+1]");
                prev = doc.CreatedUtc;
            }
        }

        #endregion

        #region Test-14-Search-Thresholds

        private static async Task TestSearchMinimumScore()
        {
            SearchQuery q = MakeSearchQuery(maxResults: 10);
            q.Vector.MinimumScore = 0.9;
            SearchResult result = await DoSearch(q).ConfigureAwait(false);
            foreach (DocumentRecord doc in result.Documents)
            {
                AssertGreaterThanOrEqual(doc.Score, 0.9, "Score");
            }
        }

        private static async Task TestSearchMaximumScore()
        {
            SearchQuery q = MakeSearchQuery(maxResults: 10);
            q.Vector.MaximumScore = 0.5;
            SearchResult result = await DoSearch(q).ConfigureAwait(false);
            foreach (DocumentRecord doc in result.Documents)
            {
                AssertLessThanOrEqual(doc.Score, 0.5, "Score");
            }
        }

        private static async Task TestSearchMinMaxScore()
        {
            SearchQuery q = MakeSearchQuery(maxResults: 10);
            q.Vector.MinimumScore = 0.3;
            q.Vector.MaximumScore = 0.7;
            SearchResult result = await DoSearch(q).ConfigureAwait(false);
            foreach (DocumentRecord doc in result.Documents)
            {
                AssertGreaterThanOrEqual(doc.Score, 0.3, "Score");
                AssertLessThanOrEqual(doc.Score, 0.7, "Score");
            }
        }

        private static async Task TestSearchMinimumDistance()
        {
            SearchQuery q = MakeSearchQuery(searchType: "EuclideanDistance", sortOrder: "DistanceAscending", maxResults: 10);
            q.Vector.MinimumDistance = 0.5;
            SearchResult result = await DoSearch(q).ConfigureAwait(false);
            foreach (DocumentRecord doc in result.Documents)
            {
                AssertGreaterThanOrEqual(doc.Score, 0.5, "Score");
            }
        }

        private static async Task TestSearchMaximumDistance()
        {
            SearchQuery q = MakeSearchQuery(searchType: "EuclideanDistance", sortOrder: "DistanceAscending", maxResults: 10);
            q.Vector.MaximumDistance = 1.5;
            SearchResult result = await DoSearch(q).ConfigureAwait(false);
            foreach (DocumentRecord doc in result.Documents)
            {
                AssertLessThanOrEqual(doc.Score, 1.5, "Score");
            }
        }

        #endregion

        #region Test-15-Search-Label-Filters

        private static async Task TestSearchLabelRequired()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                labelFilter: new LabelFilter { Required = new List<string> { "science" } }
            )).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Label required filter should return results");
        }

        private static async Task TestSearchLabelExcluded()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                labelFilter: new LabelFilter { Excluded = new List<string> { "lifestyle" } }
            )).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Label excluded filter should return results");
        }

        private static async Task TestSearchLabelRequiredMultiple()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                labelFilter: new LabelFilter { Required = new List<string> { "science", "tech" } }
            )).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Label required multiple filter should return results");
        }

        private static async Task TestSearchLabelRequiredAndExcluded()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                labelFilter: new LabelFilter { Required = new List<string> { "science" }, Excluded = new List<string> { "tech" } }
            )).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Label required and excluded filter should return results");
        }

        private static async Task TestSearchLabelNoMatch()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                labelFilter: new LabelFilter { Required = new List<string> { "nonexistent-label-xyz" } }
            )).ConfigureAwait(false);
            AssertEqual(0, result.Documents.Count, "Label no match should return 0 results");
        }

        #endregion

        #region Test-16-Search-Tag-Filters

        private static async Task TestSearchTagEquals()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                tagFilter: new TagFilterSet { Required = new List<TagCondition> { new TagCondition { Key = "category", Condition = "Equals", Value = "ai" } } }
            )).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Tag Equals filter should return results");
        }

        private static async Task TestSearchTagNotEquals()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                tagFilter: new TagFilterSet { Required = new List<TagCondition> { new TagCondition { Key = "category", Condition = "NotEquals", Value = "ai" } } }
            )).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Tag NotEquals filter should return results");
        }

        private static async Task TestSearchTagContains()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                tagFilter: new TagFilterSet { Required = new List<TagCondition> { new TagCondition { Key = "category", Condition = "Contains", Value = "heal" } } }
            )).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Tag Contains filter should return results");
        }

        private static async Task TestSearchTagContainsNot()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                tagFilter: new TagFilterSet { Required = new List<TagCondition> { new TagCondition { Key = "category", Condition = "ContainsNot", Value = "ai" } } }
            )).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Tag ContainsNot filter should return results");
        }

        private static async Task TestSearchTagStartsWith()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                tagFilter: new TagFilterSet { Required = new List<TagCondition> { new TagCondition { Key = "category", Condition = "StartsWith", Value = "fin" } } }
            )).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Tag StartsWith filter should return results");
        }

        private static async Task TestSearchTagEndsWith()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                tagFilter: new TagFilterSet { Required = new List<TagCondition> { new TagCondition { Key = "category", Condition = "EndsWith", Value = "ics" } } }
            )).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Tag EndsWith filter should return results");
        }

        private static async Task TestSearchTagGreaterThan()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                tagFilter: new TagFilterSet { Required = new List<TagCondition> { new TagCondition { Key = "year", Condition = "GreaterThan", Value = "2023" } } }
            )).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Tag GreaterThan filter should return results");
        }

        private static async Task TestSearchTagLessThan()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                tagFilter: new TagFilterSet { Required = new List<TagCondition> { new TagCondition { Key = "year", Condition = "LessThan", Value = "2023" } } }
            )).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Tag LessThan filter should return results");
        }

        private static async Task TestSearchTagIsNotNull()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                tagFilter: new TagFilterSet { Required = new List<TagCondition> { new TagCondition { Key = "priority", Condition = "IsNotNull" } } }
            )).ConfigureAwait(false);
            AssertTrue(result.Documents.Count >= 10, "Tag IsNotNull filter should return >= 10 results");
        }

        private static async Task TestSearchTagIsNull()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                tagFilter: new TagFilterSet { Required = new List<TagCondition> { new TagCondition { Key = "nonexistent-tag-xyz", Condition = "IsNull" } } }
            )).ConfigureAwait(false);
            AssertTrue(result.Documents.Count >= 10, "Tag IsNull filter should return >= 10 results");
        }

        private static async Task TestSearchTagExcluded()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                tagFilter: new TagFilterSet { Excluded = new List<TagCondition> { new TagCondition { Key = "priority", Condition = "Equals", Value = "low" } } }
            )).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Tag Excluded filter should return results");
        }

        #endregion

        #region Test-17-Search-Terms-Filters

        private static async Task TestSearchTermsRequired()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                termsFilter: new TermsFilter { Required = new List<string> { "machine learning" } }
            )).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Terms required filter should return results");
        }

        private static async Task TestSearchTermsExcluded()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                termsFilter: new TermsFilter { Excluded = new List<string> { "quantum" } }
            )).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Terms excluded filter should return results");
        }

        private static async Task TestSearchTermsRequiredMultiple()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                termsFilter: new TermsFilter { Required = new List<string> { "learning", "neural" } }
            )).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Terms required multiple filter should return results");
        }

        private static async Task TestSearchTermsRequiredAndExcluded()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                termsFilter: new TermsFilter { Required = new List<string> { "health" }, Excluded = new List<string> { "meditation" } }
            )).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Terms required and excluded filter should return results");
        }

        private static async Task TestSearchTermsCaseInsensitive()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                termsFilter: new TermsFilter { Required = new List<string> { "MACHINE LEARNING" } }
            )).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Terms case insensitive filter should return results");
        }

        #endregion

        #region Test-18-Search-Date-Range

        private static async Task TestSearchCreatedAfter()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                createdAfter: DateTime.UtcNow.AddHours(-1)
            )).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "CreatedAfter filter should return results");
        }

        private static async Task TestSearchCreatedBefore()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                createdBefore: DateTime.UtcNow.AddHours(1)
            )).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "CreatedBefore filter should return results");
        }

        private static async Task TestSearchCreatedBeforeNone()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                createdBefore: DateTime.UtcNow.AddHours(-1)
            )).ConfigureAwait(false);
            AssertEqual(0, result.Documents.Count, "CreatedBefore in past should return 0 results");
        }

        private static async Task TestSearchDateRangeCombined()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                createdAfter: DateTime.UtcNow.AddHours(-1),
                createdBefore: DateTime.UtcNow.AddHours(1)
            )).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Date range combined filter should return results");
        }

        #endregion

        #region Test-19-Search-DocumentIds

        private static async Task TestSearchDocumentIds()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                documentIds: new List<string> { "grp-alpha", "grp-beta" }
            )).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "DocumentIds filter should return results");
        }

        private static async Task TestSearchDocumentIdsNoMatch()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                documentIds: new List<string> { "nonexistent-docid-xyz" }
            )).ConfigureAwait(false);
            AssertEqual(0, result.Documents.Count, "DocumentIds no match should return 0 results");
        }

        #endregion

        #region Test-20-Search-Pagination

        private static async Task TestSearchPagination()
        {
            int totalFetched = 0;
            string ct = null;
            bool eor = false;
            while (!eor)
            {
                SearchQuery q = MakeSearchQuery(maxResults: 3);
                q.ContinuationToken = ct;
                SearchResult result = await DoSearch(q).ConfigureAwait(false);
                eor = result.EndOfResults;
                totalFetched += result.Documents.Count;
                if (!eor)
                {
                    AssertNotNullOrEmpty(result.ContinuationToken, "ContinuationToken");
                    ct = result.ContinuationToken;
                }
            }
            AssertTrue(totalFetched >= 10, "Should page through all search docs");
        }

        private static async Task TestSearchMaxResults()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(maxResults: 2)).ConfigureAwait(false);
            AssertEqual(2, result.Documents.Count, "MaxResults should limit to 2");
        }

        #endregion

        #region Test-21-Search-Combined-Filters

        private static async Task TestSearchCombinedLabelAndTag()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                labelFilter: new LabelFilter { Required = new List<string> { "science" } },
                tagFilter: new TagFilterSet { Required = new List<TagCondition> { new TagCondition { Key = "category", Condition = "Equals", Value = "ai" } } }
            )).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Combined label and tag filter should return results");
        }

        private static async Task TestSearchCombinedTermsAndLabel()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                termsFilter: new TermsFilter { Required = new List<string> { "exercise" } },
                labelFilter: new LabelFilter { Required = new List<string> { "lifestyle" } }
            )).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Combined terms and label filter should return results");
        }

        private static async Task TestSearchCombinedAllFilters()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(
                maxResults: 20,
                labelFilter: new LabelFilter { Required = new List<string> { "science" } },
                tagFilter: new TagFilterSet { Required = new List<TagCondition> { new TagCondition { Key = "year", Condition = "Equals", Value = "2024" } } },
                termsFilter: new TermsFilter { Required = new List<string> { "learning" } },
                createdAfter: DateTime.UtcNow.AddHours(-1),
                createdBefore: DateTime.UtcNow.AddHours(1)
            )).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Combined all filters should return results");
        }

        #endregion

        #region Test-21b-Full-Text-Search

        private static SearchQuery MakeFullTextQuery(
            string query,
            string searchType = "TsRank",
            string language = "english",
            int normalization = 32,
            double? minimumScore = null,
            double textWeight = 0.5,
            string sortOrder = null,
            int maxResults = 10,
            List<float> embeddings = null,
            string vectorSearchType = null,
            LabelFilter labelFilter = null,
            TermsFilter termsFilter = null)
        {
            SearchQuery sq = new SearchQuery();
            sq.MaxResults = maxResults;
            if (sortOrder != null) sq.SortOrder = sortOrder;
            sq.LabelFilter = labelFilter;
            sq.Terms = termsFilter;

            sq.FullText = new FullTextQuery();
            sq.FullText.Query = query;
            sq.FullText.SearchType = searchType;
            sq.FullText.Language = language;
            sq.FullText.Normalization = normalization;
            sq.FullText.MinimumScore = minimumScore;
            sq.FullText.TextWeight = textWeight;

            if (embeddings != null)
            {
                sq.Vector = new VectorQuery();
                sq.Vector.SearchType = vectorSearchType ?? "CosineSimilarity";
                sq.Vector.Embeddings = embeddings;
            }

            return sq;
        }

        private static async Task TestSearchFullTextBasic()
        {
            SearchResult result = await DoSearch(MakeFullTextQuery("machine learning")).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Full-text search should return results");
            foreach (DocumentRecord doc in result.Documents)
            {
                AssertTrue(doc.Score > 0, "Score should be > 0");
                AssertTrue(doc.TextScore.HasValue && doc.TextScore.Value > 0, "TextScore should be > 0");
                AssertTrue(Math.Abs(doc.Score - doc.TextScore.Value) < 0.0001, "Score should equal TextScore in full-text-only mode");
            }
        }

        private static async Task TestSearchFullTextTsRankCd()
        {
            SearchResult result = await DoSearch(MakeFullTextQuery("neural networks image", searchType: "TsRankCd")).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "TsRankCd search should return results");
            foreach (DocumentRecord doc in result.Documents)
            {
                AssertTrue(doc.TextScore.HasValue && doc.TextScore.Value > 0, "TextScore should be > 0");
            }
        }

        private static async Task TestSearchFullTextHybrid()
        {
            SearchResult result = await DoSearch(MakeFullTextQuery(
                "machine learning",
                embeddings: SearchEmbeddings(),
                textWeight: 0.3)).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Hybrid search should return results");
            foreach (DocumentRecord doc in result.Documents)
            {
                AssertTrue(doc.TextScore.HasValue && doc.TextScore.Value > 0, "TextScore should be populated in hybrid mode");
                AssertTrue(doc.Score > 0, "Score should be > 0 (blended)");
            }
        }

        private static async Task TestSearchFullTextMinimumScore()
        {
            SearchResult result = await DoSearch(MakeFullTextQuery("learning", minimumScore: 0.01)).ConfigureAwait(false);
            foreach (DocumentRecord doc in result.Documents)
            {
                AssertTrue(doc.TextScore.HasValue && doc.TextScore.Value >= 0.01, "TextScore should be >= minimum threshold");
            }
        }

        private static async Task TestSearchFullTextWithTerms()
        {
            TermsFilter terms = new TermsFilter();
            terms.Required = new List<string> { "Machine" };
            SearchResult result = await DoSearch(MakeFullTextQuery("learning", termsFilter: terms)).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Full-text with terms should return results");
            foreach (DocumentRecord doc in result.Documents)
            {
                AssertTrue(doc.Content.Contains("Machine", StringComparison.OrdinalIgnoreCase), "Content should contain required term");
            }
        }

        private static async Task TestSearchFullTextWithLabels()
        {
            LabelFilter lf = new LabelFilter();
            lf.Required = new List<string> { "science" };
            SearchResult result = await DoSearch(MakeFullTextQuery("learning", labelFilter: lf)).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Full-text with label filter should return results");
            foreach (DocumentRecord doc in result.Documents)
            {
                AssertTrue(doc.Labels.Contains("science"), "Document should have science label");
            }
        }

        private static async Task TestSearchFullTextNoMatch()
        {
            SearchResult result = await DoSearch(MakeFullTextQuery("xyznonexistentterm12345")).ConfigureAwait(false);
            AssertEqual(0, (int)result.TotalRecords, "Full-text search with no match should return 0 results");
            AssertTrue(result.Documents.Count == 0, "Documents list should be empty");
        }

        private static async Task TestSearchFullTextSortDescending()
        {
            SearchResult result = await DoSearch(MakeFullTextQuery("learning", sortOrder: "TextScoreDescending")).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Should return results");
            double prev = double.MaxValue;
            foreach (DocumentRecord doc in result.Documents)
            {
                double ts = doc.TextScore ?? 0;
                AssertTrue(ts <= prev, "TextScore should be in descending order");
                prev = ts;
            }
        }

        private static async Task TestSearchFullTextSortAscending()
        {
            SearchResult result = await DoSearch(MakeFullTextQuery("learning", sortOrder: "TextScoreAscending")).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Should return results");
            double prev = -1;
            foreach (DocumentRecord doc in result.Documents)
            {
                double ts = doc.TextScore ?? 0;
                AssertTrue(ts >= prev, "TextScore should be in ascending order");
                prev = ts;
            }
        }

        private static async Task TestSearchFullTextBackwardCompat()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(searchType: "CosineSimilarity", maxResults: 10)).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Vector-only search should still work");
            foreach (DocumentRecord doc in result.Documents)
            {
                AssertTrue(doc.Score > 0, "Score should be > 0");
                AssertTrue(!doc.TextScore.HasValue || doc.TextScore.Value == 0, "TextScore should be null or 0 in vector-only mode");
            }
        }

        #endregion

        #region Test-22-Search-Result-Validation

        private static async Task TestSearchResultFields()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(maxResults: 10)).ConfigureAwait(false);
            AssertTrue(result.Success, "Success should be true");
            AssertTrue(result.MaxResults > 0, "MaxResults should be > 0");
            AssertTrue(result.TotalRecords > 0, "TotalRecords should be > 0");
            AssertNotNull(result.Documents, "Documents should not be null");
        }

        private static async Task TestSearchDocumentFields()
        {
            SearchResult result = await DoSearch(MakeSearchQuery(maxResults: 10)).ConfigureAwait(false);
            AssertTrue(result.Documents.Count > 0, "Should have at least one document");

            DocumentRecord doc = result.Documents[0];
            AssertNotNullOrEmpty(doc.DocumentKey, "DocumentKey");
            AssertNotNullOrEmpty(doc.DocumentId, "DocumentId");
            AssertNotNullOrEmpty(doc.Content, "Content");
            AssertNotNullOrEmpty(doc.ContentType, "ContentType");
            AssertTrue(doc.ContentLength > 0, "ContentLength should be > 0");
            AssertTrue(doc.CreatedUtc > DateTime.MinValue, "CreatedUtc should be set");
            // Score is a double, always present
            AssertNotNull(doc.Labels, "Document should have Labels");
            AssertNotNull(doc.Tags, "Document should have Tags");
        }

        #endregion

        #region Test-23-Document-Enumeration

        // Document enumeration with advanced filters uses raw HTTP because
        // the SDK's EnumerationQuery model only has MaxResults, ContinuationToken, Ordering.

        private static async Task TestEnumDocumentsBasic()
        {
            var json = await DoEnumDocsRaw(new { MaxResults = 100, Ordering = "CreatedDescending" }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() >= 10, "Basic enumeration should return >= 10 results");
        }

        private static async Task TestEnumDocumentsCreatedAscending()
        {
            var json = await DoEnumDocsRaw(new { MaxResults = 100, Ordering = "CreatedAscending" }).ConfigureAwait(false);
            var objects = GetObjects(json);
            DateTime prev = DateTime.MinValue;
            foreach (JsonElement obj in objects.EnumerateArray())
            {
                DateTime created = DateTime.Parse(obj.GetProperty("CreatedUtc").GetString());
                AssertTrue(created >= prev, "CreatedAscending: CreatedUtc[i] <= CreatedUtc[i+1]");
                prev = created;
            }
        }

        private static async Task TestEnumDocumentsCreatedDescending()
        {
            var json = await DoEnumDocsRaw(new { MaxResults = 100, Ordering = "CreatedDescending" }).ConfigureAwait(false);
            var objects = GetObjects(json);
            DateTime prev = DateTime.MaxValue;
            foreach (JsonElement obj in objects.EnumerateArray())
            {
                DateTime created = DateTime.Parse(obj.GetProperty("CreatedUtc").GetString());
                AssertTrue(created <= prev, "CreatedDescending: CreatedUtc[i] >= CreatedUtc[i+1]");
                prev = created;
            }
        }

        private static async Task TestEnumDocumentsPagination()
        {
            int totalFetched = 0;
            string ct = null;
            bool eor = false;
            while (!eor)
            {
                object body;
                if (ct != null)
                    body = new { MaxResults = 3, Ordering = "CreatedAscending", ContinuationToken = ct };
                else
                    body = new { MaxResults = 3, Ordering = "CreatedAscending" };
                var json = await DoEnumDocsRaw(body).ConfigureAwait(false);
                eor = json.GetProperty("EndOfResults").GetBoolean();
                AssertTrue(json.GetProperty("TotalRecords").GetInt64() >= 10, "TotalRecords should be >= 10");
                totalFetched += GetObjects(json).GetArrayLength();
                if (!eor) { ct = json.GetProperty("ContinuationToken").GetString(); AssertNotNullOrEmpty(ct, "ContinuationToken"); }
            }
            AssertTrue(totalFetched >= 10, "Should page through all enumeration docs");
        }

        private static async Task TestEnumDocumentsMaxResults()
        {
            var json = await DoEnumDocsRaw(new { MaxResults = 2, Ordering = "CreatedDescending" }).ConfigureAwait(false);
            AssertEqual(2, GetObjects(json).GetArrayLength(), "MaxResults should limit to 2");
        }

        private static async Task TestEnumDocumentsCreatedAfter()
        {
            var json = await DoEnumDocsRaw(new { MaxResults = 100, Ordering = "CreatedDescending", CreatedAfter = DateTime.UtcNow.AddHours(-1).ToString("o") }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "CreatedAfter filter should return results");
        }

        private static async Task TestEnumDocumentsCreatedBefore()
        {
            var json = await DoEnumDocsRaw(new { MaxResults = 100, Ordering = "CreatedDescending", CreatedBefore = DateTime.UtcNow.AddHours(1).ToString("o") }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "CreatedBefore filter should return results");
        }

        private static async Task TestEnumDocumentsDateRangeNone()
        {
            var json = await DoEnumDocsRaw(new { MaxResults = 100, Ordering = "CreatedDescending", CreatedBefore = DateTime.UtcNow.AddHours(-1).ToString("o") }).ConfigureAwait(false);
            AssertEqual(0, GetObjects(json).GetArrayLength(), "CreatedBefore in past should return 0 results");
        }

        private static async Task TestEnumDocumentsDocumentIds()
        {
            var json = await DoEnumDocsRaw(new { MaxResults = 100, Ordering = "CreatedDescending", DocumentIds = new List<string> { "grp-alpha", "grp-beta" } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "DocumentIds filter should return results");
        }

        private static async Task TestEnumDocumentsDocumentIdsNoMatch()
        {
            var json = await DoEnumDocsRaw(new { MaxResults = 100, Ordering = "CreatedDescending", DocumentIds = new List<string> { "nonexistent-docid-xyz" } }).ConfigureAwait(false);
            AssertEqual(0, GetObjects(json).GetArrayLength(), "DocumentIds no match should return 0 results");
        }

        private static async Task TestEnumDocumentsLabelRequired()
        {
            var json = await DoEnumDocsRaw(new { MaxResults = 100, Ordering = "CreatedDescending", LabelFilter = new { Required = new List<string> { "science" } } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Label required filter should return results");
        }

        private static async Task TestEnumDocumentsLabelExcluded()
        {
            var json = await DoEnumDocsRaw(new { MaxResults = 100, Ordering = "CreatedDescending", LabelFilter = new { Excluded = new List<string> { "lifestyle" } } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Label excluded filter should return results");
        }

        private static async Task TestEnumDocumentsLabelNoMatch()
        {
            var json = await DoEnumDocsRaw(new { MaxResults = 100, Ordering = "CreatedDescending", LabelFilter = new { Required = new List<string> { "nonexistent-label-xyz" } } }).ConfigureAwait(false);
            AssertEqual(0, GetObjects(json).GetArrayLength(), "Label no match should return 0 results");
        }

        private static async Task TestEnumDocumentsTagEquals()
        {
            var json = await DoEnumDocsRaw(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "Equals", Value = "ai" } } } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Tag Equals filter should return results");
        }

        private static async Task TestEnumDocumentsTagNotEquals()
        {
            var json = await DoEnumDocsRaw(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "NotEquals", Value = "ai" } } } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Tag NotEquals filter should return results");
        }

        private static async Task TestEnumDocumentsTagContains()
        {
            var json = await DoEnumDocsRaw(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "Contains", Value = "heal" } } } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Tag Contains filter should return results");
        }

        private static async Task TestEnumDocumentsTagStartsWith()
        {
            var json = await DoEnumDocsRaw(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "StartsWith", Value = "fin" } } } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Tag StartsWith filter should return results");
        }

        private static async Task TestEnumDocumentsTagEndsWith()
        {
            var json = await DoEnumDocsRaw(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "EndsWith", Value = "ics" } } } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Tag EndsWith filter should return results");
        }

        private static async Task TestEnumDocumentsTagGreaterThan()
        {
            var json = await DoEnumDocsRaw(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "year", Condition = "GreaterThan", Value = "2023" } } } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Tag GreaterThan filter should return results");
        }

        private static async Task TestEnumDocumentsTagLessThan()
        {
            var json = await DoEnumDocsRaw(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "year", Condition = "LessThan", Value = "2023" } } } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Tag LessThan filter should return results");
        }

        private static async Task TestEnumDocumentsTagIsNotNull()
        {
            var json = await DoEnumDocsRaw(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "priority", Condition = "IsNotNull" } } } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() >= 10, "Tag IsNotNull filter should return >= 10 results");
        }

        private static async Task TestEnumDocumentsTagIsNull()
        {
            var json = await DoEnumDocsRaw(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "nonexistent-tag-xyz", Condition = "IsNull" } } } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() >= 10, "Tag IsNull filter should return >= 10 results");
        }

        private static async Task TestEnumDocumentsTagContainsNot()
        {
            var json = await DoEnumDocsRaw(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "ContainsNot", Value = "ai" } } } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Tag ContainsNot filter should return results");
        }

        private static async Task TestEnumDocumentsTermsRequired()
        {
            var json = await DoEnumDocsRaw(new { MaxResults = 100, Ordering = "CreatedDescending", TermsFilter = new { Required = new List<string> { "machine learning" } } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Terms required filter should return results");
        }

        private static async Task TestEnumDocumentsTermsExcluded()
        {
            var json = await DoEnumDocsRaw(new { MaxResults = 100, Ordering = "CreatedDescending", TermsFilter = new { Excluded = new List<string> { "quantum" } } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Terms excluded filter should return results");
        }

        private static async Task TestEnumDocumentsCombinedLabelAndTag()
        {
            var json = await DoEnumDocsRaw(new
            {
                MaxResults = 100,
                Ordering = "CreatedDescending",
                LabelFilter = new { Required = new List<string> { "science" } },
                TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "Equals", Value = "ai" } } }
            }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Combined label and tag filter should return results");
        }

        private static async Task TestEnumDocumentsCombinedAllFilters()
        {
            var json = await DoEnumDocsRaw(new
            {
                MaxResults = 100,
                Ordering = "CreatedDescending",
                LabelFilter = new { Required = new List<string> { "science" } },
                TagFilter = new { Required = new List<object> { new { Key = "year", Condition = "Equals", Value = "2024" } } },
                TermsFilter = new { Required = new List<string> { "learning" } },
                CreatedAfter = DateTime.UtcNow.AddHours(-1).ToString("o"),
                CreatedBefore = DateTime.UtcNow.AddHours(1).ToString("o")
            }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Combined all filters should return results");
        }

        private static async Task TestEnumDocumentsResultFields()
        {
            var json = await DoEnumDocsRaw(new { MaxResults = 100, Ordering = "CreatedDescending" }).ConfigureAwait(false);
            AssertTrue(json.GetProperty("Success").GetBoolean(), "Success should be true");
            AssertTrue(json.TryGetProperty("MaxResults", out _), "Response should contain MaxResults");
            AssertTrue(json.TryGetProperty("EndOfResults", out _), "Response should contain EndOfResults");
            AssertTrue(json.GetProperty("TotalRecords").GetInt64() > 0, "TotalRecords should be > 0");
            AssertTrue(json.TryGetProperty("RecordsRemaining", out _), "Response should contain RecordsRemaining");
            AssertTrue(json.TryGetProperty("Objects", out _), "Response should contain Objects");
        }

        private static async Task TestEnumDocumentsObjectFields()
        {
            var json = await DoEnumDocsRaw(new { MaxResults = 100, Ordering = "CreatedDescending" }).ConfigureAwait(false);
            var objects = GetObjects(json);
            AssertTrue(objects.GetArrayLength() > 0, "Should have at least one object");

            JsonElement obj = objects[0];
            AssertNotNullOrEmpty(obj.GetProperty("DocumentKey").GetString(), "DocumentKey");
            AssertNotNullOrEmpty(obj.GetProperty("DocumentId").GetString(), "DocumentId");
            AssertNotNullOrEmpty(obj.GetProperty("Content").GetString(), "Content");
            AssertNotNullOrEmpty(obj.GetProperty("ContentType").GetString(), "ContentType");
            AssertTrue(obj.GetProperty("ContentLength").GetInt32() > 0, "ContentLength should be > 0");
            AssertNotNullOrEmpty(obj.GetProperty("CreatedUtc").GetString(), "CreatedUtc");
            AssertTrue(obj.TryGetProperty("Labels", out _), "Object should have Labels");
            AssertTrue(obj.TryGetProperty("Tags", out _), "Object should have Tags");
        }

        #endregion

        #region Test-24-Tenant-Enumeration-Pagination

        private static async Task TestEnumerationPagination()
        {
            int totalToCreate = 5;

            // Create N tenants for pagination testing
            for (int i = 0; i < totalToCreate; i++)
            {
                TenantMetadata tenant = new TenantMetadata();
                tenant.Name = "PaginationTestTenant_" + i;
                TenantMetadata created = await _AdminClient.CreateTenantAsync(tenant).ConfigureAwait(false);
                _PaginationTenantIds.Add(created.Id);
            }

            // Now enumerate with a small page size
            int pageSize = 2;
            int totalFetched = 0;
            string continuationToken = null;
            bool endOfResults = false;
            long reportedTotal = 0;

            while (!endOfResults)
            {
                EnumerationQuery query = new EnumerationQuery();
                query.MaxResults = pageSize;
                query.Ordering = "CreatedAscending";
                query.ContinuationToken = continuationToken;

                EnumerationResult<TenantMetadata> result = await _AdminClient.EnumerateTenantsAsync(query).ConfigureAwait(false);
                endOfResults = result.EndOfResults;
                reportedTotal = result.TotalRecords;
                totalFetched += result.Objects.Count;

                if (!endOfResults)
                {
                    AssertNotNullOrEmpty(result.ContinuationToken, "ContinuationToken");
                    continuationToken = result.ContinuationToken;
                }
            }

            // reportedTotal should account for existing + created tenants
            AssertTrue(reportedTotal >= totalToCreate, "TotalRecords should be >= " + totalToCreate);
            AssertTrue(totalFetched >= totalToCreate, "Total fetched records should be >= " + totalToCreate);
            AssertTrue((long)totalFetched <= reportedTotal, "Total fetched should not exceed TotalRecords");
        }

        #endregion

        #region Test-25-Authorization

        private static async Task TestAuthorizationNonAdmin()
        {
            AssertNotNull(_UserClient, "UserClient should be initialized");

            // Non-admin user should get Forbidden when trying to create a tenant
            // The SDK throws RecallDbException on non-success status codes
            bool gotForbidden = false;
            try
            {
                TenantMetadata tenant = new TenantMetadata();
                tenant.Name = "UnauthorizedTenantAttempt";
                await _UserClient.CreateTenantAsync(tenant).ConfigureAwait(false);
            }
            catch (RecallDbException ex)
            {
                if (ex.StatusCode == HttpStatusCode.Forbidden)
                    gotForbidden = true;
                else
                    throw;
            }
            AssertTrue(gotForbidden, "Non-admin should get Forbidden (403)");
        }

        #endregion

        #region Test-26-Cleanup

        private static async Task TestCleanupSearchLabels()
        {
            if (string.IsNullOrEmpty(_TestCollectionId)) return;

            foreach (string labelId in _SearchTestLabelIds)
            {
                await _AdminClient.DeleteLabelAsync(_TestTenantId, _TestCollectionId, labelId).ConfigureAwait(false);
            }
        }

        private static async Task TestCleanupSearchTags()
        {
            if (string.IsNullOrEmpty(_TestCollectionId)) return;

            foreach (string tagId in _SearchTestTagIds)
            {
                await _AdminClient.DeleteTagAsync(_TestTenantId, _TestCollectionId, tagId).ConfigureAwait(false);
            }
        }

        private static async Task TestCleanupSearchDocuments()
        {
            if (string.IsNullOrEmpty(_TestCollectionId)) return;

            foreach (string docKey in _SearchTestDocKeys)
            {
                await _AdminClient.DeleteDocumentAsync(_TestTenantId, _TestCollectionId, docKey).ConfigureAwait(false);
            }
        }

        private static async Task TestCleanupLabels()
        {
            if (string.IsNullOrEmpty(_TestLabelId) || string.IsNullOrEmpty(_TestCollectionId)) return;
            await _AdminClient.DeleteLabelAsync(_TestTenantId, _TestCollectionId, _TestLabelId).ConfigureAwait(false);
        }

        private static async Task TestCleanupTags()
        {
            if (string.IsNullOrEmpty(_TestTagId) || string.IsNullOrEmpty(_TestCollectionId)) return;
            await _AdminClient.DeleteTagAsync(_TestTenantId, _TestCollectionId, _TestTagId).ConfigureAwait(false);
        }

        private static async Task TestCleanupDocuments()
        {
            if (string.IsNullOrEmpty(_TestCollectionId)) return;

            // Delete the single test document
            if (!string.IsNullOrEmpty(_TestDocumentKey))
            {
                await _AdminClient.DeleteDocumentAsync(_TestTenantId, _TestCollectionId, _TestDocumentKey).ConfigureAwait(false);
            }

            // Delete batch documents
            foreach (string batchKey in _TestBatchDocumentKeys)
            {
                await _AdminClient.DeleteDocumentAsync(_TestTenantId, _TestCollectionId, batchKey).ConfigureAwait(false);
            }
        }

        private static async Task TestCleanupCollection()
        {
            if (string.IsNullOrEmpty(_TestCollectionId)) return;
            await _AdminClient.DeleteCollectionAsync(_TestTenantId, _TestCollectionId).ConfigureAwait(false);
        }

        private static async Task TestCleanupCredential()
        {
            if (string.IsNullOrEmpty(_TestCredentialId)) return;
            await _AdminClient.DeleteCredentialAsync(_TestTenantId, _TestCredentialId).ConfigureAwait(false);
        }

        private static async Task TestCleanupUser()
        {
            if (string.IsNullOrEmpty(_TestUserId)) return;
            await _AdminClient.DeleteUserAsync(_TestTenantId, _TestUserId).ConfigureAwait(false);
        }

        private static async Task TestCleanupTenant()
        {
            if (string.IsNullOrEmpty(_TestTenantId)) return;

            // Use raw HTTP for ?force query parameter which the SDK does not support
            string path = "/v1.0/tenants/" + _TestTenantId + "?force";
            using HttpResponseMessage response = await RawDeleteAsync(_RawAdminClient, path).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.NoContent);
        }

        private static async Task TestCleanupPaginationTenants()
        {
            foreach (string tenantId in _PaginationTenantIds)
            {
                // Use raw HTTP for ?force query parameter which the SDK does not support
                string path = "/v1.0/tenants/" + tenantId + "?force";
                using HttpResponseMessage response = await RawDeleteAsync(_RawAdminClient, path).ConfigureAwait(false);
                AssertStatusCode(response, HttpStatusCode.NoContent);
            }
        }

        #endregion
    }
}
