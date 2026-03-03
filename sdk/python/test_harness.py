"""
RecallDB Python SDK Integration Test Harness.

Mirrors the test cases from src/Test.Automated/Program.cs for consistency
across all SDK test harnesses.

Usage:
    python test_harness.py [endpoint] [api_key]
    python test_harness.py http://localhost:8600 recalldbadmin
"""

import argparse
import sys
import time
import uuid
from datetime import datetime, timedelta, timezone

from recalldb_sdk import RecallDbClient, RecallDbException


# ---------------------------------------------------------------------------
# Globals
# ---------------------------------------------------------------------------

_endpoint = "http://localhost:8600"
_api_key = "recalldbadmin"
_admin_client = None
_user_client = None
_passed = 0
_failed = 0
_failed_tests = []
_total_start = None

# Test data IDs
_test_tenant_id = None
_test_user_id = None
_test_credential_id = None
_test_user_bearer_token = None
_test_collection_id = None
_test_document_key = None
_test_batch_document_keys = []
_test_label_id = None
_test_tag_id = None
_pagination_tenant_ids = []

# Search / enumeration test dataset
_search_test_doc_keys = []
_search_test_label_ids = []
_search_test_tag_ids = []


# ---------------------------------------------------------------------------
# Test Runner
# ---------------------------------------------------------------------------

def run_test(name, fn):
    global _passed, _failed
    start = time.perf_counter()
    try:
        fn()
        elapsed = int((time.perf_counter() - start) * 1000)
        _passed += 1
        print(f"  [PASS] {name} ({elapsed} ms)")
    except Exception as e:
        elapsed = int((time.perf_counter() - start) * 1000)
        _failed += 1
        _failed_tests.append(name)
        print(f"  [FAIL] {name} ({elapsed} ms)")
        print(f"         {type(e).__name__}: {e}")


# ---------------------------------------------------------------------------
# Assertion Helpers
# ---------------------------------------------------------------------------

def assert_true(condition, message):
    if not condition:
        raise AssertionError(f"Assertion failed: {message}")


def assert_equal(expected, actual, name):
    if expected != actual:
        raise AssertionError(f"Expected '{expected}' but got '{actual}' for {name}")


def assert_not_none(value, name):
    if value is None:
        raise AssertionError(f"Expected non-null value for {name}")


def assert_not_empty(value, name):
    if not value:
        raise AssertionError(f"Expected non-null/non-empty value for {name}")


def assert_gte(value, threshold, name):
    if value < threshold:
        raise AssertionError(f"Expected {name} ({value}) >= {threshold}")


def assert_lte(value, threshold, name):
    if value > threshold:
        raise AssertionError(f"Expected {name} ({value}) <= {threshold}")


def assert_status(exc_fn, expected_status):
    """Call exc_fn and expect it to raise RecallDbException with given status."""
    try:
        exc_fn()
        raise AssertionError(f"Expected status {expected_status} but call succeeded")
    except RecallDbException as e:
        if e.status_code != expected_status:
            raise AssertionError(
                f"Expected status {expected_status} but got {e.status_code}")


# ---------------------------------------------------------------------------
# Test-1: Connectivity
# ---------------------------------------------------------------------------

def test_connectivity_get():
    resp = _admin_client.health()
    assert_not_none(resp, "health response")
    assert_equal("RecallDB", resp.get("Name"), "Name")


def test_connectivity_head():
    result = _admin_client.tenant_exists("default")
    # HEAD / is done via health endpoint; we simply verify the server is reachable
    # by using a HEAD call to tenants (which is the SDK's HEAD mechanism)
    # The actual connectivity HEAD test is just verifying server responds to HEAD
    import requests
    resp = requests.head(_endpoint + "/")
    assert_equal(200, resp.status_code, "HEAD / status")


# ---------------------------------------------------------------------------
# Test-2: Authentication
# ---------------------------------------------------------------------------

def test_authenticate_bearer():
    resp = _admin_client.authenticate({"BearerToken": "default"})
    assert_true(resp.get("Success"), "Authentication should succeed")


def test_authenticate_email_password():
    resp = _admin_client.authenticate({
        "TenantId": "default",
        "Email": "admin@recall",
        "Password": "password"
    })
    assert_true(resp.get("Success"), "Authentication should succeed")


# ---------------------------------------------------------------------------
# Test-3: Tenant CRUD
# ---------------------------------------------------------------------------

def test_tenant_create():
    global _test_tenant_id
    resp = _admin_client.create_tenant({"Name": "IntegrationTestTenant"})
    _test_tenant_id = resp.get("Id")
    assert_not_empty(_test_tenant_id, "TenantId")
    assert_equal("IntegrationTestTenant", resp.get("Name"), "Name")


def test_tenant_read():
    resp = _admin_client.get_tenant(_test_tenant_id)
    assert_equal(_test_tenant_id, resp.get("Id"), "Id")


def test_tenant_update():
    resp = _admin_client.update_tenant(_test_tenant_id, {"Name": "IntegrationTestTenantUpdated"})
    assert_equal("IntegrationTestTenantUpdated", resp.get("Name"), "Name")


def test_tenant_enumerate():
    resp = _admin_client.enumerate_tenants({"MaxResults": 100})
    assert_true(resp.get("Success"), "Enumeration should succeed")
    assert_true(resp.get("TotalRecords", 0) >= 1, "TotalRecords should be >= 1")


def test_tenant_exists():
    result = _admin_client.tenant_exists(_test_tenant_id)
    assert_true(result, "Tenant should exist")


# ---------------------------------------------------------------------------
# Test-4: User CRUD
# ---------------------------------------------------------------------------

def test_user_create():
    global _test_user_id
    resp = _admin_client.create_user(_test_tenant_id, {
        "Email": "testuser@integrationtest.local",
        "FirstName": "Test",
        "LastName": "User",
        "PasswordSha256": "5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8",
        "IsAdmin": False,
        "IsTenantAdmin": False,
        "Active": True
    })
    _test_user_id = resp.get("Id")
    assert_not_empty(_test_user_id, "UserId")


def test_user_read():
    resp = _admin_client.get_user(_test_tenant_id, _test_user_id)
    assert_equal(_test_user_id, resp.get("Id"), "Id")
    assert_equal("testuser@integrationtest.local", resp.get("Email"), "Email")


def test_user_update():
    resp = _admin_client.update_user(_test_tenant_id, _test_user_id, {
        "Email": "testuser@integrationtest.local",
        "FirstName": "TestUpdated",
        "LastName": "UserUpdated"
    })
    assert_equal("TestUpdated", resp.get("FirstName"), "FirstName")


def test_user_enumerate():
    resp = _admin_client.enumerate_users(_test_tenant_id, {"MaxResults": 100})
    assert_true(resp.get("TotalRecords", 0) >= 1, "TotalRecords should be >= 1")


def test_user_exists():
    result = _admin_client.user_exists(_test_tenant_id, _test_user_id)
    assert_true(result, "User should exist")


# ---------------------------------------------------------------------------
# Test-5: Credential CRUD
# ---------------------------------------------------------------------------

def test_credential_create():
    global _test_credential_id, _test_user_bearer_token, _user_client
    resp = _admin_client.create_credential(_test_tenant_id, {
        "UserId": _test_user_id,
        "Name": "IntegrationTestCredential",
        "Active": True
    })
    _test_credential_id = resp.get("Id")
    assert_not_empty(_test_credential_id, "CredentialId")
    _test_user_bearer_token = resp.get("BearerToken")
    assert_not_empty(_test_user_bearer_token, "BearerToken")
    _user_client = RecallDbClient(_endpoint, _test_user_bearer_token)


def test_credential_read():
    resp = _admin_client.get_credential(_test_tenant_id, _test_credential_id)
    assert_equal(_test_credential_id, resp.get("Id"), "Id")


def test_credential_enumerate():
    resp = _admin_client.enumerate_credentials(_test_tenant_id, {"MaxResults": 100})
    assert_true(resp.get("TotalRecords", 0) >= 1, "TotalRecords should be >= 1")


def test_credential_exists():
    result = _admin_client.credential_exists(_test_tenant_id, _test_credential_id)
    assert_true(result, "Credential should exist")


# ---------------------------------------------------------------------------
# Test-6: Collection CRUD
# ---------------------------------------------------------------------------

def test_collection_create():
    global _test_collection_id
    resp = _admin_client.create_collection(_test_tenant_id, {
        "Name": "IntegrationTestCollection",
        "Description": "Test collection for integration tests",
        "Dimensionality": 3
    })
    _test_collection_id = resp.get("Id")
    assert_not_empty(_test_collection_id, "CollectionId")
    assert_equal(3, resp.get("Dimensionality"), "Dimensionality")


