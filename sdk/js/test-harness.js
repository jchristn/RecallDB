/**
 * RecallDB JavaScript SDK Integration Test Harness.
 *
 * Mirrors the test cases from src/Test.Automated/Program.cs for consistency
 * across all SDK test harnesses.
 *
 * Usage:
 *   node test-harness.js [endpoint] [api_key]
 *   node test-harness.js http://localhost:8600 recalldbadmin
 */

const { RecallDbClient, RecallDbException } = require("./recalldb-sdk");
const crypto = require("crypto");

// ---------------------------------------------------------------------------
// Globals
// ---------------------------------------------------------------------------

let _endpoint = "http://localhost:8600";
let _apiKey = "recalldbadmin";
let _adminClient = null;
let _userClient = null;
let _passed = 0;
let _failed = 0;
const _failedTests = [];
let _totalStart = null;

// Test data IDs
let _testTenantId = null;
let _testUserId = null;
let _testCredentialId = null;
let _testUserBearerToken = null;
let _testCollectionId = null;
let _testDocumentKey = null;
const _testBatchDocumentKeys = [];
let _testLabelId = null;
let _testTagId = null;
const _paginationTenantIds = [];

// Search / enumeration test dataset
let _searchTestDocKeys = [];
const _searchTestLabelIds = [];
const _searchTestTagIds = [];

const VECTOR_EMBEDDINGS = [0.9, 0.1, 0.05];

// ---------------------------------------------------------------------------
// Test Runner
// ---------------------------------------------------------------------------

async function runTest(name, fn) {
    const start = performance.now();
    try {
        await fn();
        const elapsed = Math.round(performance.now() - start);
        _passed++;
        console.log(`  [PASS] ${name} (${elapsed} ms)`);
    } catch (e) {
        const elapsed = Math.round(performance.now() - start);
        _failed++;
        _failedTests.push(name);
        console.log(`  [FAIL] ${name} (${elapsed} ms)`);
        console.log(`         ${e.constructor.name}: ${e.message}`);
    }
}

// ---------------------------------------------------------------------------
// Assertion Helpers
// ---------------------------------------------------------------------------

function assertTrue(condition, message) {
    if (!condition) throw new Error("Assertion failed: " + message);
}

function assertEqual(expected, actual, name) {
    if (expected !== actual)
        throw new Error("Expected '" + expected + "' but got '" + actual + "' for " + name);
}

function assertNotNull(value, name) {
    if (value == null) throw new Error("Expected non-null value for " + name);
}

function assertNotEmpty(value, name) {
    if (!value) throw new Error("Expected non-null/non-empty value for " + name);
}

function assertGte(value, threshold, name) {
    if (value < threshold)
        throw new Error("Expected " + name + " (" + value + ") >= " + threshold);
}

function assertLte(value, threshold, name) {
    if (value > threshold)
        throw new Error("Expected " + name + " (" + value + ") <= " + threshold);
}

function utcIso(deltaHours) {
    return new Date(Date.now() + deltaHours * 3600000).toISOString();
}

function shortId() {
    return crypto.randomBytes(4).toString("hex");
}

// ---------------------------------------------------------------------------
// Search / Enum helpers
// ---------------------------------------------------------------------------

async function doSearch(query) {
    const resp = await _adminClient.search(_testTenantId, _testCollectionId, query);
    assertTrue(resp.Success, "Search should succeed");
    return resp;
}

async function doEnumDocs(query) {
    const resp = await _adminClient.enumerateDocuments(_testTenantId, _testCollectionId, query);
    assertTrue(resp.Success, "Enumeration should succeed");
    return resp;
}

// ---------------------------------------------------------------------------
// Test-1: Connectivity
// ---------------------------------------------------------------------------

async function testConnectivityGet() {
    const resp = await _adminClient.health();
    assertNotNull(resp, "health response");
    assertEqual("RecallDB", resp.Name, "Name");
}

async function testConnectivityHead() {
    const resp = await fetch(_endpoint + "/", { method: "HEAD", headers: { "Authorization": "Bearer " + _apiKey } });
    assertEqual(200, resp.status, "HEAD / status");
}

// ---------------------------------------------------------------------------
// Test-2: Authentication
// ---------------------------------------------------------------------------

async function testAuthenticateBearer() {
    const resp = await _adminClient.authenticate({ BearerToken: "default" });
    assertTrue(resp.Success, "Authentication should succeed");
}

async function testAuthenticateEmailPassword() {
    const resp = await _adminClient.authenticate({ TenantId: "default", Email: "admin@recall", Password: "password" });
    assertTrue(resp.Success, "Authentication should succeed");
}

// ---------------------------------------------------------------------------
// Test-3: Tenant CRUD
// ---------------------------------------------------------------------------

async function testTenantCreate() {
    const resp = await _adminClient.createTenant({ Name: "IntegrationTestTenant" });
    _testTenantId = resp.Id;
    assertNotEmpty(_testTenantId, "TenantId");
    assertEqual("IntegrationTestTenant", resp.Name, "Name");
}

async function testTenantRead() {
    const resp = await _adminClient.getTenant(_testTenantId);
    assertEqual(_testTenantId, resp.Id, "Id");
}

async function testTenantUpdate() {
    const resp = await _adminClient.updateTenant(_testTenantId, { Name: "IntegrationTestTenantUpdated" });
    assertEqual("IntegrationTestTenantUpdated", resp.Name, "Name");
}

async function testTenantEnumerate() {
    const resp = await _adminClient.enumerateTenants({ MaxResults: 100 });
    assertTrue(resp.Success, "Enumeration should succeed");
    assertTrue(resp.TotalRecords >= 1, "TotalRecords should be >= 1");
}

async function testTenantExists() {
    const result = await _adminClient.tenantExists(_testTenantId);
    assertTrue(result, "Tenant should exist");
}

// ---------------------------------------------------------------------------
// Test-4: User CRUD
// ---------------------------------------------------------------------------

async function testUserCreate() {
    const resp = await _adminClient.createUser(_testTenantId, {
        Email: "testuser@integrationtest.local",
        FirstName: "Test",
        LastName: "User",
        PasswordSha256: "5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8",
        IsAdmin: false,
        IsTenantAdmin: false,
        Active: true
    });
    _testUserId = resp.Id;
    assertNotEmpty(_testUserId, "UserId");
}

async function testUserRead() {
    const resp = await _adminClient.getUser(_testTenantId, _testUserId);
    assertEqual(_testUserId, resp.Id, "Id");
    assertEqual("testuser@integrationtest.local", resp.Email, "Email");
}

async function testUserUpdate() {
    const resp = await _adminClient.updateUser(_testTenantId, _testUserId, {
        Email: "testuser@integrationtest.local",
        FirstName: "TestUpdated",
        LastName: "UserUpdated"
    });
    assertEqual("TestUpdated", resp.FirstName, "FirstName");
}

async function testUserEnumerate() {
    const resp = await _adminClient.enumerateUsers(_testTenantId, { MaxResults: 100 });
    assertTrue(resp.TotalRecords >= 1, "TotalRecords should be >= 1");
}

async function testUserExists() {
    const result = await _adminClient.userExists(_testTenantId, _testUserId);
    assertTrue(result, "User should exist");
}

// ---------------------------------------------------------------------------
// Test-5: Credential CRUD
// ---------------------------------------------------------------------------

async function testCredentialCreate() {
    const resp = await _adminClient.createCredential(_testTenantId, {
        UserId: _testUserId,
        Name: "IntegrationTestCredential",
        Active: true
    });
    _testCredentialId = resp.Id;
    assertNotEmpty(_testCredentialId, "CredentialId");
    _testUserBearerToken = resp.BearerToken;
    assertNotEmpty(_testUserBearerToken, "BearerToken");
    _userClient = new RecallDbClient(_endpoint, _testUserBearerToken);
}

