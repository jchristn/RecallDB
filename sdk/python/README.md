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
from recalldb_sdk import RecallDbClient, __version__

print(__version__)  # 0.2.2

client = RecallDbClient("http://127.0.0.1:8600", "your-bearer-token")

# Check server health
health = client.health()
print(health["Name"])  # RecallDB

# Authenticate
auth = client.authenticate({"BearerToken": "your-bearer-token"})
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

### Server capabilities

Servers that predate a search field silently ignore it: the request succeeds and the result is computed without it. Check the server's capability list before relying on a newer field.

```python
from recalldb_sdk import Capabilities

info = client.get_server_info()
print(info["Version"], info["Capabilities"])  # Capabilities is [] on older servers

if client.supports(Capabilities.COLLAPSE):  # cached per client; supports(name, refresh=True) refetches
    ...
```

| Constant | Capability | Enables |
|----------|------------|---------|
| `Capabilities.HYBRID_RRF` | `search.hybrid.rrf` | `Hybrid.Strategy` `Rrf`, `VectorRank`, `TextRank` |
| `Capabilities.HYBRID_RECENCY` | `search.hybrid.recency` | `Hybrid.RecencyWeight`, `RecencyRank` |
| `Capabilities.COLLAPSE` | `search.collapse` | `Collapse`, `GroupKey`, `GroupHits` |
| `Capabilities.INCLUDE_EMBEDDINGS` | `search.include-embeddings` | `IncludeEmbeddings`, `Embeddings` on hits |
| `Capabilities.FULLTEXT_MINIMUM_SHOULD_MATCH` | `search.fulltext.minimum-should-match` | `FullText.MinimumShouldMatch` |

The module also defines named constants for the other string values: `HybridStrategies`, `FullTextMatchModes`, `FullTextSearchTypes`, `VectorSearchTypes`, `SortOrders`, and `CollapseFields`.

### Single-call hybrid search with collapse and recency

One request runs the vector and text legs, fuses them, adds a recency signal, and returns one hit per parent (here, chunks tagged with a `parentKey` tag).

```python
from recalldb_sdk import Capabilities, CollapseFields, HybridStrategies, VectorSearchTypes

query = {
    "Vector": {"SearchType": VectorSearchTypes.COSINE_SIMILARITY, "Embeddings": [0.1, 0.2, 0.3, ...]},
    "FullText": {"Query": "quarterly planning notes"},
    "Hybrid": {"Strategy": HybridStrategies.RRF},
    "MaxResults": 10,
}

if client.supports(Capabilities.COLLAPSE):
    # One hit per parentKey value; MaxResults, TotalRecords, and continuation count groups.
    query["Collapse"] = {"Field": CollapseFields.TAG, "TagKey": "parentKey"}

if client.supports(Capabilities.HYBRID_RECENCY):
    # 0.0 to 1.0; Rrf only. Newer groups (by their newest chunk) rank higher.
    query["Hybrid"]["RecencyWeight"] = 0.3

results = client.search("tenant-id", collection["Id"], query)
if results.get("Notice"):
    print("Notice:", results["Notice"])

for d in results["Documents"]:
    print(d.get("GroupKey"), d.get("GroupHits"), d["Score"],
          d.get("VectorRank"), d.get("TextRank"), d.get("RecencyRank"))
```

Notes:

- `Collapse.Field` is `DocumentId` (default) or `Tag` (requires `TagKey`). `Collapse` is rejected with HTTP 400 with `Hybrid.Strategy` `Filter` and when the request has neither a vector nor a text query. `Collapse.CandidatePool` (1 to 10000) sets how many candidates are grouped for vector-only and full-text-only searches; a pool with fewer groups than `MaxResults` returns fewer hits and a `Notice`.
- `Hybrid.RecencyWeight` is used only by `Rrf`; `Linear` and `Filter` ignore it and return a `Notice`.
- `FullText.MinimumShouldMatch` (1 to 3, default 1) requires at least that many distinct query terms per document, with `MatchMode` `Any` only; other match modes return HTTP 400 when it is above 1.

### Stored vectors (IncludeEmbeddings)

Search results do not include stored vectors unless you set `"IncludeEmbeddings": True`, which adds `Embeddings` to each hit. Vectors are sent as JSON numbers, which costs about 4 KB per hit at 384 dimensions and about 8 KB per hit at 768 dimensions, so request them only when you need them (for example for client-side reranking or deduplication).

### Timeouts and a caller-supplied session

Every request uses a timeout, 100 seconds by default. Pass `timeout` in seconds, or `None` to disable it; any other value must be positive or `ValueError` is raised. When the timeout elapses the call raises `requests.exceptions.Timeout` instead of hanging.

To add retries or connection pooling settings, pass your own `requests.Session`. The client does not modify a supplied session: its headers are left alone, and the client sends its `Authorization` and `Content-Type` headers on each request instead. `client.close()` closes only a session the client created, never yours.

```python
import requests
from requests.adapters import HTTPAdapter
from urllib3.util.retry import Retry

from recalldb_sdk import RecallDbClient

retry = Retry(
    total=5,
    backoff_factor=0.5,                 # 0.5 s, 1 s, 2 s, ...
    status_forcelist=[429, 502, 503],
    allowed_methods=None,               # retry every method; limit this if writes must not repeat
    respect_retry_after_header=True,
)
session = requests.Session()
session.mount("http://", HTTPAdapter(max_retries=retry))
session.mount("https://", HTTPAdapter(max_retries=retry))

client = RecallDbClient("http://127.0.0.1:8600", "your-bearer-token", timeout=30, session=session)

try:
    client.health()
except requests.exceptions.Timeout:
    print("RecallDB did not respond within 30 seconds")
```

### Errors

A failed call raises `RecallDbException` with these attributes:

| Attribute | Description |
|-----------|-------------|
| `status_code` | HTTP status code |
| `response_body` | The raw response body text, unchanged (empty for HEAD requests) |
| `error_code` | The body's `Error` field, for example `BadRequest` or `NotAuthorized`; `None` when the body is not a JSON object |
| `error_message` | The first non-empty of the body's `Context`, `Message`, and `Description` fields; `None` when the body is not a JSON object |

When the body parses, the exception message reads `RecallDB API returned 400 (BadRequest): RrfK must be between 1 and 100000. (Parameter 'RrfK')`. Otherwise it is `RecallDB API returned <status>: <body>` as before.

```python
from recalldb_sdk import RecallDbException

try:
    client.search("tenant-id", collection["Id"], {"FullText": {"Query": "x", "TextWeight": 1.5}})
except RecallDbException as e:
    print(e.status_code, e.error_code, e.error_message)
```

### Exists calls (behavior change)

`tenant_exists`, `user_exists`, `credential_exists`, `collection_exists`, and `document_exists` return `True` for HTTP 200 and `False` for HTTP 404, and now raise `RecallDbException` for any other status (for example 401, 403, 429, or 5xx). Previously every non-200 status returned `False`, so an expired token or an overloaded server looked like a missing record. Callers that relied on `False` for those cases must catch the exception.

### Path encoding

Every caller-supplied path segment (tenant, user, credential, collection, document key, document ID, label, and tag IDs) is URL-encoded, so keys such as `a/b#1?x=50% off` round-trip correctly.

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