def test_collection_read():
    resp = _admin_client.get_collection(_test_tenant_id, _test_collection_id)
    assert_equal(_test_collection_id, resp.get("Id"), "Id")


def test_collection_update():
    resp = _admin_client.update_collection(_test_tenant_id, _test_collection_id, {
        "Name": "IntegrationTestCollectionUpdated",
        "Description": "Updated description",
        "Dimensionality": 3
    })
    assert_equal("IntegrationTestCollectionUpdated", resp.get("Name"), "Name")


def test_collection_enumerate():
    resp = _admin_client.enumerate_collections(_test_tenant_id, {"MaxResults": 100})
    assert_true(resp.get("TotalRecords", 0) >= 1, "TotalRecords should be >= 1")


def test_collection_exists():
    result = _admin_client.collection_exists(_test_tenant_id, _test_collection_id)
    assert_true(result, "Collection should exist")


# ---------------------------------------------------------------------------
# Test-7: Document CRUD
# ---------------------------------------------------------------------------

def test_document_create():
    global _test_document_key
    doc_key = "testdoc-" + uuid.uuid4().hex[:8]
    resp = _admin_client.create_document(_test_tenant_id, _test_collection_id, {
        "DocumentKey": doc_key,
        "DocumentId": "testdocid-001",
        "Content": "This is a test document for integration testing.",
        "ContentType": "Text",
        "Position": 0,
        "Embeddings": [0.1, 0.2, 0.3]
    })
    _test_document_key = resp.get("DocumentKey")
    assert_not_empty(_test_document_key, "DocumentKey")


def test_document_read():
    resp = _admin_client.get_document(_test_tenant_id, _test_collection_id, _test_document_key)
    assert_equal(_test_document_key, resp.get("DocumentKey"), "DocumentKey")
    assert_equal("This is a test document for integration testing.", resp.get("Content"), "Content")


def test_document_update():
    resp = _admin_client.update_document(_test_tenant_id, _test_collection_id, _test_document_key, {
        "DocumentKey": _test_document_key,
        "DocumentId": "testdocid-001",
        "Content": "This is an updated test document.",
        "ContentType": "Text",
        "Position": 0,
        "Embeddings": [0.1, 0.2, 0.3]
    })
    assert_equal("This is an updated test document.", resp.get("Content"), "Content")


# ---------------------------------------------------------------------------
# Test-8: Document Batch
# ---------------------------------------------------------------------------

def test_document_batch_create():
    global _test_batch_document_keys
    docs = []
    for i in range(3):
        batch_key = f"batchdoc-{i}-{uuid.uuid4().hex[:8]}"
        _test_batch_document_keys.append(batch_key)
        base_val = (i + 1) * 0.1
        docs.append({
            "DocumentKey": batch_key,
            "DocumentId": f"batchdocid-{i}",
            "Content": f"Batch document number {i}",
            "ContentType": "Text",
            "Position": 0,
            "Embeddings": [base_val, base_val + 0.1, base_val + 0.2]
        })
    resp = _admin_client.create_document_batch(_test_tenant_id, _test_collection_id, docs)
    assert_true(isinstance(resp, list), "Response should be a list")
    assert_equal(3, len(resp), "Batch should create 3 documents")

    # Verify each document was created
    for batch_key in _test_batch_document_keys:
        doc = _admin_client.get_document(_test_tenant_id, _test_collection_id, batch_key)
        assert_not_none(doc, f"Batch doc {batch_key}")


# ---------------------------------------------------------------------------
# Test-9: Label CRUD
# ---------------------------------------------------------------------------

def test_label_create():
    global _test_label_id
    resp = _admin_client.create_label(_test_tenant_id, _test_collection_id, {
        "DocumentKey": _test_document_key,
        "Label": "integration-test-label"
    })
    _test_label_id = resp.get("Id")
    assert_not_empty(_test_label_id, "LabelId")
    assert_equal("integration-test-label", resp.get("Label"), "Label")


def test_label_list():
    resp = _admin_client.list_labels(_test_tenant_id, _test_collection_id)
    assert_true(resp.get("TotalRecords", 0) >= 1, "TotalRecords should be >= 1")


# ---------------------------------------------------------------------------
# Test-10: Tag CRUD
# ---------------------------------------------------------------------------

def test_tag_create():
    global _test_tag_id
    resp = _admin_client.create_tag(_test_tenant_id, _test_collection_id, {
        "DocumentKey": _test_document_key,
        "Key": "environment",
        "Value": "integration-test"
    })
    _test_tag_id = resp.get("Id")
    assert_not_empty(_test_tag_id, "TagId")
    assert_equal("environment", resp.get("Key"), "Key")


def test_tag_list():
    resp = _admin_client.list_tags(_test_tenant_id, _test_collection_id)
    assert_true(resp.get("TotalRecords", 0) >= 1, "TotalRecords should be >= 1")


# ---------------------------------------------------------------------------
# Test-11: Search Data Setup
# ---------------------------------------------------------------------------

def test_search_data_setup():
    global _search_test_doc_keys, _search_test_label_ids, _search_test_tag_ids

    # 1. Create 10 documents via batch
    docs = [
        {"DocumentKey": "srch-doc-0", "DocumentId": "grp-alpha", "Content": "Machine learning is transforming industries worldwide", "ContentType": "Text", "Embeddings": [0.9, 0.1, 0.05]},
        {"DocumentKey": "srch-doc-1", "DocumentId": "grp-alpha", "Content": "Deep learning neural networks for image recognition", "ContentType": "Code", "Embeddings": [0.8, 0.15, 0.1]},
        {"DocumentKey": "srch-doc-2", "DocumentId": "grp-beta", "Content": "Quantum computing breakthroughs in 2024", "ContentType": "Text", "Embeddings": [0.1, 0.9, 0.05]},
        {"DocumentKey": "srch-doc-3", "DocumentId": "grp-beta", "Content": "Classical physics and thermodynamics overview", "ContentType": "Text", "Embeddings": [0.05, 0.85, 0.15]},
        {"DocumentKey": "srch-doc-4", "DocumentId": "grp-gamma", "Content": "Cooking recipes from Mediterranean cuisine", "ContentType": "Text", "Embeddings": [0.1, 0.1, 0.9]},
        {"DocumentKey": "srch-doc-5", "DocumentId": "grp-gamma", "Content": "Travel guide to Southeast Asia for beginners", "ContentType": "Text", "Embeddings": [0.05, 0.15, 0.85]},
        {"DocumentKey": "srch-doc-6", "DocumentId": "grp-delta", "Content": "Financial markets analysis and stock trading strategies", "ContentType": "Text", "Embeddings": [0.5, 0.5, 0.1]},
        {"DocumentKey": "srch-doc-7", "DocumentId": "grp-delta", "Content": "Startup funding and venture capital trends", "ContentType": "Text", "Embeddings": [0.45, 0.55, 0.05]},
        {"DocumentKey": "srch-doc-8", "DocumentId": "grp-epsilon", "Content": "Health benefits of regular exercise and nutrition", "ContentType": "Text", "Embeddings": [0.3, 0.3, 0.5]},
        {"DocumentKey": "srch-doc-9", "DocumentId": "grp-epsilon", "Content": "Mental health awareness and meditation techniques", "ContentType": "Text", "Embeddings": [0.25, 0.35, 0.45]},
    ]
    resp = _admin_client.create_document_batch(_test_tenant_id, _test_collection_id, docs)
    assert_true(isinstance(resp, list), "Batch response should be a list")
    assert_equal(10, len(resp), "Batch should create 10 documents")
    _search_test_doc_keys = [f"srch-doc-{i}" for i in range(10)]

    # 2. Create labels
    label_map = [
        ["science", "tech"],
        ["science", "tech", "featured"],
        ["science"],
        ["science", "educational"],
        ["lifestyle"],
        ["lifestyle", "featured"],
        ["business"],
        ["business", "featured"],
        ["lifestyle", "science"],
        ["lifestyle", "educational"],
    ]
    for i in range(10):
        for label in label_map[i]:
            resp = _admin_client.create_label(_test_tenant_id, _test_collection_id, {
                "DocumentKey": _search_test_doc_keys[i],
                "Label": label
            })
            _search_test_label_ids.append(resp.get("Id"))

    # 3. Create tags
    tag_map = [
        [("category", "ai"), ("priority", "high"), ("year", "2024")],
        [("category", "ai"), ("priority", "medium"), ("year", "2024")],
        [("category", "physics"), ("priority", "high"), ("year", "2024")],
        [("category", "physics"), ("priority", "low"), ("year", "2023")],
        [("category", "cooking"), ("priority", "medium"), ("year", "2023")],
        [("category", "travel"), ("priority", "low"), ("year", "2022")],
        [("category", "finance"), ("priority", "high"), ("year", "2024")],
        [("category", "finance"), ("priority", "medium"), ("year", "2023")],
        [("category", "health"), ("priority", "high"), ("year", "2024")],
        [("category", "health"), ("priority", "low"), ("year", "2022")],
    ]
    for i in range(10):
        for key, value in tag_map[i]:
            resp = _admin_client.create_tag(_test_tenant_id, _test_collection_id, {
                "DocumentKey": _search_test_doc_keys[i],
                "Key": key,
                "Value": value
            })
            _search_test_tag_ids.append(resp.get("Id"))