async function testCredentialRead() {
    const resp = await _adminClient.getCredential(_testTenantId, _testCredentialId);
    assertEqual(_testCredentialId, resp.Id, "Id");
}

async function testCredentialEnumerate() {
    const resp = await _adminClient.enumerateCredentials(_testTenantId, { MaxResults: 100 });
    assertTrue(resp.TotalRecords >= 1, "TotalRecords should be >= 1");
}

async function testCredentialExists() {
    const result = await _adminClient.credentialExists(_testTenantId, _testCredentialId);
    assertTrue(result, "Credential should exist");
}

// ---------------------------------------------------------------------------
// Test-6: Collection CRUD
// ---------------------------------------------------------------------------

async function testCollectionCreate() {
    const resp = await _adminClient.createCollection(_testTenantId, {
        Name: "IntegrationTestCollection",
        Description: "Test collection for integration tests",
        Dimensionality: 3
    });
    _testCollectionId = resp.Id;
    assertNotEmpty(_testCollectionId, "CollectionId");
    assertEqual(3, resp.Dimensionality, "Dimensionality");
}

async function testCollectionRead() {
    const resp = await _adminClient.getCollection(_testTenantId, _testCollectionId);
    assertEqual(_testCollectionId, resp.Id, "Id");
}

async function testCollectionUpdate() {
    const resp = await _adminClient.updateCollection(_testTenantId, _testCollectionId, {
        Name: "IntegrationTestCollectionUpdated",
        Description: "Updated description",
        Dimensionality: 3
    });
    assertEqual("IntegrationTestCollectionUpdated", resp.Name, "Name");
}

async function testCollectionEnumerate() {
    const resp = await _adminClient.enumerateCollections(_testTenantId, { MaxResults: 100 });
    assertTrue(resp.TotalRecords >= 1, "TotalRecords should be >= 1");
}

async function testCollectionExists() {
    const result = await _adminClient.collectionExists(_testTenantId, _testCollectionId);
    assertTrue(result, "Collection should exist");
}

// ---------------------------------------------------------------------------
// Test-7: Document CRUD
// ---------------------------------------------------------------------------

async function testDocumentCreate() {
    const docKey = "testdoc-" + shortId();
    const resp = await _adminClient.createDocument(_testTenantId, _testCollectionId, {
        DocumentKey: docKey,
        DocumentId: "testdocid-001",
        Content: "This is a test document for integration testing.",
        ContentType: "Text",
        Position: 0,
        Embeddings: [0.1, 0.2, 0.3]
    });
    _testDocumentKey = resp.DocumentKey;
    assertNotEmpty(_testDocumentKey, "DocumentKey");
}

async function testDocumentRead() {
    const resp = await _adminClient.getDocument(_testTenantId, _testCollectionId, _testDocumentKey);
    assertEqual(_testDocumentKey, resp.DocumentKey, "DocumentKey");
    assertEqual("This is a test document for integration testing.", resp.Content, "Content");
}

async function testDocumentUpdate() {
    const resp = await _adminClient.updateDocument(_testTenantId, _testCollectionId, _testDocumentKey, {
        DocumentKey: _testDocumentKey,
        DocumentId: "testdocid-001",
        Content: "This is an updated test document.",
        ContentType: "Text",
        Position: 0,
        Embeddings: [0.1, 0.2, 0.3]
    });
    assertEqual("This is an updated test document.", resp.Content, "Content");
}

// ---------------------------------------------------------------------------
// Test-8: Document Batch
// ---------------------------------------------------------------------------

async function testDocumentBatchCreate() {
    const docs = [];
    for (let i = 0; i < 3; i++) {
        const batchKey = "batchdoc-" + i + "-" + shortId();
        _testBatchDocumentKeys.push(batchKey);
        const baseVal = (i + 1) * 0.1;
        docs.push({
            DocumentKey: batchKey,
            DocumentId: "batchdocid-" + i,
            Content: "Batch document number " + i,
            ContentType: "Text",
            Position: 0,
            Embeddings: [baseVal, baseVal + 0.1, baseVal + 0.2]
        });
    }
    const resp = await _adminClient.createDocumentBatch(_testTenantId, _testCollectionId, docs);
    assertTrue(Array.isArray(resp), "Response should be an array");
    assertEqual(3, resp.length, "Batch should create 3 documents");

    for (const batchKey of _testBatchDocumentKeys) {
        const doc = await _adminClient.getDocument(_testTenantId, _testCollectionId, batchKey);
        assertNotNull(doc, "Batch doc " + batchKey);
    }
}

// ---------------------------------------------------------------------------
// Test-9: Label CRUD
// ---------------------------------------------------------------------------

async function testLabelCreate() {
    const resp = await _adminClient.createLabel(_testTenantId, _testCollectionId, {
        DocumentKey: _testDocumentKey,
        Label: "integration-test-label"
    });
    _testLabelId = resp.Id;
    assertNotEmpty(_testLabelId, "LabelId");
    assertEqual("integration-test-label", resp.Label, "Label");
}

async function testLabelList() {
    const resp = await _adminClient.listLabels(_testTenantId, _testCollectionId);
    assertTrue(resp.TotalRecords >= 1, "TotalRecords should be >= 1");
}

// ---------------------------------------------------------------------------
// Test-10: Tag CRUD
// ---------------------------------------------------------------------------

async function testTagCreate() {
    const resp = await _adminClient.createTag(_testTenantId, _testCollectionId, {
        DocumentKey: _testDocumentKey,
        Key: "environment",
        Value: "integration-test"
    });
    _testTagId = resp.Id;
    assertNotEmpty(_testTagId, "TagId");
    assertEqual("environment", resp.Key, "Key");
}

async function testTagList() {
    const resp = await _adminClient.listTags(_testTenantId, _testCollectionId);
    assertTrue(resp.TotalRecords >= 1, "TotalRecords should be >= 1");
}

// ---------------------------------------------------------------------------
// Test-11: Search Data Setup
// ---------------------------------------------------------------------------

