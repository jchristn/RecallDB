namespace RecallDb.Sdk.TestHarness
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using RecallDb.Sdk;
    using RecallDb.Sdk.Models;

    /// <summary>
    /// RecallDB SDK test harness.
    /// </summary>
    public class Program
    {
        private static string _Endpoint = "http://localhost:8600";
        private static string _Token = "default";
        private static string _TenantId = "default";
        private static string _CollectionId = null;
        private static string _DocumentKey = null;

        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static async Task Main(string[] args)
        {
            if (args.Length >= 1) _Endpoint = args[0];
            if (args.Length >= 2) _Token = args[1];

            Console.WriteLine("RecallDB SDK Test Harness");
            Console.WriteLine("Endpoint : " + _Endpoint);
            Console.WriteLine("Token    : " + _Token);
            Console.WriteLine("");

            using (RecallDbClient client = new RecallDbClient(_Endpoint, _Token))
            {
                await TestHealthAsync(client);
                await TestAuthenticateAsync(client);
                await TestTenantsAsync(client);
                await TestUsersAsync(client);
                await TestCredentialsAsync(client);
                await TestCollectionsAsync(client);
                await TestDocumentsAsync(client);
                await TestLabelsAsync(client);
                await TestTagsAsync(client);
                await TestSearchAsync(client);
                await CleanupAsync(client);
            }

            Console.WriteLine("");
            Console.WriteLine("All tests completed.");
        }

        private static async Task TestHealthAsync(RecallDbClient client)
        {
            Console.WriteLine("[Health] GET /");
            Dictionary<string, object> health = await client.HealthAsync();
            Console.WriteLine("[Health] Success: " + (health != null));
            Console.WriteLine("");
        }

        private static async Task TestAuthenticateAsync(RecallDbClient client)
        {
            Console.WriteLine("[Auth] POST /v1.0/authenticate");
            AuthenticateRequest req = new AuthenticateRequest();
            req.BearerToken = _Token;
            AuthenticateResponse resp = await client.AuthenticateAsync(req);
            Console.WriteLine("[Auth] Success: " + resp.Success);
            Console.WriteLine("");
        }

        private static async Task TestTenantsAsync(RecallDbClient client)
        {
            Console.WriteLine("[Tenants] Enumerate");
            EnumerationResult<TenantMetadata> tenants = await client.EnumerateTenantsAsync(new EnumerationQuery());
            Console.WriteLine("[Tenants] Total: " + tenants.TotalRecords);

            Console.WriteLine("[Tenants] Get tenant: " + _TenantId);
            TenantMetadata tenant = await client.GetTenantAsync(_TenantId);
            Console.WriteLine("[Tenants] Name: " + tenant.Name);

            Console.WriteLine("[Tenants] Exists: " + _TenantId);
            bool exists = await client.TenantExistsAsync(_TenantId);
            Console.WriteLine("[Tenants] Exists: " + exists);
            Console.WriteLine("");
        }

        private static async Task TestUsersAsync(RecallDbClient client)
        {
            Console.WriteLine("[Users] Enumerate");
            EnumerationResult<UserMaster> users = await client.EnumerateUsersAsync(_TenantId, new EnumerationQuery());
            Console.WriteLine("[Users] Total: " + users.TotalRecords);

            Console.WriteLine("[Users] Get user: default");
            UserMaster user = await client.GetUserAsync(_TenantId, "default");
            Console.WriteLine("[Users] Email: " + user.Email);

            Console.WriteLine("[Users] Exists: default");
            bool exists = await client.UserExistsAsync(_TenantId, "default");
            Console.WriteLine("[Users] Exists: " + exists);
            Console.WriteLine("");
        }

        private static async Task TestCredentialsAsync(RecallDbClient client)
        {
            Console.WriteLine("[Credentials] Enumerate");
            EnumerationResult<Credential> creds = await client.EnumerateCredentialsAsync(_TenantId, new EnumerationQuery());
            Console.WriteLine("[Credentials] Total: " + creds.TotalRecords);

            Console.WriteLine("[Credentials] Get credential: default");
            Credential cred = await client.GetCredentialAsync(_TenantId, "default");
            Console.WriteLine("[Credentials] Name: " + cred.Name);

            Console.WriteLine("[Credentials] Exists: default");
            bool exists = await client.CredentialExistsAsync(_TenantId, "default");
            Console.WriteLine("[Credentials] Exists: " + exists);
            Console.WriteLine("");
        }

        private static async Task TestCollectionsAsync(RecallDbClient client)
        {
            Console.WriteLine("[Collections] Create");
            CollectionMetadata col = new CollectionMetadata();
            col.Name = "SDK Test Collection";
            col.Description = "Created by SDK test harness";
            col.Dimensionality = 384;
            col = await client.CreateCollectionAsync(_TenantId, col);
            _CollectionId = col.Id;
            Console.WriteLine("[Collections] Created: " + _CollectionId);

            Console.WriteLine("[Collections] Get: " + _CollectionId);
            col = await client.GetCollectionAsync(_TenantId, _CollectionId);
            Console.WriteLine("[Collections] Name: " + col.Name);

            Console.WriteLine("[Collections] Exists: " + _CollectionId);
            bool exists = await client.CollectionExistsAsync(_TenantId, _CollectionId);
            Console.WriteLine("[Collections] Exists: " + exists);

            Console.WriteLine("[Collections] Update: " + _CollectionId);
            col.Description = "Updated by SDK test harness";
            col = await client.UpdateCollectionAsync(_TenantId, _CollectionId, col);
            Console.WriteLine("[Collections] Updated description: " + col.Description);

            Console.WriteLine("[Collections] Enumerate");
            EnumerationResult<CollectionMetadata> collections = await client.EnumerateCollectionsAsync(_TenantId, new EnumerationQuery());
            Console.WriteLine("[Collections] Total: " + collections.TotalRecords);
            Console.WriteLine("");
        }

        private static async Task TestDocumentsAsync(RecallDbClient client)
        {
            if (string.IsNullOrEmpty(_CollectionId))
            {
                Console.WriteLine("[Documents] Skipped - no collection");
                return;
            }

            Console.WriteLine("[Documents] Create");
            DocumentRecord doc = new DocumentRecord();
            doc.Content = "This is a test document for the RecallDB SDK.";
            doc.ContentType = "Text";
            List<float> embeddings = new List<float>();
            for (int i = 0; i < 384; i++) embeddings.Add(0.01f * i);
            doc.Embeddings = embeddings;
            doc = await client.CreateDocumentAsync(_TenantId, _CollectionId, doc);
            _DocumentKey = doc.DocumentKey;
            Console.WriteLine("[Documents] Created: " + _DocumentKey);

            Console.WriteLine("[Documents] Get: " + _DocumentKey);
            doc = await client.GetDocumentAsync(_TenantId, _CollectionId, _DocumentKey);
            Console.WriteLine("[Documents] Content length: " + doc.ContentLength);

            Console.WriteLine("[Documents] Exists: " + _DocumentKey);
            bool exists = await client.DocumentExistsAsync(_TenantId, _CollectionId, _DocumentKey);
            Console.WriteLine("[Documents] Exists: " + exists);

            Console.WriteLine("[Documents] Enumerate");
            EnumerationResult<DocumentRecord> docs = await client.EnumerateDocumentsAsync(_TenantId, _CollectionId, new EnumerationQuery());
            Console.WriteLine("[Documents] Total: " + docs.TotalRecords);

            Console.WriteLine("[Documents] Batch create");
            List<DocumentRecord> batch = new List<DocumentRecord>();
            for (int i = 0; i < 3; i++)
            {
                DocumentRecord batchDoc = new DocumentRecord();
                batchDoc.Content = "Batch document " + i;
                batchDoc.ContentType = "Text";
                batchDoc.Embeddings = embeddings;
                batch.Add(batchDoc);
            }
            List<DocumentRecord> created = await client.CreateDocumentBatchAsync(_TenantId, _CollectionId, batch);
            Console.WriteLine("[Documents] Batch created: " + created.Count);
            Console.WriteLine("");
        }

        private static async Task TestLabelsAsync(RecallDbClient client)
        {
            if (string.IsNullOrEmpty(_CollectionId) || string.IsNullOrEmpty(_DocumentKey))
            {
                Console.WriteLine("[Labels] Skipped - no collection or document");
                return;
            }

            Console.WriteLine("[Labels] Create");
            LabelRecord label = new LabelRecord();
            label.DocumentKey = _DocumentKey;
            label.Label = "test-label";
            label = await client.CreateLabelAsync(_TenantId, _CollectionId, label);
            Console.WriteLine("[Labels] Created: " + label.Id);

            Console.WriteLine("[Labels] Get: " + label.Id);
            LabelRecord fetched = await client.GetLabelAsync(_TenantId, _CollectionId, label.Id);
            Console.WriteLine("[Labels] Label value: " + fetched.Label);

            Console.WriteLine("[Labels] List");
            EnumerationResult<LabelRecord> labels = await client.ListLabelsAsync(_TenantId, _CollectionId);
            Console.WriteLine("[Labels] Total: " + labels.TotalRecords);

            Console.WriteLine("[Labels] Delete: " + label.Id);
            await client.DeleteLabelAsync(_TenantId, _CollectionId, label.Id);
            Console.WriteLine("[Labels] Deleted");
            Console.WriteLine("");
        }

        private static async Task TestTagsAsync(RecallDbClient client)
        {
            if (string.IsNullOrEmpty(_CollectionId) || string.IsNullOrEmpty(_DocumentKey))
            {
                Console.WriteLine("[Tags] Skipped - no collection or document");
                return;
            }

            Console.WriteLine("[Tags] Create");
            TagRecord tag = new TagRecord();
            tag.DocumentKey = _DocumentKey;
            tag.Key = "category";
            tag.Value = "test";
            tag = await client.CreateTagAsync(_TenantId, _CollectionId, tag);
            Console.WriteLine("[Tags] Created: " + tag.Id);

            Console.WriteLine("[Tags] Get: " + tag.Id);
            TagRecord fetched = await client.GetTagAsync(_TenantId, _CollectionId, tag.Id);
            Console.WriteLine("[Tags] Key: " + fetched.Key + " Value: " + fetched.Value);

            Console.WriteLine("[Tags] List");
            EnumerationResult<TagRecord> tags = await client.ListTagsAsync(_TenantId, _CollectionId);
            Console.WriteLine("[Tags] Total: " + tags.TotalRecords);

            Console.WriteLine("[Tags] Delete: " + tag.Id);
            await client.DeleteTagAsync(_TenantId, _CollectionId, tag.Id);
            Console.WriteLine("[Tags] Deleted");
            Console.WriteLine("");
        }

        private static async Task TestSearchAsync(RecallDbClient client)
        {
            if (string.IsNullOrEmpty(_CollectionId))
            {
                Console.WriteLine("[Search] Skipped - no collection");
                return;
            }

            Console.WriteLine("[Search] Search collection: " + _CollectionId);
            SearchQuery query = new SearchQuery();
            query.MaxResults = 10;
            VectorQuery vector = new VectorQuery();
            vector.SearchType = "CosineSimilarity";
            List<float> embeddings = new List<float>();
            for (int i = 0; i < 384; i++) embeddings.Add(0.01f * i);
            vector.Embeddings = embeddings;
            vector.MinimumScore = 0.0;
            query.Vector = vector;

            SearchResult result = await client.SearchAsync(_TenantId, _CollectionId, query);
            Console.WriteLine("[Search] Success: " + result.Success);
            Console.WriteLine("[Search] Documents found: " + result.Documents.Count);
            Console.WriteLine("");
        }

        private static async Task CleanupAsync(RecallDbClient client)
        {
            if (!string.IsNullOrEmpty(_CollectionId))
            {
                Console.WriteLine("[Cleanup] Deleting collection: " + _CollectionId);
                await client.DeleteCollectionAsync(_TenantId, _CollectionId);
                Console.WriteLine("[Cleanup] Collection deleted");
            }
        }
    }
}