# ---------------------------------------------------------------------------
# Search helpers
# ---------------------------------------------------------------------------

_VECTOR_EMBEDDINGS = [0.9, 0.1, 0.05]


def do_search(query):
    resp = _admin_client.search(_test_tenant_id, _test_collection_id, query)
    assert_true(resp.get("Success"), "Search should succeed")
    return resp


def do_enum_docs(query):
    resp = _admin_client.enumerate_documents(_test_tenant_id, _test_collection_id, query)
    assert_true(resp.get("Success"), "Enumeration should succeed")
    return resp


# ---------------------------------------------------------------------------
# Test-12: Vector Search
# ---------------------------------------------------------------------------

def test_search_cosine_similarity():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "MaxResults": 10})
    docs = resp.get("Documents", [])
    assert_true(len(docs) > 0, "Cosine similarity search should return results")
    prev = float("inf")
    for doc in docs:
        score = doc["Score"]
        assert_true(score <= prev, "Cosine similarity results should be ordered by score descending")
        prev = score


def test_search_cosine_distance():
    resp = do_search({"Vector": {"SearchType": "CosineDistance", "Embeddings": _VECTOR_EMBEDDINGS}, "SortOrder": "DistanceAscending", "MaxResults": 10})
    docs = resp.get("Documents", [])
    assert_true(len(docs) > 0, "Cosine distance search should return results")
    prev = -1.0
    for doc in docs:
        score = doc["Score"]
        assert_true(score >= prev, "Cosine distance results should be ordered by score ascending")
        prev = score


def test_search_euclidean_similarity():
    resp = do_search({"Vector": {"SearchType": "EuclideanSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "MaxResults": 10})
    docs = resp.get("Documents", [])
    assert_true(len(docs) > 0, "Euclidean similarity search should return results")
    for doc in docs:
        assert_true("Score" in doc, "Each document should have a Score property")


def test_search_euclidean_distance():
    resp = do_search({"Vector": {"SearchType": "EuclideanDistance", "Embeddings": _VECTOR_EMBEDDINGS}, "SortOrder": "DistanceAscending", "MaxResults": 10})
    docs = resp.get("Documents", [])
    assert_true(len(docs) > 0, "Euclidean distance search should return results")
    prev = -1.0
    for doc in docs:
        score = doc["Score"]
        assert_true(score >= prev, "Euclidean distance results should be ordered by score ascending")
        prev = score


def test_search_inner_product():
    resp = do_search({"Vector": {"SearchType": "InnerProduct", "Embeddings": _VECTOR_EMBEDDINGS}, "MaxResults": 10})
    docs = resp.get("Documents", [])
    assert_true(len(docs) > 0, "Inner product search should return results")
    for doc in docs:
        assert_true("Score" in doc, "Each document should have a Score property")


# ---------------------------------------------------------------------------
# Test-13: Search Sort Orders
# ---------------------------------------------------------------------------

def test_search_sort_score_descending():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "SortOrder": "ScoreDescending", "MaxResults": 10})
    prev = float("inf")
    for doc in resp.get("Documents", []):
        score = doc["Score"]
        assert_true(score <= prev, "ScoreDescending: score[i] >= score[i+1]")
        prev = score


def test_search_sort_score_ascending():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "SortOrder": "ScoreAscending", "MaxResults": 10})
    prev = -1.0
    for doc in resp.get("Documents", []):
        score = doc["Score"]
        assert_true(score >= prev, "ScoreAscending: score[i] <= score[i+1]")
        prev = score


def test_search_sort_distance_ascending():
    resp = do_search({"Vector": {"SearchType": "EuclideanDistance", "Embeddings": _VECTOR_EMBEDDINGS}, "SortOrder": "DistanceAscending", "MaxResults": 10})
    prev = -1.0
    for doc in resp.get("Documents", []):
        score = doc["Score"]
        assert_true(score >= prev, "DistanceAscending: score[i] <= score[i+1]")
        prev = score


def test_search_sort_distance_descending():
    resp = do_search({"Vector": {"SearchType": "EuclideanDistance", "Embeddings": _VECTOR_EMBEDDINGS}, "SortOrder": "DistanceDescending", "MaxResults": 10})
    prev = float("inf")
    for doc in resp.get("Documents", []):
        score = doc["Score"]
        assert_true(score <= prev, "DistanceDescending: score[i] >= score[i+1]")
        prev = score


def test_search_sort_created_ascending():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "SortOrder": "CreatedAscending", "MaxResults": 10})
    prev = ""
    for doc in resp.get("Documents", []):
        created = doc["CreatedUtc"]
        assert_true(created >= prev, "CreatedAscending: CreatedUtc[i] <= CreatedUtc[i+1]")
        prev = created


def test_search_sort_created_descending():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "SortOrder": "CreatedDescending", "MaxResults": 10})
    prev = "9999-12-31T23:59:59Z"
    for doc in resp.get("Documents", []):
        created = doc["CreatedUtc"]
        assert_true(created <= prev, "CreatedDescending: CreatedUtc[i] >= CreatedUtc[i+1]")
        prev = created


# ---------------------------------------------------------------------------
# Test-14: Search Thresholds
# ---------------------------------------------------------------------------

def test_search_minimum_score():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "MinimumScore": 0.9, "MaxResults": 10})
    for doc in resp.get("Documents", []):
        assert_gte(doc["Score"], 0.9, "Score")


def test_search_maximum_score():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "MaximumScore": 0.5, "MaxResults": 10})
    for doc in resp.get("Documents", []):
        assert_lte(doc["Score"], 0.5, "Score")


def test_search_min_max_score():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "MinimumScore": 0.3, "MaximumScore": 0.7, "MaxResults": 10})
    for doc in resp.get("Documents", []):
        assert_gte(doc["Score"], 0.3, "Score")
        assert_lte(doc["Score"], 0.7, "Score")


def test_search_minimum_distance():
    resp = do_search({"Vector": {"SearchType": "EuclideanDistance", "Embeddings": _VECTOR_EMBEDDINGS}, "SortOrder": "DistanceAscending", "MinimumDistance": 0.5, "MaxResults": 10})
    for doc in resp.get("Documents", []):
        assert_gte(doc["Score"], 0.5, "Score")


def test_search_maximum_distance():
    resp = do_search({"Vector": {"SearchType": "EuclideanDistance", "Embeddings": _VECTOR_EMBEDDINGS}, "SortOrder": "DistanceAscending", "MaximumDistance": 1.5, "MaxResults": 10})
    for doc in resp.get("Documents", []):
        assert_lte(doc["Score"], 1.5, "Score")


# ---------------------------------------------------------------------------
# Test-15: Search Label Filters
# ---------------------------------------------------------------------------

def test_search_label_required():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "LabelFilter": {"Required": ["science"]}, "MaxResults": 20})
    assert_true(len(resp.get("Documents", [])) > 0, "Label required filter should return results")


def test_search_label_excluded():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "LabelFilter": {"Excluded": ["lifestyle"]}, "MaxResults": 20})
    assert_true(len(resp.get("Documents", [])) > 0, "Label excluded filter should return results")


def test_search_label_required_multiple():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "LabelFilter": {"Required": ["science", "tech"]}, "MaxResults": 20})
    assert_true(len(resp.get("Documents", [])) > 0, "Label required multiple filter should return results")


def test_search_label_required_and_excluded():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "LabelFilter": {"Required": ["science"], "Excluded": ["tech"]}, "MaxResults": 20})
    assert_true(len(resp.get("Documents", [])) > 0, "Label required and excluded filter should return results")


def test_search_label_no_match():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "LabelFilter": {"Required": ["nonexistent-label-xyz"]}, "MaxResults": 20})
    assert_equal(0, len(resp.get("Documents", [])), "Label no match count")


# ---------------------------------------------------------------------------
# Test-16: Search Tag Filters
# ---------------------------------------------------------------------------

def test_search_tag_equals():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "TagFilter": {"Required": [{"Key": "category", "Condition": "Equals", "Value": "ai"}]}, "MaxResults": 20})
    assert_true(len(resp.get("Documents", [])) > 0, "Tag Equals filter should return results")


def test_search_tag_not_equals():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "TagFilter": {"Required": [{"Key": "category", "Condition": "NotEquals", "Value": "ai"}]}, "MaxResults": 20})
    assert_true(len(resp.get("Documents", [])) > 0, "Tag NotEquals filter should return results")