async function testSearchDataSetup() {
    const docs = [
        { DocumentKey: "srch-doc-0", DocumentId: "grp-alpha", Content: "Machine learning is transforming industries worldwide", ContentType: "Text", Embeddings: [0.9, 0.1, 0.05] },
        { DocumentKey: "srch-doc-1", DocumentId: "grp-alpha", Content: "Deep learning neural networks for image recognition", ContentType: "Code", Embeddings: [0.8, 0.15, 0.1] },
        { DocumentKey: "srch-doc-2", DocumentId: "grp-beta", Content: "Quantum computing breakthroughs in 2024", ContentType: "Text", Embeddings: [0.1, 0.9, 0.05] },
        { DocumentKey: "srch-doc-3", DocumentId: "grp-beta", Content: "Classical physics and thermodynamics overview", ContentType: "Text", Embeddings: [0.05, 0.85, 0.15] },
        { DocumentKey: "srch-doc-4", DocumentId: "grp-gamma", Content: "Cooking recipes from Mediterranean cuisine", ContentType: "Text", Embeddings: [0.1, 0.1, 0.9] },
        { DocumentKey: "srch-doc-5", DocumentId: "grp-gamma", Content: "Travel guide to Southeast Asia for beginners", ContentType: "Text", Embeddings: [0.05, 0.15, 0.85] },
        { DocumentKey: "srch-doc-6", DocumentId: "grp-delta", Content: "Financial markets analysis and stock trading strategies", ContentType: "Text", Embeddings: [0.5, 0.5, 0.1] },
        { DocumentKey: "srch-doc-7", DocumentId: "grp-delta", Content: "Startup funding and venture capital trends", ContentType: "Text", Embeddings: [0.45, 0.55, 0.05] },
        { DocumentKey: "srch-doc-8", DocumentId: "grp-epsilon", Content: "Health benefits of regular exercise and nutrition", ContentType: "Text", Embeddings: [0.3, 0.3, 0.5] },
        { DocumentKey: "srch-doc-9", DocumentId: "grp-epsilon", Content: "Mental health awareness and meditation techniques", ContentType: "Text", Embeddings: [0.25, 0.35, 0.45] }
    ];
    const resp = await _adminClient.createDocumentBatch(_testTenantId, _testCollectionId, docs);
    assertTrue(Array.isArray(resp), "Batch response should be an array");
    assertEqual(10, resp.length, "Batch should create 10 documents");
    _searchTestDocKeys = docs.map(d => d.DocumentKey);

    const labelMap = [
        ["science", "tech"], ["science", "tech", "featured"], ["science"], ["science", "educational"],
        ["lifestyle"], ["lifestyle", "featured"], ["business"], ["business", "featured"],
        ["lifestyle", "science"], ["lifestyle", "educational"]
    ];
    for (let i = 0; i < 10; i++) {
        for (const label of labelMap[i]) {
            const lr = await _adminClient.createLabel(_testTenantId, _testCollectionId, { DocumentKey: _searchTestDocKeys[i], Label: label });
            _searchTestLabelIds.push(lr.Id);
        }
    }

    const tagMap = [
        [["category", "ai"], ["priority", "high"], ["year", "2024"]],
        [["category", "ai"], ["priority", "medium"], ["year", "2024"]],
        [["category", "physics"], ["priority", "high"], ["year", "2024"]],
        [["category", "physics"], ["priority", "low"], ["year", "2023"]],
        [["category", "cooking"], ["priority", "medium"], ["year", "2023"]],
        [["category", "travel"], ["priority", "low"], ["year", "2022"]],
        [["category", "finance"], ["priority", "high"], ["year", "2024"]],
        [["category", "finance"], ["priority", "medium"], ["year", "2023"]],
        [["category", "health"], ["priority", "high"], ["year", "2024"]],
        [["category", "health"], ["priority", "low"], ["year", "2022"]]
    ];
    for (let i = 0; i < 10; i++) {
        for (const [key, value] of tagMap[i]) {
            const tr = await _adminClient.createTag(_testTenantId, _testCollectionId, { DocumentKey: _searchTestDocKeys[i], Key: key, Value: value });
            _searchTestTagIds.push(tr.Id);
        }
    }
}

// ---------------------------------------------------------------------------
// Test-12: Vector Search
// ---------------------------------------------------------------------------

async function testSearchCosineSimilarity() {
    const resp = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, MaxResults: 10 });
    const docs = resp.Documents || [];
    assertTrue(docs.length > 0, "Cosine similarity search should return results");
    let prev = Infinity;
    for (const doc of docs) { assertTrue(doc.Score <= prev, "Cosine similarity results should be ordered by score descending"); prev = doc.Score; }
}

async function testSearchCosineDistance() {
    const resp = await doSearch({ Vector: { SearchType: "CosineDistance", Embeddings: VECTOR_EMBEDDINGS }, SortOrder: "DistanceAscending", MaxResults: 10 });
    const docs = resp.Documents || [];
    assertTrue(docs.length > 0, "Cosine distance search should return results");
    let prev = -1;
    for (const doc of docs) { assertTrue(doc.Score >= prev, "Cosine distance results should be ordered by score ascending"); prev = doc.Score; }
}

async function testSearchEuclideanSimilarity() {
    const resp = await doSearch({ Vector: { SearchType: "EuclideanSimilarity", Embeddings: VECTOR_EMBEDDINGS }, MaxResults: 10 });
    assertTrue((resp.Documents || []).length > 0, "Euclidean similarity search should return results");
    for (const doc of resp.Documents) assertTrue("Score" in doc, "Each document should have a Score property");
}

async function testSearchEuclideanDistance() {
    const resp = await doSearch({ Vector: { SearchType: "EuclideanDistance", Embeddings: VECTOR_EMBEDDINGS }, SortOrder: "DistanceAscending", MaxResults: 10 });
    const docs = resp.Documents || [];
    assertTrue(docs.length > 0, "Euclidean distance search should return results");
    let prev = -1;
    for (const doc of docs) { assertTrue(doc.Score >= prev, "Euclidean distance results should be ordered by score ascending"); prev = doc.Score; }
}

async function testSearchInnerProduct() {
    const resp = await doSearch({ Vector: { SearchType: "InnerProduct", Embeddings: VECTOR_EMBEDDINGS }, MaxResults: 10 });
    assertTrue((resp.Documents || []).length > 0, "Inner product search should return results");
    for (const doc of resp.Documents) assertTrue("Score" in doc, "Each document should have a Score property");
}

// ---------------------------------------------------------------------------
// Test-13: Search Sort Orders
// ---------------------------------------------------------------------------

async function testSearchSortScoreDescending() {
    const resp = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, SortOrder: "ScoreDescending", MaxResults: 10 });
    let prev = Infinity;
    for (const doc of (resp.Documents || [])) { assertTrue(doc.Score <= prev, "ScoreDescending: score[i] >= score[i+1]"); prev = doc.Score; }
}

async function testSearchSortScoreAscending() {
    const resp = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, SortOrder: "ScoreAscending", MaxResults: 10 });
    let prev = -1;
    for (const doc of (resp.Documents || [])) { assertTrue(doc.Score >= prev, "ScoreAscending: score[i] <= score[i+1]"); prev = doc.Score; }
}

async function testSearchSortDistanceAscending() {
    const resp = await doSearch({ Vector: { SearchType: "EuclideanDistance", Embeddings: VECTOR_EMBEDDINGS }, SortOrder: "DistanceAscending", MaxResults: 10 });
    let prev = -1;
    for (const doc of (resp.Documents || [])) { assertTrue(doc.Score >= prev, "DistanceAscending: score[i] <= score[i+1]"); prev = doc.Score; }
}

async function testSearchSortDistanceDescending() {
    const resp = await doSearch({ Vector: { SearchType: "EuclideanDistance", Embeddings: VECTOR_EMBEDDINGS }, SortOrder: "DistanceDescending", MaxResults: 10 });
    let prev = Infinity;
    for (const doc of (resp.Documents || [])) { assertTrue(doc.Score <= prev, "DistanceDescending: score[i] >= score[i+1]"); prev = doc.Score; }
}

async function testSearchSortCreatedAscending() {
    const resp = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, SortOrder: "CreatedAscending", MaxResults: 10 });
    let prev = "";
    for (const doc of (resp.Documents || [])) { assertTrue(doc.CreatedUtc >= prev, "CreatedAscending: CreatedUtc[i] <= CreatedUtc[i+1]"); prev = doc.CreatedUtc; }
}

async function testSearchSortCreatedDescending() {
    const resp = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, SortOrder: "CreatedDescending", MaxResults: 10 });
    let prev = "9999-12-31T23:59:59Z";
    for (const doc of (resp.Documents || [])) { assertTrue(doc.CreatedUtc <= prev, "CreatedDescending: CreatedUtc[i] >= CreatedUtc[i+1]"); prev = doc.CreatedUtc; }
}

// ---------------------------------------------------------------------------
// Test-14: Search Thresholds
// ---------------------------------------------------------------------------

