"""
RecallDB Python SDK test harness.

Usage:
    python test_harness.py --endpoint http://localhost:8600 --token default
"""

import argparse
import sys

from recalldb_sdk import RecallDbClient, RecallDbException


def main():
    parser = argparse.ArgumentParser(description="RecallDB SDK Test Harness")
    parser.add_argument("--endpoint", default="http://localhost:8600", help="RecallDB server endpoint")
    parser.add_argument("--token", default="default", help="Bearer token")
    args = parser.parse_args()

    print("RecallDB Python SDK Test Harness")
    print(f"Endpoint : {args.endpoint}")
    print(f"Token    : {args.token}")
    print()

    client = RecallDbClient(args.endpoint, args.token)
    tenant_id = "default"
    collection_id = None
    document_key = None

    try:
        # Health
        print("[Health] GET /")
        health = client.health()
        print(f"[Health] Success: {health is not None}")
        print()

        # Authenticate
        print("[Auth] POST /v1.0/authenticate")
        auth_resp = client.authenticate({"BearerToken": args.token})
        print(f"[Auth] Success: {auth_resp.get('Success')}")
        print()

        # Tenants
        print("[Tenants] Enumerate")
        tenants = client.enumerate_tenants()
        print(f"[Tenants] Total: {tenants.get('TotalRecords')}")

        print(f"[Tenants] Get tenant: {tenant_id}")
        tenant = client.get_tenant(tenant_id)
        print(f"[Tenants] Name: {tenant.get('Name')}")

        print(f"[Tenants] Exists: {tenant_id}")
        exists = client.tenant_exists(tenant_id)
        print(f"[Tenants] Exists: {exists}")
        print()

        # Users
        print("[Users] Enumerate")
        users = client.enumerate_users(tenant_id)
        print(f"[Users] Total: {users.get('TotalRecords')}")

        print("[Users] Get user: default")
        user = client.get_user(tenant_id, "default")
        print(f"[Users] Email: {user.get('Email')}")

        print("[Users] Exists: default")
        exists = client.user_exists(tenant_id, "default")
        print(f"[Users] Exists: {exists}")
        print()

        # Credentials
        print("[Credentials] Enumerate")
        creds = client.enumerate_credentials(tenant_id)
        print(f"[Credentials] Total: {creds.get('TotalRecords')}")

        print("[Credentials] Get credential: default")
        cred = client.get_credential(tenant_id, "default")
        print(f"[Credentials] Name: {cred.get('Name')}")

        print("[Credentials] Exists: default")
        exists = client.credential_exists(tenant_id, "default")
        print(f"[Credentials] Exists: {exists}")
        print()

        # Collections
        print("[Collections] Create")
        col = client.create_collection(tenant_id, {
            "Name": "Python SDK Test Collection",
            "Description": "Created by Python SDK test harness",
            "Dimensionality": 384
        })
        collection_id = col.get("Id")
        print(f"[Collections] Created: {collection_id}")

        print(f"[Collections] Get: {collection_id}")
        col = client.get_collection(tenant_id, collection_id)
        print(f"[Collections] Name: {col.get('Name')}")

        print(f"[Collections] Exists: {collection_id}")
        exists = client.collection_exists(tenant_id, collection_id)
        print(f"[Collections] Exists: {exists}")

        print(f"[Collections] Update: {collection_id}")
        col["Description"] = "Updated by Python SDK test harness"
        col = client.update_collection(tenant_id, collection_id, col)
        print(f"[Collections] Updated description: {col.get('Description')}")

        print("[Collections] Enumerate")
        collections = client.enumerate_collections(tenant_id)
        print(f"[Collections] Total: {collections.get('TotalRecords')}")
        print()

        # Documents
        embeddings = [0.01 * i for i in range(384)]

        print("[Documents] Create")
        doc = client.create_document(tenant_id, collection_id, {
            "Content": "This is a test document for the RecallDB Python SDK.",
            "ContentType": "Text",
            "Embeddings": embeddings
        })
        document_key = doc.get("DocumentKey")
        print(f"[Documents] Created: {document_key}")

        print(f"[Documents] Get: {document_key}")
        doc = client.get_document(tenant_id, collection_id, document_key)
        print(f"[Documents] Content length: {doc.get('ContentLength')}")

        print(f"[Documents] Exists: {document_key}")
        exists = client.document_exists(tenant_id, collection_id, document_key)
        print(f"[Documents] Exists: {exists}")

        print("[Documents] Enumerate")
        docs = client.enumerate_documents(tenant_id, collection_id)
        print(f"[Documents] Total: {docs.get('TotalRecords')}")

        print("[Documents] Batch create")
        batch = [
            {"Content": f"Batch document {i}", "ContentType": "Text", "Embeddings": embeddings}
            for i in range(3)
        ]
        created = client.create_document_batch(tenant_id, collection_id, batch)
        print(f"[Documents] Batch created: {len(created)}")
        print()

        # Labels
        print("[Labels] Create")
        label = client.create_label(tenant_id, collection_id, {
            "DocumentKey": document_key,
            "Label": "test-label"
        })
        label_id = label.get("Id")
        print(f"[Labels] Created: {label_id}")

        print(f"[Labels] Get: {label_id}")
        fetched = client.get_label(tenant_id, collection_id, label_id)
        print(f"[Labels] Label value: {fetched.get('Label')}")

        print("[Labels] List")
        labels = client.list_labels(tenant_id, collection_id)
        print(f"[Labels] Total: {labels.get('TotalRecords')}")

        print(f"[Labels] Delete: {label_id}")
        client.delete_label(tenant_id, collection_id, label_id)
        print("[Labels] Deleted")
        print()

        # Tags
        print("[Tags] Create")
        tag = client.create_tag(tenant_id, collection_id, {
            "DocumentKey": document_key,
            "Key": "category",
            "Value": "test"
        })
        tag_id = tag.get("Id")
        print(f"[Tags] Created: {tag_id}")

        print(f"[Tags] Get: {tag_id}")
        fetched = client.get_tag(tenant_id, collection_id, tag_id)
        print(f"[Tags] Key: {fetched.get('Key')} Value: {fetched.get('Value')}")

        print("[Tags] List")
        tags = client.list_tags(tenant_id, collection_id)
        print(f"[Tags] Total: {tags.get('TotalRecords')}")

        print(f"[Tags] Delete: {tag_id}")
        client.delete_tag(tenant_id, collection_id, tag_id)
        print("[Tags] Deleted")
        print()

        # Search
        print(f"[Search] Search collection: {collection_id}")
        result = client.search(tenant_id, collection_id, {
            "SortOrder": "ScoreDescending",
            "MaxResults": 10,
            "Vector": {
                "SearchType": "CosineSimilarity",
                "Embeddings": embeddings,
                "MinimumScore": 0.0
            }
        })
        print(f"[Search] Success: {result.get('Success')}")
        print(f"[Search] Documents found: {len(result.get('Documents', []))}")
        print()

    except RecallDbException as e:
        print(f"API Error: {e}")
        sys.exit(1)

    finally:
        # Cleanup
        if collection_id:
            print(f"[Cleanup] Deleting collection: {collection_id}")
            try:
                client.delete_collection(tenant_id, collection_id)
                print("[Cleanup] Collection deleted")
            except RecallDbException as e:
                print(f"[Cleanup] Error: {e}")

    print()
    print("All tests completed.")


if __name__ == "__main__":
    main()