def test_search_tag_contains():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "TagFilter": {"Required": [{"Key": "category", "Condition": "Contains", "Value": "heal"}]}, "MaxResults": 20})
    assert_true(len(resp.get("Documents", [])) > 0, "Tag Contains filter should return results")


def test_search_tag_contains_not():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "TagFilter": {"Required": [{"Key": "category", "Condition": "ContainsNot", "Value": "ai"}]}, "MaxResults": 20})
    assert_true(len(resp.get("Documents", [])) > 0, "Tag ContainsNot filter should return results")


def test_search_tag_starts_with():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "TagFilter": {"Required": [{"Key": "category", "Condition": "StartsWith", "Value": "fin"}]}, "MaxResults": 20})
    assert_true(len(resp.get("Documents", [])) > 0, "Tag StartsWith filter should return results")


def test_search_tag_ends_with():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "TagFilter": {"Required": [{"Key": "category", "Condition": "EndsWith", "Value": "ics"}]}, "MaxResults": 20})
    assert_true(len(resp.get("Documents", [])) > 0, "Tag EndsWith filter should return results")


def test_search_tag_greater_than():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "TagFilter": {"Required": [{"Key": "year", "Condition": "GreaterThan", "Value": "2023"}]}, "MaxResults": 20})
    assert_true(len(resp.get("Documents", [])) > 0, "Tag GreaterThan filter should return results")


def test_search_tag_less_than():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "TagFilter": {"Required": [{"Key": "year", "Condition": "LessThan", "Value": "2023"}]}, "MaxResults": 20})
    assert_true(len(resp.get("Documents", [])) > 0, "Tag LessThan filter should return results")


def test_search_tag_is_not_null():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "TagFilter": {"Required": [{"Key": "priority", "Condition": "IsNotNull"}]}, "MaxResults": 20})
    assert_true(len(resp.get("Documents", [])) >= 10, "Tag IsNotNull filter should return >= 10 results")


def test_search_tag_is_null():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "TagFilter": {"Required": [{"Key": "nonexistent-tag-xyz", "Condition": "IsNull"}]}, "MaxResults": 20})
    assert_true(len(resp.get("Documents", [])) >= 10, "Tag IsNull filter should return >= 10 results")


def test_search_tag_excluded():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "TagFilter": {"Excluded": [{"Key": "priority", "Condition": "Equals", "Value": "low"}]}, "MaxResults": 20})
    assert_true(len(resp.get("Documents", [])) > 0, "Tag Excluded filter should return results")


# ---------------------------------------------------------------------------
# Test-17: Search Terms Filters
# ---------------------------------------------------------------------------

def test_search_terms_required():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "TermsFilter": {"Required": ["machine learning"]}, "MaxResults": 20})
    assert_true(len(resp.get("Documents", [])) > 0, "Terms required filter should return results")


def test_search_terms_excluded():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "TermsFilter": {"Excluded": ["quantum"]}, "MaxResults": 20})
    assert_true(len(resp.get("Documents", [])) > 0, "Terms excluded filter should return results")


def test_search_terms_required_multiple():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "TermsFilter": {"Required": ["learning", "neural"]}, "MaxResults": 20})
    assert_true(len(resp.get("Documents", [])) > 0, "Terms required multiple filter should return results")


def test_search_terms_required_and_excluded():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "TermsFilter": {"Required": ["health"], "Excluded": ["meditation"]}, "MaxResults": 20})
    assert_true(len(resp.get("Documents", [])) > 0, "Terms required and excluded filter should return results")


def test_search_terms_case_insensitive():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "TermsFilter": {"Required": ["MACHINE LEARNING"]}, "MaxResults": 20})
    assert_true(len(resp.get("Documents", [])) > 0, "Terms case insensitive filter should return results")


# ---------------------------------------------------------------------------
# Test-18: Search Date Range
# ---------------------------------------------------------------------------

def _utc_iso(delta_hours):
    return (datetime.now(timezone.utc) + timedelta(hours=delta_hours)).strftime("%Y-%m-%dT%H:%M:%S.%fZ")


def test_search_created_after():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "CreatedAfter": _utc_iso(-1), "MaxResults": 20})
    assert_true(len(resp.get("Documents", [])) > 0, "CreatedAfter filter should return results")


def test_search_created_before():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "CreatedBefore": _utc_iso(1), "MaxResults": 20})
    assert_true(len(resp.get("Documents", [])) > 0, "CreatedBefore filter should return results")


def test_search_created_before_none():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "CreatedBefore": _utc_iso(-1), "MaxResults": 20})
    assert_equal(0, len(resp.get("Documents", [])), "CreatedBefore in past result count")


def test_search_date_range_combined():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "CreatedAfter": _utc_iso(-1), "CreatedBefore": _utc_iso(1), "MaxResults": 20})
    assert_true(len(resp.get("Documents", [])) > 0, "Date range combined filter should return results")


# ---------------------------------------------------------------------------
# Test-19: Search DocumentIds
# ---------------------------------------------------------------------------

def test_search_document_ids():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "DocumentIds": ["grp-alpha", "grp-beta"], "MaxResults": 20})
    assert_true(len(resp.get("Documents", [])) > 0, "DocumentIds filter should return results")


def test_search_document_ids_no_match():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "DocumentIds": ["nonexistent-docid-xyz"], "MaxResults": 20})
    assert_equal(0, len(resp.get("Documents", [])), "DocumentIds no match result count")


# ---------------------------------------------------------------------------
# Test-20: Search Pagination
# ---------------------------------------------------------------------------

def test_search_pagination():
    total_fetched = 0
    ct = None
    eor = False
    while not eor:
        query = {"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "MaxResults": 3}
        if ct:
            query["ContinuationToken"] = ct
        resp = do_search(query)
        eor = resp.get("EndOfResults", True)
        total_fetched += len(resp.get("Documents", []))
        if not eor:
            ct = resp.get("ContinuationToken")
            assert_not_empty(ct, "ContinuationToken")
    assert_true(total_fetched >= 10, "Should page through all search docs")


def test_search_max_results():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "MaxResults": 2})
    assert_equal(2, len(resp.get("Documents", [])), "MaxResults result count")


# ---------------------------------------------------------------------------
# Test-21: Search Combined Filters
# ---------------------------------------------------------------------------

def test_search_combined_label_and_tag():
    resp = do_search({
        "Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS},
        "LabelFilter": {"Required": ["science"]},
        "TagFilter": {"Required": [{"Key": "category", "Condition": "Equals", "Value": "ai"}]},
        "MaxResults": 20
    })
    assert_true(len(resp.get("Documents", [])) > 0, "Combined label and tag filter should return results")


def test_search_combined_terms_and_label():
    resp = do_search({
        "Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS},
        "TermsFilter": {"Required": ["exercise"]},
        "LabelFilter": {"Required": ["lifestyle"]},
        "MaxResults": 20
    })
    assert_true(len(resp.get("Documents", [])) > 0, "Combined terms and label filter should return results")


def test_search_combined_all_filters():
    resp = do_search({
        "Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS},
        "LabelFilter": {"Required": ["science"]},
        "TagFilter": {"Required": [{"Key": "year", "Condition": "Equals", "Value": "2024"}]},
        "TermsFilter": {"Required": ["learning"]},
        "CreatedAfter": _utc_iso(-1),
        "CreatedBefore": _utc_iso(1),
        "MaxResults": 20
    })
    assert_true(len(resp.get("Documents", [])) > 0, "Combined all filters should return results")


# ---------------------------------------------------------------------------
# Test-21b: Full-Text Search
# ---------------------------------------------------------------------------

def test_search_full_text_basic():
    resp = do_search({"FullText": {"Query": "machine learning"}, "MaxResults": 10})
    docs = resp.get("Documents", [])
    assert_true(len(docs) > 0, "Full-text search should return results")
    for doc in docs:
        assert_true(doc["Score"] > 0, "Score should be > 0")
        assert_true(doc.get("TextScore") is not None and doc["TextScore"] > 0, "TextScore should be > 0")


def test_search_full_text_hybrid():
    resp = do_search({
        "Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS},
        "FullText": {"Query": "machine learning", "TextWeight": 0.3},
        "MaxResults": 10
    })
    docs = resp.get("Documents", [])
    assert_true(len(docs) > 0, "Hybrid search should return results")
    for doc in docs:
        assert_true(doc.get("TextScore") is not None and doc["TextScore"] > 0, "TextScore should be populated")
        assert_true(doc["Score"] > 0, "Score should be > 0 (blended)")