async function testSearchMinimumScore() {
    const resp = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, MinimumScore: 0.9, MaxResults: 10 });
    for (const doc of (resp.Documents || [])) assertGte(doc.Score, 0.9, "Score");
}

async function testSearchMaximumScore() {
    const resp = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, MaximumScore: 0.5, MaxResults: 10 });
    for (const doc of (resp.Documents || [])) assertLte(doc.Score, 0.5, "Score");
}

async function testSearchMinMaxScore() {
    const resp = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, MinimumScore: 0.3, MaximumScore: 0.7, MaxResults: 10 });
    for (const doc of (resp.Documents || [])) { assertGte(doc.Score, 0.3, "Score"); assertLte(doc.Score, 0.7, "Score"); }
}

async function testSearchMinimumDistance() {
    const resp = await doSearch({ Vector: { SearchType: "EuclideanDistance", Embeddings: VECTOR_EMBEDDINGS }, SortOrder: "DistanceAscending", MinimumDistance: 0.5, MaxResults: 10 });
    for (const doc of (resp.Documents || [])) assertGte(doc.Score, 0.5, "Score");
}

async function testSearchMaximumDistance() {
    const resp = await doSearch({ Vector: { SearchType: "EuclideanDistance", Embeddings: VECTOR_EMBEDDINGS }, SortOrder: "DistanceAscending", MaximumDistance: 1.5, MaxResults: 10 });
    for (const doc of (resp.Documents || [])) assertLte(doc.Score, 1.5, "Score");
}

// ---------------------------------------------------------------------------
// Test-15: Search Label Filters
// ---------------------------------------------------------------------------

async function testSearchLabelRequired() {
    const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, LabelFilter: { Required: ["science"] }, MaxResults: 20 });
    assertTrue((r.Documents || []).length > 0, "Label required filter should return results");
}

async function testSearchLabelExcluded() {
    const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, LabelFilter: { Excluded: ["lifestyle"] }, MaxResults: 20 });
    assertTrue((r.Documents || []).length > 0, "Label excluded filter should return results");
}

async function testSearchLabelRequiredMultiple() {
    const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, LabelFilter: { Required: ["science", "tech"] }, MaxResults: 20 });
    assertTrue((r.Documents || []).length > 0, "Label required multiple filter should return results");
}

async function testSearchLabelRequiredAndExcluded() {
    const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, LabelFilter: { Required: ["science"], Excluded: ["tech"] }, MaxResults: 20 });
    assertTrue((r.Documents || []).length > 0, "Label required and excluded filter should return results");
}

async function testSearchLabelNoMatch() {
    const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, LabelFilter: { Required: ["nonexistent-label-xyz"] }, MaxResults: 20 });
    assertEqual(0, (r.Documents || []).length, "Label no match count");
}

// ---------------------------------------------------------------------------
// Test-16: Search Tag Filters
// ---------------------------------------------------------------------------

async function testSearchTagEquals() { const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, TagFilter: { Required: [{ Key: "category", Condition: "Equals", Value: "ai" }] }, MaxResults: 20 }); assertTrue((r.Documents || []).length > 0, "Tag Equals filter should return results"); }
async function testSearchTagNotEquals() { const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, TagFilter: { Required: [{ Key: "category", Condition: "NotEquals", Value: "ai" }] }, MaxResults: 20 }); assertTrue((r.Documents || []).length > 0, "Tag NotEquals filter should return results"); }
async function testSearchTagContains() { const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, TagFilter: { Required: [{ Key: "category", Condition: "Contains", Value: "heal" }] }, MaxResults: 20 }); assertTrue((r.Documents || []).length > 0, "Tag Contains filter should return results"); }
async function testSearchTagContainsNot() { const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, TagFilter: { Required: [{ Key: "category", Condition: "ContainsNot", Value: "ai" }] }, MaxResults: 20 }); assertTrue((r.Documents || []).length > 0, "Tag ContainsNot filter should return results"); }
async function testSearchTagStartsWith() { const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, TagFilter: { Required: [{ Key: "category", Condition: "StartsWith", Value: "fin" }] }, MaxResults: 20 }); assertTrue((r.Documents || []).length > 0, "Tag StartsWith filter should return results"); }
async function testSearchTagEndsWith() { const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, TagFilter: { Required: [{ Key: "category", Condition: "EndsWith", Value: "ics" }] }, MaxResults: 20 }); assertTrue((r.Documents || []).length > 0, "Tag EndsWith filter should return results"); }
async function testSearchTagGreaterThan() { const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, TagFilter: { Required: [{ Key: "year", Condition: "GreaterThan", Value: "2023" }] }, MaxResults: 20 }); assertTrue((r.Documents || []).length > 0, "Tag GreaterThan filter should return results"); }
async function testSearchTagLessThan() { const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, TagFilter: { Required: [{ Key: "year", Condition: "LessThan", Value: "2023" }] }, MaxResults: 20 }); assertTrue((r.Documents || []).length > 0, "Tag LessThan filter should return results"); }
async function testSearchTagIsNotNull() { const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, TagFilter: { Required: [{ Key: "priority", Condition: "IsNotNull" }] }, MaxResults: 20 }); assertTrue((r.Documents || []).length >= 10, "Tag IsNotNull filter should return >= 10 results"); }
async function testSearchTagIsNull() { const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, TagFilter: { Required: [{ Key: "nonexistent-tag-xyz", Condition: "IsNull" }] }, MaxResults: 20 }); assertTrue((r.Documents || []).length >= 10, "Tag IsNull filter should return >= 10 results"); }
async function testSearchTagExcluded() { const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, TagFilter: { Excluded: [{ Key: "priority", Condition: "Equals", Value: "low" }] }, MaxResults: 20 }); assertTrue((r.Documents || []).length > 0, "Tag Excluded filter should return results"); }

// ---------------------------------------------------------------------------
// Test-17: Search Terms Filters
// ---------------------------------------------------------------------------

async function testSearchTermsRequired() { const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, TermsFilter: { Required: ["machine learning"] }, MaxResults: 20 }); assertTrue((r.Documents || []).length > 0, "Terms required filter should return results"); }
async function testSearchTermsExcluded() { const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, TermsFilter: { Excluded: ["quantum"] }, MaxResults: 20 }); assertTrue((r.Documents || []).length > 0, "Terms excluded filter should return results"); }
async function testSearchTermsRequiredMultiple() { const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, TermsFilter: { Required: ["learning", "neural"] }, MaxResults: 20 }); assertTrue((r.Documents || []).length > 0, "Terms required multiple filter should return results"); }
async function testSearchTermsRequiredAndExcluded() { const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, TermsFilter: { Required: ["health"], Excluded: ["meditation"] }, MaxResults: 20 }); assertTrue((r.Documents || []).length > 0, "Terms required and excluded filter should return results"); }
async function testSearchTermsCaseInsensitive() { const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, TermsFilter: { Required: ["MACHINE LEARNING"] }, MaxResults: 20 }); assertTrue((r.Documents || []).length > 0, "Terms case insensitive filter should return results"); }

// ---------------------------------------------------------------------------
// Test-18: Search Date Range
// ---------------------------------------------------------------------------

async function testSearchCreatedAfter() { const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, CreatedAfter: utcIso(-1), MaxResults: 20 }); assertTrue((r.Documents || []).length > 0, "CreatedAfter filter should return results"); }
async function testSearchCreatedBefore() { const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, CreatedBefore: utcIso(1), MaxResults: 20 }); assertTrue((r.Documents || []).length > 0, "CreatedBefore filter should return results"); }
async function testSearchCreatedBeforeNone() { const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, CreatedBefore: utcIso(-1), MaxResults: 20 }); assertEqual(0, (r.Documents || []).length, "CreatedBefore in past result count"); }
async function testSearchDateRangeCombined() { const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, CreatedAfter: utcIso(-1), CreatedBefore: utcIso(1), MaxResults: 20 }); assertTrue((r.Documents || []).length > 0, "Date range combined filter should return results"); }

