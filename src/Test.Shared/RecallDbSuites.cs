namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    using static Test.Shared.TestHelpers;

    public static class RecallDbSuites
    {
        public static IReadOnlyList<TestSuiteDescriptor> All { get; } = BuildSuites();

        private static IReadOnlyList<TestSuiteDescriptor> BuildSuites()
        {
            return new List<TestSuiteDescriptor>
            {
                new TestSuiteDescriptor(
                    suiteId: "RecallDb",
                    displayName: "RecallDB Integration Tests",
                    beforeSuiteAsync: ct =>
                    {
                        AdminClient = CreateHttpClient(ApiKey);
                        return ValueTask.CompletedTask;
                    },
                    afterSuiteAsync: ct =>
                    {
                        if (AdminClient != null) AdminClient.Dispose();
                        if (UserClient != null) UserClient.Dispose();
                        return ValueTask.CompletedTask;
                    },
                    cases: new List<TestCaseDescriptor>
                    {
                        // 1. Connectivity
                        Case("ConnectivityGet", "Connectivity: GET /", async ct =>
                        {
                            using HttpResponseMessage response = await GetAsync(AdminClient, "/").ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.OK);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.TryGetProperty("Name", out JsonElement nameElem), "Response should contain Name");
                            AssertEqual("RecallDB", nameElem.GetString(), "Name");
                        }),

                        // 2. Connectivity HEAD
                        Case("ConnectivityHead", "Connectivity: HEAD /", async ct =>
                        {
                            using HttpResponseMessage response = await HeadAsync(AdminClient, "/").ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.OK);
                        }),

                        // 3. Authentication Bearer
                        Case("AuthenticateBearer", "Authentication: POST /v1.0/authenticate with bearer token", async ct =>
                        {
                            object body = new { BearerToken = "default" };
                            using HttpResponseMessage response = await PostAsync(AdminClient, "/v1.0/authenticate", body).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.OK);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.TryGetProperty("Success", out JsonElement successElem), "Response should contain Success");
                            AssertTrue(successElem.GetBoolean(), "Authentication should succeed");
                        }),

                        // 4. Authentication Email+Password
                        Case("AuthenticateEmailPassword", "Authentication: POST /v1.0/authenticate with email+password", async ct =>
                        {
                            object body = new { TenantId = "default", Email = "admin@recall", Password = "password" };
                            using HttpResponseMessage response = await PostAsync(AdminClient, "/v1.0/authenticate", body).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.OK);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.TryGetProperty("Success", out JsonElement successElem), "Response should contain Success");
                            AssertTrue(successElem.GetBoolean(), "Authentication should succeed");
                        }),

                        // 5. Tenant Create
                        Case("TenantCreate", "Tenant: PUT create", async ct =>
                        {
                            object body = new { Name = "IntegrationTestTenant" };
                            using HttpResponseMessage response = await PutAsync(AdminClient, "/v1.0/tenants", body).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.Created);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.TryGetProperty("Id", out JsonElement idElem), "Response should contain Id");
                            TestTenantId = idElem.GetString();
                            AssertNotNullOrEmpty(TestTenantId, "TenantId");

                            AssertTrue(json.TryGetProperty("Name", out JsonElement nameElem), "Response should contain Name");
                            AssertEqual("IntegrationTestTenant", nameElem.GetString(), "Name");
                        }),

                        // 6. Tenant Read
                        Case("TenantRead", "Tenant: GET read", async ct =>
                        {
                            using HttpResponseMessage response = await GetAsync(AdminClient, "/v1.0/tenants/" + TestTenantId).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.OK);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.TryGetProperty("Id", out JsonElement idElem), "Response should contain Id");
                            AssertEqual(TestTenantId, idElem.GetString(), "Id");
                        }),

                        // 7. Tenant Update
                        Case("TenantUpdate", "Tenant: PUT update", async ct =>
                        {
                            object body = new { Name = "IntegrationTestTenantUpdated" };
                            using HttpResponseMessage response = await PutAsync(AdminClient, "/v1.0/tenants/" + TestTenantId, body).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.OK);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.TryGetProperty("Name", out JsonElement nameElem), "Response should contain Name");
                            AssertEqual("IntegrationTestTenantUpdated", nameElem.GetString(), "Name");
                        }),

                        // 8. Tenant Enumerate
                        Case("TenantEnumerate", "Tenant: POST enumerate", async ct =>
                        {
                            object body = new { MaxResults = 100 };
                            using HttpResponseMessage response = await PostAsync(AdminClient, "/v1.0/tenants/enumerate", body).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.OK);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.TryGetProperty("Success", out JsonElement successElem), "Response should contain Success");
                            AssertTrue(successElem.GetBoolean(), "Enumeration should succeed");
                            AssertTrue(json.TryGetProperty("TotalRecords", out JsonElement totalElem), "Response should contain TotalRecords");
                            AssertTrue(totalElem.GetInt64() >= 1, "TotalRecords should be >= 1");
                        }),

                        // 9. Tenant Exists
                        Case("TenantExists", "Tenant: HEAD exists", async ct =>
                        {
                            using HttpResponseMessage response = await HeadAsync(AdminClient, "/v1.0/tenants/" + TestTenantId).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.OK);
                        }),

                        // 10. User Create
                        Case("UserCreate", "User: PUT create", async ct =>
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
                            using HttpResponseMessage response = await PutAsync(AdminClient, "/v1.0/tenants/" + TestTenantId + "/users", body).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.Created);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.TryGetProperty("Id", out JsonElement idElem), "Response should contain Id");
                            TestUserId = idElem.GetString();
                            AssertNotNullOrEmpty(TestUserId, "UserId");
                        }),

                        // 11. User Read
                        Case("UserRead", "User: GET read", async ct =>
                        {
                            using HttpResponseMessage response = await GetAsync(AdminClient, "/v1.0/tenants/" + TestTenantId + "/users/" + TestUserId).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.OK);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.TryGetProperty("Id", out JsonElement idElem), "Response should contain Id");
                            AssertEqual(TestUserId, idElem.GetString(), "Id");
                            AssertTrue(json.TryGetProperty("Email", out JsonElement emailElem), "Response should contain Email");
                            AssertEqual("testuser@integrationtest.local", emailElem.GetString(), "Email");
                        }),

                        // 12. User Update
                        Case("UserUpdate", "User: PUT update", async ct =>
                        {
                            object body = new
                            {
                                Email = "testuser@integrationtest.local",
                                FirstName = "TestUpdated",
                                LastName = "UserUpdated"
                            };
                            using HttpResponseMessage response = await PutAsync(AdminClient, "/v1.0/tenants/" + TestTenantId + "/users/" + TestUserId, body).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.OK);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.TryGetProperty("FirstName", out JsonElement fnElem), "Response should contain FirstName");
                            AssertEqual("TestUpdated", fnElem.GetString(), "FirstName");
                        }),

                        // 13. User Enumerate
                        Case("UserEnumerate", "User: POST enumerate", async ct =>
                        {
                            object body = new { MaxResults = 100 };
                            using HttpResponseMessage response = await PostAsync(AdminClient, "/v1.0/tenants/" + TestTenantId + "/users/enumerate", body).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.OK);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.TryGetProperty("TotalRecords", out JsonElement totalElem), "Response should contain TotalRecords");
                            AssertTrue(totalElem.GetInt64() >= 1, "TotalRecords should be >= 1");
                        }),

                        // 14. User Exists
                        Case("UserExists", "User: HEAD exists", async ct =>
                        {
                            using HttpResponseMessage response = await HeadAsync(AdminClient, "/v1.0/tenants/" + TestTenantId + "/users/" + TestUserId).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.OK);
                        }),

                        // 15. Credential Create
                        Case("CredentialCreate", "Credential: PUT create", async ct =>
                        {
                            object body = new
                            {
                                UserId = TestUserId,
                                Name = "IntegrationTestCredential",
                                Active = true
                            };
                            using HttpResponseMessage response = await PutAsync(AdminClient, "/v1.0/tenants/" + TestTenantId + "/credentials", body).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.Created);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.TryGetProperty("Id", out JsonElement idElem), "Response should contain Id");
                            TestCredentialId = idElem.GetString();
                            AssertNotNullOrEmpty(TestCredentialId, "CredentialId");

                            AssertTrue(json.TryGetProperty("BearerToken", out JsonElement tokenElem), "Response should contain BearerToken");
                            TestUserBearerToken = tokenElem.GetString();
                            AssertNotNullOrEmpty(TestUserBearerToken, "BearerToken");

                            // Create the user-level HttpClient for authorization tests later
                            UserClient = CreateHttpClient(TestUserBearerToken);
                        }),

                        // 16. Credential Read
                        Case("CredentialRead", "Credential: GET read", async ct =>
                        {
                            using HttpResponseMessage response = await GetAsync(AdminClient, "/v1.0/tenants/" + TestTenantId + "/credentials/" + TestCredentialId).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.OK);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.TryGetProperty("Id", out JsonElement idElem), "Response should contain Id");
                            AssertEqual(TestCredentialId, idElem.GetString(), "Id");
                        }),

                        // 17. Credential Enumerate
                        Case("CredentialEnumerate", "Credential: POST enumerate", async ct =>
                        {
                            object body = new { MaxResults = 100 };
                            using HttpResponseMessage response = await PostAsync(AdminClient, "/v1.0/tenants/" + TestTenantId + "/credentials/enumerate", body).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.OK);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.TryGetProperty("TotalRecords", out JsonElement totalElem), "Response should contain TotalRecords");
                            AssertTrue(totalElem.GetInt64() >= 1, "TotalRecords should be >= 1");
                        }),

                        // 18. Credential Exists
                        Case("CredentialExists", "Credential: HEAD exists", async ct =>
                        {
                            using HttpResponseMessage response = await HeadAsync(AdminClient, "/v1.0/tenants/" + TestTenantId + "/credentials/" + TestCredentialId).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.OK);
                        }),

                        // 19. Collection Create
                        Case("CollectionCreate", "Collection: PUT create", async ct =>
                        {
                            object body = new
                            {
                                Name = "IntegrationTestCollection",
                                Description = "Test collection for integration tests",
                                Dimensionality = 3
                            };
                            using HttpResponseMessage response = await PutAsync(AdminClient, "/v1.0/tenants/" + TestTenantId + "/collections", body).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.Created);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.TryGetProperty("Id", out JsonElement idElem), "Response should contain Id");
                            TestCollectionId = idElem.GetString();
                            AssertNotNullOrEmpty(TestCollectionId, "CollectionId");

                            AssertTrue(json.TryGetProperty("Dimensionality", out JsonElement dimElem), "Response should contain Dimensionality");
                            AssertEqual(3, dimElem.GetInt32(), "Dimensionality");
                        }),

                        // 20. Collection Read
                        Case("CollectionRead", "Collection: GET read", async ct =>
                        {
                            using HttpResponseMessage response = await GetAsync(AdminClient, "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.OK);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.TryGetProperty("Id", out JsonElement idElem), "Response should contain Id");
                            AssertEqual(TestCollectionId, idElem.GetString(), "Id");
                        }),

                        // 21. Collection Update
                        Case("CollectionUpdate", "Collection: PUT update", async ct =>
                        {
                            object body = new
                            {
                                Name = "IntegrationTestCollectionUpdated",
                                Description = "Updated description",
                                Dimensionality = 3
                            };
                            using HttpResponseMessage response = await PutAsync(AdminClient, "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId, body).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.OK);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.TryGetProperty("Name", out JsonElement nameElem), "Response should contain Name");
                            AssertEqual("IntegrationTestCollectionUpdated", nameElem.GetString(), "Name");
                        }),

                        // 22. Collection Enumerate
                        Case("CollectionEnumerate", "Collection: POST enumerate", async ct =>
                        {
                            object body = new { MaxResults = 100 };
                            using HttpResponseMessage response = await PostAsync(AdminClient, "/v1.0/tenants/" + TestTenantId + "/collections/enumerate", body).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.OK);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.TryGetProperty("TotalRecords", out JsonElement totalElem), "Response should contain TotalRecords");
                            AssertTrue(totalElem.GetInt64() >= 1, "TotalRecords should be >= 1");
                        }),

                        // 23. Collection Exists
                        Case("CollectionExists", "Collection: HEAD exists", async ct =>
                        {
                            using HttpResponseMessage response = await HeadAsync(AdminClient, "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.OK);
                        }),

                        // 24. Document Create
                        Case("DocumentCreate", "Document: PUT create", async ct =>
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

                            string path = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/documents";
                            using HttpResponseMessage response = await PutAsync(AdminClient, path, body).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.Created);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.TryGetProperty("DocumentKey", out JsonElement keyElem), "Response should contain DocumentKey");
                            TestDocumentKey = keyElem.GetString();
                            AssertNotNullOrEmpty(TestDocumentKey, "DocumentKey");
                        }),

                        // 25. Document Read
                        Case("DocumentRead", "Document: GET read", async ct =>
                        {
                            string path = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/documents/" + TestDocumentKey;
                            using HttpResponseMessage response = await GetAsync(AdminClient, path).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.OK);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.TryGetProperty("DocumentKey", out JsonElement keyElem), "Response should contain DocumentKey");
                            AssertEqual(TestDocumentKey, keyElem.GetString(), "DocumentKey");
                            AssertTrue(json.TryGetProperty("Content", out JsonElement contentElem), "Response should contain Content");
                            AssertEqual("This is a test document for integration testing.", contentElem.GetString(), "Content");
                        }),

                        // 26. Document Update
                        Case("DocumentUpdate", "Document: PUT update", async ct =>
                        {
                            object body = new
                            {
                                DocumentKey = TestDocumentKey,
                                DocumentId = "testdocid-001",
                                Content = "This is an updated test document.",
                                ContentType = "Text",
                                Position = 0,
                                Embeddings = new List<float> { 0.1f, 0.2f, 0.3f }
                            };

                            string path = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/documents/" + TestDocumentKey;
                            using HttpResponseMessage response = await PutAsync(AdminClient, path, body).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.OK);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.TryGetProperty("Content", out JsonElement contentElem), "Response should contain Content");
                            AssertEqual("This is an updated test document.", contentElem.GetString(), "Content");
                        }),

                        // 27. Document Batch Create
                        Case("DocumentBatchCreate", "Document: POST batch create", async ct =>
                        {
                            List<object> docs = new List<object>();

                            for (int i = 0; i < 3; i++)
                            {
                                string batchKey = "batchdoc-" + i + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                                TestBatchDocumentKeys.Add(batchKey);

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

                            string path = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/documents/batch";
                            using HttpResponseMessage response = await PostAsync(AdminClient, path, docs).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.Created);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.ValueKind == JsonValueKind.Array, "Response should be an array");
                            AssertEqual(3, json.GetArrayLength(), "Batch should create 3 documents");

                            // Verify all documents were created by reading each one
                            foreach (string batchKey in TestBatchDocumentKeys)
                            {
                                string readPath = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/documents/" + batchKey;
                                using HttpResponseMessage readResp = await GetAsync(AdminClient, readPath).ConfigureAwait(false);
                                AssertStatusCode(readResp, HttpStatusCode.OK);
                            }
                        }),

                        // 28. Document Batch Delete by Keys
                        Case("DocumentBatchDeleteByKeys", "Document: POST batch delete by keys", async ct =>
                        {
                            // Create some temporary documents for this test
                            List<string> tempKeys = new List<string>();
                            List<object> docs = new List<object>();

                            for (int i = 0; i < 3; i++)
                            {
                                string key = "batchdel-" + i + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                                tempKeys.Add(key);

                                float baseVal = (i + 1) * 0.1f;
                                docs.Add(new
                                {
                                    DocumentKey = key,
                                    DocumentId = "batchdel-docid-" + i,
                                    Content = "Batch delete test document " + i,
                                    ContentType = "Text",
                                    Position = 0,
                                    Embeddings = new List<float> { baseVal, baseVal + 0.1f, baseVal + 0.2f }
                                });
                            }

                            // Create the documents via batch endpoint
                            string createPath = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/documents/batch";
                            using HttpResponseMessage createResp = await PostAsync(AdminClient, createPath, docs).ConfigureAwait(false);
                            AssertStatusCode(createResp, HttpStatusCode.Created);

                            // Verify they exist
                            foreach (string key in tempKeys)
                            {
                                string readPath = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/documents/" + key;
                                using HttpResponseMessage readResp = await GetAsync(AdminClient, readPath).ConfigureAwait(false);
                                AssertStatusCode(readResp, HttpStatusCode.OK);
                            }

                            // Batch delete by keys
                            string deletePath = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/documents/batch/delete";
                            object deleteBody = new { DocumentKeys = tempKeys };
                            using HttpResponseMessage deleteResp = await PostAsync(AdminClient, deletePath, deleteBody).ConfigureAwait(false);
                            AssertStatusCode(deleteResp, HttpStatusCode.NoContent);

                            // Verify they are gone (expect 404)
                            foreach (string key in tempKeys)
                            {
                                string readPath = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/documents/" + key;
                                using HttpResponseMessage readResp = await GetAsync(AdminClient, readPath).ConfigureAwait(false);
                                AssertStatusCode(readResp, HttpStatusCode.NotFound);
                            }
                        }),

                        // 29. Document Delete by Filter
                        Case("DocumentDeleteByFilter", "Document: POST delete by filter", async ct =>
                        {
                            // Create documents with a unique DocumentId for filter-based deletion
                            string uniqueDocId = "filterdel-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                            List<string> tempKeys = new List<string>();
                            List<object> docs = new List<object>();

                            for (int i = 0; i < 3; i++)
                            {
                                string key = "filterdel-" + i + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                                tempKeys.Add(key);

                                float baseVal = (i + 1) * 0.1f;
                                docs.Add(new
                                {
                                    DocumentKey = key,
                                    DocumentId = uniqueDocId,
                                    Content = "Filter delete test document " + i,
                                    ContentType = "Text",
                                    Position = i,
                                    Embeddings = new List<float> { baseVal, baseVal + 0.1f, baseVal + 0.2f }
                                });
                            }

                            // Create the documents via batch endpoint
                            string createPath = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/documents/batch";
                            using HttpResponseMessage createResp = await PostAsync(AdminClient, createPath, docs).ConfigureAwait(false);
                            AssertStatusCode(createResp, HttpStatusCode.Created);

                            // Verify they exist
                            foreach (string key in tempKeys)
                            {
                                string readPath = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/documents/" + key;
                                using HttpResponseMessage readResp = await GetAsync(AdminClient, readPath).ConfigureAwait(false);
                                AssertStatusCode(readResp, HttpStatusCode.OK);
                            }

                            // Delete by filter using DocumentIds
                            string filterDeletePath = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/documents/delete/filter";
                            object filterBody = new
                            {
                                MaxResults = 100,
                                Ordering = "CreatedDescending",
                                DocumentIds = new List<string> { uniqueDocId }
                            };
                            using HttpResponseMessage filterResp = await PostAsync(AdminClient, filterDeletePath, filterBody).ConfigureAwait(false);
                            AssertStatusCode(filterResp, HttpStatusCode.OK);

                            // Verify response contains DocumentsDeleted count
                            JsonElement json = await ReadResponse<JsonElement>(filterResp).ConfigureAwait(false);
                            AssertTrue(json.TryGetProperty("DocumentsDeleted", out JsonElement deletedElem), "Response should contain DocumentsDeleted");
                            long deletedCount = deletedElem.GetInt64();
                            AssertTrue(deletedCount >= 3, "DocumentsDeleted should be >= 3, got " + deletedCount);

                            // Verify they are gone (expect 404)
                            foreach (string key in tempKeys)
                            {
                                string readPath = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/documents/" + key;
                                using HttpResponseMessage readResp = await GetAsync(AdminClient, readPath).ConfigureAwait(false);
                                AssertStatusCode(readResp, HttpStatusCode.NotFound);
                            }
                        }),

                        // 30. Label Create
                        Case("LabelCreate", "Label: PUT create", async ct =>
                        {
                            object body = new
                            {
                                DocumentKey = TestDocumentKey,
                                Label = "integration-test-label"
                            };

                            string path = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/labels";
                            using HttpResponseMessage response = await PutAsync(AdminClient, path, body).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.Created);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.TryGetProperty("Id", out JsonElement idElem), "Response should contain Id");
                            TestLabelId = idElem.GetString();
                            AssertNotNullOrEmpty(TestLabelId, "LabelId");

                            AssertTrue(json.TryGetProperty("Label", out JsonElement labelElem), "Response should contain Label");
                            AssertEqual("integration-test-label", labelElem.GetString(), "Label");
                        }),

                        // 31. Label List
                        Case("LabelList", "Label: GET list", async ct =>
                        {
                            string path = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/labels";
                            using HttpResponseMessage response = await GetAsync(AdminClient, path).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.OK);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.TryGetProperty("TotalRecords", out JsonElement totalElem), "Response should contain TotalRecords");
                            AssertTrue(totalElem.GetInt64() >= 1, "TotalRecords should be >= 1");
                        }),

                        // 32. Label Distinct
                        Case("LabelDistinct", "Label: GET distinct", async ct =>
                        {
                            string path = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/labels/distinct";
                            using HttpResponseMessage response = await GetAsync(AdminClient, path).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.OK);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.ValueKind == JsonValueKind.Array, "Response should be a JSON array");
                            AssertTrue(json.GetArrayLength() >= 1, "Distinct labels count should be >= 1");

                            bool found = false;
                            foreach (JsonElement elem in json.EnumerateArray())
                            {
                                if (elem.GetString() == "integration-test-label") { found = true; break; }
                            }
                            AssertTrue(found, "Distinct labels should contain 'integration-test-label'");
                        }),

                        // 33. Tag Create
                        Case("TagCreate", "Tag: PUT create", async ct =>
                        {
                            object body = new
                            {
                                DocumentKey = TestDocumentKey,
                                Key = "environment",
                                Value = "integration-test"
                            };

                            string path = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/tags";
                            using HttpResponseMessage response = await PutAsync(AdminClient, path, body).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.Created);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.TryGetProperty("Id", out JsonElement idElem), "Response should contain Id");
                            TestTagId = idElem.GetString();
                            AssertNotNullOrEmpty(TestTagId, "TagId");

                            AssertTrue(json.TryGetProperty("Key", out JsonElement keyElem), "Response should contain Key");
                            AssertEqual("environment", keyElem.GetString(), "Key");
                        }),

                        // 34. Tag List
                        Case("TagList", "Tag: GET list", async ct =>
                        {
                            string path = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/tags";
                            using HttpResponseMessage response = await GetAsync(AdminClient, path).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.OK);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.TryGetProperty("TotalRecords", out JsonElement totalElem), "Response should contain TotalRecords");
                            AssertTrue(totalElem.GetInt64() >= 1, "TotalRecords should be >= 1");
                        }),

                        // 35. Tag Distinct
                        Case("TagDistinct", "Tag: GET distinct keys", async ct =>
                        {
                            string path = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/tags/distinct";
                            using HttpResponseMessage response = await GetAsync(AdminClient, path).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.OK);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.ValueKind == JsonValueKind.Array, "Response should be a JSON array");
                            AssertTrue(json.GetArrayLength() >= 1, "Distinct tag keys count should be >= 1");

                            bool found = false;
                            foreach (JsonElement elem in json.EnumerateArray())
                            {
                                if (elem.GetString() == "environment") { found = true; break; }
                            }
                            AssertTrue(found, "Distinct tag keys should contain 'environment'");
                        }),

                        // ───────────────────────────── Negative / error-path tests ─────────────────────────────
                        // These exercise authentication, authorization, not-found, and validation failure paths.
                        // They run after the core happy-path CRUD (so the shared tenant/collection/document and the
                        // non-admin UserClient all exist) and before the search dataset is created.

                        // N1. Authenticate with an invalid bearer token -> 401, Success=false
                        Case("AuthenticateInvalidBearer", "Negative: authenticate with invalid bearer token", async ct =>
                        {
                            object body = new { BearerToken = "invalid-token-" + Guid.NewGuid().ToString("N") };
                            using HttpResponseMessage response = await PostAsync(AdminClient, "/v1.0/authenticate", body).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.Unauthorized);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.TryGetProperty("Success", out JsonElement successElem), "Response should contain Success");
                            AssertTrue(!successElem.GetBoolean(), "Authentication with an invalid bearer token should not succeed");
                        }),

                        // N2. Authenticate with a wrong password -> 401, Success=false
                        Case("AuthenticateInvalidPassword", "Negative: authenticate with wrong password", async ct =>
                        {
                            object body = new { TenantId = "default", Email = "admin@recall", Password = "definitely-wrong-password" };
                            using HttpResponseMessage response = await PostAsync(AdminClient, "/v1.0/authenticate", body).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.Unauthorized);

                            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
                            AssertTrue(json.TryGetProperty("Success", out JsonElement successElem), "Response should contain Success");
                            AssertTrue(!successElem.GetBoolean(), "Authentication with a wrong password should not succeed");
                        }),

                        // N3. Authenticate with neither bearer token nor email/password -> 400
                        Case("AuthenticateMissingCredentials", "Negative: authenticate with empty body", async ct =>
                        {
                            object body = new { };
                            using HttpResponseMessage response = await PostAsync(AdminClient, "/v1.0/authenticate", body).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.BadRequest);
                        }),

                        // N4. Protected endpoint with an invalid bearer token -> 401
                        Case("UnauthenticatedRequestRejected", "Negative: protected endpoint rejects invalid token", async ct =>
                        {
                            using HttpClient badClient = CreateHttpClient("invalid-bearer-" + Guid.NewGuid().ToString("N"));
                            object body = new { MaxResults = 1 };
                            using HttpResponseMessage response = await PostAsync(badClient, "/v1.0/tenants/enumerate", body).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.Unauthorized);
                        }),

                        // N5. Read a non-existent tenant -> 404
                        Case("TenantReadNotFound", "Negative: read non-existent tenant", async ct =>
                        {
                            using HttpResponseMessage response = await GetAsync(AdminClient, "/v1.0/tenants/nonexistent-" + Guid.NewGuid().ToString("N")).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.NotFound);
                        }),

                        // N6. Read a non-existent user -> 404
                        Case("UserReadNotFound", "Negative: read non-existent user", async ct =>
                        {
                            string path = "/v1.0/tenants/" + TestTenantId + "/users/nonexistent-" + Guid.NewGuid().ToString("N");
                            using HttpResponseMessage response = await GetAsync(AdminClient, path).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.NotFound);
                        }),

                        // N7. Read a non-existent collection -> 404
                        Case("CollectionReadNotFound", "Negative: read non-existent collection", async ct =>
                        {
                            string path = "/v1.0/tenants/" + TestTenantId + "/collections/nonexistent-" + Guid.NewGuid().ToString("N");
                            using HttpResponseMessage response = await GetAsync(AdminClient, path).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.NotFound);
                        }),

                        // N8. Read a non-existent document -> 404
                        Case("DocumentReadNotFound", "Negative: read non-existent document", async ct =>
                        {
                            string path = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/documents/nonexistent-" + Guid.NewGuid().ToString("N");
                            using HttpResponseMessage response = await GetAsync(AdminClient, path).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.NotFound);
                        }),

                        // N9. Create a document whose embeddings dimensionality mismatches the collection -> 400
                        Case("DocumentCreateWrongDimensionality", "Negative: document create with wrong embedding dimensionality", async ct =>
                        {
                            object body = new
                            {
                                DocumentKey = "baddim-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                                Content = "Wrong dimensionality document",
                                ContentType = "Text",
                                Position = 0,
                                // Collection dimensionality is 3; supply 4 values
                                Embeddings = new List<float> { 0.1f, 0.2f, 0.3f, 0.4f }
                            };
                            string path = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/documents";
                            using HttpResponseMessage response = await PutAsync(AdminClient, path, body).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.BadRequest);
                        }),

                        // N10. Batch create where one document has wrong dimensionality -> 400 (whole batch rejected)
                        Case("DocumentBatchWrongDimensionality", "Negative: batch create with wrong embedding dimensionality", async ct =>
                        {
                            List<object> docs = new List<object>
                            {
                                new { DocumentKey = "batchbad-a-" + Guid.NewGuid().ToString("N").Substring(0, 8), Content = "ok", ContentType = "Text", Embeddings = new List<float> { 0.1f, 0.2f, 0.3f } },
                                new { DocumentKey = "batchbad-b-" + Guid.NewGuid().ToString("N").Substring(0, 8), Content = "bad", ContentType = "Text", Embeddings = new List<float> { 0.1f, 0.2f } }
                            };
                            string path = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/documents/batch";
                            using HttpResponseMessage response = await PostAsync(AdminClient, path, docs).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.BadRequest);
                        }),

                        // N11. Create a document in a non-existent collection -> 404
                        Case("DocumentCreateCollectionNotFound", "Negative: document create in non-existent collection", async ct =>
                        {
                            object body = new
                            {
                                DocumentKey = "orphan-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                                Content = "Orphan document",
                                ContentType = "Text",
                                Position = 0,
                                Embeddings = new List<float> { 0.1f, 0.2f, 0.3f }
                            };
                            string path = "/v1.0/tenants/" + TestTenantId + "/collections/nonexistent-" + Guid.NewGuid().ToString("N") + "/documents";
                            using HttpResponseMessage response = await PutAsync(AdminClient, path, body).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.NotFound);
                        }),

                        // N12. Search a non-existent collection -> 404
                        Case("SearchCollectionNotFound", "Negative: search a non-existent collection", async ct =>
                        {
                            object body = new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.1f, 0.2f, 0.3f } }, MaxResults = 5 };
                            string path = "/v1.0/tenants/" + TestTenantId + "/collections/nonexistent-" + Guid.NewGuid().ToString("N") + "/search";
                            using HttpResponseMessage response = await PostAsync(AdminClient, path, body).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.NotFound);
                        }),

                        // N13. Non-admin credential token accessing another tenant -> 403
                        Case("CrossTenantAccessForbidden", "Negative: non-admin cannot read another tenant", async ct =>
                        {
                            AssertNotNull(UserClient, "UserClient should be initialized");
                            // UserClient is scoped to TestTenantId; reading the built-in "default" tenant must be forbidden.
                            using HttpResponseMessage response = await GetAsync(UserClient, "/v1.0/tenants/default").ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.Forbidden);
                        }),

                        // 36. Search Data Setup
                        Case("SearchDataSetup", "Search data: setup test documents, labels, and tags", async ct =>
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

                            using HttpResponseMessage batchResp = await PostAsync(AdminClient, DocBasePath() + "/batch", docs).ConfigureAwait(false);
                            AssertStatusCode(batchResp, HttpStatusCode.Created);
                            JsonElement batchJson = await ReadResponse<JsonElement>(batchResp).ConfigureAwait(false);
                            AssertTrue(batchJson.ValueKind == JsonValueKind.Array, "Batch response should be an array");
                            AssertEqual(10, batchJson.GetArrayLength(), "Batch should create 10 documents");

                            SearchTestDocKeys = new List<string> { "srch-doc-0", "srch-doc-1", "srch-doc-2", "srch-doc-3", "srch-doc-4", "srch-doc-5", "srch-doc-6", "srch-doc-7", "srch-doc-8", "srch-doc-9" };

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
                                    object labelBody = new { DocumentKey = SearchTestDocKeys[i], Label = label };
                                    using HttpResponseMessage labelResp = await PutAsync(AdminClient, LabelBasePath(), labelBody).ConfigureAwait(false);
                                    AssertStatusCode(labelResp, HttpStatusCode.Created);
                                    JsonElement labelJson = await ReadResponse<JsonElement>(labelResp).ConfigureAwait(false);
                                    SearchTestLabelIds.Add(labelJson.GetProperty("Id").GetString());
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
                                    object tagBody = new { DocumentKey = SearchTestDocKeys[i], Key = kv[0], Value = kv[1] };
                                    using HttpResponseMessage tagResp = await PutAsync(AdminClient, TagBasePath(), tagBody).ConfigureAwait(false);
                                    AssertStatusCode(tagResp, HttpStatusCode.Created);
                                    JsonElement tagJson = await ReadResponse<JsonElement>(tagResp).ConfigureAwait(false);
                                    SearchTestTagIds.Add(tagJson.GetProperty("Id").GetString());
                                }
                            }
                        }),

                        // 37. Search Cosine Similarity
                        Case("SearchCosineSimilarity", "Search: cosine similarity", async ct =>
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
                        }),

                        // 38. Search Cosine Distance
                        Case("SearchCosineDistance", "Search: cosine distance", async ct =>
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
                        }),

                        // 39. Search Euclidean Similarity
                        Case("SearchEuclideanSimilarity", "Search: euclidean similarity", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "EuclideanSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, MaxResults = 10 }).ConfigureAwait(false);
                            var docsElem = GetDocs(json);
                            AssertTrue(docsElem.GetArrayLength() > 0, "Euclidean similarity search should return results");

                            foreach (JsonElement doc in docsElem.EnumerateArray())
                            {
                                AssertTrue(doc.TryGetProperty("Score", out _), "Each document should have a Score property");
                            }
                        }),

                        // 40. Search Euclidean Distance
                        Case("SearchEuclideanDistance", "Search: euclidean distance", async ct =>
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
                        }),

                        // 41. Search Inner Product
                        Case("SearchInnerProduct", "Search: inner product", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "InnerProduct", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, MaxResults = 10 }).ConfigureAwait(false);
                            var docsElem = GetDocs(json);
                            AssertTrue(docsElem.GetArrayLength() > 0, "Inner product search should return results");

                            foreach (JsonElement doc in docsElem.EnumerateArray())
                            {
                                AssertTrue(doc.TryGetProperty("Score", out _), "Each document should have a Score property");
                            }
                        }),

                        // 42. Search Sort Score Descending
                        Case("SearchSortScoreDescending", "Search sort: score descending", async ct =>
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
                        }),

                        // 43. Search Sort Score Ascending
                        Case("SearchSortScoreAscending", "Search sort: score ascending", async ct =>
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
                        }),

                        // 44. Search Sort Distance Ascending
                        Case("SearchSortDistanceAscending", "Search sort: distance ascending", async ct =>
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
                        }),

                        // 45. Search Sort Distance Descending
                        Case("SearchSortDistanceDescending", "Search sort: distance descending", async ct =>
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
                        }),

                        // 46. Search Sort Created Ascending
                        Case("SearchSortCreatedAscending", "Search sort: created ascending", async ct =>
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
                        }),

                        // 47. Search Sort Created Descending
                        Case("SearchSortCreatedDescending", "Search sort: created descending", async ct =>
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
                        }),

                        // 48. Search Minimum Score
                        Case("SearchMinimumScore", "Search threshold: minimum score", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, MinimumScore = 0.9, MaxResults = 10 }).ConfigureAwait(false);
                            foreach (JsonElement doc in GetDocs(json).EnumerateArray())
                            {
                                AssertGreaterThanOrEqual(doc.GetProperty("Score").GetDouble(), 0.9, "Score");
                            }
                        }),

                        // 49. Search Maximum Score
                        Case("SearchMaximumScore", "Search threshold: maximum score", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, MaximumScore = 0.5, MaxResults = 10 }).ConfigureAwait(false);
                            foreach (JsonElement doc in GetDocs(json).EnumerateArray())
                            {
                                AssertLessThanOrEqual(doc.GetProperty("Score").GetDouble(), 0.5, "Score");
                            }
                        }),

                        // 50. Search Min Max Score
                        Case("SearchMinMaxScore", "Search threshold: min and max score", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, MinimumScore = 0.3, MaximumScore = 0.7, MaxResults = 10 }).ConfigureAwait(false);
                            foreach (JsonElement doc in GetDocs(json).EnumerateArray())
                            {
                                double score = doc.GetProperty("Score").GetDouble();
                                AssertGreaterThanOrEqual(score, 0.3, "Score");
                                AssertLessThanOrEqual(score, 0.7, "Score");
                            }
                        }),

                        // 51. Search Minimum Distance
                        Case("SearchMinimumDistance", "Search threshold: minimum distance", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "EuclideanDistance", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, SortOrder = "DistanceAscending", MinimumDistance = 0.5, MaxResults = 10 }).ConfigureAwait(false);
                            foreach (JsonElement doc in GetDocs(json).EnumerateArray())
                            {
                                AssertGreaterThanOrEqual(doc.GetProperty("Score").GetDouble(), 0.5, "Score");
                            }
                        }),

                        // 52. Search Maximum Distance
                        Case("SearchMaximumDistance", "Search threshold: maximum distance", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "EuclideanDistance", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, SortOrder = "DistanceAscending", MaximumDistance = 1.5, MaxResults = 10 }).ConfigureAwait(false);
                            foreach (JsonElement doc in GetDocs(json).EnumerateArray())
                            {
                                AssertLessThanOrEqual(doc.GetProperty("Score").GetDouble(), 1.5, "Score");
                            }
                        }),

                        // 53. Search Label Required
                        Case("SearchLabelRequired", "Search label: required", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, LabelFilter = new { Required = new List<string> { "science" } }, MaxResults = 20 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Label required filter should return results");
                        }),

                        // 54. Search Label Excluded
                        Case("SearchLabelExcluded", "Search label: excluded", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, LabelFilter = new { Excluded = new List<string> { "lifestyle" } }, MaxResults = 20 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Label excluded filter should return results");
                        }),

                        // 55. Search Label Required Multiple
                        Case("SearchLabelRequiredMultiple", "Search label: required multiple", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, LabelFilter = new { Required = new List<string> { "science", "tech" } }, MaxResults = 20 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Label required multiple filter should return results");
                        }),

                        // 56. Search Label Required And Excluded
                        Case("SearchLabelRequiredAndExcluded", "Search label: required and excluded", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, LabelFilter = new { Required = new List<string> { "science" }, Excluded = new List<string> { "tech" } }, MaxResults = 20 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Label required and excluded filter should return results");
                        }),

                        // 57. Search Label No Match
                        Case("SearchLabelNoMatch", "Search label: no match", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, LabelFilter = new { Required = new List<string> { "nonexistent-label-xyz" } }, MaxResults = 20 }).ConfigureAwait(false);
                            AssertEqual(0, GetDocs(json).GetArrayLength(), "Label no match should return 0 results");
                        }),

                        // 58. Search Tag Equals
                        Case("SearchTagEquals", "Search tag: equals", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "Equals", Value = "ai" } } }, MaxResults = 20 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Tag Equals filter should return results");
                        }),

                        // 59. Search Tag Not Equals
                        Case("SearchTagNotEquals", "Search tag: not equals", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "NotEquals", Value = "ai" } } }, MaxResults = 20 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Tag NotEquals filter should return results");
                        }),

                        // 60. Search Tag Contains
                        Case("SearchTagContains", "Search tag: contains", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "Contains", Value = "heal" } } }, MaxResults = 20 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Tag Contains filter should return results");
                        }),

                        // 61. Search Tag Contains Not
                        Case("SearchTagContainsNot", "Search tag: contains not", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "ContainsNot", Value = "ai" } } }, MaxResults = 20 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Tag ContainsNot filter should return results");
                        }),

                        // 62. Search Tag Starts With
                        Case("SearchTagStartsWith", "Search tag: starts with", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "StartsWith", Value = "fin" } } }, MaxResults = 20 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Tag StartsWith filter should return results");
                        }),

                        // 63. Search Tag Ends With
                        Case("SearchTagEndsWith", "Search tag: ends with", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "EndsWith", Value = "ics" } } }, MaxResults = 20 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Tag EndsWith filter should return results");
                        }),

                        // 64. Search Tag Greater Than
                        Case("SearchTagGreaterThan", "Search tag: greater than", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TagFilter = new { Required = new List<object> { new { Key = "year", Condition = "GreaterThan", Value = "2023" } } }, MaxResults = 20 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Tag GreaterThan filter should return results");
                        }),

                        // 65. Search Tag Less Than
                        Case("SearchTagLessThan", "Search tag: less than", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TagFilter = new { Required = new List<object> { new { Key = "year", Condition = "LessThan", Value = "2023" } } }, MaxResults = 20 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Tag LessThan filter should return results");
                        }),

                        // 66. Search Tag Is Not Null
                        Case("SearchTagIsNotNull", "Search tag: is not null", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TagFilter = new { Required = new List<object> { new { Key = "priority", Condition = "IsNotNull" } } }, MaxResults = 20 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() >= 10, "Tag IsNotNull filter should return >= 10 results");
                        }),

                        // 67. Search Tag Is Null
                        Case("SearchTagIsNull", "Search tag: is null", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TagFilter = new { Required = new List<object> { new { Key = "nonexistent-tag-xyz", Condition = "IsNull" } } }, MaxResults = 20 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() >= 10, "Tag IsNull filter should return >= 10 results");
                        }),

                        // 68. Search Tag Excluded
                        Case("SearchTagExcluded", "Search tag: excluded", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TagFilter = new { Excluded = new List<object> { new { Key = "priority", Condition = "Equals", Value = "low" } } }, MaxResults = 20 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Tag Excluded filter should return results");
                        }),

                        // 69. Search Terms Required
                        Case("SearchTermsRequired", "Search terms: required", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TermsFilter = new { Required = new List<string> { "machine learning" } }, MaxResults = 20 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Terms required filter should return results");
                        }),

                        // 70. Search Terms Excluded
                        Case("SearchTermsExcluded", "Search terms: excluded", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TermsFilter = new { Excluded = new List<string> { "quantum" } }, MaxResults = 20 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Terms excluded filter should return results");
                        }),

                        // 71. Search Terms Required Multiple
                        Case("SearchTermsRequiredMultiple", "Search terms: required multiple", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TermsFilter = new { Required = new List<string> { "learning", "neural" } }, MaxResults = 20 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Terms required multiple filter should return results");
                        }),

                        // 72. Search Terms Required And Excluded
                        Case("SearchTermsRequiredAndExcluded", "Search terms: required and excluded", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TermsFilter = new { Required = new List<string> { "health" }, Excluded = new List<string> { "meditation" } }, MaxResults = 20 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Terms required and excluded filter should return results");
                        }),

                        // 73. Search Terms Case Insensitive
                        Case("SearchTermsCaseInsensitive", "Search terms: case insensitive", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, TermsFilter = new { Required = new List<string> { "MACHINE LEARNING" } }, MaxResults = 20 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Terms case insensitive filter should return results");
                        }),

                        // 74. Search Created After
                        Case("SearchCreatedAfter", "Search date: created after", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, CreatedAfter = DateTime.UtcNow.AddHours(-1).ToString("o"), MaxResults = 20 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "CreatedAfter filter should return results");
                        }),

                        // 75. Search Created Before
                        Case("SearchCreatedBefore", "Search date: created before", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, CreatedBefore = DateTime.UtcNow.AddHours(1).ToString("o"), MaxResults = 20 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "CreatedBefore filter should return results");
                        }),

                        // 76. Search Created Before None
                        Case("SearchCreatedBeforeNone", "Search date: created before none", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, CreatedBefore = DateTime.UtcNow.AddHours(-1).ToString("o"), MaxResults = 20 }).ConfigureAwait(false);
                            AssertEqual(0, GetDocs(json).GetArrayLength(), "CreatedBefore in past should return 0 results");
                        }),

                        // 77. Search Date Range Combined
                        Case("SearchDateRangeCombined", "Search date: range combined", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, CreatedAfter = DateTime.UtcNow.AddHours(-1).ToString("o"), CreatedBefore = DateTime.UtcNow.AddHours(1).ToString("o"), MaxResults = 20 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Date range combined filter should return results");
                        }),

                        // 78. Search Document Ids
                        Case("SearchDocumentIds", "Search docids: filter by document ids", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, DocumentIds = new List<string> { "grp-alpha", "grp-beta" }, MaxResults = 20 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "DocumentIds filter should return results");
                        }),

                        // 79. Search Document Ids No Match
                        Case("SearchDocumentIdsNoMatch", "Search docids: no match", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, DocumentIds = new List<string> { "nonexistent-docid-xyz" }, MaxResults = 20 }).ConfigureAwait(false);
                            AssertEqual(0, GetDocs(json).GetArrayLength(), "DocumentIds no match should return 0 results");
                        }),

                        // 80. Search Pagination
                        Case("SearchPagination", "Search pagination: page through results", async ct =>
                        {
                            int totalFetched = 0;
                            string continuationToken = null;
                            bool eor = false;
                            while (!eor)
                            {
                                object body;
                                if (continuationToken != null)
                                    body = new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, MaxResults = 3, ContinuationToken = continuationToken };
                                else
                                    body = new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, MaxResults = 3 };
                                var json = await DoSearch(body).ConfigureAwait(false);
                                eor = json.GetProperty("EndOfResults").GetBoolean();
                                totalFetched += GetDocs(json).GetArrayLength();
                                if (!eor) { continuationToken = json.GetProperty("ContinuationToken").GetString(); AssertNotNullOrEmpty(continuationToken, "ContinuationToken"); }
                            }
                            AssertTrue(totalFetched >= 10, "Should page through all search docs");
                        }),

                        // 81. Search Max Results
                        Case("SearchMaxResults", "Search pagination: max results", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, MaxResults = 2 }).ConfigureAwait(false);
                            AssertEqual(2, GetDocs(json).GetArrayLength(), "MaxResults should limit to 2");
                        }),

                        // 82. Search Combined Label And Tag
                        Case("SearchCombinedLabelAndTag", "Search combined: label and tag", async ct =>
                        {
                            var json = await DoSearch(new
                            {
                                Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } },
                                LabelFilter = new { Required = new List<string> { "science" } },
                                TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "Equals", Value = "ai" } } },
                                MaxResults = 20
                            }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Combined label and tag filter should return results");
                        }),

                        // 83. Search Combined Terms And Label
                        Case("SearchCombinedTermsAndLabel", "Search combined: terms and label", async ct =>
                        {
                            var json = await DoSearch(new
                            {
                                Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } },
                                TermsFilter = new { Required = new List<string> { "exercise" } },
                                LabelFilter = new { Required = new List<string> { "lifestyle" } },
                                MaxResults = 20
                            }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Combined terms and label filter should return results");
                        }),

                        // 84. Search Combined All Filters
                        Case("SearchCombinedAllFilters", "Search combined: all filters", async ct =>
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
                        }),

                        // 85. Search Full Text Basic
                        Case("SearchFullTextBasic", "Search full-text: basic query", async ct =>
                        {
                            var json = await DoSearch(new { FullText = new { Query = "machine learning" }, MaxResults = 10 }).ConfigureAwait(false);
                            var docs = GetDocs(json);
                            AssertTrue(docs.GetArrayLength() > 0, "Full-text search should return results");
                            foreach (JsonElement doc in docs.EnumerateArray())
                            {
                                AssertTrue(doc.GetProperty("Score").GetDouble() > 0, "Score should be > 0");
                                AssertTrue(doc.TryGetProperty("TextScore", out JsonElement ts) && ts.GetDouble() > 0, "TextScore should be > 0");
                            }
                        }),

                        // 86. Search Full Text TsRank
                        Case("SearchFullTextTsRank", "Search full-text: TsRank scoring", async ct =>
                        {
                            var json = await DoSearch(new { FullText = new { Query = "learning", SearchType = "TsRank" }, MaxResults = 10 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "TsRank search should return results");
                        }),

                        // 87. Search Full Text TsRankCd
                        Case("SearchFullTextTsRankCd", "Search full-text: TsRankCd scoring", async ct =>
                        {
                            var json = await DoSearch(new { FullText = new { Query = "neural networks image", SearchType = "TsRankCd" }, MaxResults = 10 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "TsRankCd search should return results");
                        }),

                        // 88. Search Full Text No Match
                        Case("SearchFullTextNoMatch", "Search full-text: no match", async ct =>
                        {
                            var json = await DoSearch(new { FullText = new { Query = "xyznonexistentterm12345" }, MaxResults = 10 }).ConfigureAwait(false);
                            AssertTrue(json.GetProperty("TotalRecords").GetInt64() == 0, "Should return 0 total records");
                            AssertTrue(GetDocs(json).GetArrayLength() == 0, "Documents should be empty");
                        }),

                        // 89. Search Full Text Minimum Score
                        Case("SearchFullTextMinimumScore", "Search full-text: minimum score threshold", async ct =>
                        {
                            var json = await DoSearch(new { FullText = new { Query = "learning", MinimumScore = 0.01 }, MaxResults = 10 }).ConfigureAwait(false);
                            foreach (JsonElement doc in GetDocs(json).EnumerateArray())
                            {
                                double ts = doc.GetProperty("TextScore").GetDouble();
                                AssertTrue(ts >= 0.01, "TextScore should be >= minimum threshold");
                            }
                        }),

                        // 90. Search Full Text Sort Descending
                        Case("SearchFullTextSortDescending", "Search full-text: sort text score descending", async ct =>
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
                        }),

                        // 91. Search Full Text Sort Ascending
                        Case("SearchFullTextSortAscending", "Search full-text: sort text score ascending", async ct =>
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
                        }),

                        // 92. Search Hybrid Basic
                        Case("SearchHybridBasic", "Search hybrid: vector + full-text", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = SearchEmb }, FullText = new { Query = "machine learning" }, MaxResults = 10 }).ConfigureAwait(false);
                            var docs = GetDocs(json);
                            AssertTrue(docs.GetArrayLength() > 0, "Hybrid search should return results");
                            foreach (JsonElement doc in docs.EnumerateArray())
                            {
                                AssertTrue(doc.GetProperty("Score").GetDouble() > 0, "Score should be > 0");
                                AssertTrue(doc.TryGetProperty("TextScore", out JsonElement ts) && ts.GetDouble() > 0, "TextScore should be populated");
                            }
                        }),

                        // 93. Search Hybrid Custom Weight
                        Case("SearchHybridCustomWeight", "Search hybrid: custom text weight", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = SearchEmb }, FullText = new { Query = "machine learning", TextWeight = 0.3 }, MaxResults = 10 }).ConfigureAwait(false);
                            var docs = GetDocs(json);
                            AssertTrue(docs.GetArrayLength() > 0, "Hybrid search with custom weight should return results");
                        }),

                        // 94. Search Hybrid With Label Filter
                        Case("SearchHybridWithLabelFilter", "Search hybrid: with label filter", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = SearchEmb }, FullText = new { Query = "learning" }, LabelFilter = new { Required = new List<string> { "science" } }, MaxResults = 20 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Hybrid with label filter should return results");
                        }),

                        // 95. Search Hybrid With Tag Filter
                        Case("SearchHybridWithTagFilter", "Search hybrid: with tag filter", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = SearchEmb }, FullText = new { Query = "learning" }, TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "Equals", Value = "ai" } } }, MaxResults = 20 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Hybrid with tag filter should return results");
                        }),

                        // 96. Search Hybrid With Terms Filter
                        Case("SearchHybridWithTermsFilter", "Search hybrid: with terms filter", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = SearchEmb }, FullText = new { Query = "learning" }, TermsFilter = new { Required = new List<string> { "Machine" } }, MaxResults = 20 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Hybrid with terms filter should return results");
                        }),

                        // 97. Search Hybrid With Date Range
                        Case("SearchHybridWithDateRange", "Search hybrid: with date range", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = SearchEmb }, FullText = new { Query = "learning" }, CreatedAfter = DateTime.UtcNow.AddHours(-1).ToString("o"), CreatedBefore = DateTime.UtcNow.AddHours(1).ToString("o"), MaxResults = 20 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "Hybrid with date range should return results");
                        }),

                        // 98. Search Full Text Pagination
                        Case("SearchFullTextPagination", "Search full-text: pagination", async ct =>
                        {
                            int totalFetched = 0;
                            string continuationToken = null;
                            bool eor = false;
                            while (!eor)
                            {
                                object body;
                                if (continuationToken != null)
                                    body = new { FullText = new { Query = "learning" }, MaxResults = 2, ContinuationToken = continuationToken };
                                else
                                    body = new { FullText = new { Query = "learning" }, MaxResults = 2 };
                                var json = await DoSearch(body).ConfigureAwait(false);
                                eor = json.GetProperty("EndOfResults").GetBoolean();
                                totalFetched += GetDocs(json).GetArrayLength();
                                if (!eor)
                                {
                                    continuationToken = json.GetProperty("ContinuationToken").GetString();
                                    AssertNotNullOrEmpty(continuationToken, "ContinuationToken");
                                }
                            }
                            AssertTrue(totalFetched > 0, "Should page through full-text results");
                        }),

                        // 99. Search Full Text Max Results
                        Case("SearchFullTextMaxResults", "Search full-text: max results", async ct =>
                        {
                            var json = await DoSearch(new { FullText = new { Query = "learning" }, MaxResults = 1 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() <= 1, "MaxResults should limit to 1");
                        }),

                        // 100. Search Full Text With Label Filter
                        Case("SearchFullTextWithLabelFilter", "Search full-text: with label filter", async ct =>
                        {
                            var json = await DoSearch(new { FullText = new { Query = "learning" }, LabelFilter = new { Required = new List<string> { "science" } }, MaxResults = 20 }).ConfigureAwait(false);
                            var docs = GetDocs(json);
                            AssertTrue(docs.GetArrayLength() > 0, "Full-text with label filter should return results");
                            foreach (JsonElement doc in docs.EnumerateArray())
                            {
                                AssertTrue(doc.GetProperty("TextScore").GetDouble() > 0, "TextScore should be > 0");
                            }
                        }),

                        // 101. Search Full Text With Tag Filter
                        Case("SearchFullTextWithTagFilter", "Search full-text: with tag filter", async ct =>
                        {
                            var json = await DoSearch(new { FullText = new { Query = "learning" }, TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "Equals", Value = "ai" } } }, MaxResults = 20 }).ConfigureAwait(false);
                            var docs = GetDocs(json);
                            AssertTrue(docs.GetArrayLength() > 0, "Full-text with tag filter should return results");
                            foreach (JsonElement doc in docs.EnumerateArray())
                            {
                                AssertTrue(doc.GetProperty("TextScore").GetDouble() > 0, "TextScore should be > 0");
                            }
                        }),

                        // 102. Search Full Text With Terms Filter
                        Case("SearchFullTextWithTermsFilter", "Search full-text: with terms filter", async ct =>
                        {
                            var json = await DoSearch(new { FullText = new { Query = "learning" }, TermsFilter = new { Required = new List<string> { "Machine" } }, MaxResults = 20 }).ConfigureAwait(false);
                            var docs = GetDocs(json);
                            AssertTrue(docs.GetArrayLength() > 0, "Full-text with terms filter should return results");
                            foreach (JsonElement doc in docs.EnumerateArray())
                            {
                                AssertTrue(doc.GetProperty("TextScore").GetDouble() > 0, "TextScore should be > 0");
                            }
                        }),

                        // 103. Search Full Text With Date Range
                        Case("SearchFullTextWithDateRange", "Search full-text: with date range", async ct =>
                        {
                            var json = await DoSearch(new { FullText = new { Query = "learning" }, CreatedAfter = DateTime.UtcNow.AddHours(-1).ToString("o"), CreatedBefore = DateTime.UtcNow.AddHours(1).ToString("o"), MaxResults = 20 }).ConfigureAwait(false);
                            var docs = GetDocs(json);
                            AssertTrue(docs.GetArrayLength() > 0, "Full-text with date range should return results");
                            foreach (JsonElement doc in docs.EnumerateArray())
                            {
                                AssertTrue(doc.GetProperty("TextScore").GetDouble() > 0, "TextScore should be > 0");
                            }
                        }),

                        // 104. Search Full Text With Document Ids
                        Case("SearchFullTextWithDocumentIds", "Search full-text: with document ids", async ct =>
                        {
                            var json = await DoSearch(new { FullText = new { Query = "learning" }, DocumentIds = new List<string> { "grp-alpha" }, MaxResults = 20 }).ConfigureAwait(false);
                            var docs = GetDocs(json);
                            AssertTrue(docs.GetArrayLength() > 0, "Full-text with document ID filter should return results");
                            foreach (JsonElement doc in docs.EnumerateArray())
                            {
                                AssertTrue(doc.GetProperty("TextScore").GetDouble() > 0, "TextScore should be > 0");
                                AssertEqual("grp-alpha", doc.GetProperty("DocumentId").GetString(), "DocumentId should match filter");
                            }
                        }),

                        // 105. Search Full Text Distance Is Zero
                        Case("SearchFullTextDistanceIsZero", "Search full-text: distance is zero", async ct =>
                        {
                            var json = await DoSearch(new { FullText = new { Query = "learning" }, MaxResults = 10 }).ConfigureAwait(false);
                            var docs = GetDocs(json);
                            AssertTrue(docs.GetArrayLength() > 0, "Should return results");
                            foreach (JsonElement doc in docs.EnumerateArray())
                            {
                                double distance = doc.GetProperty("Distance").GetDouble();
                                AssertTrue(distance == 0.0, "Full-text-only search should return Distance = 0.0, got " + distance);
                            }
                        }),

                        // 106. Search Full Text Rank Types Differ
                        Case("SearchFullTextRankTypesDiffer", "Search full-text: TsRank vs TsRankCd differ", async ct =>
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
                        }),

                        // 107. Search Hybrid Weight Zero
                        Case("SearchHybridWeightZero", "Search hybrid: TextWeight 0.0 matches vector-only", async ct =>
                        {
                            // TextWeight = 0.0 means pure vector scoring; hybrid score should equal vector score
                            var jsonHybrid = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = SearchEmb }, FullText = new { Query = "learning", TextWeight = 0.0 }, MaxResults = 10 }).ConfigureAwait(false);
                            var jsonVector = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = SearchEmb }, MaxResults = 10 }).ConfigureAwait(false);

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
                        }),

                        // 108. Search Hybrid Weight One
                        Case("SearchHybridWeightOne", "Search hybrid: TextWeight 1.0 matches full-text-only", async ct =>
                        {
                            // TextWeight = 1.0 means pure full-text scoring; hybrid score should equal text score
                            var jsonHybrid = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = SearchEmb }, FullText = new { Query = "learning", TextWeight = 1.0 }, MaxResults = 10 }).ConfigureAwait(false);
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
                        }),

                        // 109. Search Hybrid Blended Score Formula
                        Case("SearchHybridBlendedScoreFormula", "Search hybrid: blended score formula", async ct =>
                        {
                            // Verify Score = (1 - TextWeight) * vectorScore + TextWeight * textScore
                            double textWeight = 0.4;
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = SearchEmb }, FullText = new { Query = "machine learning", TextWeight = textWeight }, MaxResults = 10 }).ConfigureAwait(false);
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
                        }),

                        // 110. Search Backward Compat Vector Only
                        Case("SearchBackwardCompatVectorOnly", "Search backward compat: vector-only unchanged", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = SearchEmb }, MaxResults = 10 }).ConfigureAwait(false);
                            var docs = GetDocs(json);
                            AssertTrue(docs.GetArrayLength() > 0, "Vector-only search should still work");
                            foreach (JsonElement doc in docs.EnumerateArray())
                            {
                                AssertTrue(doc.GetProperty("Score").GetDouble() > 0, "Score should be > 0");
                            }
                        }),

                        // 111. Search Result Fields
                        Case("SearchResultFields", "Search validation: result fields", async ct =>
                        {
                            var json = await DoSearch(new { Vector = new { SearchType = "CosineSimilarity", Embeddings = new List<float> { 0.9f, 0.1f, 0.05f } }, MaxResults = 10 }).ConfigureAwait(false);
                            AssertTrue(json.GetProperty("Success").GetBoolean(), "Success should be true");
                            AssertTrue(json.TryGetProperty("MaxResults", out _), "Response should contain MaxResults");
                            AssertTrue(json.TryGetProperty("EndOfResults", out _), "Response should contain EndOfResults");
                            AssertTrue(json.GetProperty("TotalRecords").GetInt64() > 0, "TotalRecords should be > 0");
                            AssertTrue(json.TryGetProperty("RecordsRemaining", out _), "Response should contain RecordsRemaining");
                            AssertTrue(json.TryGetProperty("Documents", out _), "Response should contain Documents");
                        }),

                        // 112. Search Document Fields
                        Case("SearchDocumentFields", "Search validation: document fields", async ct =>
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
                        }),

                        // 113. Neighbor Data Setup
                        Case("NeighborDataSetup", "Neighbor retrieval: setup data", async ct =>
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

                            using HttpResponseMessage batchResp = await PostAsync(AdminClient, DocBasePath() + "/batch", docs).ConfigureAwait(false);
                            AssertStatusCode(batchResp, HttpStatusCode.Created);

                            // Add labels and tags to neighbor test docs
                            foreach (string dk in new[] { "nbr-doc-0", "nbr-doc-1", "nbr-doc-2", "nbr-doc-3", "nbr-doc-4", "nbr-doc-5" })
                            {
                                using HttpResponseMessage labelResp = await PutAsync(AdminClient, LabelBasePath(), new { DocumentKey = dk, Label = "neighbor-test" }).ConfigureAwait(false);
                                AssertStatusCode(labelResp, HttpStatusCode.Created);

                                using HttpResponseMessage tagResp = await PutAsync(AdminClient, TagBasePath(), new { DocumentKey = dk, Key = "suite", Value = "neighbor" }).ConfigureAwait(false);
                                AssertStatusCode(tagResp, HttpStatusCode.Created);
                            }
                        }),

                        // 114. Neighbor Vector Search
                        Case("NeighborVectorSearch", "Neighbor retrieval: vector search with neighbors", async ct =>
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
                        }),

                        // 115. Neighbor Hybrid Search
                        Case("NeighborHybridSearch", "Neighbor retrieval: hybrid search with neighbors", async ct =>
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
                        }),

                        // 116. Neighbor Full Text Search
                        Case("NeighborFullTextSearch", "Neighbor retrieval: full-text search with neighbors", async ct =>
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
                        }),

                        // 117. Neighbor Boundary Chunk
                        Case("NeighborBoundaryChunk", "Neighbor retrieval: boundary chunk (position 0)", async ct =>
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
                        }),

                        // 118. Neighbor Single Chunk Document
                        Case("NeighborSingleChunkDocument", "Neighbor retrieval: single-chunk document", async ct =>
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
                        }),

                        // 119. Neighbor Null And Zero
                        Case("NeighborNullAndZero", "Neighbor retrieval: null and zero means no neighbors", async ct =>
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
                        }),

                        // 120. Neighbor Overlapping
                        Case("NeighborOverlapping", "Neighbor retrieval: overlapping results same document", async ct =>
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
                        }),

                        // 121. Neighbor Labels And Tags
                        Case("NeighborLabelsAndTags", "Neighbor retrieval: neighbor labels and tags", async ct =>
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
                        }),

                        // 122. Enum Documents Basic
                        Case("EnumDocumentsBasic", "Enum docs: basic", async ct =>
                        {
                            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending" }).ConfigureAwait(false);
                            AssertTrue(GetObjects(json).GetArrayLength() >= 10, "Basic enumeration should return >= 10 results");
                        }),

                        // 123. Enum Documents Created Ascending
                        Case("EnumDocumentsCreatedAscending", "Enum docs: created ascending", async ct =>
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
                        }),

                        // 124. Enum Documents Created Descending
                        Case("EnumDocumentsCreatedDescending", "Enum docs: created descending", async ct =>
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
                        }),

                        // 125. Enum Documents Pagination
                        Case("EnumDocumentsPagination", "Enum docs: pagination", async ct =>
                        {
                            int totalFetched = 0;
                            string continuationToken = null;
                            bool eor = false;
                            while (!eor)
                            {
                                object body;
                                if (continuationToken != null)
                                    body = new { MaxResults = 3, Ordering = "CreatedAscending", ContinuationToken = continuationToken };
                                else
                                    body = new { MaxResults = 3, Ordering = "CreatedAscending" };
                                var json = await DoEnumDocs(body).ConfigureAwait(false);
                                eor = json.GetProperty("EndOfResults").GetBoolean();
                                AssertTrue(json.GetProperty("TotalRecords").GetInt64() >= 10, "TotalRecords should be >= 10");
                                totalFetched += GetObjects(json).GetArrayLength();
                                if (!eor) { continuationToken = json.GetProperty("ContinuationToken").GetString(); AssertNotNullOrEmpty(continuationToken, "ContinuationToken"); }
                            }
                            AssertTrue(totalFetched >= 10, "Should page through all enumeration docs");
                        }),

                        // 126. Enum Documents Max Results
                        Case("EnumDocumentsMaxResults", "Enum docs: max results", async ct =>
                        {
                            var json = await DoEnumDocs(new { MaxResults = 2, Ordering = "CreatedDescending" }).ConfigureAwait(false);
                            AssertEqual(2, GetObjects(json).GetArrayLength(), "MaxResults should limit to 2");
                        }),

                        // 127. Enum Documents Created After
                        Case("EnumDocumentsCreatedAfter", "Enum docs: created after", async ct =>
                        {
                            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", CreatedAfter = DateTime.UtcNow.AddHours(-1).ToString("o") }).ConfigureAwait(false);
                            AssertTrue(GetObjects(json).GetArrayLength() > 0, "CreatedAfter filter should return results");
                        }),

                        // 128. Enum Documents Created Before
                        Case("EnumDocumentsCreatedBefore", "Enum docs: created before", async ct =>
                        {
                            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", CreatedBefore = DateTime.UtcNow.AddHours(1).ToString("o") }).ConfigureAwait(false);
                            AssertTrue(GetObjects(json).GetArrayLength() > 0, "CreatedBefore filter should return results");
                        }),

                        // 129. Enum Documents Date Range None
                        Case("EnumDocumentsDateRangeNone", "Enum docs: date range none", async ct =>
                        {
                            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", CreatedBefore = DateTime.UtcNow.AddHours(-1).ToString("o") }).ConfigureAwait(false);
                            AssertEqual(0, GetObjects(json).GetArrayLength(), "CreatedBefore in past should return 0 results");
                        }),

                        // 130. Enum Documents Document Ids
                        Case("EnumDocumentsDocumentIds", "Enum docs: document ids", async ct =>
                        {
                            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", DocumentIds = new List<string> { "grp-alpha", "grp-beta" } }).ConfigureAwait(false);
                            AssertTrue(GetObjects(json).GetArrayLength() > 0, "DocumentIds filter should return results");
                        }),

                        // 131. Enum Documents Document Ids No Match
                        Case("EnumDocumentsDocumentIdsNoMatch", "Enum docs: document ids no match", async ct =>
                        {
                            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", DocumentIds = new List<string> { "nonexistent-docid-xyz" } }).ConfigureAwait(false);
                            AssertEqual(0, GetObjects(json).GetArrayLength(), "DocumentIds no match should return 0 results");
                        }),

                        // 132. Enum Documents Label Required
                        Case("EnumDocumentsLabelRequired", "Enum docs: label required", async ct =>
                        {
                            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", LabelFilter = new { Required = new List<string> { "science" } } }).ConfigureAwait(false);
                            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Label required filter should return results");
                        }),

                        // 133. Enum Documents Label Excluded
                        Case("EnumDocumentsLabelExcluded", "Enum docs: label excluded", async ct =>
                        {
                            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", LabelFilter = new { Excluded = new List<string> { "lifestyle" } } }).ConfigureAwait(false);
                            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Label excluded filter should return results");
                        }),

                        // 134. Enum Documents Label No Match
                        Case("EnumDocumentsLabelNoMatch", "Enum docs: label no match", async ct =>
                        {
                            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", LabelFilter = new { Required = new List<string> { "nonexistent-label-xyz" } } }).ConfigureAwait(false);
                            AssertEqual(0, GetObjects(json).GetArrayLength(), "Label no match should return 0 results");
                        }),

                        // 135. Enum Documents Tag Equals
                        Case("EnumDocumentsTagEquals", "Enum docs: tag equals", async ct =>
                        {
                            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "Equals", Value = "ai" } } } }).ConfigureAwait(false);
                            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Tag Equals filter should return results");
                        }),

                        // 136. Enum Documents Tag Not Equals
                        Case("EnumDocumentsTagNotEquals", "Enum docs: tag not equals", async ct =>
                        {
                            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "NotEquals", Value = "ai" } } } }).ConfigureAwait(false);
                            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Tag NotEquals filter should return results");
                        }),

                        // 137. Enum Documents Tag Contains
                        Case("EnumDocumentsTagContains", "Enum docs: tag contains", async ct =>
                        {
                            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "Contains", Value = "heal" } } } }).ConfigureAwait(false);
                            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Tag Contains filter should return results");
                        }),

                        // 138. Enum Documents Tag Starts With
                        Case("EnumDocumentsTagStartsWith", "Enum docs: tag starts with", async ct =>
                        {
                            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "StartsWith", Value = "fin" } } } }).ConfigureAwait(false);
                            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Tag StartsWith filter should return results");
                        }),

                        // 139. Enum Documents Tag Ends With
                        Case("EnumDocumentsTagEndsWith", "Enum docs: tag ends with", async ct =>
                        {
                            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "EndsWith", Value = "ics" } } } }).ConfigureAwait(false);
                            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Tag EndsWith filter should return results");
                        }),

                        // 140. Enum Documents Tag Greater Than
                        Case("EnumDocumentsTagGreaterThan", "Enum docs: tag greater than", async ct =>
                        {
                            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "year", Condition = "GreaterThan", Value = "2023" } } } }).ConfigureAwait(false);
                            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Tag GreaterThan filter should return results");
                        }),

                        // 141. Enum Documents Tag Less Than
                        Case("EnumDocumentsTagLessThan", "Enum docs: tag less than", async ct =>
                        {
                            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "year", Condition = "LessThan", Value = "2023" } } } }).ConfigureAwait(false);
                            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Tag LessThan filter should return results");
                        }),

                        // 142. Enum Documents Tag Is Not Null
                        Case("EnumDocumentsTagIsNotNull", "Enum docs: tag is not null", async ct =>
                        {
                            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "priority", Condition = "IsNotNull" } } } }).ConfigureAwait(false);
                            AssertTrue(GetObjects(json).GetArrayLength() >= 10, "Tag IsNotNull filter should return >= 10 results");
                        }),

                        // 143. Enum Documents Tag Is Null
                        Case("EnumDocumentsTagIsNull", "Enum docs: tag is null", async ct =>
                        {
                            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "nonexistent-tag-xyz", Condition = "IsNull" } } } }).ConfigureAwait(false);
                            AssertTrue(GetObjects(json).GetArrayLength() >= 10, "Tag IsNull filter should return >= 10 results");
                        }),

                        // 144. Enum Documents Tag Contains Not
                        Case("EnumDocumentsTagContainsNot", "Enum docs: tag contains not", async ct =>
                        {
                            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "ContainsNot", Value = "ai" } } } }).ConfigureAwait(false);
                            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Tag ContainsNot filter should return results");
                        }),

                        // 145. Enum Documents Terms Required
                        Case("EnumDocumentsTermsRequired", "Enum docs: terms required", async ct =>
                        {
                            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", TermsFilter = new { Required = new List<string> { "machine learning" } } }).ConfigureAwait(false);
                            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Terms required filter should return results");
                        }),

                        // 146. Enum Documents Terms Excluded
                        Case("EnumDocumentsTermsExcluded", "Enum docs: terms excluded", async ct =>
                        {
                            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending", TermsFilter = new { Excluded = new List<string> { "quantum" } } }).ConfigureAwait(false);
                            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Terms excluded filter should return results");
                        }),

                        // 147. Enum Documents Combined Label And Tag
                        Case("EnumDocumentsCombinedLabelAndTag", "Enum docs: combined label and tag", async ct =>
                        {
                            var json = await DoEnumDocs(new
                            {
                                MaxResults = 100,
                                Ordering = "CreatedDescending",
                                LabelFilter = new { Required = new List<string> { "science" } },
                                TagFilter = new { Required = new List<object> { new { Key = "category", Condition = "Equals", Value = "ai" } } }
                            }).ConfigureAwait(false);
                            AssertTrue(GetObjects(json).GetArrayLength() > 0, "Combined label and tag filter should return results");
                        }),

                        // 148. Enum Documents Combined All Filters
                        Case("EnumDocumentsCombinedAllFilters", "Enum docs: combined all filters", async ct =>
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
                        }),

                        // 149. Enum Documents Result Fields
                        Case("EnumDocumentsResultFields", "Enum docs: result fields", async ct =>
                        {
                            var json = await DoEnumDocs(new { MaxResults = 100, Ordering = "CreatedDescending" }).ConfigureAwait(false);
                            AssertTrue(json.GetProperty("Success").GetBoolean(), "Success should be true");
                            AssertTrue(json.TryGetProperty("MaxResults", out _), "Response should contain MaxResults");
                            AssertTrue(json.TryGetProperty("EndOfResults", out _), "Response should contain EndOfResults");
                            AssertTrue(json.GetProperty("TotalRecords").GetInt64() > 0, "TotalRecords should be > 0");
                            AssertTrue(json.TryGetProperty("RecordsRemaining", out _), "Response should contain RecordsRemaining");
                            AssertTrue(json.TryGetProperty("Objects", out _), "Response should contain Objects");
                        }),

                        // 150. Enum Documents Object Fields
                        Case("EnumDocumentsObjectFields", "Enum docs: object fields", async ct =>
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
                        }),

                        // 151. Enumeration Pagination
                        Case("EnumerationPagination", "Enumeration: tenant pagination", async ct =>
                        {
                            int totalToCreate = 5;

                            // Create N tenants for pagination testing
                            for (int i = 0; i < totalToCreate; i++)
                            {
                                object body = new { Name = "PaginationTestTenant_" + i };
                                using HttpResponseMessage createResp = await PutAsync(AdminClient, "/v1.0/tenants", body).ConfigureAwait(false);
                                AssertStatusCode(createResp, HttpStatusCode.Created);

                                JsonElement createJson = await ReadResponse<JsonElement>(createResp).ConfigureAwait(false);
                                string tenantId = createJson.GetProperty("Id").GetString();
                                PaginationTenantIds.Add(tenantId);
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

                                using HttpResponseMessage enumResp = await PostAsync(AdminClient, "/v1.0/tenants/enumerate", enumBody).ConfigureAwait(false);
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
                        }),

                        // 152. Authorization Non Admin
                        Case("AuthorizationNonAdmin", "Authorization: non-admin cannot create tenant", async ct =>
                        {
                            AssertNotNull(UserClient, "UserClient should be initialized");

                            object body = new { Name = "UnauthorizedTenantAttempt" };
                            using HttpResponseMessage response = await PutAsync(UserClient, "/v1.0/tenants", body).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.Forbidden);
                        }),

                        // 153. Cleanup Search Labels
                        Case("CleanupSearchLabels", "Cleanup: delete search labels", async ct =>
                        {
                            if (string.IsNullOrEmpty(TestCollectionId)) return;

                            foreach (string labelId in SearchTestLabelIds)
                            {
                                string path = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/labels/" + labelId;
                                using HttpResponseMessage response = await DeleteAsync(AdminClient, path).ConfigureAwait(false);
                                AssertStatusCode(response, HttpStatusCode.NoContent);
                            }
                        }),

                        // 154. Cleanup Search Tags
                        Case("CleanupSearchTags", "Cleanup: delete search tags", async ct =>
                        {
                            if (string.IsNullOrEmpty(TestCollectionId)) return;

                            foreach (string tagId in SearchTestTagIds)
                            {
                                string path = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/tags/" + tagId;
                                using HttpResponseMessage response = await DeleteAsync(AdminClient, path).ConfigureAwait(false);
                                AssertStatusCode(response, HttpStatusCode.NoContent);
                            }
                        }),

                        // 155. Cleanup Search Documents
                        Case("CleanupSearchDocuments", "Cleanup: delete search documents", async ct =>
                        {
                            if (string.IsNullOrEmpty(TestCollectionId)) return;

                            foreach (string docKey in SearchTestDocKeys)
                            {
                                string path = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/documents/" + docKey;
                                using HttpResponseMessage response = await DeleteAsync(AdminClient, path).ConfigureAwait(false);
                                AssertStatusCode(response, HttpStatusCode.NoContent);
                            }
                        }),

                        // 156. Cleanup Labels
                        Case("CleanupLabels", "Cleanup: delete labels", async ct =>
                        {
                            if (string.IsNullOrEmpty(TestLabelId) || string.IsNullOrEmpty(TestCollectionId)) return;

                            string path = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/labels/" + TestLabelId;
                            using HttpResponseMessage response = await DeleteAsync(AdminClient, path).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.NoContent);
                        }),

                        // 157. Cleanup Tags
                        Case("CleanupTags", "Cleanup: delete tags", async ct =>
                        {
                            if (string.IsNullOrEmpty(TestTagId) || string.IsNullOrEmpty(TestCollectionId)) return;

                            string path = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/tags/" + TestTagId;
                            using HttpResponseMessage response = await DeleteAsync(AdminClient, path).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.NoContent);
                        }),

                        // 158. Cleanup Documents
                        Case("CleanupDocuments", "Cleanup: delete documents", async ct =>
                        {
                            if (string.IsNullOrEmpty(TestCollectionId)) return;

                            // Delete the single test document
                            if (!string.IsNullOrEmpty(TestDocumentKey))
                            {
                                string path = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/documents/" + TestDocumentKey;
                                using HttpResponseMessage response = await DeleteAsync(AdminClient, path).ConfigureAwait(false);
                                AssertStatusCode(response, HttpStatusCode.NoContent);
                            }

                            // Delete batch documents using batch delete endpoint
                            if (TestBatchDocumentKeys.Count > 0)
                            {
                                string batchDeletePath = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId + "/documents/batch/delete";
                                object batchDeleteBody = new { DocumentKeys = TestBatchDocumentKeys };
                                using HttpResponseMessage batchResp = await PostAsync(AdminClient, batchDeletePath, batchDeleteBody).ConfigureAwait(false);
                                AssertStatusCode(batchResp, HttpStatusCode.NoContent);
                            }
                        }),

                        // 159. Cleanup Collection
                        Case("CleanupCollection", "Cleanup: delete collection", async ct =>
                        {
                            if (string.IsNullOrEmpty(TestCollectionId)) return;

                            string path = "/v1.0/tenants/" + TestTenantId + "/collections/" + TestCollectionId;
                            using HttpResponseMessage response = await DeleteAsync(AdminClient, path).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.NoContent);
                        }),

                        // 160. Cleanup Credential
                        Case("CleanupCredential", "Cleanup: delete credential", async ct =>
                        {
                            if (string.IsNullOrEmpty(TestCredentialId)) return;

                            string path = "/v1.0/tenants/" + TestTenantId + "/credentials/" + TestCredentialId;
                            using HttpResponseMessage response = await DeleteAsync(AdminClient, path).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.NoContent);
                        }),

                        // 161. Cleanup User
                        Case("CleanupUser", "Cleanup: delete user", async ct =>
                        {
                            if (string.IsNullOrEmpty(TestUserId)) return;

                            string path = "/v1.0/tenants/" + TestTenantId + "/users/" + TestUserId;
                            using HttpResponseMessage response = await DeleteAsync(AdminClient, path).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.NoContent);
                        }),

                        // 162. Cleanup Tenant
                        Case("CleanupTenant", "Cleanup: delete tenant", async ct =>
                        {
                            if (string.IsNullOrEmpty(TestTenantId)) return;

                            string path = "/v1.0/tenants/" + TestTenantId + "?force";
                            using HttpResponseMessage response = await DeleteAsync(AdminClient, path).ConfigureAwait(false);
                            AssertStatusCode(response, HttpStatusCode.NoContent);
                        }),

                        // 163. Cleanup Pagination Tenants
                        Case("CleanupPaginationTenants", "Cleanup: delete pagination tenants", async ct =>
                        {
                            foreach (string tenantId in PaginationTenantIds)
                            {
                                string path = "/v1.0/tenants/" + tenantId + "?force";
                                using HttpResponseMessage response = await DeleteAsync(AdminClient, path).ConfigureAwait(false);
                                AssertStatusCode(response, HttpStatusCode.NoContent);
                            }
                        })
                    })
            };
        }

        private static TestCaseDescriptor Case(string caseId, string displayName, Func<CancellationToken, Task> execute)
        {
            return new TestCaseDescriptor(
                suiteId: "RecallDb",
                caseId: caseId,
                displayName: displayName,
                executeAsync: execute);
        }
    }
}
