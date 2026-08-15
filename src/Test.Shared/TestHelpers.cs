namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    public static class TestHelpers
    {
        #region Shared-State

        // Endpoint and API key default to the local server, but can be overridden
        // via the RECALLDB_ENDPOINT / RECALLDB_APIKEY environment variables. This
        // allows the xUnit and NUnit adapters (which have no command-line arguments)
        // to target a non-default endpoint, mirroring the Test.Automated CLI flags.
        public static string Endpoint = ResolveEndpoint();
        public static string ApiKey = ResolveApiKey();
        public static HttpClient AdminClient = null;
        public static HttpClient UserClient = null;

        private static string ResolveEndpoint()
        {
            string env = Environment.GetEnvironmentVariable("RECALLDB_ENDPOINT");
            if (!string.IsNullOrWhiteSpace(env)) return env.TrimEnd('/');
            return "http://127.0.0.1:8600";
        }

        private static string ResolveApiKey()
        {
            string env = Environment.GetEnvironmentVariable("RECALLDB_APIKEY");
            if (!string.IsNullOrWhiteSpace(env)) return env;
            return "recalldbadmin";
        }

        public static JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        // Test data IDs to track for cleanup
        public static string TestTenantId = null;
        public static string TestUserId = null;
        public static string TestCredentialId = null;
        public static string TestUserBearerToken = null;
        public static string TestCollectionId = null;
        public static string TestDocumentKey = null;
        public static List<string> TestBatchDocumentKeys = new List<string>();
        public static string TestLabelId = null;
        public static string TestTagId = null;
        public static List<string> PaginationTenantIds = new List<string>();

        // Search/enumeration test dataset
        public static List<string> SearchTestDocKeys = new List<string>();
        public static List<string> SearchTestLabelIds = new List<string>();
        public static List<string> SearchTestTagIds = new List<string>();

        public static readonly List<float> SearchEmb = new List<float> { 0.9f, 0.1f, 0.05f };

        #endregion

        #region HTTP-Helpers

        public static HttpClient CreateHttpClient(string bearerToken)
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(Endpoint);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        public static async Task<HttpResponseMessage> GetAsync(HttpClient client, string path)
        {
            HttpResponseMessage response = await client.GetAsync(path).ConfigureAwait(false);
            return response;
        }

        public static async Task<HttpResponseMessage> PutAsync(HttpClient client, string path, object body)
        {
            string json = JsonSerializer.Serialize(body, JsonOptions);
            using StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PutAsync(path, content).ConfigureAwait(false);
            return response;
        }

        public static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body)
        {
            string json = JsonSerializer.Serialize(body, JsonOptions);
            using StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync(path, content).ConfigureAwait(false);
            return response;
        }

        public static async Task<HttpResponseMessage> DeleteAsync(HttpClient client, string path)
        {
            HttpResponseMessage response = await client.DeleteAsync(path).ConfigureAwait(false);
            return response;
        }

        public static async Task<HttpResponseMessage> HeadAsync(HttpClient client, string path)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, path);
            HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
            return response;
        }

        public static async Task<T> ReadResponse<T>(HttpResponseMessage response)
        {
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (string.IsNullOrEmpty(body)) return default;
            T result = JsonSerializer.Deserialize<T>(body, JsonOptions);
            return result;
        }

        #endregion

        #region Assertions

        public static void AssertStatusCode(HttpResponseMessage response, HttpStatusCode expected)
        {
            if (response.StatusCode != expected)
            {
                throw new Exception(
                    "Expected status " + (int)expected + " " + expected +
                    " but got " + (int)response.StatusCode + " " + response.StatusCode);
            }
        }

        public static void AssertTrue(bool condition, string message)
        {
            if (!condition) throw new Exception("Assertion failed: " + message);
        }

        public static void AssertNotNull(object value, string name)
        {
            if (value == null) throw new Exception("Expected non-null value for " + name);
        }

        public static void AssertNotNullOrEmpty(string value, string name)
        {
            if (string.IsNullOrEmpty(value)) throw new Exception("Expected non-null/non-empty value for " + name);
        }

        public static void AssertEqual(string expected, string actual, string name)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                throw new Exception(
                    "Expected '" + expected + "' but got '" + actual + "' for " + name);
            }
        }

        public static void AssertEqual(int expected, int actual, string name)
        {
            if (expected != actual)
            {
                throw new Exception(
                    "Expected " + expected + " but got " + actual + " for " + name);
            }
        }

        public static void AssertEqual(long expected, long actual, string name)
        {
            if (expected != actual)
            {
                throw new Exception(
                    "Expected " + expected + " but got " + actual + " for " + name);
            }
        }

        public static void AssertGreaterThanOrEqual(double value, double threshold, string name)
        {
            if (value < threshold)
                throw new Exception("Expected " + name + " (" + value + ") >= " + threshold);
        }

        public static void AssertLessThanOrEqual(double value, double threshold, string name)
        {
            if (value > threshold)
                throw new Exception("Expected " + name + " (" + value + ") <= " + threshold);
        }

        #endregion

        #region Path-Helpers

        public static string SearchPath()
        {
            return "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/search";
        }

        public static string EnumDocPath()
        {
            return "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/documents/enumerate";
        }

        public static string DocBasePath()
        {
            return "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/documents";
        }

        public static string LabelBasePath()
        {
            return "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/labels";
        }

        public static string TagBasePath()
        {
            return "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/tags";
        }

        public static async Task<JsonElement> DoSearch(object body)
        {
            using HttpResponseMessage response = await PostAsync(AdminClient, SearchPath(), body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);
            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.GetProperty("Success").GetBoolean(), "Search should succeed");
            return json;
        }

        public static async Task<JsonElement> DoEnumDocs(object body)
        {
            using HttpResponseMessage response = await PostAsync(AdminClient, EnumDocPath(), body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);
            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.GetProperty("Success").GetBoolean(), "Enumeration should succeed");
            return json;
        }

        public static JsonElement GetDocs(JsonElement json)
        {
            return json.GetProperty("Documents");
        }

        public static JsonElement GetObjects(JsonElement json)
        {
            return json.GetProperty("Objects");
        }

        #endregion
    }
}