// ---------------------------------------------------------------------------
// Test-19: Search DocumentIds
// ---------------------------------------------------------------------------

async function testSearchDocumentIds() { const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, DocumentIds: ["grp-alpha", "grp-beta"], MaxResults: 20 }); assertTrue((r.Documents || []).length > 0, "DocumentIds filter should return results"); }
async function testSearchDocumentIdsNoMatch() { const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, DocumentIds: ["nonexistent-docid-xyz"], MaxResults: 20 }); assertEqual(0, (r.Documents || []).length, "DocumentIds no match result count"); }

// ---------------------------------------------------------------------------
// Test-20: Search Pagination
// ---------------------------------------------------------------------------

async function testSearchPagination() {
    let totalFetched = 0, ct = null, eor = false;
    while (!eor) {
        const query = { Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, MaxResults: 3 };
        if (ct) query.ContinuationToken = ct;
        const resp = await doSearch(query);
        eor = resp.EndOfResults;
        totalFetched += (resp.Documents || []).length;
        if (!eor) { ct = resp.ContinuationToken; assertNotEmpty(ct, "ContinuationToken"); }
    }
    assertTrue(totalFetched >= 10, "Should page through all search docs");
}

async function testSearchMaxResults() {
    const resp = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, MaxResults: 2 });
    assertEqual(2, (resp.Documents || []).length, "MaxResults result count");
}

// ---------------------------------------------------------------------------
// Test-21: Search Combined Filters
// ---------------------------------------------------------------------------

async function testSearchCombinedLabelAndTag() {
    const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, LabelFilter: { Required: ["science"] }, TagFilter: { Required: [{ Key: "category", Condition: "Equals", Value: "ai" }] }, MaxResults: 20 });
    assertTrue((r.Documents || []).length > 0, "Combined label and tag filter should return results");
}

async function testSearchCombinedTermsAndLabel() {
    const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, TermsFilter: { Required: ["exercise"] }, LabelFilter: { Required: ["lifestyle"] }, MaxResults: 20 });
    assertTrue((r.Documents || []).length > 0, "Combined terms and label filter should return results");
}

async function testSearchCombinedAllFilters() {
    const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, LabelFilter: { Required: ["science"] }, TagFilter: { Required: [{ Key: "year", Condition: "Equals", Value: "2024" }] }, TermsFilter: { Required: ["learning"] }, CreatedAfter: utcIso(-1), CreatedBefore: utcIso(1), MaxResults: 20 });
    assertTrue((r.Documents || []).length > 0, "Combined all filters should return results");
}

// ---------------------------------------------------------------------------
// Test-22: Search Result Validation
// ---------------------------------------------------------------------------

async function testSearchResultFields() {
    const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, MaxResults: 10 });
    assertTrue(r.Success, "Success should be true");
    assertTrue("MaxResults" in r, "Response should contain MaxResults");
    assertTrue("EndOfResults" in r, "Response should contain EndOfResults");
    assertTrue(r.TotalRecords > 0, "TotalRecords should be > 0");
    assertTrue("RecordsRemaining" in r, "Response should contain RecordsRemaining");
    assertTrue("Documents" in r, "Response should contain Documents");
}

async function testSearchDocumentFields() {
    const r = await doSearch({ Vector: { SearchType: "CosineSimilarity", Embeddings: VECTOR_EMBEDDINGS }, MaxResults: 10 });
    const docs = r.Documents || [];
    assertTrue(docs.length > 0, "Should have at least one document");
    const doc = docs[0];
    assertNotEmpty(doc.DocumentKey, "DocumentKey");
    assertNotEmpty(doc.DocumentId, "DocumentId");
    assertNotEmpty(doc.Content, "Content");
    assertNotEmpty(doc.ContentType, "ContentType");
    assertTrue(doc.ContentLength > 0, "ContentLength should be > 0");
    assertNotEmpty(doc.CreatedUtc, "CreatedUtc");
    assertTrue("Score" in doc, "Document should have Score");
    assertTrue("Labels" in doc, "Document should have Labels");
    assertTrue("Tags" in doc, "Document should have Tags");
}

// ---------------------------------------------------------------------------
// Test-23: Document Enumeration
// ---------------------------------------------------------------------------

async function testEnumDocumentsBasic() { const r = await doEnumDocs({ MaxResults: 100, Ordering: "CreatedDescending" }); assertTrue((r.Objects || []).length >= 10, "Basic enumeration should return >= 10 results"); }

async function testEnumDocumentsCreatedAscending() {
    const r = await doEnumDocs({ MaxResults: 100, Ordering: "CreatedAscending" });
    let prev = "";
    for (const obj of (r.Objects || [])) { assertTrue(obj.CreatedUtc >= prev, "CreatedAscending: CreatedUtc[i] <= CreatedUtc[i+1]"); prev = obj.CreatedUtc; }
}

async function testEnumDocumentsCreatedDescending() {
    const r = await doEnumDocs({ MaxResults: 100, Ordering: "CreatedDescending" });
    let prev = "9999-12-31T23:59:59Z";
    for (const obj of (r.Objects || [])) { assertTrue(obj.CreatedUtc <= prev, "CreatedDescending: CreatedUtc[i] >= CreatedUtc[i+1]"); prev = obj.CreatedUtc; }
}

async function testEnumDocumentsPagination() {
    let totalFetched = 0, ct = null, eor = false;
    while (!eor) {
        const query = { MaxResults: 3, Ordering: "CreatedAscending" };
        if (ct) query.ContinuationToken = ct;
        const r = await doEnumDocs(query);
        eor = r.EndOfResults;
        assertTrue(r.TotalRecords >= 10, "TotalRecords should be >= 10");
        totalFetched += (r.Objects || []).length;
        if (!eor) { ct = r.ContinuationToken; assertNotEmpty(ct, "ContinuationToken"); }
    }
    assertTrue(totalFetched >= 10, "Should page through all enumeration docs");
}

