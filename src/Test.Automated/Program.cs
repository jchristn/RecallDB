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

        // Search/enumeration test dataset
        private static List<string> _SearchTestDocKeys = new List<string>();
        private static List<string> _SearchTestLabelIds = new List<string>();
        private static List<string> _SearchTestTagIds = new List<string>();

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
            await RunTest("Search full-text: TsRank scoring", TestSearchFullTextTsRank);
            await RunTest("Search full-text: TsRankCd scoring", TestSearchFullTextTsRankCd);
            await RunTest("Search full-text: no match", TestSearchFullTextNoMatch);
            await RunTest("Search full-text: minimum score threshold", TestSearchFullTextMinimumScore);
            await RunTest("Search full-text: sort text score descending", TestSearchFullTextSortDescending);
            await RunTest("Search full-text: sort text score ascending", TestSearchFullTextSortAscending);
            await RunTest("Search hybrid: vector + full-text", TestSearchHybridBasic);
            await RunTest("Search hybrid: custom text weight", TestSearchHybridCustomWeight);
            await RunTest("Search hybrid: with label filter", TestSearchHybridWithLabelFilter);
            await RunTest("Search hybrid: with tag filter", TestSearchHybridWithTagFilter);
            await RunTest("Search hybrid: with terms filter", TestSearchHybridWithTermsFilter);
            await RunTest("Search hybrid: with date range", TestSearchHybridWithDateRange);
            await RunTest("Search full-text: pagination", TestSearchFullTextPagination);
            await RunTest("Search full-text: max results", TestSearchFullTextMaxResults);
            await RunTest("Search full-text: with label filter", TestSearchFullTextWithLabelFilter);
            await RunTest("Search full-text: with tag filter", TestSearchFullTextWithTagFilter);
            await RunTest("Search full-text: with terms filter", TestSearchFullTextWithTermsFilter);
            await RunTest("Search full-text: with date range", TestSearchFullTextWithDateRange);
            await RunTest("Search full-text: with document ids", TestSearchFullTextWithDocumentIds);
            await RunTest("Search full-text: distance is zero", TestSearchFullTextDistanceIsZero);
            await RunTest("Search full-text: TsRank vs TsRankCd differ", TestSearchFullTextRankTypesDiffer);
            await RunTest("Search hybrid: TextWeight 0.0 matches vector-only", TestSearchHybridWeightZero);
            await RunTest("Search hybrid: TextWeight 1.0 matches full-text-only", TestSearchHybridWeightOne);
            await RunTest("Search hybrid: blended score formula", TestSearchHybridBlendedScoreFormula);
            await RunTest("Search backward compat: vector-only unchanged", TestSearchBackwardCompatVectorOnly);

            // 22. Search Result Validation
            await RunTest("Search validation: result fields", TestSearchResultFields);
            await RunTest("Search validation: document fields", TestSearchDocumentFields);

            // 22b. Neighbor Retrieval
            await RunTest("Neighbor retrieval: setup data", TestNeighborDataSetup);
            await RunTest("Neighbor retrieval: vector search with neighbors", TestNeighborVectorSearch);
            await RunTest("Neighbor retrieval: hybrid search with neighbors", TestNeighborHybridSearch);
            await RunTest("Neighbor retrieval: full-text search with neighbors", TestNeighborFullTextSearch);
            await RunTest("Neighbor retrieval: boundary chunk (position 0)", TestNeighborBoundaryChunk);
            await RunTest("Neighbor retrieval: single-chunk document", TestNeighborSingleChunkDocument);
            await RunTest("Neighbor retrieval: null and zero means no neighbors", TestNeighborNullAndZero);
            await RunTest("Neighbor retrieval: overlapping results same document", TestNeighborOverlapping);
            await RunTest("Neighbor retrieval: neighbor labels and tags", TestNeighborLabelsAndTags);

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
            using StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PutAsync(path, content).ConfigureAwait(false);
            return response;
        }

        private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body)
        {
            string json = JsonSerializer.Serialize(body, _JsonOptions);
            using StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
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
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, path);
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

        private static string SearchPath()
        {
            return "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/search";
        }

        private static string EnumDocPath()
        {
            return "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/documents/enumerate";
        }

        private static string DocBasePath()
        {
            return "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/documents";
        }

        private static string LabelBasePath()
        {
            return "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/labels";
        }

        private static string TagBasePath()
        {
            return "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/tags";
        }

        private static async Task<JsonElement> DoSearch(object body)
        {
            using HttpResponseMessage response = await PostAsync(_AdminClient, SearchPath(), body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);
            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.GetProperty("Success").GetBoolean(), "Search should succeed");
            return json;
        }

        private static async Task<JsonElement> DoEnumDocs(object body)
        {
            using HttpResponseMessage response = await PostAsync(_AdminClient, EnumDocPath(), body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);
            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.GetProperty("Success").GetBoolean(), "Enumeration should succeed");
            return json;
        }

        private static JsonElement GetDocs(JsonElement json)
        {
            return json.GetProperty("Documents");
        }

        private static JsonElement GetObjects(JsonElement json)
        {
            return json.GetProperty("Objects");
        }

        #endregion

        #region Test-1-Connectivity

        private static async Task TestConnectivityGet()
        {
            using HttpResponseMessage response = await GetAsync(_AdminClient, "/").ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Name", out JsonElement nameElem), "Response should contain Name");
            AssertEqual("RecallDB", nameElem.GetString(), "Name");
        }

        private static async Task TestConnectivityHead()
        {
            using HttpResponseMessage response = await HeadAsync(_AdminClient, "/").ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);
        }

        #endregion

        #region Test-2-Authentication

        private static async Task TestAuthenticateBearer()
        {
            object body = new { BearerToken = "default" };
            using HttpResponseMessage response = await PostAsync(_AdminClient, "/v1.0/authenticate", body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Success", out JsonElement successElem), "Response should contain Success");
            AssertTrue(successElem.GetBoolean(), "Authentication should succeed");
        }

        private static async Task TestAuthenticateEmailPassword()
        {
            object body = new { TenantId = "default", Email = "admin@recall", Password = "password" };
            using HttpResponseMessage response = await PostAsync(_AdminClient, "/v1.0/authenticate", body).ConfigureAwait(false);
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
            using HttpResponseMessage response = await PutAsync(_AdminClient, "/v1.0/tenants", body).ConfigureAwait(false);
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
            using HttpResponseMessage response = await GetAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Id", out JsonElement idElem), "Response should contain Id");
            AssertEqual(_TestTenantId, idElem.GetString(), "Id");
        }

        private static async Task TestTenantUpdate()
        {
            object body = new { Name = "IntegrationTestTenantUpdated" };
            using HttpResponseMessage response = await PutAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId, body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Name", out JsonElement nameElem), "Response should contain Name");
            AssertEqual("IntegrationTestTenantUpdated", nameElem.GetString(), "Name");
        }

        private static async Task TestTenantEnumerate()
        {
            object body = new { MaxResults = 100 };
            using HttpResponseMessage response = await PostAsync(_AdminClient, "/v1.0/tenants/enumerate", body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Success", out JsonElement successElem), "Response should contain Success");
            AssertTrue(successElem.GetBoolean(), "Enumeration should succeed");
            AssertTrue(json.TryGetProperty("TotalRecords", out JsonElement totalElem), "Response should contain TotalRecords");
            AssertTrue(totalElem.GetInt64() >= 1, "TotalRecords should be >= 1");
        }

        private static async Task TestTenantExists()
        {
            using HttpResponseMessage response = await HeadAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId).ConfigureAwait(false);
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
            using HttpResponseMessage response = await PutAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId + "/users", body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.Created);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Id", out JsonElement idElem), "Response should contain Id");
            _TestUserId = idElem.GetString();
            AssertNotNullOrEmpty(_TestUserId, "UserId");
        }

        private static async Task TestUserRead()
        {
            using HttpResponseMessage response = await GetAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId + "/users/" + _TestUserId).ConfigureAwait(false);
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
            using HttpResponseMessage response = await PutAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId + "/users/" + _TestUserId, body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("FirstName", out JsonElement fnElem), "Response should contain FirstName");
            AssertEqual("TestUpdated", fnElem.GetString(), "FirstName");
        }

        private static async Task TestUserEnumerate()
        {
            object body = new { MaxResults = 100 };
            using HttpResponseMessage response = await PostAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId + "/users/enumerate", body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("TotalRecords", out JsonElement totalElem), "Response should contain TotalRecords");
            AssertTrue(totalElem.GetInt64() >= 1, "TotalRecords should be >= 1");
        }

        private static async Task TestUserExists()
        {
            using HttpResponseMessage response = await HeadAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId + "/users/" + _TestUserId).ConfigureAwait(false);
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
            using HttpResponseMessage response = await PutAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId + "/credentials", body).ConfigureAwait(false);
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
            using HttpResponseMessage response = await GetAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId + "/credentials/" + _TestCredentialId).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Id", out JsonElement idElem), "Response should contain Id");
            AssertEqual(_TestCredentialId, idElem.GetString(), "Id");
        }

        private static async Task TestCredentialEnumerate()
        {
            object body = new { MaxResults = 100 };
            using HttpResponseMessage response = await PostAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId + "/credentials/enumerate", body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("TotalRecords", out JsonElement totalElem), "Response should contain TotalRecords");
            AssertTrue(totalElem.GetInt64() >= 1, "TotalRecords should be >= 1");
        }

        private static async Task TestCredentialExists()
        {
            using HttpResponseMessage response = await HeadAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId + "/credentials/" + _TestCredentialId).ConfigureAwait(false);
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
            using HttpResponseMessage response = await PutAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId + "/collections", body).ConfigureAwait(false);
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
            using HttpResponseMessage response = await GetAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId).ConfigureAwait(false);
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
            using HttpResponseMessage response = await PutAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId, body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("Name", out JsonElement nameElem), "Response should contain Name");
            AssertEqual("IntegrationTestCollectionUpdated", nameElem.GetString(), "Name");
        }

        private static async Task TestCollectionEnumerate()
        {
            object body = new { MaxResults = 100 };
            using HttpResponseMessage response = await PostAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId + "/collections/enumerate", body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("TotalRecords", out JsonElement totalElem), "Response should contain TotalRecords");
            AssertTrue(totalElem.GetInt64() >= 1, "TotalRecords should be >= 1");
        }

        private static async Task TestCollectionExists()
        {
            using HttpResponseMessage response = await HeadAsync(_AdminClient, "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId).ConfigureAwait(false);
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
            using HttpResponseMessage response = await PutAsync(_AdminClient, path, body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.Created);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("DocumentKey", out JsonElement keyElem), "Response should contain DocumentKey");
            _TestDocumentKey = keyElem.GetString();
            AssertNotNullOrEmpty(_TestDocumentKey, "DocumentKey");
        }

        private static async Task TestDocumentRead()
        {
            string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/documents/" + _TestDocumentKey;
            using HttpResponseMessage response = await GetAsync(_AdminClient, path).ConfigureAwait(false);
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
            using HttpResponseMessage response = await PutAsync(_AdminClient, path, body).ConfigureAwait(false);
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
            using HttpResponseMessage response = await PostAsync(_AdminClient, path, docs).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.Created);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.ValueKind == JsonValueKind.Array, "Response should be an array");
            AssertEqual(3, json.GetArrayLength(), "Batch should create 3 documents");

            // Verify all documents were created by reading each one
            foreach (string batchKey in _TestBatchDocumentKeys)
            {
                string readPath = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/documents/" + batchKey;
                using HttpResponseMessage readResp = await GetAsync(_AdminClient, readPath).ConfigureAwait(false);
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
            using HttpResponseMessage response = await PutAsync(_AdminClient, path, body).ConfigureAwait(false);
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
            using HttpResponseMessage response = await GetAsync(_AdminClient, path).ConfigureAwait(false);
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
            using HttpResponseMessage response = await PutAsync(_AdminClient, path, body).ConfigureAwait(false);
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
            using HttpResponseMessage response = await GetAsync(_AdminClient, path).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);

            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.TryGetProperty("TotalRecords", out JsonElement totalElem), "Response should contain TotalRecords");
            AssertTrue(totalElem.GetInt64() >= 1, "TotalRecords should be >= 1");
        }

        #endregion

        #region Test-11-Search-Data-Setup

        private static async Task TestSearchDataSetup()
        {
            // 1. Create 10 documents via batch
            List<object> docs = new List<object>
            {
                new { DocumentKey = "srch-doc-0", DocumentId = "grp-alpha", Content = "Machine learning is transforming industries worldwide", ContentType = "Text", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } },
                new { DocumentKey = "srch-doc-1", DocumentId = "grp-alpha", Content = "Deep learning neural networks for image recognition", ContentType = "Code", Embeddings = new List<float> { 0.8f, 0.15f, 0.1f } },
                new { DocumentKey = "srch-doc-2", DocumentId = "grp-beta", Content = "Quantum computing breakthroughs in 2024", ContentType = "Text", Embeddings = new List<float> { 0.1f, 0.9f, 0.05f } },
                new { DocumentKey = "srch-doc-3", DocumentId = "grp-beta", Content = "Classical physics and thermodynamics overview", ContentType = "Text", Embeddings = new List<float> { 0.05f, 0.85f, 0.15f } },
                new { DocumentKey = "srch-doc-4", DocumentId = "grp-gamma", Content = "Cooking recipes from Mediterranean cuisine", ContentType = "Text", Embeddings = new List<float> { 0.1f, 0.1f, 0.9f } },
                new { DocumentKey = "srch-doc-5", DocumentId = "grp-gamma", Content = "Travel guide to Southeast Asia for beginners", ContentType = "Text", Embeddings = new List<float> { 0.05f, 0.15f, 0.85f } },
                new { DocumentKey = "srch-doc-6", DocumentId = "grp-delta", Content = "Financial markets analysis and stock trading strategies", ContentType = "Text", Embeddings = new List<float> { 0.5f, 0.5f, 0.1f } },
                new { DocumentKey = "srch-doc-7", DocumentId = "grp-delta", Content = "Startup funding and venture capital trends", ContentType = "Text", Embeddings = new List<float> { 0.45f, 0.55f, 0.05f } },
                new { DocumentKey = "srch-doc-8", DocumentId = "grp-epsilon", Content = "Health benefits of regular exercise and nutrition", ContentType = "Text", Embeddings = new List<float> { 0.3f, 0.3f, 0.5f } },
                new { DocumentKey = "srch-doc-9", DocumentId = "grp-epsilon", Content = "Mental health awareness and meditation techniques", ContentType = "Text", Embeddings = new List<float> { 0.25f, 0.35f, 0.45f } }
            };

            using HttpResponseMessage batchResp = await PostAsync(_AdminClient, DocBasePath() + "/batch", docs).ConfigureAwait(false);
            AssertStatusCode(batchResp, HttpStatusCode.Created);
            JsonElement batchJson = await ReadResponse<JsonElement>(batchResp).ConfigureAwait(false);
            AssertTrue(batchJson.ValueKind == JsonValueKind.Array, "Batch response should be an array");
            AssertEqual(10, batchJson.GetArrayLength(), "Batch should create 10 documents");

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
                foreach (string label in labelMap[i])
                {
                    object labelBody = new { DocumentKey = _SearchTestDocKeys[i], Label = label };
                    using HttpResponseMessage labelResp = await PutAsync(_AdminClient, LabelBasePath(), labelBody).ConfigureAwait(false);
                    AssertStatusCode(labelResp, HttpStatusCode.Created);
                    JsonElement labelJson = await ReadResponse<JsonElement>(labelResp).ConfigureAwait(false);
                    _SearchTestLabelIds.Add(labelJson.GetProperty("Id").GetString());
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
                    object tagBody = new { DocumentKey = _SearchTestDocKeys[i], Key = kv[0], Value = kv[1] };
                    using HttpResponseMessage tagResp = await PutAsync(_AdminClient, TagBasePath(), tagBody).ConfigureAwait(false);
                    AssertStatusCode(tagResp, HttpStatusCode.Created);
                    JsonElement tagJson = await ReadResponse<JsonElement>(tagResp).ConfigureAwait(false);
                    _SearchTestTagIds.Add(tagJson.GetProperty("Id").GetString());
                }
            }
        }

        #endregion

        #region Test-12-Vector-Search

        private static async Task TestSearchCosineSimilarity()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, MaxResults = 10 }).ConfigureAwait(false);
            var docsElem = GetDocs(json);
            AssertTrue(docsElem.GetArrayLength() > 0, "Cosine similarity search should return results");

            double prev = double.MaxValue;
            foreach (JsonElement doc in docsElem.EnumerateArray())
            {
                double score = doc.GetProperty("Score").GetDouble();
                AssertTrue(score <= prev, "Cosine similarity results should be ordered by score descending");
                prev = score;
            }
        }

        private static async Task TestSearchCosineDistance()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineDistance", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, SortOrder = "DistanceAscending", MaxResults = 10 }).ConfigureAwait(false);
            var docsElem = GetDocs(json);
            AssertTrue(docsElem.GetArrayLength() > 0, "Cosine distance search should return results");

            double prev = -1;
            foreach (JsonElement doc in docsElem.EnumerateArray())
            {
                double score = doc.GetProperty("Score").GetDouble();
                AssertTrue(score >= prev, "Cosine distance results should be ordered by score ascending");
                prev = score;
            }
        }

        private static async Task TestSearchEuclideanSimilarity()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "EuclideanSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, MaxResults = 10 }).ConfigureAwait(false);
            var docsElem = GetDocs(json);
            AssertTrue(docsElem.GetArrayLength() > 0, "Euclidean similarity search should return results");

            foreach (JsonElement doc in docsElem.EnumerateArray())
            {
                AssertTrue(doc.TryGetProperty("Score", out _), "Each document should have a Score property");
            }
        }

        private static async Task TestSearchEuclideanDistance()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "EuclideanDistance", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, SortOrder = "DistanceAscending", MaxResults = 10 }).ConfigureAwait(false);
            var docsElem = GetDocs(json);
            AssertTrue(docsElem.GetArrayLength() > 0, "Euclidean distance search should return results");

            double prev = -1;
            foreach (JsonElement doc in docsElem.EnumerateArray())
            {
                double score = doc.GetProperty("Score").GetDouble();
                AssertTrue(score >= prev, "Euclidean distance results should be ordered by score ascending");
                prev = score;
            }
        }

        private static async Task TestSearchInnerProduct()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "InnerProduct", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, MaxResults = 10 }).ConfigureAwait(false);
            var docsElem = GetDocs(json);
            AssertTrue(docsElem.GetArrayLength() > 0, "Inner product search should return results");

            foreach (JsonElement doc in docsElem.EnumerateArray())
            {
                AssertTrue(doc.TryGetProperty("Score", out _), "Each document should have a Score property");
            }
        }

        #endregion

        #region Test-13-Search-Sort-Orders

        private static async Task TestSearchSortScoreDescending()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, SortOrder = "ScoreDescending", MaxResults = 10 }).ConfigureAwait(false);
            var docsElem = GetDocs(json);
            double prev = double.MaxValue;
            foreach (JsonElement doc in docsElem.EnumerateArray())
            {
                double score = doc.GetProperty("Score").GetDouble();
                AssertTrue(score <= prev, "ScoreDescending: score[i] >= score[i+1]");
                prev = score;
            }
        }

        private static async Task TestSearchSortScoreAscending()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, SortOrder = "ScoreAscending", MaxResults = 10 }).ConfigureAwait(false);
            var docsElem = GetDocs(json);
            double prev = -1;
            foreach (JsonElement doc in docsElem.EnumerateArray())
            {
                double score = doc.GetProperty("Score").GetDouble();
                AssertTrue(score >= prev, "ScoreAscending: score[i] <= score[i+1]");
                prev = score;
            }
        }

        private static async Task TestSearchSortDistanceAscending()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "EuclideanDistance", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, SortOrder = "DistanceAscending", MaxResults = 10 }).ConfigureAwait(false);
            var docsElem = GetDocs(json);
            double prev = -1;
            foreach (JsonElement doc in docsElem.EnumerateArray())
            {
                double score = doc.GetProperty("Score").GetDouble();
                AssertTrue(score >= prev, "DistanceAscending: score[i] <= score[i+1]");
                prev = score;
            }
        }

        private static async Task TestSearchSortDistanceDescending()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "EuclideanDistance", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, SortOrder = "DistanceDescending", MaxResults = 10 }).ConfigureAwait(false);
            var docsElem = GetDocs(json);
            double prev = double.MaxValue;
            foreach (JsonElement doc in docsElem.EnumerateArray())
            {
                double score = doc.GetProperty("Score").GetDouble();
                AssertTrue(score <= prev, "DistanceDescending: score[i] >= score[i+1]");
                prev = score;
            }
        }

        private static async Task TestSearchSortCreatedAscending()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, SortOrder = "CreatedAscending", MaxResults = 10 }).ConfigureAwait(false);
            var docsElem = GetDocs(json);
            DateTime prev = DateTime.MinValue;
            foreach (JsonElement doc in docsElem.EnumerateArray())
            {
                DateTime created = DateTime.Parse(doc.GetProperty("CreatedUtc").GetString());
                AssertTrue(created >= prev, "CreatedAscending: CreatedUtc[i] <= CreatedUtc[i+1]");
                prev = created;
            }
        }

        private static async Task TestSearchSortCreatedDescending()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, SortOrder = "CreatedDescending", MaxResults = 10 }).ConfigureAwait(false);
            var docsElem = GetDocs(json);
            DateTime prev = DateTime.MaxValue;
            foreach (JsonElement doc in docsElem.EnumerateArray())
            {
                DateTime created = DateTime.Parse(doc.GetProperty("CreatedUtc").GetString());
                AssertTrue(created <= prev, "CreatedDescending: CreatedUtc[i] >= CreatedUtc[i+1]");
                prev = created;
            }
        }

        #endregion

        #region Test-14-Search-Thresholds

        private static async Task TestSearchMinimumScore()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, MinimumScore = 0.9, MaxResults = 10 }).ConfigureAwait(false);
            foreach (JsonElement doc in GetDocs(json).EnumerateArray())
            {
                AssertGreaterThanOrEqual(doc.GetProperty("Score").GetDouble(), 0.9, "Score");
            }
        }

        private static async Task TestSearchMaximumScore()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, MaximumScore = 0.5, MaxResults = 10 }).ConfigureAwait(false);
            foreach (JsonElement doc in GetDocs(json).EnumerateArray())
            {
                AssertLessThanOrEqual(doc.GetProperty("Score").GetDouble(), 0.5, "Score");
            }
        }

        private static async Task TestSearchMinMaxScore()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, MinimumScore = 0.3, MaximumScore = 0.7, MaxResults = 10 }).ConfigureAwait(false);
            foreach (JsonElement doc in GetDocs(json).EnumerateArray())
            {
                double score = doc.GetProperty("Score").GetDouble();
                AssertGreaterThanOrEqual(score, 0.3, "Score");
                AssertLessThanOrEqual(score, 0.7, "Score");
            }
        }

        private static async Task TestSearchMinimumDistance()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "EuclideanDistance", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, SortOrder = "DistanceAscending", MinimumDistance = 0.5, MaxResults = 10 }).ConfigureAwait(false);
            foreach (JsonElement doc in GetDocs(json).EnumerateArray())
            {
                AssertGreaterThanOrEqual(doc.GetProperty("Score").GetDouble(), 0.5, "Score");
            }
        }

        private static async Task TestSearchMaximumDistance()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "EuclideanDistance", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, SortOrder = "DistanceAscending", MaximumDistance = 1.5, MaxResults = 10 }).ConfigureAwait(false);
            foreach (JsonElement doc in GetDocs(json).EnumerateArray())
            {
                AssertLessThanOrEqual(doc.GetProperty("Score").GetDouble(), 1.5, "Score");
            }
        }

        #endregion

        #region Test-15-Search-Label-Filters

        private static async Task TestSearchLabelRequired()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, LabelFilter = new { Required = new List<string> { "science" } }, MaxResults = 20 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Label required filter should return results");
        }

        private static async Task TestSearchLabelExcluded()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, LabelFilter = new { Excluded = new List<string> { "lifestyle" } }, MaxResults = 20 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Label excluded filter should return results");
        }

        private static async Task TestSearchLabelRequiredMultiple()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, LabelFilter = new { Required = new List<string> { "science", "tech" } }, MaxResults = 20 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Label required multiple filter should return results");
        }

        private static async Task TestSearchLabelRequiredAndExcluded()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, LabelFilter = new { Required = new List<string> { "science" }, Excluded = new List<string> { "tech" } }, MaxResults = 20 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Label required and excluded filter should return results");
        }

        private static async Task TestSearchLabelNoMatch()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, LabelFilter = new { Required = new List<string> { "nonexistent-label-xyz" } }, MaxResults = 20 }).ConfigureAwait(false);
            AssertEqual(0, GetDocs(json).GetArrayLength(), "Label no match should return 0 results");
        }

        #endregion

        #region Test-16-Search-Tag-Filters

        private static async Task TestSearchTagEquals()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "Equals", Value = "ai" } } }, MaxResults = 20 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Tag Equals filter should return results");
        }

        private static async Task TestSearchTagNotEquals()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "NotEquals", Value = "ai" } } }, MaxResults = 20 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Tag NotEquals filter should return results");
        }

        private static async Task TestSearchTagContains()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "Contains", Value = "heal" } } }, MaxResults = 20 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Tag Contains filter should return results");
        }

        private static async Task TestSearchTagContainsNot()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "ContainsNot", Value = "ai" } } }, MaxResults = 20 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Tag ContainsNot filter should return results");
        }

        private static async Task TestSearchTagStartsWith()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "StartsWith", Value = "fin" } } }, MaxResults = 20 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Tag StartsWith filter should return results");
        }

        private static async Task TestSearchTagEndsWith()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "EndsWith", Value = "ics" } } }, MaxResults = 20 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Tag EndsWith filter should return results");
        }

        private static async Task TestSearchTagGreaterThan()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TagFilter = new { Required = new List<object> { new { Key = "year", Condition = "GreaterThan", Value = "2023" } } }, MaxResults = 20 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Tag GreaterThan filter should return results");
        }

        private static async Task TestSearchTagLessThan()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TagFilter = new { Required = new List<object> { new { Key = "year", Condition = "LessThan", Value = "2023" } } }, MaxResults = 20 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Tag LessThan filter should return results");
        }

        private static async Task TestSearchTagIsNotNull()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TagFilter = new { Required = new List<object> { new { Key = "priority", Condition = "IsNotNull" } } }, MaxResults = 20 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() >= 10, "Tag IsNotNull filter should return >= 10 results");
        }

        private static async Task TestSearchTagIsNull()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TagFilter = new { Required = new List<object> { new { Key = "nonexistent-tag-xyz", Condition = "IsNull" } } }, MaxResults = 20 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() >= 10, "Tag IsNull filter should return >= 10 results");
        }

        private static async Task TestSearchTagExcluded()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TagFilter = new { Excluded = new List<object> { new { Key = "priority", Condition = "Equals", Value = "low" } } }, MaxResults = 20 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Tag Excluded filter should return results");
        }

        #endregion

        #region Test-17-Search-Terms-Filters

        private static async Task TestSearchTermsRequired()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TermsFilter = new { Required = new List<string> { "machine learning" } }, MaxResults = 20 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Terms required filter should return results");
        }

        private static async Task TestSearchTermsExcluded()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TermsFilter = new { Excluded = new List<string> { "quantum" } }, MaxResults = 20 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Terms excluded filter should return results");
        }

        private static async Task TestSearchTermsRequiredMultiple()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TermsFilter = new { Required = new List<string> { "learning", "neural" } }, MaxResults = 20 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Terms required multiple filter should return results");
        }

        private static async Task TestSearchTermsRequiredAndExcluded()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TermsFilter = new { Required = new List<string> { "health" }, Excluded = new List<string> { "meditation" } }, MaxResults = 20 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Terms required and excluded filter should return results");
        }

        private static async Task TestSearchTermsCaseInsensitive()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TermsFilter = new { Required = new List<string> { "MACHINE LEARNING" } }, MaxResults = 20 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Terms case insensitive filter should return results");
        }

        #endregion

        #region Test-18-Search-Date-Range

        private static async Task TestSearchCreatedAfter()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, CreatedAfter = DateTime.UtcNow.AddHours(-1).ToString("o"), MaxResults = 20 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "CreatedAfter filter should return results");
        }

        private static async Task TestSearchCreatedBefore()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, CreatedBefore = DateTime.UtcNow.AddHours(1).ToString("o"), MaxResults = 20 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "CreatedBefore filter should return results");
        }

        private static async Task TestSearchCreatedBeforeNone()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, CreatedBefore = DateTime.UtcNow.AddHours(-1).ToString("o"), MaxResults = 20 }).ConfigureAwait(false);
            AssertEqual(0, GetDocs(json).GetArrayLength(), "CreatedBefore in past should return 0 results");
        }

        private static async Task TestSearchDateRangeCombined()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, CreatedAfter = DateTime.UtcNow.AddHours(-1).ToString("o"), CreatedBefore = DateTime.UtcNow.AddHours(1).ToString("o"), MaxResults = 20 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Date range combined filter should return results");
        }

        #endregion

        #region Test-19-Search-DocumentIds

        private static async Task TestSearchDocumentIds()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, DocumentIds = new List<string> { "grp-alpha", "grp-beta" }, MaxResults = 20 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "DocumentIds filter should return results");
        }

        private static async Task TestSearchDocumentIdsNoMatch()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, DocumentIds = new List<string> { "nonexistent-docid-xyz" }, MaxResults = 20 }).ConfigureAwait(false);
            AssertEqual(0, GetDocs(json).GetArrayLength(), "DocumentIds no match should return 0 results");
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
                object body;
                if (ct != null)
                    body = new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, MaxResults = 3, ContinuationToken = ct };
                else
                    body = new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, MaxResults = 3 };
                var json = await DoSearch(body).ConfigureAwait(false);
                eor = json.GetProperty("EndOfResults").GetBoolean();
                totalFetched += GetDocs(json).GetArrayLength();
                if (!eor) { ct = json.GetProperty("ContinuationToken").GetString(); AssertNotNullOrEmpty(ct, "ContinuationToken"); }
            }
            AssertTrue(totalFetched >= 10, "Should page through all search docs");
        }

        private static async Task TestSearchMaxResults()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, MaxResults = 2 }).ConfigureAwait(false);
            AssertEqual(2, GetDocs(json).GetArrayLength(), "MaxResults should limit to 2");
        }

        #endregion

        #region Test-21-Search-Combined-Filters

        private static async Task TestSearchCombinedLabelAndTag()
        {
            var json = await DoSearch(new
            {
                Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } },
                LabelFilter = new { Required = new List<string> { "science" } },
                TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "Equals", Value = "ai" } } },
                MaxResults = 20
            }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Combined label and tag filter should return results");
        }

        private static async Task TestSearchCombinedTermsAndLabel()
        {
            var json = await DoSearch(new
            {
                Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } },
                TermsFilter = new { Required = new List<string> { "exercise" } },
                LabelFilter = new { Required = new List<string> { "lifestyle" } },
                MaxResults = 20
            }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Combined terms and label filter should return results");
        }

        private static async Task TestSearchCombinedAllFilters()
        {
            var json = await DoSearch(new
            {
                Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } },
                LabelFilter = new { Required = new List<string> { "science" } },
                TagFilter = new { Required = new List<object> { new { Key = "year", Condition = "Equals", Value = "2024" } } },
                TermsFilter = new { Required = new List<string> { "learning" } },
                CreatedAfter = DateTime.UtcNow.AddHours(-1).ToString("o"),
                CreatedBefore = DateTime.UtcNow.AddHours(1).ToString("o"),
                MaxResults = 20
            }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Combined all filters should return results");
        }

        #endregion

        #region Test-21b-Full-Text-Search

        private static readonly List<float> _SearchEmb = new List<float> { 0.9f, 0.1f, 0.05f };

        private static async Task TestSearchFullTextBasic()
        {
            var json = await DoSearch(new { FullText = new { Query = "machine learning" }, MaxResults = 10 }).ConfigureAwait(false);
            var docs = GetDocs(json);
            AssertTrue(docs.GetArrayLength() > 0, "Full-text search should return results");
            foreach (JsonElement doc in docs.EnumerateArray())
            {
                AssertTrue(doc.GetProperty("Score").GetDouble() > 0, "Score should be > 0");
                AssertTrue(doc.TryGetProperty("TextScore", out JsonElement ts) && ts.GetDouble() > 0, "TextScore should be > 0");
            }
        }

        private static async Task TestSearchFullTextTsRank()
        {
            var json = await DoSearch(new { FullText = new { Query = "learning", SearchType = "TsRank" }, MaxResults = 10 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "TsRank search should return results");
        }

        private static async Task TestSearchFullTextTsRankCd()
        {
            var json = await DoSearch(new { FullText = new { Query = "neural networks image", SearchType = "TsRankCd" }, MaxResults = 10 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "TsRankCd search should return results");
        }

        private static async Task TestSearchFullTextNoMatch()
        {
            var json = await DoSearch(new { FullText = new { Query = "xyznonexistentterm12345" }, MaxResults = 10 }).ConfigureAwait(false);
            AssertTrue(json.GetProperty("TotalRecords").GetInt64() == 0, "Should return 0 total records");
            AssertTrue(GetDocs(json).GetArrayLength() == 0, "Documents should be empty");
        }

        private static async Task TestSearchFullTextMinimumScore()
        {
            var json = await DoSearch(new { FullText = new { Query = "learning", MinimumScore = 0.01 }, MaxResults = 10 }).ConfigureAwait(false);
            foreach (JsonElement doc in GetDocs(json).EnumerateArray())
            {
                double ts = doc.GetProperty("TextScore").GetDouble();
                AssertTrue(ts >= 0.01, "TextScore should be >= minimum threshold");
            }
        }

        private static async Task TestSearchFullTextSortDescending()
        {
            var json = await DoSearch(new { FullText = new { Query = "learning" }, SortOrder = "TextScoreDescending", MaxResults = 10 }).ConfigureAwait(false);
            var docs = GetDocs(json);
            AssertTrue(docs.GetArrayLength() > 0, "Should return results");
            double prev = double.MaxValue;
            foreach (JsonElement doc in docs.EnumerateArray())
            {
                double ts = doc.GetProperty("TextScore").GetDouble();
                AssertTrue(ts <= prev, "TextScore should be in descending order");
                prev = ts;
            }
        }

        private static async Task TestSearchFullTextSortAscending()
        {
            var json = await DoSearch(new { FullText = new { Query = "learning" }, SortOrder = "TextScoreAscending", MaxResults = 10 }).ConfigureAwait(false);
            var docs = GetDocs(json);
            AssertTrue(docs.GetArrayLength() > 0, "Should return results");
            double prev = -1;
            foreach (JsonElement doc in docs.EnumerateArray())
            {
                double ts = doc.GetProperty("TextScore").GetDouble();
                AssertTrue(ts >= prev, "TextScore should be in ascending order");
                prev = ts;
            }
        }

        private static async Task TestSearchHybridBasic()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = _SearchEmb }, FullText = new { Query = "machine learning" }, MaxResults = 10 }).ConfigureAwait(false);
            var docs = GetDocs(json);
            AssertTrue(docs.GetArrayLength() > 0, "Hybrid search should return results");
            foreach (JsonElement doc in docs.EnumerateArray())
            {
                AssertTrue(doc.GetProperty("Score").GetDouble() > 0, "Score should be > 0");
                AssertTrue(doc.TryGetProperty("TextScore", out JsonElement ts) && ts.GetDouble() > 0, "TextScore should be populated");
            }
        }

        private static async Task TestSearchHybridCustomWeight()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = _SearchEmb }, FullText = new { Query = "machine learning", TextWeight = 0.3 }, MaxResults = 10 }).ConfigureAwait(false);
            var docs = GetDocs(json);
            AssertTrue(docs.GetArrayLength() > 0, "Hybrid search with custom weight should return results");
        }

        private static async Task TestSearchHybridWithLabelFilter()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = _SearchEmb }, FullText = new { Query = "learning" }, LabelFilter = new { Required = new List<string> { "science" } }, MaxResults = 20 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Hybrid with label filter should return results");
        }

        private static async Task TestSearchHybridWithTagFilter()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = _SearchEmb }, FullText = new { Query = "learning" }, TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "Equals", Value = "ai" } } }, MaxResults = 20 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Hybrid with tag filter should return results");
        }

        private static async Task TestSearchHybridWithTermsFilter()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = _SearchEmb }, FullText = new { Query = "learning" }, TermsFilter = new { Required = new List<string> { "Machine" } }, MaxResults = 20 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Hybrid with terms filter should return results");
        }

        private static async Task TestSearchHybridWithDateRange()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = _SearchEmb }, FullText = new { Query = "learning" }, CreatedAfter = DateTime.UtcNow.AddHours(-1).ToString("o"), CreatedBefore = DateTime.UtcNow.AddHours(1).ToString("o"), MaxResults = 20 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Hybrid with date range should return results");
        }

        private static async Task TestSearchFullTextPagination()
        {
            int totalFetched = 0;
            string ct = null;
            bool eor = false;
            while (!eor)
            {
                object body;
                if (ct != null)
                    body = new { FullText = new { Query = "learning" }, MaxResults = 2, ContinuationToken = ct };
                else
                    body = new { FullText = new { Query = "learning" }, MaxResults = 2 };
                var json = await DoSearch(body).ConfigureAwait(false);
                eor = json.GetProperty("EndOfResults").GetBoolean();
                totalFetched += GetDocs(json).GetArrayLength();
                if (!eor)
                {
                    ct = json.GetProperty("ContinuationToken").GetString();
                    AssertNotNullOrEmpty(ct, "ContinuationToken");
                }
            }
            AssertTrue(totalFetched > 0, "Should page through full-text results");
        }

        private static async Task TestSearchFullTextMaxResults()
        {
            var json = await DoSearch(new { FullText = new { Query = "learning" }, MaxResults = 1 }).ConfigureAwait(false);
            AssertTrue(GetDocs(json).GetArrayLength() <= 1, "MaxResults should limit to 1");
        }

        private static async Task TestSearchFullTextWithLabelFilter()
        {
            var json = await DoSearch(new { FullText = new { Query = "learning" }, LabelFilter = new { Required = new List<string> { "science" } }, MaxResults = 20 }).ConfigureAwait(false);
            var docs = GetDocs(json);
            AssertTrue(docs.GetArrayLength() > 0, "Full-text with label filter should return results");
            foreach (JsonElement doc in docs.EnumerateArray())
            {
                AssertTrue(doc.GetProperty("TextScore").GetDouble() > 0, "TextScore should be > 0");
            }
        }

        private static async Task TestSearchFullTextWithTagFilter()
        {
            var json = await DoSearch(new { FullText = new { Query = "learning" }, TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "Equals", Value = "ai" } } }, MaxResults = 20 }).ConfigureAwait(false);
            var docs = GetDocs(json);
            AssertTrue(docs.GetArrayLength() > 0, "Full-text with tag filter should return results");
            foreach (JsonElement doc in docs.EnumerateArray())
            {
                AssertTrue(doc.GetProperty("TextScore").GetDouble() > 0, "TextScore should be > 0");
            }
        }

        private static async Task TestSearchFullTextWithTermsFilter()
        {
            var json = await DoSearch(new { FullText = new { Query = "learning" }, TermsFilter = new { Required = new List<string> { "Machine" } }, MaxResults = 20 }).ConfigureAwait(false);
            var docs = GetDocs(json);
            AssertTrue(docs.GetArrayLength() > 0, "Full-text with terms filter should return results");
            foreach (JsonElement doc in docs.EnumerateArray())
            {
                AssertTrue(doc.GetProperty("TextScore").GetDouble() > 0, "TextScore should be > 0");
            }
        }

        private static async Task TestSearchFullTextWithDateRange()
        {
            var json = await DoSearch(new { FullText = new { Query = "learning" }, CreatedAfter = DateTime.UtcNow.AddHours(-1).ToString("o"), CreatedBefore = DateTime.UtcNow.AddHours(1).ToString("o"), MaxResults = 20 }).ConfigureAwait(false);
            var docs = GetDocs(json);
            AssertTrue(docs.GetArrayLength() > 0, "Full-text with date range should return results");
            foreach (JsonElement doc in docs.EnumerateArray())
            {
                AssertTrue(doc.GetProperty("TextScore").GetDouble() > 0, "TextScore should be > 0");
            }
        }

        private static async Task TestSearchFullTextWithDocumentIds()
        {
            var json = await DoSearch(new { FullText = new { Query = "learning" }, DocumentIds = new List<string> { "grp-alpha" }, MaxResults = 20 }).ConfigureAwait(false);
            var docs = GetDocs(json);
            AssertTrue(docs.GetArrayLength() > 0, "Full-text with document ID filter should return results");
            foreach (JsonElement doc in docs.EnumerateArray())
            {
                AssertTrue(doc.GetProperty("TextScore").GetDouble() > 0, "TextScore should be > 0");
                AssertEqual("grp-alpha", doc.GetProperty("DocumentId").GetString(), "DocumentId should match filter");
            }
        }

        private static async Task TestSearchFullTextDistanceIsZero()
        {
            var json = await DoSearch(new { FullText = new { Query = "learning" }, MaxResults = 10 }).ConfigureAwait(false);
            var docs = GetDocs(json);
            AssertTrue(docs.GetArrayLength() > 0, "Should return results");
            foreach (JsonElement doc in docs.EnumerateArray())
            {
                double distance = doc.GetProperty("Distance").GetDouble();
                AssertTrue(distance == 0.0, "Full-text-only search should return Distance = 0.0, got " + distance);
            }
        }

        private static async Task TestSearchFullTextRankTypesDiffer()
        {
            var jsonRank = await DoSearch(new { FullText = new { Query = "Deep learning neural networks for image recognition", SearchType = "TsRank" }, MaxResults = 10 }).ConfigureAwait(false);
            var jsonRankCd = await DoSearch(new { FullText = new { Query = "Deep learning neural networks for image recognition", SearchType = "TsRankCd" }, MaxResults = 10 }).ConfigureAwait(false);

            var docsRank = GetDocs(jsonRank);
            var docsRankCd = GetDocs(jsonRankCd);
            AssertTrue(docsRank.GetArrayLength() > 0, "TsRank should return results");
            AssertTrue(docsRankCd.GetArrayLength() > 0, "TsRankCd should return results");

            double scoreRank = docsRank[0].GetProperty("TextScore").GetDouble();
            double scoreRankCd = docsRankCd[0].GetProperty("TextScore").GetDouble();
            AssertTrue(scoreRank != scoreRankCd, "TsRank (" + scoreRank + ") and TsRankCd (" + scoreRankCd + ") should produce different scores for the same query");
        }

        private static async Task TestSearchHybridWeightZero()
        {
            // TextWeight = 0.0 means pure vector scoring; hybrid score should equal vector score
            var jsonHybrid = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = _SearchEmb }, FullText = new { Query = "learning", TextWeight = 0.0 }, MaxResults = 10 }).ConfigureAwait(false);
            var jsonVector = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = _SearchEmb }, MaxResults = 10 }).ConfigureAwait(false);

            var docsHybrid = GetDocs(jsonHybrid);
            var docsVector = GetDocs(jsonVector);
            AssertTrue(docsHybrid.GetArrayLength() > 0, "Hybrid weight=0 should return results");
            AssertTrue(docsVector.GetArrayLength() > 0, "Vector-only should return results");

            // The top result ordering should match since text weight is zero
            string hybridTopKey = docsHybrid[0].GetProperty("DocumentKey").GetString();
            string vectorTopKey = docsVector[0].GetProperty("DocumentKey").GetString();
            AssertEqual(vectorTopKey, hybridTopKey, "TextWeight=0.0 should produce same top result as vector-only");

            double hybridScore = docsHybrid[0].GetProperty("Score").GetDouble();
            double vectorScore = docsVector[0].GetProperty("Score").GetDouble();
            AssertTrue(Math.Abs(hybridScore - vectorScore) < 0.001, "TextWeight=0.0 hybrid score (" + hybridScore + ") should match vector score (" + vectorScore + ")");
        }

        private static async Task TestSearchHybridWeightOne()
        {
            // TextWeight = 1.0 means pure full-text scoring; hybrid score should equal text score
            var jsonHybrid = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = _SearchEmb }, FullText = new { Query = "learning", TextWeight = 1.0 }, MaxResults = 10 }).ConfigureAwait(false);
            var jsonFts = await DoSearch(new { FullText = new { Query = "learning" }, MaxResults = 10 }).ConfigureAwait(false);

            var docsHybrid = GetDocs(jsonHybrid);
            var docsFts = GetDocs(jsonFts);
            AssertTrue(docsHybrid.GetArrayLength() > 0, "Hybrid weight=1 should return results");
            AssertTrue(docsFts.GetArrayLength() > 0, "Full-text-only should return results");

            // The top result ordering should match since vector weight is zero
            string hybridTopKey = docsHybrid[0].GetProperty("DocumentKey").GetString();
            string ftsTopKey = docsFts[0].GetProperty("DocumentKey").GetString();
            AssertEqual(ftsTopKey, hybridTopKey, "TextWeight=1.0 should produce same top result as full-text-only");

            double hybridScore = docsHybrid[0].GetProperty("Score").GetDouble();
            double ftsTextScore = docsFts[0].GetProperty("TextScore").GetDouble();
            AssertTrue(Math.Abs(hybridScore - ftsTextScore) < 0.001, "TextWeight=1.0 hybrid score (" + hybridScore + ") should match FTS text score (" + ftsTextScore + ")");
        }

        private static async Task TestSearchHybridBlendedScoreFormula()
        {
            // Verify Score = (1 - TextWeight) * vectorScore + TextWeight * textScore
            double textWeight = 0.4;
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = _SearchEmb }, FullText = new { Query = "machine learning", TextWeight = textWeight }, MaxResults = 10 }).ConfigureAwait(false);
            var docs = GetDocs(json);
            AssertTrue(docs.GetArrayLength() > 0, "Hybrid search should return results");

            foreach (JsonElement doc in docs.EnumerateArray())
            {
                double score = doc.GetProperty("Score").GetDouble();
                double textScore = doc.GetProperty("TextScore").GetDouble();
                double distance = doc.GetProperty("Distance").GetDouble();

                // Vector score for cosine similarity = 1 - distance
                double vectorScore = 1.0 - distance;
                double expectedScore = (1.0 - textWeight) * vectorScore + textWeight * textScore;

                AssertTrue(Math.Abs(score - expectedScore) < 0.01,
                    "Blended score (" + score + ") should equal (1-" + textWeight + ")*" + vectorScore + " + " + textWeight + "*" + textScore + " = " + expectedScore);
            }
        }

        private static async Task TestSearchBackwardCompatVectorOnly()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = _SearchEmb }, MaxResults = 10 }).ConfigureAwait(false);
            var docs = GetDocs(json);
            AssertTrue(docs.GetArrayLength() > 0, "Vector-only search should still work");
            foreach (JsonElement doc in docs.EnumerateArray())
            {
                AssertTrue(doc.GetProperty("Score").GetDouble() > 0, "Score should be > 0");
            }
        }

        #endregion

        #region Test-22-Search-Result-Validation

        private static async Task TestSearchResultFields()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, MaxResults = 10 }).ConfigureAwait(false);
            AssertTrue(json.GetProperty("Success").GetBoolean(), "Success should be true");
            AssertTrue(json.TryGetProperty("MaxResults", out _), "Response should contain MaxResults");
            AssertTrue(json.TryGetProperty("EndOfResults", out _), "Response should contain EndOfResults");
            AssertTrue(json.GetProperty("TotalRecords").GetInt64() > 0, "TotalRecords should be > 0");
            AssertTrue(json.TryGetProperty("RecordsRemaining", out _), "Response should contain RecordsRemaining");
            AssertTrue(json.TryGetProperty("Documents", out _), "Response should contain Documents");
        }

        private static async Task TestSearchDocumentFields()
        {
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, MaxResults = 10 }).ConfigureAwait(false);
            var docsElem = GetDocs(json);
            AssertTrue(docsElem.GetArrayLength() > 0, "Should have at least one document");

            JsonElement doc = docsElem[0];
            AssertNotNullOrEmpty(doc.GetProperty("DocumentKey").GetString(), "DocumentKey");
            AssertNotNullOrEmpty(doc.GetProperty("DocumentId").GetString(), "DocumentId");
            AssertNotNullOrEmpty(doc.GetProperty("Content").GetString(), "Content");
            AssertNotNullOrEmpty(doc.GetProperty("ContentType").GetString(), "ContentType");
            AssertTrue(doc.GetProperty("ContentLength").GetInt32() > 0, "ContentLength should be > 0");
            AssertNotNullOrEmpty(doc.GetProperty("CreatedUtc").GetString(), "CreatedUtc");
            AssertTrue(doc.TryGetProperty("Score", out _), "Document should have Score");
            AssertTrue(doc.TryGetProperty("Labels", out _), "Document should have Labels");
            AssertTrue(doc.TryGetProperty("Tags", out _), "Document should have Tags");
        }

        #endregion

        #region Test-22b-Neighbor-Retrieval

        private static async Task TestNeighborDataSetup()
        {
            // Create a multi-chunk document (5 chunks at positions 0-4) and a single-chunk document
            List<object> docs = new List<object>
            {
                new { DocumentKey = "nbr-doc-0", DocumentId = "nbr-group-a", Position = 0, Content = "Chapter one introduction to the topic", ContentType = "Text", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } },
                new { DocumentKey = "nbr-doc-1", DocumentId = "nbr-group-a", Position = 1, Content = "Chapter two background and context", ContentType = "Text", Embeddings = new List<float> { 0.85f, 0.12f, 0.08f } },
                new { DocumentKey = "nbr-doc-2", DocumentId = "nbr-group-a", Position = 2, Content = "Chapter three core methodology explained", ContentType = "Text", Embeddings = new List<float> { 0.8f, 0.15f, 0.1f } },
                new { DocumentKey = "nbr-doc-3", DocumentId = "nbr-group-a", Position = 3, Content = "Chapter four results and analysis", ContentType = "Text", Embeddings = new List<float> { 0.75f, 0.18f, 0.12f } },
                new { DocumentKey = "nbr-doc-4", DocumentId = "nbr-group-a", Position = 4, Content = "Chapter five conclusion and future work", ContentType = "Text", Embeddings = new List<float> { 0.7f, 0.2f, 0.15f } },
                new { DocumentKey = "nbr-doc-5", DocumentId = "nbr-group-b", Position = 0, Content = "Standalone single chunk document", ContentType = "Text", Embeddings = new List<float> { 0.6f, 0.3f, 0.2f } }
            };

            using HttpResponseMessage batchResp = await PostAsync(_AdminClient, DocBasePath() + "/batch", docs).ConfigureAwait(false);
            AssertStatusCode(batchResp, HttpStatusCode.Created);

            // Add labels and tags to neighbor test docs
            foreach (string dk in new[] { "nbr-doc-0", "nbr-doc-1", "nbr-doc-2", "nbr-doc-3", "nbr-doc-4", "nbr-doc-5" })
            {
                using HttpResponseMessage labelResp = await PutAsync(_AdminClient, LabelBasePath(), new { DocumentKey = dk, Label = "neighbor-test" }).ConfigureAwait(false);
                AssertStatusCode(labelResp, HttpStatusCode.Created);

                using HttpResponseMessage tagResp = await PutAsync(_AdminClient, TagBasePath(), new { DocumentKey = dk, Key = "suite", Value = "neighbor" }).ConfigureAwait(false);
                AssertStatusCode(tagResp, HttpStatusCode.Created);
            }
        }

        private static async Task TestNeighborVectorSearch()
        {
            // Search for nbr-doc-0 (position 0) with IncludeNeighbors=2
            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, MaxResults = 1, IncludeNeighbors = 2, DocumentIds = new List<string> { "nbr-group-a" } }).ConfigureAwait(false);
            var docs = GetDocs(json);
            AssertTrue(docs.GetArrayLength() > 0, "Should have at least one result");
            JsonElement doc = docs[0];
            AssertTrue(doc.TryGetProperty("Neighbors", out JsonElement neighbors), "Document should have Neighbors");
            AssertTrue(neighbors.GetArrayLength() > 0, "Neighbors should not be empty");
            AssertTrue(neighbors.GetArrayLength() <= 4, "Neighbors should be <= 4 (2 before + 2 after)");

            // Verify neighbors are ordered by Position ascending
            int prevPos = -1;
            foreach (JsonElement nb in neighbors.EnumerateArray())
            {
                int pos = nb.GetProperty("Position").GetInt32();
                AssertTrue(pos > prevPos, "Neighbors should be ordered by Position ascending");
                AssertTrue(pos != doc.GetProperty("Position").GetInt32(), "Neighbor should not be the matched chunk itself");
                prevPos = pos;
            }
        }

        private static async Task TestNeighborHybridSearch()
        {
            var json = await DoSearch(new
            {
                Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } },
                FullText = new { Query = "chapter introduction", TextWeight = 0.5 },
                MaxResults = 1,
                IncludeNeighbors = 2,
                DocumentIds = new List<string> { "nbr-group-a" }
            }).ConfigureAwait(false);
            var docs = GetDocs(json);
            AssertTrue(docs.GetArrayLength() > 0, "Hybrid search should return results");
            JsonElement doc = docs[0];
            AssertTrue(doc.TryGetProperty("Neighbors", out JsonElement neighbors), "Document should have Neighbors");
            AssertTrue(neighbors.GetArrayLength() > 0, "Neighbors should not be empty");
        }

        private static async Task TestNeighborFullTextSearch()
        {
            var json = await DoSearch(new
            {
                FullText = new { Query = "chapter introduction topic" },
                MaxResults = 1,
                IncludeNeighbors = 2,
                DocumentIds = new List<string> { "nbr-group-a" }
            }).ConfigureAwait(false);
            var docs = GetDocs(json);
            AssertTrue(docs.GetArrayLength() > 0, "Full-text search should return results");
            JsonElement doc = docs[0];
            AssertTrue(doc.TryGetProperty("Neighbors", out JsonElement neighbors), "Document should have Neighbors");
            AssertTrue(neighbors.GetArrayLength() > 0, "Neighbors should not be empty");
        }

        private static async Task TestNeighborBoundaryChunk()
        {
            // Search specifically for position 0 chunk - should only have neighbors after
            var json = await DoSearch(new
            {
                Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } },
                MaxResults = 1,
                IncludeNeighbors = 2,
                DocumentIds = new List<string> { "nbr-group-a" }
            }).ConfigureAwait(false);
            var docs = GetDocs(json);
            AssertTrue(docs.GetArrayLength() > 0, "Should return results");
            JsonElement doc = docs[0];
            int docPos = doc.GetProperty("Position").GetInt32();
            if (docPos == 0)
            {
                JsonElement neighbors = doc.GetProperty("Neighbors");
                foreach (JsonElement nb in neighbors.EnumerateArray())
                {
                    AssertTrue(nb.GetProperty("Position").GetInt32() > 0, "Boundary chunk at position 0 should only have neighbors after it");
                }
            }
        }

        private static async Task TestNeighborSingleChunkDocument()
        {
            var json = await DoSearch(new
            {
                Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.6f, 0.3f, 0.2f } },
                MaxResults = 1,
                IncludeNeighbors = 2,
                DocumentIds = new List<string> { "nbr-group-b" }
            }).ConfigureAwait(false);
            var docs = GetDocs(json);
            AssertTrue(docs.GetArrayLength() > 0, "Should return the single-chunk document");
            JsonElement doc = docs[0];
            if (doc.TryGetProperty("Neighbors", out JsonElement neighbors))
            {
                AssertTrue(neighbors.GetArrayLength() == 0, "Single-chunk document should have empty Neighbors");
            }
        }

        private static async Task TestNeighborNullAndZero()
        {
            // IncludeNeighbors = 0 means no neighbors
            var json0 = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, MaxResults = 1, IncludeNeighbors = 0 }).ConfigureAwait(false);
            var docs0 = GetDocs(json0);
            AssertTrue(docs0.GetArrayLength() > 0, "Should return results");
            JsonElement doc0 = docs0[0];
            if (doc0.TryGetProperty("Neighbors", out JsonElement nb0))
            {
                AssertTrue(nb0.ValueKind == JsonValueKind.Null, "Neighbors should be null when IncludeNeighbors is 0");
            }

            // No IncludeNeighbors set means no neighbors
            var jsonNull = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, MaxResults = 1 }).ConfigureAwait(false);
            var docsNull = GetDocs(jsonNull);
            AssertTrue(docsNull.GetArrayLength() > 0, "Should return results");
            JsonElement docNull = docsNull[0];
            if (docNull.TryGetProperty("Neighbors", out JsonElement nbNull))
            {
                AssertTrue(nbNull.ValueKind == JsonValueKind.Null, "Neighbors should be null when IncludeNeighbors is not set");
            }
        }

        private static async Task TestNeighborOverlapping()
        {
            // Search for two close chunks in the same document
            var json = await DoSearch(new
            {
                Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } },
                MaxResults = 5,
                IncludeNeighbors = 2,
                DocumentIds = new List<string> { "nbr-group-a" }
            }).ConfigureAwait(false);
            var docs = GetDocs(json);
            AssertTrue(docs.GetArrayLength() > 1, "Should return multiple results from same document");

            foreach (JsonElement doc in docs.EnumerateArray())
            {
                if (doc.TryGetProperty("Neighbors", out JsonElement neighbors) && neighbors.ValueKind == JsonValueKind.Array)
                {
                    int docPos = doc.GetProperty("Position").GetInt32();
                    foreach (JsonElement nb in neighbors.EnumerateArray())
                    {
                        AssertTrue(nb.GetProperty("Position").GetInt32() != docPos, "Neighbor should not be the matched chunk itself");
                    }
                }
            }
        }

        private static async Task TestNeighborLabelsAndTags()
        {
            var json = await DoSearch(new
            {
                Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } },
                MaxResults = 1,
                IncludeNeighbors = 2,
                DocumentIds = new List<string> { "nbr-group-a" }
            }).ConfigureAwait(false);
            var docs = GetDocs(json);
            AssertTrue(docs.GetArrayLength() > 0, "Should return results");
            JsonElement doc = docs[0];
            JsonElement neighbors = doc.GetProperty("Neighbors");
            AssertTrue(neighbors.GetArrayLength() > 0, "Should have neighbors");

            foreach (JsonElement nb in neighbors.EnumerateArray())
            {
                AssertTrue(nb.TryGetProperty("Labels", out JsonElement labels), "Neighbor should have Labels");
                AssertTrue(labels.GetArrayLength() > 0, "Neighbor should have at least one label");
                AssertTrue(nb.TryGetProperty("Tags", out JsonElement tags), "Neighbor should have Tags");
            }
        }

        #endregion

        #region Test-23-Document-Enumeration

        private static async Task TestEnumDocumentsBasic()
        {
            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending" }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() >= 10, "Basic enumeration should return >= 10 results");
        }

        private static async Task TestEnumDocumentsCreatedAscending()
        {
            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedAscending" }).ConfigureAwait(false);
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
            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending" }).ConfigureAwait(false);
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
                var json = await DoEnumDocs(body).ConfigureAwait(false);
                eor = json.GetProperty("EndOfResults").GetBoolean();
                AssertTrue(json.GetProperty("TotalRecords").GetInt64() >= 10, "TotalRecords should be >= 10");
                totalFetched += GetObjects(json).GetArrayLength();
                if (!eor) { ct = json.GetProperty("ContinuationToken").GetString(); AssertNotNullOrEmpty(ct, "ContinuationToken"); }
            }
            AssertTrue(totalFetched >= 10, "Should page through all enumeration docs");
        }

        private static async Task TestEnumDocumentsMaxResults()
        {
            var json = await DoEnumDocs(new { MaxResults = 2, Ordering = "CreatedDescending" }).ConfigureAwait(false);
            AssertEqual(2, GetObjects(json).GetArrayLength(), "MaxResults should limit to 2");
        }

        private static async Task TestEnumDocumentsCreatedAfter()
        {
            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", CreatedAfter = DateTime.UtcNow.AddHours(-1).ToString("o") }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "CreatedAfter filter should return results");
        }

        private static async Task TestEnumDocumentsCreatedBefore()
        {
            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", CreatedBefore = DateTime.UtcNow.AddHours(1).ToString("o") }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "CreatedBefore filter should return results");
        }

        private static async Task TestEnumDocumentsDateRangeNone()
        {
            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", CreatedBefore = DateTime.UtcNow.AddHours(-1).ToString("o") }).ConfigureAwait(false);
            AssertEqual(0, GetObjects(json).GetArrayLength(), "CreatedBefore in past should return 0 results");
        }

        private static async Task TestEnumDocumentsDocumentIds()
        {
            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", DocumentIds = new List<string> { "grp-alpha", "grp-beta" } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "DocumentIds filter should return results");
        }

        private static async Task TestEnumDocumentsDocumentIdsNoMatch()
        {
            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", DocumentIds = new List<string> { "nonexistent-docid-xyz" } }).ConfigureAwait(false);
            AssertEqual(0, GetObjects(json).GetArrayLength(), "DocumentIds no match should return 0 results");
        }

        private static async Task TestEnumDocumentsLabelRequired()
        {
            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", LabelFilter = new { Required = new List<string> { "science" } } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Label required filter should return results");
        }

        private static async Task TestEnumDocumentsLabelExcluded()
        {
            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", LabelFilter = new { Excluded = new List<string> { "lifestyle" } } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Label excluded filter should return results");
        }

        private static async Task TestEnumDocumentsLabelNoMatch()
        {
            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", LabelFilter = new { Required = new List<string> { "nonexistent-label-xyz" } } }).ConfigureAwait(false);
            AssertEqual(0, GetObjects(json).GetArrayLength(), "Label no match should return 0 results");
        }

        private static async Task TestEnumDocumentsTagEquals()
        {
            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "Equals", Value = "ai" } } } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Tag Equals filter should return results");
        }

        private static async Task TestEnumDocumentsTagNotEquals()
        {
            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "NotEquals", Value = "ai" } } } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Tag NotEquals filter should return results");
        }

        private static async Task TestEnumDocumentsTagContains()
        {
            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "Contains", Value = "heal" } } } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Tag Contains filter should return results");
        }

        private static async Task TestEnumDocumentsTagStartsWith()
        {
            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "StartsWith", Value = "fin" } } } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Tag StartsWith filter should return results");
        }

        private static async Task TestEnumDocumentsTagEndsWith()
        {
            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "EndsWith", Value = "ics" } } } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Tag EndsWith filter should return results");
        }

        private static async Task TestEnumDocumentsTagGreaterThan()
        {
            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "year", Condition = "GreaterThan", Value = "2023" } } } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Tag GreaterThan filter should return results");
        }

        private static async Task TestEnumDocumentsTagLessThan()
        {
            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "year", Condition = "LessThan", Value = "2023" } } } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Tag LessThan filter should return results");
        }

        private static async Task TestEnumDocumentsTagIsNotNull()
        {
            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "priority", Condition = "IsNotNull" } } } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() >= 10, "Tag IsNotNull filter should return >= 10 results");
        }

        private static async Task TestEnumDocumentsTagIsNull()
        {
            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "nonexistent-tag-xyz", Condition = "IsNull" } } } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() >= 10, "Tag IsNull filter should return >= 10 results");
        }

        private static async Task TestEnumDocumentsTagContainsNot()
        {
            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "ContainsNot", Value = "ai" } } } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Tag ContainsNot filter should return results");
        }

        private static async Task TestEnumDocumentsTermsRequired()
        {
            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", TermsFilter = new { Required = new List<string> { "machine learning" } } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Terms required filter should return results");
        }

        private static async Task TestEnumDocumentsTermsExcluded()
        {
            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", TermsFilter = new { Excluded = new List<string> { "quantum" } } }).ConfigureAwait(false);
            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Terms excluded filter should return results");
        }

        private static async Task TestEnumDocumentsCombinedLabelAndTag()
        {
            var json = await DoEnumDocs(new
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
            var json = await DoEnumDocs(new
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
            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending" }).ConfigureAwait(false);
            AssertTrue(json.GetProperty("Success").GetBoolean(), "Success should be true");
            AssertTrue(json.TryGetProperty("MaxResults", out _), "Response should contain MaxResults");
            AssertTrue(json.TryGetProperty("EndOfResults", out _), "Response should contain EndOfResults");
            AssertTrue(json.GetProperty("TotalRecords").GetInt64() > 0, "TotalRecords should be > 0");
            AssertTrue(json.TryGetProperty("RecordsRemaining", out _), "Response should contain RecordsRemaining");
            AssertTrue(json.TryGetProperty("Objects", out _), "Response should contain Objects");
        }

        private static async Task TestEnumDocumentsObjectFields()
        {
            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending" }).ConfigureAwait(false);
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
                object body = new { Name = "PaginationTestTenant_" + i };
                using HttpResponseMessage createResp = await PutAsync(_AdminClient, "/v1.0/tenants", body).ConfigureAwait(false);
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

                using HttpResponseMessage enumResp = await PostAsync(_AdminClient, "/v1.0/tenants/enumerate", enumBody).ConfigureAwait(false);
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

        #region Test-25-Authorization

        private static async Task TestAuthorizationNonAdmin()
        {
            AssertNotNull(_UserClient, "UserClient should be initialized");

            object body = new { Name = "UnauthorizedTenantAttempt" };
            using HttpResponseMessage response = await PutAsync(_UserClient, "/v1.0/tenants", body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.Forbidden);
        }

        #endregion

        #region Test-26-Cleanup

        private static async Task TestCleanupSearchLabels()
        {
            if (string.IsNullOrEmpty(_TestCollectionId)) return;

            foreach (string labelId in _SearchTestLabelIds)
            {
                string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/labels/" + labelId;
                using HttpResponseMessage response = await DeleteAsync(_AdminClient, path).ConfigureAwait(false);
                AssertStatusCode(response, HttpStatusCode.NoContent);
            }
        }

        private static async Task TestCleanupSearchTags()
        {
            if (string.IsNullOrEmpty(_TestCollectionId)) return;

            foreach (string tagId in _SearchTestTagIds)
            {
                string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/tags/" + tagId;
                using HttpResponseMessage response = await DeleteAsync(_AdminClient, path).ConfigureAwait(false);
                AssertStatusCode(response, HttpStatusCode.NoContent);
            }
        }

        private static async Task TestCleanupSearchDocuments()
        {
            if (string.IsNullOrEmpty(_TestCollectionId)) return;

            foreach (string docKey in _SearchTestDocKeys)
            {
                string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/documents/" + docKey;
                using HttpResponseMessage response = await DeleteAsync(_AdminClient, path).ConfigureAwait(false);
                AssertStatusCode(response, HttpStatusCode.NoContent);
            }
        }

        private static async Task TestCleanupLabels()
        {
            if (string.IsNullOrEmpty(_TestLabelId) || string.IsNullOrEmpty(_TestCollectionId)) return;

            string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/labels/" + _TestLabelId;
            using HttpResponseMessage response = await DeleteAsync(_AdminClient, path).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.NoContent);
        }

        private static async Task TestCleanupTags()
        {
            if (string.IsNullOrEmpty(_TestTagId) || string.IsNullOrEmpty(_TestCollectionId)) return;

            string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/tags/" + _TestTagId;
            using HttpResponseMessage response = await DeleteAsync(_AdminClient, path).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.NoContent);
        }

        private static async Task TestCleanupDocuments()
        {
            if (string.IsNullOrEmpty(_TestCollectionId)) return;

            // Delete the single test document
            if (!string.IsNullOrEmpty(_TestDocumentKey))
            {
                string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/documents/" + _TestDocumentKey;
                using HttpResponseMessage response = await DeleteAsync(_AdminClient, path).ConfigureAwait(false);
                AssertStatusCode(response, HttpStatusCode.NoContent);
            }

            // Delete batch documents
            foreach (string batchKey in _TestBatchDocumentKeys)
            {
                string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId + "/documents/" + batchKey;
                using HttpResponseMessage response = await DeleteAsync(_AdminClient, path).ConfigureAwait(false);
                AssertStatusCode(response, HttpStatusCode.NoContent);
            }
        }

        private static async Task TestCleanupCollection()
        {
            if (string.IsNullOrEmpty(_TestCollectionId)) return;

            string path = "/v1.0/tenants/" + _TestTenantId + "/collections/" + _TestCollectionId;
            using HttpResponseMessage response = await DeleteAsync(_AdminClient, path).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.NoContent);
        }

        private static async Task TestCleanupCredential()
        {
            if (string.IsNullOrEmpty(_TestCredentialId)) return;

            string path = "/v1.0/tenants/" + _TestTenantId + "/credentials/" + _TestCredentialId;
            using HttpResponseMessage response = await DeleteAsync(_AdminClient, path).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.NoContent);
        }

        private static async Task TestCleanupUser()
        {
            if (string.IsNullOrEmpty(_TestUserId)) return;

            string path = "/v1.0/tenants/" + _TestTenantId + "/users/" + _TestUserId;
            using HttpResponseMessage response = await DeleteAsync(_AdminClient, path).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.NoContent);
        }

        private static async Task TestCleanupTenant()
        {
            if (string.IsNullOrEmpty(_TestTenantId)) return;

            string path = "/v1.0/tenants/" + _TestTenantId + "?force";
            using HttpResponseMessage response = await DeleteAsync(_AdminClient, path).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.NoContent);
        }

        private static async Task TestCleanupPaginationTenants()
        {
            foreach (string tenantId in _PaginationTenantIds)
            {
                string path = "/v1.0/tenants/" + tenantId + "?force";
                using HttpResponseMessage response = await DeleteAsync(_AdminClient, path).ConfigureAwait(false);
                AssertStatusCode(response, HttpStatusCode.NoContent);
            }
        }

        #endregion
    }
}
