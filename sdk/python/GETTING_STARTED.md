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
| `endpoint` | `http://localhost:8600` | RecallDB server URL |
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

## Output

The test harness runs 100+ integration tests and outputs results in this format:

```
=========================================
  RecallDB Integration Test Harness
  (Python SDK)
=========================================
  Endpoint : http://localhost:8600
  API Key  : recalldbadmin
=========================================

  [PASS] Connectivity: GET / (12 ms)
  [PASS] Connectivity: HEAD / (3 ms)
  ...

=========================================
  Test Summary
=========================================
  Total    : 119
  Passed   : 119
  Failed   : 0
  Runtime  : 4523 ms
  Result   : PASS
=========================================
```

The process exits with code `0` on success or `1` if any tests fail.