async function testEnumDocumentsMaxResults() { const r = await doEnumDocs({ MaxResults: 2, Ordering: "CreatedDescending" }); assertEqual(2, (r.Objects || []).length, "MaxResults result count"); }
async function testEnumDocumentsCreatedAfter() { const r = await doEnumDocs({ MaxResults: 100, Ordering: "CreatedDescending", CreatedAfter: utcIso(-1) }); assertTrue((r.Objects || []).length > 0, "CreatedAfter filter should return results"); }
async function testEnumDocumentsCreatedBefore() { const r = await doEnumDocs({ MaxResults: 100, Ordering: "CreatedDescending", CreatedBefore: utcIso(1) }); assertTrue((r.Objects || []).length > 0, "CreatedBefore filter should return results"); }
async function testEnumDocumentsDateRangeNone() { const r = await doEnumDocs({ MaxResults: 100, Ordering: "CreatedDescending", CreatedBefore: utcIso(-1) }); assertEqual(0, (r.Objects || []).length, "CreatedBefore in past result count"); }
async function testEnumDocumentsDocumentIds() { const r = await doEnumDocs({ MaxResults: 100, Ordering: "CreatedDescending", DocumentIds: ["grp-alpha", "grp-beta"] }); assertTrue((r.Objects || []).length > 0, "DocumentIds filter should return results"); }
async function testEnumDocumentsDocumentIdsNoMatch() { const r = await doEnumDocs({ MaxResults: 100, Ordering: "CreatedDescending", DocumentIds: ["nonexistent-docid-xyz"] }); assertEqual(0, (r.Objects || []).length, "DocumentIds no match result count"); }
async function testEnumDocumentsLabelRequired() { const r = await doEnumDocs({ MaxResults: 100, Ordering: "CreatedDescending", LabelFilter: { Required: ["science"] } }); assertTrue((r.Objects || []).length > 0, "Label required filter should return results"); }
async function testEnumDocumentsLabelExcluded() { const r = await doEnumDocs({ MaxResults: 100, Ordering: "CreatedDescending", LabelFilter: { Excluded: ["lifestyle"] } }); assertTrue((r.Objects || []).length > 0, "Label excluded filter should return results"); }
async function testEnumDocumentsLabelNoMatch() { const r = await doEnumDocs({ MaxResults: 100, Ordering: "CreatedDescending", LabelFilter: { Required: ["nonexistent-label-xyz"] } }); assertEqual(0, (r.Objects || []).length, "Label no match result count"); }
async function testEnumDocumentsTagEquals() { const r = await doEnumDocs({ MaxResults: 100, Ordering: "CreatedDescending", TagFilter: { Required: [{ Key: "category", Condition: "Equals", Value: "ai" }] } }); assertTrue((r.Objects || []).length > 0, "Tag Equals filter should return results"); }
async function testEnumDocumentsTagNotEquals() { const r = await doEnumDocs({ MaxResults: 100, Ordering: "CreatedDescending", TagFilter: { Required: [{ Key: "category", Condition: "NotEquals", Value: "ai" }] } }); assertTrue((r.Objects || []).length > 0, "Tag NotEquals filter should return results"); }
async function testEnumDocumentsTagContains() { const r = await doEnumDocs({ MaxResults: 100, Ordering: "CreatedDescending", TagFilter: { Required: [{ Key: "category", Condition: "Contains", Value: "heal" }] } }); assertTrue((r.Objects || []).length > 0, "Tag Contains filter should return results"); }
async function testEnumDocumentsTagStartsWith() { const r = await doEnumDocs({ MaxResults: 100, Ordering: "CreatedDescending", TagFilter: { Required: [{ Key: "category", Condition: "StartsWith", Value: "fin" }] } }); assertTrue((r.Objects || []).length > 0, "Tag StartsWith filter should return results"); }
async function testEnumDocumentsTagEndsWith() { const r = await doEnumDocs({ MaxResults: 100, Ordering: "CreatedDescending", TagFilter: { Required: [{ Key: "category", Condition: "EndsWith", Value: "ics" }] } }); assertTrue((r.Objects || []).length > 0, "Tag EndsWith filter should return results"); }
async function testEnumDocumentsTagGreaterThan() { const r = await doEnumDocs({ MaxResults: 100, Ordering: "CreatedDescending", TagFilter: { Required: [{ Key: "year", Condition: "GreaterThan", Value: "2023" }] } }); assertTrue((r.Objects || []).length > 0, "Tag GreaterThan filter should return results"); }
async function testEnumDocumentsTagLessThan() { const r = await doEnumDocs({ MaxResults: 100, Ordering: "CreatedDescending", TagFilter: { Required: [{ Key: "year", Condition: "LessThan", Value: "2023" }] } }); assertTrue((r.Objects || []).length > 0, "Tag LessThan filter should return results"); }
async function testEnumDocumentsTagIsNotNull() { const r = await doEnumDocs({ MaxResults: 100, Ordering: "CreatedDescending", TagFilter: { Required: [{ Key: "priority", Condition: "IsNotNull" }] } }); assertTrue((r.Objects || []).length >= 10, "Tag IsNotNull filter should return >= 10 results"); }
async function testEnumDocumentsTagIsNull() { const r = await doEnumDocs({ MaxResults: 100, Ordering: "CreatedDescending", TagFilter: { Required: [{ Key: "nonexistent-tag-xyz", Condition: "IsNull" }] } }); assertTrue((r.Objects || []).length >= 10, "Tag IsNull filter should return >= 10 results"); }
async function testEnumDocumentsTagContainsNot() { const r = await doEnumDocs({ MaxResults: 100, Ordering: "CreatedDescending", TagFilter: { Required: [{ Key: "category", Condition: "ContainsNot", Value: "ai" }] } }); assertTrue((r.Objects || []).length > 0, "Tag ContainsNot filter should return results"); }
async function testEnumDocumentsTermsRequired() { const r = await doEnumDocs({ MaxResults: 100, Ordering: "CreatedDescending", TermsFilter: { Required: ["machine learning"] } }); assertTrue((r.Objects || []).length > 0, "Terms required filter should return results"); }
async function testEnumDocumentsTermsExcluded() { const r = await doEnumDocs({ MaxResults: 100, Ordering: "CreatedDescending", TermsFilter: { Excluded: ["quantum"] } }); assertTrue((r.Objects || []).length > 0, "Terms excluded filter should return results"); }

async function testEnumDocumentsCombinedLabelAndTag() {
    const r = await doEnumDocs({ MaxResults: 100, Ordering: "CreatedDescending", LabelFilter: { Required: ["science"] }, TagFilter: { Required: [{ Key: "category", Condition: "Equals", Value: "ai" }] } });
    assertTrue((r.Objects || []).length > 0, "Combined label and tag filter should return results");
}

async function testEnumDocumentsCombinedAllFilters() {
    const r = await doEnumDocs({ MaxResults: 100, Ordering: "CreatedDescending", LabelFilter: { Required: ["science"] }, TagFilter: { Required: [{ Key: "year", Condition: "Equals", Value: "2024" }] }, TermsFilter: { Required: ["learning"] }, CreatedAfter: utcIso(-1), CreatedBefore: utcIso(1) });
    assertTrue((r.Objects || []).length > 0, "Combined all filters should return results");
}

async function testEnumDocumentsResultFields() {
    const r = await doEnumDocs({ MaxResults: 100, Ordering: "CreatedDescending" });
    assertTrue(r.Success, "Success should be true");
    assertTrue("MaxResults" in r, "Response should contain MaxResults");
    assertTrue("EndOfResults" in r, "Response should contain EndOfResults");
    assertTrue(r.TotalRecords > 0, "TotalRecords should be > 0");
    assertTrue("RecordsRemaining" in r, "Response should contain RecordsRemaining");
    assertTrue("Objects" in r, "Response should contain Objects");
}

async function testEnumDocumentsObjectFields() {
    const r = await doEnumDocs({ MaxResults: 100, Ordering: "CreatedDescending" });
    const objects = r.Objects || [];
    assertTrue(objects.length > 0, "Should have at least one object");
    const obj = objects[0];
    assertNotEmpty(obj.DocumentKey, "DocumentKey");
    assertNotEmpty(obj.DocumentId, "DocumentId");
    assertNotEmpty(obj.Content, "Content");
    assertNotEmpty(obj.ContentType, "ContentType");
    assertTrue(obj.ContentLength > 0, "ContentLength should be > 0");
    assertNotEmpty(obj.CreatedUtc, "CreatedUtc");
    assertTrue("Labels" in obj, "Object should have Labels");
    assertTrue("Tags" in obj, "Object should have Tags");
}

// ---------------------------------------------------------------------------
// Test-24: Tenant Enumeration Pagination
// ---------------------------------------------------------------------------

