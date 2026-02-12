/**
 * RecallDB JavaScript SDK test harness.
 *
 * Usage:
 *   node test-harness.js [endpoint] [token]
 *   node test-harness.js http://localhost:8600 default
 */

const { RecallDbClient, RecallDbException } = require("./recalldb-sdk");

async function main() {
    const endpoint = process.argv[2] || "http://localhost:8600";
    const token = process.argv[3] || "default";
    const tenantId = "default";
    let collectionId = null;
    let documentKey = null;

    console.log("RecallDB JavaScript SDK Test Harness");
    console.log("Endpoint : " + endpoint);
    console.log("Token    : " + token);
    console.log("");

    const client = new RecallDbClient(endpoint, token);

    try {
        // Health
        console.log("[Health] GET /");
        const health = await client.health();
        console.log("[Health] Success: " + (health !== null));
        console.log("");

        // Authenticate
        console.log("[Auth] POST /v1.0/authenticate");
        const authResp = await client.authenticate({ BearerToken: token });
        console.log("[Auth] Success: " + authResp.Success);
        console.log("");

        // Tenants
        console.log("[Tenants] Enumerate");
        const tenants = await client.enumerateTenants();
        console.log("[Tenants] Total: " + tenants.TotalRecords);

        console.log("[Tenants] Get tenant: " + tenantId);
        const tenant = await client.getTenant(tenantId);
        console.log("[Tenants] Name: " + tenant.Name);

        console.log("[Tenants] Exists: " + tenantId);
        const tenantExists = await client.tenantExists(tenantId);
        console.log("[Tenants] Exists: " + tenantExists);
        console.log("");

        // Users
        console.log("[Users] Enumerate");
        const users = await client.enumerateUsers(tenantId);
        console.log("[Users] Total: " + users.TotalRecords);

        console.log("[Users] Get user: default");
        const user = await client.getUser(tenantId, "default");
        console.log("[Users] Email: " + user.Email);

        console.log("[Users] Exists: default");
        const userExists = await client.userExists(tenantId, "default");
        console.log("[Users] Exists: " + userExists);
        console.log("");

        // Credentials
        console.log("[Credentials] Enumerate");
        const creds = await client.enumerateCredentials(tenantId);
        console.log("[Credentials] Total: " + creds.TotalRecords);

        console.log("[Credentials] Get credential: default");
        const cred = await client.getCredential(tenantId, "default");
        console.log("[Credentials] Name: " + cred.Name);

        console.log("[Credentials] Exists: default");
        const credExists = await client.credentialExists(tenantId, "default");
        console.log("[Credentials] Exists: " + credExists);
        console.log("");

        // Collections
        console.log("[Collections] Create");
        let col = await client.createCollection(tenantId, {
            Name: "JS SDK Test Collection",
            Description: "Created by JS SDK test harness",
            Dimensionality: 384
        });
        collectionId = col.Id;
        console.log("[Collections] Created: " + collectionId);

        console.log("[Collections] Get: " + collectionId);
        col = await client.getCollection(tenantId, collectionId);
        console.log("[Collections] Name: " + col.Name);

        console.log("[Collections] Exists: " + collectionId);
        const colExists = await client.collectionExists(tenantId, collectionId);
        console.log("[Collections] Exists: " + colExists);

        console.log("[Collections] Update: " + collectionId);
        col.Description = "Updated by JS SDK test harness";
        col = await client.updateCollection(tenantId, collectionId, col);
        console.log("[Collections] Updated description: " + col.Description);

        console.log("[Collections] Enumerate");
        const collections = await client.enumerateCollections(tenantId);
        console.log("[Collections] Total: " + collections.TotalRecords);
        console.log("");

        // Documents
        const embeddings = Array.from({ length: 384 }, (_, i) => 0.01 * i);

        console.log("[Documents] Create");
        let doc = await client.createDocument(tenantId, collectionId, {
            Content: "This is a test document for the RecallDB JS SDK.",
            ContentType: "Text",
            Embeddings: embeddings
        });
        documentKey = doc.DocumentKey;
        console.log("[Documents] Created: " + documentKey);

        console.log("[Documents] Get: " + documentKey);
        doc = await client.getDocument(tenantId, collectionId, documentKey);
        console.log("[Documents] Content length: " + doc.ContentLength);

        console.log("[Documents] Exists: " + documentKey);
        const docExists = await client.documentExists(tenantId, collectionId, documentKey);
        console.log("[Documents] Exists: " + docExists);

        console.log("[Documents] Enumerate");
        const docs = await client.enumerateDocuments(tenantId, collectionId);
        console.log("[Documents] Total: " + docs.TotalRecords);

        console.log("[Documents] Batch create");
        const batch = [];
        for (let i = 0; i < 3; i++) {
            batch.push({
                Content: "Batch document " + i,
                ContentType: "Text",
                Embeddings: embeddings
            });
        }
        const created = await client.createDocumentBatch(tenantId, collectionId, batch);
        console.log("[Documents] Batch created: " + created.length);
        console.log("");

        // Labels
        console.log("[Labels] Create");
        let label = await client.createLabel(tenantId, collectionId, {
            DocumentKey: documentKey,
            Label: "test-label"
        });
        const labelId = label.Id;
        console.log("[Labels] Created: " + labelId);

        console.log("[Labels] Get: " + labelId);
        const fetchedLabel = await client.getLabel(tenantId, collectionId, labelId);
        console.log("[Labels] Label value: " + fetchedLabel.Label);

        console.log("[Labels] List");
        const labels = await client.listLabels(tenantId, collectionId);
        console.log("[Labels] Total: " + labels.TotalRecords);

        console.log("[Labels] Delete: " + labelId);
        await client.deleteLabel(tenantId, collectionId, labelId);
        console.log("[Labels] Deleted");
        console.log("");

        // Tags
        console.log("[Tags] Create");
        let tag = await client.createTag(tenantId, collectionId, {
            DocumentKey: documentKey,
            Key: "category",
            Value: "test"
        });
        const tagId = tag.Id;
        console.log("[Tags] Created: " + tagId);

        console.log("[Tags] Get: " + tagId);
        const fetchedTag = await client.getTag(tenantId, collectionId, tagId);
        console.log("[Tags] Key: " + fetchedTag.Key + " Value: " + fetchedTag.Value);

        console.log("[Tags] List");
        const tags = await client.listTags(tenantId, collectionId);
        console.log("[Tags] Total: " + tags.TotalRecords);

        console.log("[Tags] Delete: " + tagId);
        await client.deleteTag(tenantId, collectionId, tagId);
        console.log("[Tags] Deleted");
        console.log("");

        // Search
        console.log("[Search] Search collection: " + collectionId);
        const result = await client.search(tenantId, collectionId, {
            SortOrder: "ScoreDescending",
            MaxResults: 10,
            Vector: {
                SearchType: "CosineSimilarity",
                Embeddings: embeddings,
                MinimumScore: 0.0
            }
        });
        console.log("[Search] Success: " + result.Success);
        console.log("[Search] Documents found: " + (result.Documents || []).length);
        console.log("");

    } catch (err) {
        if (err instanceof RecallDbException) {
            console.error("API Error: " + err.message);
        } else {
            console.error("Error: " + err.message);
        }
        process.exit(1);

    } finally {
        // Cleanup
        if (collectionId) {
            console.log("[Cleanup] Deleting collection: " + collectionId);
            try {
                await client.deleteCollection(tenantId, collectionId);
                console.log("[Cleanup] Collection deleted");
            } catch (err) {
                console.error("[Cleanup] Error: " + err.message);
            }
        }
    }

    console.log("");
    console.log("All tests completed.");
}

main();