def test_search_full_text_with_filters():
    resp = do_search({
        "FullText": {"Query": "learning"},
        "LabelFilter": {"Required": ["science"]},
        "Terms": {"Required": ["learning"]},
        "MaxResults": 10
    })
    docs = resp.get("Documents", [])
    assert_true(len(docs) > 0, "Full-text with filters should return results")
    for doc in docs:
        assert_true("science" in doc.get("Labels", []), "Document should have science label")


def test_search_full_text_no_match():
    resp = do_search({"FullText": {"Query": "xyznonexistentterm12345"}, "MaxResults": 10})
    assert_true(resp.get("TotalRecords", 0) == 0, "Should return 0 results")
    assert_true(len(resp.get("Documents", [])) == 0, "Documents list should be empty")


def test_search_full_text_backward_compat():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "MaxResults": 10})
    docs = resp.get("Documents", [])
    assert_true(len(docs) > 0, "Vector-only search should still work")
    for doc in docs:
        assert_true(doc["Score"] > 0, "Score should be > 0")


# ---------------------------------------------------------------------------
# Test-22: Search Result Validation
# ---------------------------------------------------------------------------

def test_search_result_fields():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "MaxResults": 10})
    assert_true(resp.get("Success"), "Success should be true")
    assert_true("MaxResults" in resp, "Response should contain MaxResults")
    assert_true("EndOfResults" in resp, "Response should contain EndOfResults")
    assert_true(resp.get("TotalRecords", 0) > 0, "TotalRecords should be > 0")
    assert_true("RecordsRemaining" in resp, "Response should contain RecordsRemaining")
    assert_true("Documents" in resp, "Response should contain Documents")