async function testEnumerationPagination() {
    const totalToCreate = 5;
    for (let i = 0; i < totalToCreate; i++) {
        const resp = await _adminClient.createTenant({ Name: "PaginationTestTenant_" + i });
        _paginationTenantIds.push(resp.Id);
    }
    let pageSize = 2, totalFetched = 0, ct = null, eor = false, reportedTotal = 0;
    while (!eor) {
        const query = { MaxResults: pageSize, Ordering: "CreatedAscending" };
        if (ct) query.ContinuationToken = ct;
        const resp = await _adminClient.enumerateTenants(query);
        eor = resp.EndOfResults;
        reportedTotal = resp.TotalRecords;
        totalFetched += (resp.Objects || []).length;
        if (!eor) { ct = resp.ContinuationToken; assertNotEmpty(ct, "ContinuationToken"); }
    }
    assertTrue(reportedTotal >= totalToCreate, "TotalRecords should be >= " + totalToCreate);
    assertTrue(totalFetched >= totalToCreate, "Total fetched should be >= " + totalToCreate);
    assertTrue(totalFetched <= reportedTotal, "Total fetched should not exceed TotalRecords");
}

// ---------------------------------------------------------------------------
// Test-25: Authorization
// ---------------------------------------------------------------------------

async function testAuthorizationNonAdmin() {
    assertNotNull(_userClient, "UserClient should be initialized");
    try {
        await _userClient.createTenant({ Name: "UnauthorizedTenantAttempt" });
        throw new Error("Expected 403 Forbidden but call succeeded");
    } catch (e) {
        if (e instanceof RecallDbException) {
            assertEqual(403, e.statusCode, "Status code");
        } else {
            throw e;
        }
    }
}

// ---------------------------------------------------------------------------
// Test-26: Cleanup
// ---------------------------------------------------------------------------

async function testCleanupSearchLabels() { if (!_testCollectionId) return; for (const id of _searchTestLabelIds) await _adminClient.deleteLabel(_testTenantId, _testCollectionId, id); }
async function testCleanupSearchTags() { if (!_testCollectionId) return; for (const id of _searchTestTagIds) await _adminClient.deleteTag(_testTenantId, _testCollectionId, id); }
async function testCleanupSearchDocuments() { if (!_testCollectionId) return; for (const key of _searchTestDocKeys) await _adminClient.deleteDocument(_testTenantId, _testCollectionId, key); }
async function testCleanupLabels() { if (!_testLabelId || !_testCollectionId) return; await _adminClient.deleteLabel(_testTenantId, _testCollectionId, _testLabelId); }
async function testCleanupTags() { if (!_testTagId || !_testCollectionId) return; await _adminClient.deleteTag(_testTenantId, _testCollectionId, _testTagId); }
async function testCleanupDocuments() {
    if (!_testCollectionId) return;
    if (_testDocumentKey) await _adminClient.deleteDocument(_testTenantId, _testCollectionId, _testDocumentKey);
    for (const key of _testBatchDocumentKeys) await _adminClient.deleteDocument(_testTenantId, _testCollectionId, key);
}
async function testCleanupCollection() { if (!_testCollectionId) return; await _adminClient.deleteCollection(_testTenantId, _testCollectionId); }
async function testCleanupCredential() { if (!_testCredentialId) return; await _adminClient.deleteCredential(_testTenantId, _testCredentialId); }
async function testCleanupUser() { if (!_testUserId) return; await _adminClient.deleteUser(_testTenantId, _testUserId); }

async function testCleanupTenant() {
    if (!_testTenantId) return;
    const resp = await fetch(_endpoint + "/v1.0/tenants/" + _testTenantId + "?force", { method: "DELETE", headers: { "Authorization": "Bearer " + _apiKey } });
    if (!resp.ok && resp.status !== 204) throw new Error("Delete tenant failed: " + resp.status);
}

