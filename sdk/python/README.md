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
- **Search** - Vector similarity/distance, full-text, and hybrid search with label, tag, terms, date, and pagination filters

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

### Full-text and hybrid search

```python
# Full-text only: matches documents containing any of the terms (MatchMode "Any" is the default)
text_results = client.search("tenant-id", collection["Id"], {
    "FullText": {"Query": "how do I run the test suite", "MatchMode": "Any"},
    "MaxResults": 10
})

# Hybrid: vector and text legs combined with reciprocal rank fusion
hybrid_results = client.search("tenant-id", collection["Id"], {
    "Vector": {"SearchType": "CosineSimilarity", "Embeddings": [0.1, 0.2, 0.3, ...]},
    "FullText": {"Query": "run the test suite", "TextWeight": 0.5},
    "Hybrid": {"Strategy": "Rrf", "RrfK": 60, "CandidatePool": 100},
    "MaxResults": 10
})

for d in hybrid_results["Documents"]:
    print(d["DocumentKey"], d["Score"], d.get("VectorRank"), d.get("TextRank"))

# Legacy hybrid: text match required, raw scores blended
legacy_results = client.search("tenant-id", collection["Id"], {
    "Vector": {"SearchType": "CosineSimilarity", "Embeddings": [0.1, 0.2, 0.3, ...]},
    "FullText": {"Query": "machine learning", "MatchMode": "All"},
    "Hybrid": {"Strategy": "Filter"},
    "MaxResults": 10
})
```

Full-text matching notes:

- `FullText.MatchMode` controls how the query text is matched. The default is `Any`: a document matches if it contains any of the query's terms (after stemming and stop word removal), ranked by relevance. `All` requires every term (the previous default behavior), `Phrase` requires the terms adjacent and in order, and `WebSearch` accepts web-search syntax (`"quoted phrase"`, `or`, `-exclude`).
- When both a vector and a text query are supplied, the two legs are combined with reciprocal rank fusion by default (`Hybrid.Strategy` `Rrf`). Each leg retrieves its own candidates, so a document does not need to match the text query to be returned; `Score` is the fused score in the range 0 to 1, and `VectorRank`, `TextRank`, and `VectorScore` explain where each result came from. `TextScore` is absent for results that did not match the text query.
- To restore the previous hybrid behavior (the text query is a required filter and raw scores are blended), set `Hybrid.Strategy` to `Filter` and `FullText.MatchMode` to `All`.
- `FullText.TextWeight` must be between 0.0 and 1.0, `FullText.Normalization` between 0 and 63, and `FullText.Language` must be a text search configuration installed on the server. Out-of-range values are rejected with HTTP 400.
- The search result may include a `Notice` explaining how the search was evaluated, for example when the text query contained only stop words or hybrid options were ignored.

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
