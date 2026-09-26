# Getting Started - Python SDK Test Harness

## Prerequisites

- Python 3.7+
- A running RecallDB instance

## Setup

Install dependencies:

```bash
pip install -r requirements.txt
```

## Running the Tests

```bash
python test_harness.py <endpoint> <api_key>
```

### Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `endpoint` | `http://127.0.0.1:8600` | RecallDB server URL |
| `api_key` | `recalldbadmin` | Bearer token for authentication |

### Examples

Run against a local instance with default credentials:

```bash
python test_harness.py
```

Run against a specific endpoint with a custom bearer token:

```bash
python test_harness.py https://recalldb.example.com my-bearer-token
```

## Full-Text and Hybrid Search Coverage

The harness exercises the full-text and hybrid search options, so it needs a server that supports `FullText.MatchMode` and `Hybrid`:

- Full-text search matches any query term by default (`MatchMode` `Any`). The harness checks that a query containing one indexed word and one nonexistent word returns results under `Any` and none under `All` (which restores the previous all-terms behavior).
- Hybrid search defaults to reciprocal rank fusion (`Hybrid.Strategy` `Rrf`). Results may include documents that did not match the text query, whose `TextScore` is absent; fused scores are in the range 0 to 1 and `VectorRank`/`TextRank` are reported. `Hybrid.Strategy` `Filter` restores the previous hybrid behavior, where every result must match the text query.
- Invalid options (`TextWeight` 1.5, `Normalization` 64, `Hybrid.RrfK` 0, an unknown `Language`) are expected to return HTTP 400.

A minimal hybrid RRF request body:

```json
{
  "Vector": { "SearchType": "CosineSimilarity", "Embeddings": [0.9, 0.1, 0.05] },
  "FullText": { "Query": "machine learning", "TextWeight": 0.5 },
  "Hybrid": { "Strategy": "Rrf", "RrfK": 60 },
  "MaxResults": 10
}
```

Use `127.0.0.1` rather than `localhost` for a local server: on some systems `localhost` resolves to IPv6 first and each request waits for that attempt to fail.

## Search Features Added in SDK 0.2.2

The harness also covers the newer search fields. It expects a server whose `GET /` lists them in `Capabilities` (older servers silently ignore these fields, so check `client.supports(...)` in your own code before relying on them):

- Hybrid round trip: `Hybrid.Strategy`, `RrfK`, `CandidatePool`, `FullText.MatchMode`, and `TextWeight` are accepted; hits carry `VectorScore`, `TextScore`, `VectorRank`, and `TextRank`, and a text-only hit (built with a one-candidate pool per leg) has a null `VectorRank`.
- `Notice`: hybrid options on a vector-only search return a notice.
- `IncludeEmbeddings`: off by default; when on, every hit carries a vector of the collection's dimension. Vectors are JSON numbers, about 4 KB per hit at 384 dimensions and 8 KB at 768, so request them only when needed.
- `Collapse` by tag (`parentKey`) with `Hybrid.RecencyWeight` in a dedicated collection: one hit per `GroupKey`, with `GroupHits` and `RecencyRank`; vector-only collapse by `DocumentId`; `Tag` without `TagKey` and collapse with `Filter` return 400.
- `FullText.MinimumShouldMatch` 2 excludes a document that matches one term.
- `Hybrid.RecencyWeight` 1.5 returns 400, and `RecallDbException.error_message` names `RecencyWeight`.

A single-call hybrid request with collapse and recency, guarded by capability checks:

```python
from recalldb_sdk import RecallDbClient, Capabilities, CollapseFields, HybridStrategies

client = RecallDbClient("http://127.0.0.1:8600", "recalldbadmin")
query = {
    "Vector": {"SearchType": "CosineSimilarity", "Embeddings": [0.9, 0.1, 0.05]},
    "FullText": {"Query": "machine learning"},
    "Hybrid": {"Strategy": HybridStrategies.RRF},
    "MaxResults": 10,
}
if client.supports(Capabilities.COLLAPSE):
    query["Collapse"] = {"Field": CollapseFields.TAG, "TagKey": "parentKey"}
if client.supports(Capabilities.HYBRID_RECENCY):
    query["Hybrid"]["RecencyWeight"] = 0.3
results = client.search("tenant-id", "collection-id", query)
```

## Client Behavior Covered

- Server info: `get_server_info()` returns `Version` and `Capabilities`; `supports("search.collapse")` is True and `supports("nope")` is False.
- Timeout: a local stub HTTP server that sleeps longer than a 0.3 second client timeout makes the call raise `requests.exceptions.Timeout` instead of hanging. The default timeout is 100 seconds; pass `timeout=None` to disable it.
- Caller-supplied session: a `requests.Session` with a retry adapter works, and the client leaves the session's headers unchanged:

  ```python
  import requests
  from requests.adapters import HTTPAdapter
  from urllib3.util.retry import Retry

  session = requests.Session()
  session.mount("http://", HTTPAdapter(max_retries=Retry(
      total=5, backoff_factor=0.5, status_forcelist=[429, 502, 503], allowed_methods=None)))
  client = RecallDbClient("http://127.0.0.1:8600", "recalldbadmin", timeout=30, session=session)
  ```

- Reserved characters: a document key and ID containing `#`, `?`, `/`, `%`, `=`, and spaces are created, read (by key and by ID and position), checked, and deleted.
- Exists (behavior change): `*_exists` returns True for 200 and False for 404, and raises `RecallDbException` for anything else. The harness checks that a bad token raises with status 401 while a missing document still returns False.
- Structured errors: `RecallDbException` exposes `status_code`, `response_body` (unchanged), `error_code` (the body's `Error`), and `error_message` (the first non-empty of `Context`, `Message`, and `Description`). An invalid `RrfK` gives `error_code` `BadRequest` and an `error_message` naming `RrfK`.

## Output

The test harness runs 160 integration tests and outputs results in this format:

```
=========================================
  RecallDB Integration Test Harness
  (Python SDK)
=========================================
  Endpoint : http://127.0.0.1:8600
  API Key  : recalldbadmin
=========================================

  [PASS] Connectivity: GET / (12 ms)
  [PASS] Connectivity: HEAD / (3 ms)
  ...

=========================================
  Test Summary
=========================================
  Total    : 160
  Passed   : 160
  Failed   : 0
  Runtime  : 4523 ms
  Result   : PASS
=========================================
```

The process exits with code `0` on success or `1` if any tests fail.