async function testCleanupPaginationTenants() {
    for (const tid of _paginationTenantIds) {
        const resp = await fetch(_endpoint + "/v1.0/tenants/" + tid + "?force", { method: "DELETE", headers: { "Authorization": "Bearer " + _apiKey } });
        if (!resp.ok && resp.status !== 204) throw new Error("Delete tenant failed: " + resp.status);
    }
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

async function main() {
    _endpoint = (process.argv[2] || "http://localhost:8600").replace(/\/+$/, "");
    _apiKey = process.argv[3] || "recalldbadmin";

    console.log("=========================================");
    console.log("  RecallDB Integration Test Harness");
    console.log("  (JavaScript SDK)");
    console.log("=========================================");
    console.log("  Endpoint : " + _endpoint);
    console.log("  API Key  : " + _apiKey);
    console.log("=========================================");
    console.log("");

    _adminClient = new RecallDbClient(_endpoint, _apiKey);
    _totalStart = performance.now();

    // 1. Connectivity
    await runTest("Connectivity: GET /", testConnectivityGet);
    await runTest("Connectivity: HEAD /", testConnectivityHead);

    // 2. Authentication
    await runTest("Authentication: POST /v1.0/authenticate with bearer token", testAuthenticateBearer);
    await runTest("Authentication: POST /v1.0/authenticate with email+password", testAuthenticateEmailPassword);

    // 3. Tenant CRUD
    await runTest("Tenant: PUT create", testTenantCreate);
    await runTest("Tenant: GET read", testTenantRead);
    await runTest("Tenant: PUT update", testTenantUpdate);
    await runTest("Tenant: POST enumerate", testTenantEnumerate);
    await runTest("Tenant: HEAD exists", testTenantExists);

    // 4. User CRUD
    await runTest("User: PUT create", testUserCreate);
    await runTest("User: GET read", testUserRead);
    await runTest("User: PUT update", testUserUpdate);
    await runTest("User: POST enumerate", testUserEnumerate);
    await runTest("User: HEAD exists", testUserExists);

    // 5. Credential CRUD
    await runTest("Credential: PUT create", testCredentialCreate);
    await runTest("Credential: GET read", testCredentialRead);
    await runTest("Credential: POST enumerate", testCredentialEnumerate);
    await runTest("Credential: HEAD exists", testCredentialExists);

    // 6. Collection CRUD
    await runTest("Collection: PUT create", testCollectionCreate);
    await runTest("Collection: GET read", testCollectionRead);
    await runTest("Collection: PUT update", testCollectionUpdate);
    await runTest("Collection: POST enumerate", testCollectionEnumerate);
    await runTest("Collection: HEAD exists", testCollectionExists);

    // 7. Document CRUD
    await runTest("Document: PUT create", testDocumentCreate);
    await runTest("Document: GET read", testDocumentRead);
    await runTest("Document: PUT update", testDocumentUpdate);

    // 8. Document Batch
    await runTest("Document: POST batch create", testDocumentBatchCreate);

    // 9. Label CRUD
    await runTest("Label: PUT create", testLabelCreate);
    await runTest("Label: GET list", testLabelList);

    // 10. Tag CRUD
    await runTest("Tag: PUT create", testTagCreate);
    await runTest("Tag: GET list", testTagList);

    // 11. Search Data Setup
    await runTest("Search data: setup test documents, labels, and tags", testSearchDataSetup);

    // 12. Vector Search
    await runTest("Search: cosine similarity", testSearchCosineSimilarity);
    await runTest("Search: cosine distance", testSearchCosineDistance);
    await runTest("Search: euclidean similarity", testSearchEuclideanSimilarity);
    await runTest("Search: euclidean distance", testSearchEuclideanDistance);
    await runTest("Search: inner product", testSearchInnerProduct);

    // 13. Search Sort Orders
    await runTest("Search sort: score descending", testSearchSortScoreDescending);
    await runTest("Search sort: score ascending", testSearchSortScoreAscending);
    await runTest("Search sort: distance ascending", testSearchSortDistanceAscending);
    await runTest("Search sort: distance descending", testSearchSortDistanceDescending);
    await runTest("Search sort: created ascending", testSearchSortCreatedAscending);
    await runTest("Search sort: created descending", testSearchSortCreatedDescending);

    // 14. Search Thresholds
    await runTest("Search threshold: minimum score", testSearchMinimumScore);
    await runTest("Search threshold: maximum score", testSearchMaximumScore);
    await runTest("Search threshold: min and max score", testSearchMinMaxScore);
    await runTest("Search threshold: minimum distance", testSearchMinimumDistance);
    await runTest("Search threshold: maximum distance", testSearchMaximumDistance);

    // 15. Search Label Filters
    await runTest("Search label: required", testSearchLabelRequired);
    await runTest("Search label: excluded", testSearchLabelExcluded);
    await runTest("Search label: required multiple", testSearchLabelRequiredMultiple);
    await runTest("Search label: required and excluded", testSearchLabelRequiredAndExcluded);
    await runTest("Search label: no match", testSearchLabelNoMatch);

    // 16. Search Tag Filters
    await runTest("Search tag: equals", testSearchTagEquals);
    await runTest("Search tag: not equals", testSearchTagNotEquals);
    await runTest("Search tag: contains", testSearchTagContains);
    await runTest("Search tag: contains not", testSearchTagContainsNot);
    await runTest("Search tag: starts with", testSearchTagStartsWith);
    await runTest("Search tag: ends with", testSearchTagEndsWith);
    await runTest("Search tag: greater than", testSearchTagGreaterThan);
    await runTest("Search tag: less than", testSearchTagLessThan);
    await runTest("Search tag: is not null", testSearchTagIsNotNull);
    await runTest("Search tag: is null", testSearchTagIsNull);
    await runTest("Search tag: excluded", testSearchTagExcluded);

    // 17. Search Terms Filters
    await runTest("Search terms: required", testSearchTermsRequired);
    await runTest("Search terms: excluded", testSearchTermsExcluded);
    await runTest("Search terms: required multiple", testSearchTermsRequiredMultiple);
    await runTest("Search terms: required and excluded", testSearchTermsRequiredAndExcluded);
    await runTest("Search terms: case insensitive", testSearchTermsCaseInsensitive);

    // 18. Search Date Range
    await runTest("Search date: created after", testSearchCreatedAfter);
    await runTest("Search date: created before", testSearchCreatedBefore);
    await runTest("Search date: created before none", testSearchCreatedBeforeNone);
    await runTest("Search date: range combined", testSearchDateRangeCombined);

    // 19. Search DocumentIds
    await runTest("Search docids: filter by document ids", testSearchDocumentIds);
    await runTest("Search docids: no match", testSearchDocumentIdsNoMatch);

    // 20. Search Pagination
    await runTest("Search pagination: page through results", testSearchPagination);
    await runTest("Search pagination: max results", testSearchMaxResults);

    // 21. Search Combined Filters
    await runTest("Search combined: label and tag", testSearchCombinedLabelAndTag);
    await runTest("Search combined: terms and label", testSearchCombinedTermsAndLabel);
    await runTest("Search combined: all filters", testSearchCombinedAllFilters);

    // 22. Search Result Validation
    await runTest("Search validation: result fields", testSearchResultFields);
    await runTest("Search validation: document fields", testSearchDocumentFields);

    // 23. Document Enumeration
    await runTest("Enum docs: basic", testEnumDocumentsBasic);
    await runTest("Enum docs: created ascending", testEnumDocumentsCreatedAscending);
    await runTest("Enum docs: created descending", testEnumDocumentsCreatedDescending);
    await runTest("Enum docs: pagination", testEnumDocumentsPagination);
    await runTest("Enum docs: max results", testEnumDocumentsMaxResults);
    await runTest("Enum docs: created after", testEnumDocumentsCreatedAfter);
    await runTest("Enum docs: created before", testEnumDocumentsCreatedBefore);
    await runTest("Enum docs: date range none", testEnumDocumentsDateRangeNone);
    await runTest("Enum docs: document ids", testEnumDocumentsDocumentIds);
    await runTest("Enum docs: document ids no match", testEnumDocumentsDocumentIdsNoMatch);
    await runTest("Enum docs: label required", testEnumDocumentsLabelRequired);
    await runTest("Enum docs: label excluded", testEnumDocumentsLabelExcluded);
    await runTest("Enum docs: label no match", testEnumDocumentsLabelNoMatch);
    await runTest("Enum docs: tag equals", testEnumDocumentsTagEquals);
    await runTest("Enum docs: tag not equals", testEnumDocumentsTagNotEquals);
    await runTest("Enum docs: tag contains", testEnumDocumentsTagContains);
    await runTest("Enum docs: tag starts with", testEnumDocumentsTagStartsWith);
    await runTest("Enum docs: tag ends with", testEnumDocumentsTagEndsWith);
    await runTest("Enum docs: tag greater than", testEnumDocumentsTagGreaterThan);
    await runTest("Enum docs: tag less than", testEnumDocumentsTagLessThan);
    await runTest("Enum docs: tag is not null", testEnumDocumentsTagIsNotNull);
    await runTest("Enum docs: tag is null", testEnumDocumentsTagIsNull);
    await runTest("Enum docs: tag contains not", testEnumDocumentsTagContainsNot);
    await runTest("Enum docs: terms required", testEnumDocumentsTermsRequired);
    await runTest("Enum docs: terms excluded", testEnumDocumentsTermsExcluded);
    await runTest("Enum docs: combined label and tag", testEnumDocumentsCombinedLabelAndTag);
    await runTest("Enum docs: combined all filters", testEnumDocumentsCombinedAllFilters);
    await runTest("Enum docs: result fields", testEnumDocumentsResultFields);
    await runTest("Enum docs: object fields", testEnumDocumentsObjectFields);

    // 24. Tenant Enumeration Pagination
    await runTest("Enumeration: tenant pagination", testEnumerationPagination);

    // 25. Authorization
    await runTest("Authorization: non-admin cannot create tenant", testAuthorizationNonAdmin);

    // 26. Cleanup
    await runTest("Cleanup: delete search labels", testCleanupSearchLabels);
    await runTest("Cleanup: delete search tags", testCleanupSearchTags);
    await runTest("Cleanup: delete search documents", testCleanupSearchDocuments);
    await runTest("Cleanup: delete labels", testCleanupLabels);
    await runTest("Cleanup: delete tags", testCleanupTags);
    await runTest("Cleanup: delete documents", testCleanupDocuments);
    await runTest("Cleanup: delete collection", testCleanupCollection);
    await runTest("Cleanup: delete credential", testCleanupCredential);
    await runTest("Cleanup: delete user", testCleanupUser);
    await runTest("Cleanup: delete tenant", testCleanupTenant);
    await runTest("Cleanup: delete pagination tenants", testCleanupPaginationTenants);

    const totalElapsed = Math.round(performance.now() - _totalStart);

    console.log("");
    console.log("=========================================");
    console.log("  Test Summary");
    console.log("=========================================");
    console.log("  Total    : " + (_passed + _failed));
    console.log("  Passed   : " + _passed);
    console.log("  Failed   : " + _failed);
    console.log("  Runtime  : " + totalElapsed + " ms");
    console.log("  Result   : " + (_failed === 0 ? "PASS" : "FAIL"));

    if (_failedTests.length > 0) {
        console.log("");
        console.log("  Failed Tests:");
        for (const name of _failedTests) console.log("    - " + name);
    }

    console.log("=========================================");

    process.exit(_failed === 0 ? 0 : 1);
}

main();
