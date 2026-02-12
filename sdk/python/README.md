# RecallDB Python SDK

A Python client library for interacting with the RecallDB vector database REST API.

## Overview

The RecallDB Python SDK provides a simple, typed interface for all RecallDB operations including:

- **Health** - Server health and version checks
- **Authentication** - Bearer token and email/password authentication
- **Tenants** - Multi-tenant CRUD and enumeration
- **Users** - User management within tenants
- **Credentials** - API credential/token management
- **Collections** - Vector collection CRUD with configurable dimensionality
- **Documents** - Document CRUD, batch creation, and enumeration
- **Labels** - Document label management for categorical filtering
- **Tags** - Key-value tag management for metadata filtering
- **Search** - Vector similarity/distance search with label, tag, terms, date, and pagination filters

## Requirements

- Python 3.7+
- `requests` library

## Installation

```bash
pip install -r requirements.txt
```

## Quick Start

```python
from recalldb_sdk import RecallDbClient

client = RecallDbClient("http://localhost:8600", "your-bearer-token")

# Check server health
health = client.health()
print(health["Name"])  # RecallDB

# Authenticate
auth = client.authenticate(bearer_token="your-bearer-token")
print(auth["Success"])  # True

# Create a collection
collection = client.create_collection("tenant-id", {
    "Name": "my-collection",
    "Dimensionality": 384
})

# Create a document
doc = client.create_document("tenant-id", collection["Id"], {
    "DocumentKey": "doc-1",
    "Content": "Hello world",
    "ContentType": "Text",
    "Embeddings": [0.1, 0.2, 0.3, ...]
})

# Search
results = client.search("tenant-id", collection["Id"], {
    "Vector": {
        "SearchType": "CosineSimilarity",
        "Embeddings": [0.1, 0.2, 0.3, ...]
    },
    "MaxResults": 10
})
```

## Files

| File | Description |
|------|-------------|
| `recalldb_sdk.py` | SDK client library |
| `test_harness.py` | Integration test harness (mirrors Test.Automated) |
| `requirements.txt` | Python dependencies |

## Running Tests

See [GETTING_STARTED.md](GETTING_STARTED.md) for instructions on running the integration test harness.

## License

MIT