def test_search_document_fields():
    resp = do_search({"Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS}, "MaxResults": 10})
    docs = resp.get("Documents", [])
    assert_true(len(docs) > 0, "Should have at least one document")
    doc = docs[0]
    assert_not_empty(doc.get("DocumentKey"), "DocumentKey")
    assert_not_empty(doc.get("DocumentId"), "DocumentId")
    assert_not_empty(doc.get("Content"), "Content")
    assert_not_empty(doc.get("ContentType"), "ContentType")
    assert_true(doc.get("ContentLength", 0) > 0, "ContentLength should be > 0")
    assert_not_empty(doc.get("CreatedUtc"), "CreatedUtc")
    assert_true("Score" in doc, "Document should have Score")
    assert_true("Labels" in doc, "Document should have Labels")
    assert_true("Tags" in doc, "Document should have Tags")


# ---------------------------------------------------------------------------
# Test-22b: Neighbor Retrieval
# ---------------------------------------------------------------------------

def test_neighbor_data_setup():
    """Create multi-chunk documents for neighbor retrieval testing."""
    docs = [
        {"DocumentKey": "nbr-doc-0", "DocumentId": "nbr-group-a", "Position": 0, "Content": "Chapter one introduction to the topic", "ContentType": "Text", "Embeddings": [0.9, 0.1, 0.05]},
        {"DocumentKey": "nbr-doc-1", "DocumentId": "nbr-group-a", "Position": 1, "Content": "Chapter two background and context", "ContentType": "Text", "Embeddings": [0.85, 0.12, 0.08]},
        {"DocumentKey": "nbr-doc-2", "DocumentId": "nbr-group-a", "Position": 2, "Content": "Chapter three core methodology explained", "ContentType": "Text", "Embeddings": [0.8, 0.15, 0.1]},
        {"DocumentKey": "nbr-doc-3", "DocumentId": "nbr-group-a", "Position": 3, "Content": "Chapter four results and analysis", "ContentType": "Text", "Embeddings": [0.75, 0.18, 0.12]},
        {"DocumentKey": "nbr-doc-4", "DocumentId": "nbr-group-a", "Position": 4, "Content": "Chapter five conclusion and future work", "ContentType": "Text", "Embeddings": [0.7, 0.2, 0.15]},
        {"DocumentKey": "nbr-doc-5", "DocumentId": "nbr-group-b", "Position": 0, "Content": "Standalone single chunk document", "ContentType": "Text", "Embeddings": [0.6, 0.3, 0.2]},
    ]
    _admin_client.create_document_batch(_test_tenant_id, _test_collection_id, docs)


def test_neighbor_search_with_neighbors():
    """Search with IncludeNeighbors set, assert Neighbors present in response documents."""
    resp = do_search({
        "Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS},
        "MaxResults": 1,
        "IncludeNeighbors": 2,
        "DocumentIds": ["nbr-group-a"]
    })
    docs = resp.get("Documents", [])
    assert_true(len(docs) > 0, "Should have at least one result")
    doc = docs[0]
    assert_true("Neighbors" in doc, "Document should have Neighbors when IncludeNeighbors is set")
    assert_true(doc["Neighbors"] is not None, "Neighbors should not be null")
    assert_true(len(doc["Neighbors"]) > 0, "Neighbors should not be empty")
    # Verify ordering
    positions = [nb["Position"] for nb in doc["Neighbors"]]
    assert_true(positions == sorted(positions), "Neighbors should be ordered by Position ascending")
    # Verify matched chunk not in neighbors
    for nb in doc["Neighbors"]:
        assert_true(nb["Position"] != doc["Position"], "Neighbor should not be the matched chunk itself")


def test_neighbor_search_without_neighbors():
    """Search without IncludeNeighbors, assert Neighbors is absent or null."""
    resp = do_search({
        "Vector": {"SearchType": "CosineSimilarity", "Embeddings": _VECTOR_EMBEDDINGS},
        "MaxResults": 1
    })
    docs = resp.get("Documents", [])
    assert_true(len(docs) > 0, "Should have at least one result")
    doc = docs[0]
    neighbors = doc.get("Neighbors")
    assert_true(neighbors is None, "Neighbors should be null when IncludeNeighbors is not set")


# ---------------------------------------------------------------------------
# Test-23: Document Enumeration
# ---------------------------------------------------------------------------

def test_enum_documents_basic():
    resp = do_enum_docs({"MaxResults": 100, "Ordering": "CreatedDescending"})
    assert_true(len(resp.get("Objects", [])) >= 10, "Basic enumeration should return >= 10 results")


def test_enum_documents_created_ascending():
    resp = do_enum_docs({"MaxResults": 100, "Ordering": "CreatedAscending"})
    prev = ""
    for obj in resp.get("Objects", []):
        created = obj["CreatedUtc"]
        assert_true(created >= prev, "CreatedAscending: CreatedUtc[i] <= CreatedUtc[i+1]")
        prev = created


def test_enum_documents_created_descending():
    resp = do_enum_docs({"MaxResults": 100, "Ordering": "CreatedDescending"})
    prev = "9999-12-31T23:59:59Z"
    for obj in resp.get("Objects", []):
        created = obj["CreatedUtc"]
        assert_true(created <= prev, "CreatedDescending: CreatedUtc[i] >= CreatedUtc[i+1]")
        prev = created


def test_enum_documents_pagination():
    total_fetched = 0
    ct = None
    eor = False
    while not eor:
        query = {"MaxResults": 3, "Ordering": "CreatedAscending"}
        if ct:
            query["ContinuationToken"] = ct
        resp = do_enum_docs(query)
        eor = resp.get("EndOfResults", True)
        assert_true(resp.get("TotalRecords", 0) >= 10, "TotalRecords should be >= 10")
        total_fetched += len(resp.get("Objects", []))
        if not eor:
            ct = resp.get("ContinuationToken")
            assert_not_empty(ct, "ContinuationToken")
    assert_true(total_fetched >= 10, "Should page through all enumeration docs")


def test_enum_documents_max_results():
    resp = do_enum_docs({"MaxResults": 2, "Ordering": "CreatedDescending"})
    assert_equal(2, len(resp.get("Objects", [])), "MaxResults result count")


def test_enum_documents_created_after():
    resp = do_enum_docs({"MaxResults": 100, "Ordering": "CreatedDescending", "CreatedAfter": _utc_iso(-1)})
    assert_true(len(resp.get("Objects", [])) > 0, "CreatedAfter filter should return results")


def test_enum_documents_created_before():
    resp = do_enum_docs({"MaxResults": 100, "Ordering": "CreatedDescending", "CreatedBefore": _utc_iso(1)})
    assert_true(len(resp.get("Objects", [])) > 0, "CreatedBefore filter should return results")


def test_enum_documents_date_range_none():
    resp = do_enum_docs({"MaxResults": 100, "Ordering": "CreatedDescending", "CreatedBefore": _utc_iso(-1)})
    assert_equal(0, len(resp.get("Objects", [])), "CreatedBefore in past result count")


def test_enum_documents_document_ids():
    resp = do_enum_docs({"MaxResults": 100, "Ordering": "CreatedDescending", "DocumentIds": ["grp-alpha", "grp-beta"]})
    assert_true(len(resp.get("Objects", [])) > 0, "DocumentIds filter should return results")


def test_enum_documents_document_ids_no_match():
    resp = do_enum_docs({"MaxResults": 100, "Ordering": "CreatedDescending", "DocumentIds": ["nonexistent-docid-xyz"]})
    assert_equal(0, len(resp.get("Objects", [])), "DocumentIds no match result count")


def test_enum_documents_label_required():
    resp = do_enum_docs({"MaxResults": 100, "Ordering": "CreatedDescending", "LabelFilter": {"Required": ["science"]}})
    assert_true(len(resp.get("Objects", [])) > 0, "Label required filter should return results")


def test_enum_documents_label_excluded():
    resp = do_enum_docs({"MaxResults": 100, "Ordering": "CreatedDescending", "LabelFilter": {"Excluded": ["lifestyle"]}})
    assert_true(len(resp.get("Objects", [])) > 0, "Label excluded filter should return results")


def test_enum_documents_label_no_match():
    resp = do_enum_docs({"MaxResults": 100, "Ordering": "CreatedDescending", "LabelFilter": {"Required": ["nonexistent-label-xyz"]}})
    assert_equal(0, len(resp.get("Objects", [])), "Label no match result count")


def test_enum_documents_tag_equals():
    resp = do_enum_docs({"MaxResults": 100, "Ordering": "CreatedDescending", "TagFilter": {"Required": [{"Key": "category", "Condition": "Equals", "Value": "ai"}]}})
    assert_true(len(resp.get("Objects", [])) > 0, "Tag Equals filter should return results")


def test_enum_documents_tag_not_equals():
    resp = do_enum_docs({"MaxResults": 100, "Ordering": "CreatedDescending", "TagFilter": {"Required": [{"Key": "category", "Condition": "NotEquals", "Value": "ai"}]}})
    assert_true(len(resp.get("Objects", [])) > 0, "Tag NotEquals filter should return results")


def test_enum_documents_tag_contains():
    resp = do_enum_docs({"MaxResults": 100, "Ordering": "CreatedDescending", "TagFilter": {"Required": [{"Key": "category", "Condition": "Contains", "Value": "heal"}]}})
    assert_true(len(resp.get("Objects", [])) > 0, "Tag Contains filter should return results")


def test_enum_documents_tag_starts_with():
    resp = do_enum_docs({"MaxResults": 100, "Ordering": "CreatedDescending", "TagFilter": {"Required": [{"Key": "category", "Condition": "StartsWith", "Value": "fin"}]}})
    assert_true(len(resp.get("Objects", [])) > 0, "Tag StartsWith filter should return results")


def test_enum_documents_tag_ends_with():
    resp = do_enum_docs({"MaxResults": 100, "Ordering": "CreatedDescending", "TagFilter": {"Required": [{"Key": "category", "Condition": "EndsWith", "Value": "ics"}]}})
    assert_true(len(resp.get("Objects", [])) > 0, "Tag EndsWith filter should return results")


def test_enum_documents_tag_greater_than():
    resp = do_enum_docs({"MaxResults": 100, "Ordering": "CreatedDescending", "TagFilter": {"Required": [{"Key": "year", "Condition": "GreaterThan", "Value": "2023"}]}})
    assert_true(len(resp.get("Objects", [])) > 0, "Tag GreaterThan filter should return results")


def test_enum_documents_tag_less_than():
    resp = do_enum_docs({"MaxResults": 100, "Ordering": "CreatedDescending", "TagFilter": {"Required": [{"Key": "year", "Condition": "LessThan", "Value": "2023"}]}})
    assert_true(len(resp.get("Objects", [])) > 0, "Tag LessThan filter should return results")


def test_enum_documents_tag_is_not_null():
    resp = do_enum_docs({"MaxResults": 100, "Ordering": "CreatedDescending", "TagFilter": {"Required": [{"Key": "priority", "Condition": "IsNotNull"}]}})
    assert_true(len(resp.get("Objects", [])) >= 10, "Tag IsNotNull filter should return >= 10 results")


def test_enum_documents_tag_is_null():
    resp = do_enum_docs({"MaxResults": 100, "Ordering": "CreatedDescending", "TagFilter": {"Required": [{"Key": "nonexistent-tag-xyz", "Condition": "IsNull"}]}})
    assert_true(len(resp.get("Objects", [])) >= 10, "Tag IsNull filter should return >= 10 results")


def test_enum_documents_tag_contains_not():
    resp = do_enum_docs({"MaxResults": 100, "Ordering": "CreatedDescending", "TagFilter": {"Required": [{"Key": "category", "Condition": "ContainsNot", "Value": "ai"}]}})
    assert_true(len(resp.get("Objects", [])) > 0, "Tag ContainsNot filter should return results")


def test_enum_documents_terms_required():
    resp = do_enum_docs({"MaxResults": 100, "Ordering": "CreatedDescending", "TermsFilter": {"Required": ["machine learning"]}})
    assert_true(len(resp.get("Objects", [])) > 0, "Terms required filter should return results")


def test_enum_documents_terms_excluded():
    resp = do_enum_docs({"MaxResults": 100, "Ordering": "CreatedDescending", "TermsFilter": {"Excluded": ["quantum"]}})
    assert_true(len(resp.get("Objects", [])) > 0, "Terms excluded filter should return results")


def test_enum_documents_combined_label_and_tag():
    resp = do_enum_docs({
        "MaxResults": 100, "Ordering": "CreatedDescending",
        "LabelFilter": {"Required": ["science"]},
        "TagFilter": {"Required": [{"Key": "category", "Condition": "Equals", "Value": "ai"}]}
    })
    assert_true(len(resp.get("Objects", [])) > 0, "Combined label and tag filter should return results")


def test_enum_documents_combined_all_filters():
    resp = do_enum_docs({
        "MaxResults": 100, "Ordering": "CreatedDescending",
        "LabelFilter": {"Required": ["science"]},
        "TagFilter": {"Required": [{"Key": "year", "Condition": "Equals", "Value": "2024"}]},
        "TermsFilter": {"Required": ["learning"]},
        "CreatedAfter": _utc_iso(-1),
        "CreatedBefore": _utc_iso(1)
    })
    assert_true(len(resp.get("Objects", [])) > 0, "Combined all filters should return results")


def test_enum_documents_result_fields():
    resp = do_enum_docs({"MaxResults": 100, "Ordering": "CreatedDescending"})
    assert_true(resp.get("Success"), "Success should be true")
    assert_true("MaxResults" in resp, "Response should contain MaxResults")
    assert_true("EndOfResults" in resp, "Response should contain EndOfResults")
    assert_true(resp.get("TotalRecords", 0) > 0, "TotalRecords should be > 0")
    assert_true("RecordsRemaining" in resp, "Response should contain RecordsRemaining")
    assert_true("Objects" in resp, "Response should contain Objects")


def test_enum_documents_object_fields():
    resp = do_enum_docs({"MaxResults": 100, "Ordering": "CreatedDescending"})
    objects = resp.get("Objects", [])
    assert_true(len(objects) > 0, "Should have at least one object")
    obj = objects[0]
    assert_not_empty(obj.get("DocumentKey"), "DocumentKey")
    assert_not_empty(obj.get("DocumentId"), "DocumentId")
    assert_not_empty(obj.get("Content"), "Content")
    assert_not_empty(obj.get("ContentType"), "ContentType")
    assert_true(obj.get("ContentLength", 0) > 0, "ContentLength should be > 0")
    assert_not_empty(obj.get("CreatedUtc"), "CreatedUtc")
    assert_true("Labels" in obj, "Object should have Labels")
    assert_true("Tags" in obj, "Object should have Tags")


# ---------------------------------------------------------------------------
# Test-24: Tenant Enumeration Pagination
# ---------------------------------------------------------------------------

def test_enumeration_pagination():
    global _pagination_tenant_ids
    total_to_create = 5

    for i in range(total_to_create):
        resp = _admin_client.create_tenant({"Name": f"PaginationTestTenant_{i}"})
        _pagination_tenant_ids.append(resp.get("Id"))

    page_size = 2
    total_fetched = 0
    ct = None
    eor = False
    reported_total = 0

    while not eor:
        query = {"MaxResults": page_size, "Ordering": "CreatedAscending"}
        if ct:
            query["ContinuationToken"] = ct
        resp = _admin_client.enumerate_tenants(query)
        eor = resp.get("EndOfResults", True)
        reported_total = resp.get("TotalRecords", 0)
        total_fetched += len(resp.get("Objects", []))
        if not eor:
            ct = resp.get("ContinuationToken")
            assert_not_empty(ct, "ContinuationToken")

    assert_true(reported_total >= total_to_create, f"TotalRecords should be >= {total_to_create}")
    assert_true(total_fetched >= total_to_create, f"Total fetched should be >= {total_to_create}")
    assert_true(total_fetched <= reported_total, "Total fetched should not exceed TotalRecords")


# ---------------------------------------------------------------------------
# Test-25: Authorization
# ---------------------------------------------------------------------------

def test_authorization_non_admin():
    assert_not_none(_user_client, "UserClient should be initialized")
    try:
        _user_client.create_tenant({"Name": "UnauthorizedTenantAttempt"})
        raise AssertionError("Expected 403 Forbidden but call succeeded")
    except RecallDbException as e:
        assert_equal(403, e.status_code, "Status code")


# ---------------------------------------------------------------------------
# Test-25b: Batch Delete
# ---------------------------------------------------------------------------

def test_batch_delete_by_keys():
    # Create batch documents for deletion
    keys_to_delete = []
    docs = []
    for i in range(3):
        key = f"batchdel-{i}-{uuid.uuid4().hex[:8]}"
        keys_to_delete.append(key)
        base_val = (i + 1) * 0.1
        docs.append({
            "DocumentKey": key,
            "DocumentId": "batchdel-docid",
            "Content": f"Batch delete test document {i}",
            "ContentType": "Text",
            "Position": 0,
            "Embeddings": [base_val, base_val + 0.1, base_val + 0.2]
        })
    _admin_client.create_document_batch(_test_tenant_id, _test_collection_id, docs)

    # Verify documents exist
    for key in keys_to_delete:
        exists = _admin_client.document_exists(_test_tenant_id, _test_collection_id, key)
        assert_true(exists, f"Document {key} should exist before batch delete")

    # Batch delete by keys
    _admin_client.delete_document_batch(_test_tenant_id, _test_collection_id, keys_to_delete)

    # Verify documents no longer exist
    for key in keys_to_delete:
        exists = _admin_client.document_exists(_test_tenant_id, _test_collection_id, key)
        assert_true(not exists, f"Document {key} should not exist after batch delete")


def test_delete_by_filter():
    # Create documents with a specific DocumentId for filter deletion
    filter_doc_id = f"filterdel-{uuid.uuid4().hex[:8]}"
    keys_to_delete = []
    docs = []
    for i in range(3):
        key = f"filterdel-{i}-{uuid.uuid4().hex[:8]}"
        keys_to_delete.append(key)
        base_val = (i + 1) * 0.1
        docs.append({
            "DocumentKey": key,
            "DocumentId": filter_doc_id,
            "Content": f"Filter delete test document {i}",
            "ContentType": "Text",
            "Position": 0,
            "Embeddings": [base_val, base_val + 0.1, base_val + 0.2]
        })
    _admin_client.create_document_batch(_test_tenant_id, _test_collection_id, docs)

    # Verify documents exist
    for key in keys_to_delete:
        exists = _admin_client.document_exists(_test_tenant_id, _test_collection_id, key)
        assert_true(exists, f"Document {key} should exist before filter delete")

    # Delete by filter using DocumentIds
    result = _admin_client.delete_documents_by_filter(
        _test_tenant_id, _test_collection_id, {"DocumentIds": [filter_doc_id]})
    assert_equal(3, result.get("DocumentsDeleted", 0), "DocumentsDeleted count")

    # Verify documents no longer exist
    for key in keys_to_delete:
        exists = _admin_client.document_exists(_test_tenant_id, _test_collection_id, key)
        assert_true(not exists, f"Document {key} should not exist after filter delete")


# ---------------------------------------------------------------------------
# Test-26: Cleanup
# ---------------------------------------------------------------------------

def test_cleanup_search_labels():
    if not _test_collection_id:
        return
    for label_id in _search_test_label_ids:
        _admin_client.delete_label(_test_tenant_id, _test_collection_id, label_id)


def test_cleanup_search_tags():
    if not _test_collection_id:
        return
    for tag_id in _search_test_tag_ids:
        _admin_client.delete_tag(_test_tenant_id, _test_collection_id, tag_id)


def test_cleanup_search_documents():
    if not _test_collection_id:
        return
    for doc_key in _search_test_doc_keys:
        _admin_client.delete_document(_test_tenant_id, _test_collection_id, doc_key)


def test_cleanup_labels():
    if not _test_label_id or not _test_collection_id:
        return
    _admin_client.delete_label(_test_tenant_id, _test_collection_id, _test_label_id)


def test_cleanup_tags():
    if not _test_tag_id or not _test_collection_id:
        return
    _admin_client.delete_tag(_test_tenant_id, _test_collection_id, _test_tag_id)


def test_cleanup_documents():
    if not _test_collection_id:
        return
    if _test_document_key:
        _admin_client.delete_document(_test_tenant_id, _test_collection_id, _test_document_key)
    if _test_batch_document_keys:
        _admin_client.delete_document_batch(_test_tenant_id, _test_collection_id, _test_batch_document_keys)


def test_cleanup_collection():
    if not _test_collection_id:
        return
    _admin_client.delete_collection(_test_tenant_id, _test_collection_id)


def test_cleanup_credential():
    if not _test_credential_id:
        return
    _admin_client.delete_credential(_test_tenant_id, _test_credential_id)


def test_cleanup_user():
    if not _test_user_id:
        return
    _admin_client.delete_user(_test_tenant_id, _test_user_id)


def test_cleanup_tenant():
    if not _test_tenant_id:
        return
    # Use raw request to pass ?force query param
    import requests
    resp = requests.delete(
        f"{_endpoint}/v1.0/tenants/{_test_tenant_id}?force",
        headers={"Authorization": f"Bearer {_api_key}"})
    if not resp.ok and resp.status_code != 204:
        raise RecallDbException(resp.status_code, resp.text)


def test_cleanup_pagination_tenants():
    import requests
    for tid in _pagination_tenant_ids:
        resp = requests.delete(
            f"{_endpoint}/v1.0/tenants/{tid}?force",
            headers={"Authorization": f"Bearer {_api_key}"})
        if not resp.ok and resp.status_code != 204:
            raise RecallDbException(resp.status_code, resp.text)


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    global _endpoint, _api_key, _admin_client, _total_start

    parser = argparse.ArgumentParser(description="RecallDB SDK Integration Test Harness")
    parser.add_argument("endpoint", nargs="?", default="http://localhost:8600", help="RecallDB server endpoint")
    parser.add_argument("api_key", nargs="?", default="recalldbadmin", help="Admin API key / bearer token")
    args = parser.parse_args()

    _endpoint = args.endpoint.rstrip("/")
    _api_key = args.api_key

    print("=========================================")
    print("  RecallDB Integration Test Harness")
    print("  (Python SDK)")
    print("=========================================")
    print(f"  Endpoint : {_endpoint}")
    print(f"  API Key  : {_api_key}")
    print("=========================================")
    print()

    _admin_client = RecallDbClient(_endpoint, _api_key)
    _total_start = time.perf_counter()

    # 1. Connectivity
    run_test("Connectivity: GET /", test_connectivity_get)
    run_test("Connectivity: HEAD /", test_connectivity_head)

    # 2. Authentication
    run_test("Authentication: POST /v1.0/authenticate with bearer token", test_authenticate_bearer)
    run_test("Authentication: POST /v1.0/authenticate with email+password", test_authenticate_email_password)

    # 3. Tenant CRUD
    run_test("Tenant: PUT create", test_tenant_create)
    run_test("Tenant: GET read", test_tenant_read)
    run_test("Tenant: PUT update", test_tenant_update)
    run_test("Tenant: POST enumerate", test_tenant_enumerate)
    run_test("Tenant: HEAD exists", test_tenant_exists)

    # 4. User CRUD
    run_test("User: PUT create", test_user_create)
    run_test("User: GET read", test_user_read)
    run_test("User: PUT update", test_user_update)
    run_test("User: POST enumerate", test_user_enumerate)
    run_test("User: HEAD exists", test_user_exists)

    # 5. Credential CRUD
    run_test("Credential: PUT create", test_credential_create)
    run_test("Credential: GET read", test_credential_read)
    run_test("Credential: POST enumerate", test_credential_enumerate)
    run_test("Credential: HEAD exists", test_credential_exists)

    # 6. Collection CRUD
    run_test("Collection: PUT create", test_collection_create)
    run_test("Collection: GET read", test_collection_read)
    run_test("Collection: PUT update", test_collection_update)
    run_test("Collection: POST enumerate", test_collection_enumerate)
    run_test("Collection: HEAD exists", test_collection_exists)

    # 7. Document CRUD
    run_test("Document: PUT create", test_document_create)
    run_test("Document: GET read", test_document_read)
    run_test("Document: PUT update", test_document_update)

    # 8. Document Batch
    run_test("Document: POST batch create", test_document_batch_create)

    # 9. Label CRUD
    run_test("Label: PUT create", test_label_create)
    run_test("Label: GET list", test_label_list)

    # 10. Tag CRUD
    run_test("Tag: PUT create", test_tag_create)
    run_test("Tag: GET list", test_tag_list)

    # 11. Search Data Setup
    run_test("Search data: setup test documents, labels, and tags", test_search_data_setup)

    # 12. Vector Search
    run_test("Search: cosine similarity", test_search_cosine_similarity)
    run_test("Search: cosine distance", test_search_cosine_distance)
    run_test("Search: euclidean similarity", test_search_euclidean_similarity)
    run_test("Search: euclidean distance", test_search_euclidean_distance)
    run_test("Search: inner product", test_search_inner_product)

    # 13. Search Sort Orders
    run_test("Search sort: score descending", test_search_sort_score_descending)
    run_test("Search sort: score ascending", test_search_sort_score_ascending)
    run_test("Search sort: distance ascending", test_search_sort_distance_ascending)
    run_test("Search sort: distance descending", test_search_sort_distance_descending)
    run_test("Search sort: created ascending", test_search_sort_created_ascending)
    run_test("Search sort: created descending", test_search_sort_created_descending)

    # 14. Search Thresholds
    run_test("Search threshold: minimum score", test_search_minimum_score)
    run_test("Search threshold: maximum score", test_search_maximum_score)
    run_test("Search threshold: min and max score", test_search_min_max_score)
    run_test("Search threshold: minimum distance", test_search_minimum_distance)
    run_test("Search threshold: maximum distance", test_search_maximum_distance)

    # 15. Search Label Filters
    run_test("Search label: required", test_search_label_required)
    run_test("Search label: excluded", test_search_label_excluded)
    run_test("Search label: required multiple", test_search_label_required_multiple)
    run_test("Search label: required and excluded", test_search_label_required_and_excluded)
    run_test("Search label: no match", test_search_label_no_match)

    # 16. Search Tag Filters
    run_test("Search tag: equals", test_search_tag_equals)
    run_test("Search tag: not equals", test_search_tag_not_equals)
    run_test("Search tag: contains", test_search_tag_contains)
    run_test("Search tag: contains not", test_search_tag_contains_not)
    run_test("Search tag: starts with", test_search_tag_starts_with)
    run_test("Search tag: ends with", test_search_tag_ends_with)
    run_test("Search tag: greater than", test_search_tag_greater_than)
    run_test("Search tag: less than", test_search_tag_less_than)
    run_test("Search tag: is not null", test_search_tag_is_not_null)
    run_test("Search tag: is null", test_search_tag_is_null)
    run_test("Search tag: excluded", test_search_tag_excluded)

    # 17. Search Terms Filters
    run_test("Search terms: required", test_search_terms_required)
    run_test("Search terms: excluded", test_search_terms_excluded)
    run_test("Search terms: required multiple", test_search_terms_required_multiple)
    run_test("Search terms: required and excluded", test_search_terms_required_and_excluded)
    run_test("Search terms: case insensitive", test_search_terms_case_insensitive)

    # 18. Search Date Range
    run_test("Search date: created after", test_search_created_after)
    run_test("Search date: created before", test_search_created_before)
    run_test("Search date: created before none", test_search_created_before_none)
    run_test("Search date: range combined", test_search_date_range_combined)

    # 19. Search DocumentIds
    run_test("Search docids: filter by document ids", test_search_document_ids)
    run_test("Search docids: no match", test_search_document_ids_no_match)

    # 20. Search Pagination
    run_test("Search pagination: page through results", test_search_pagination)
    run_test("Search pagination: max results", test_search_max_results)

    # 21. Search Combined Filters
    run_test("Search combined: label and tag", test_search_combined_label_and_tag)
    run_test("Search combined: terms and label", test_search_combined_terms_and_label)
    run_test("Search combined: all filters", test_search_combined_all_filters)

    # 21b. Full-Text Search
    run_test("Search full-text: basic query", test_search_full_text_basic)
    run_test("Search full-text: hybrid vector + text", test_search_full_text_hybrid)
    run_test("Search full-text: with filters", test_search_full_text_with_filters)
    run_test("Search full-text: no match", test_search_full_text_no_match)
    run_test("Search full-text: backward compat", test_search_full_text_backward_compat)

    # 22. Search Result Validation
    run_test("Search validation: result fields", test_search_result_fields)
    run_test("Search validation: document fields", test_search_document_fields)

    # 22b. Neighbor Retrieval
    run_test("Neighbor retrieval: setup data", test_neighbor_data_setup)
    run_test("Neighbor retrieval: search with neighbors", test_neighbor_search_with_neighbors)
    run_test("Neighbor retrieval: search without neighbors", test_neighbor_search_without_neighbors)

    # 23. Document Enumeration
    run_test("Enum docs: basic", test_enum_documents_basic)
    run_test("Enum docs: created ascending", test_enum_documents_created_ascending)
    run_test("Enum docs: created descending", test_enum_documents_created_descending)
    run_test("Enum docs: pagination", test_enum_documents_pagination)
    run_test("Enum docs: max results", test_enum_documents_max_results)
    run_test("Enum docs: created after", test_enum_documents_created_after)
    run_test("Enum docs: created before", test_enum_documents_created_before)
    run_test("Enum docs: date range none", test_enum_documents_date_range_none)
    run_test("Enum docs: document ids", test_enum_documents_document_ids)
    run_test("Enum docs: document ids no match", test_enum_documents_document_ids_no_match)
    run_test("Enum docs: label required", test_enum_documents_label_required)
    run_test("Enum docs: label excluded", test_enum_documents_label_excluded)
    run_test("Enum docs: label no match", test_enum_documents_label_no_match)
    run_test("Enum docs: tag equals", test_enum_documents_tag_equals)
    run_test("Enum docs: tag not equals", test_enum_documents_tag_not_equals)
    run_test("Enum docs: tag contains", test_enum_documents_tag_contains)
    run_test("Enum docs: tag starts with", test_enum_documents_tag_starts_with)
    run_test("Enum docs: tag ends with", test_enum_documents_tag_ends_with)
    run_test("Enum docs: tag greater than", test_enum_documents_tag_greater_than)
    run_test("Enum docs: tag less than", test_enum_documents_tag_less_than)
    run_test("Enum docs: tag is not null", test_enum_documents_tag_is_not_null)
    run_test("Enum docs: tag is null", test_enum_documents_tag_is_null)
    run_test("Enum docs: tag contains not", test_enum_documents_tag_contains_not)
    run_test("Enum docs: terms required", test_enum_documents_terms_required)
    run_test("Enum docs: terms excluded", test_enum_documents_terms_excluded)
    run_test("Enum docs: combined label and tag", test_enum_documents_combined_label_and_tag)
    run_test("Enum docs: combined all filters", test_enum_documents_combined_all_filters)
    run_test("Enum docs: result fields", test_enum_documents_result_fields)
    run_test("Enum docs: object fields", test_enum_documents_object_fields)

    # 24. Tenant Enumeration Pagination
    run_test("Enumeration: tenant pagination", test_enumeration_pagination)

    # 25. Authorization
    run_test("Authorization: non-admin cannot create tenant", test_authorization_non_admin)

    # 25b. Batch Delete
    run_test("Batch delete: delete by keys", test_batch_delete_by_keys)
    run_test("Batch delete: delete by filter", test_delete_by_filter)

    # 26. Cleanup
    run_test("Cleanup: delete search labels", test_cleanup_search_labels)
    run_test("Cleanup: delete search tags", test_cleanup_search_tags)
    run_test("Cleanup: delete search documents", test_cleanup_search_documents)
    run_test("Cleanup: delete labels", test_cleanup_labels)
    run_test("Cleanup: delete tags", test_cleanup_tags)
    run_test("Cleanup: delete documents", test_cleanup_documents)
    run_test("Cleanup: delete collection", test_cleanup_collection)
    run_test("Cleanup: delete credential", test_cleanup_credential)
    run_test("Cleanup: delete user", test_cleanup_user)
    run_test("Cleanup: delete tenant", test_cleanup_tenant)
    run_test("Cleanup: delete pagination tenants", test_cleanup_pagination_tenants)

    total_elapsed = int((time.perf_counter() - _total_start) * 1000)

    print()
    print("=========================================")
    print("  Test Summary")
    print("=========================================")
    print(f"  Total    : {_passed + _failed}")
    print(f"  Passed   : {_passed}")
    print(f"  Failed   : {_failed}")
    print(f"  Runtime  : {total_elapsed} ms")
    print(f"  Result   : {'PASS' if _failed == 0 else 'FAIL'}")

    if _failed_tests:
        print()
        print("  Failed Tests:")
        for name in _failed_tests:
            print(f"    - {name}")

    print("=========================================")

    sys.exit(0 if _failed == 0 else 1)


if __name__ == "__main__":
    main()
