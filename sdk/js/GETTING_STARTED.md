# Getting Started - JavaScript SDK Test Harness

## Prerequisites

- Node.js 18+ (requires native `fetch` API)
- A running RecallDB instance

## Setup

No external dependencies are required.

## Running the Tests

```bash
node test-harness.js <endpoint> <api_key>
```

### Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `endpoint` | `http://127.0.0.1:8600` | RecallDB server URL |
| `api_key` | `recalldbadmin` | Bearer token for authentication |

### Examples

Run against a local instance with default credentials:

```bash
node test-harness.js
```

Run against a specific endpoint with a custom bearer token:

```bash
node test-harness.js https://recalldb.example.com my-bearer-token
```

## Full-Text and Hybrid Search Coverage

The harness exercises the full-text and hybrid search options, so it needs a server that supports `FullText.MatchMode` and `Hybrid`:

- Full-text search matches any query term by default (`MatchMode` `Any`). The harness checks that a query containing one indexed word and one nonexistent word returns results under `Any` and none under `All` (which restores the previous all-terms behavior).
- Hybrid search defaults to reciprocal rank fusion (`Hybrid.Strategy` `Rrf`). Results may include documents that did not match the text query, whose `TextScore` is absent; fused scores are in the range 0 to 1 and `VectorRank`/`TextRank` are reported. `Hybrid.Strategy` `Filter` restores the previous hybrid behavior, where every result must match the text query.
- Invalid options (`TextWeight` 1.5, `Normalization` 64, `Hybrid.RrfK` 0, an unknown `Language`) are expected to return HTTP 400.

## Newer Search Features, Transport, and Errors

The harness also covers the 0.2.2 SDK features. They need a server that reports the `search.hybrid.recency`,
`search.collapse`, `search.include-embeddings`, and `search.fulltext.minimum-should-match` capabilities in `GET /`:

- **Hybrid round trip**: hits carry `VectorScore`, `TextScore`, `VectorRank`, and `TextRank`; a text-only hit (built with
  `Hybrid.CandidatePool` 1) has no `VectorRank`. Hybrid options on a vector-only search return a `Notice`.
- **Stored vectors**: hits have no `Embeddings` by default; `IncludeEmbeddings: true` returns a vector of the collection
  dimension. Vectors cost about 4 KB per hit at 384 dimensions and 8 KB at 768, so request them only when needed.
- **Collapse**: a dedicated collection is seeded with chunked documents tagged `parentKey`. A hybrid search with
  `Collapse: { Field: "Tag", TagKey: "parentKey" }` and `Hybrid.RecencyWeight` 0.3 must return one hit per `GroupKey`,
  each with `GroupHits` and `RecencyRank`. A vector-only collapse by `DocumentId` and the 400 for collapse with
  `Hybrid.Strategy` `Filter` are also checked. The collection is deleted afterwards.
- **Minimum should match**: `FullText.MinimumShouldMatch` 2 excludes a document that matches only one term, and is
  rejected with 400 when `MatchMode` is not `Any`.
- **Structured errors**: `Hybrid.RecencyWeight` 1.5 and `Hybrid.RrfK` 0 must reject with `RecallDbException`,
  `statusCode` 400, and an `errorMessage` naming the field, with `responseBody` left unchanged.
- **Server info**: `getServerInfo()` returns `Version` and `Capabilities`; `supports("search.collapse")` is true and
  `supports("nope")` is false.
- **Transport**: a local stub HTTP server that answers slowly checks that `timeoutMs` rejects with a `TimeoutError`
  instead of hanging, that an aborted `signal` stops a request, and that a custom `fetch` is used.
- **URL encoding and Exists**: a document whose key and document ID contain `#`, `?`, `/`, `%`, and a space is created,
  read, checked, and deleted. An Exists call with a bad token throws with `statusCode` 401, while a missing document
  still returns `false`.

Using the SDK options from your own code:

```javascript
const { RecallDbClient, RecallDbException, RecallDbTimeoutError, Capabilities } = require("./recalldb-sdk");

const client = new RecallDbClient("http://127.0.0.1:8600", "recalldbadmin", { timeoutMs: 30000 });
if (await client.supports(Capabilities.Collapse)) {
  // safe to send Collapse
}
try {
  await client.documentExists("default", "collection-id", "doc-1"); // false only for 404
} catch (e) {
  if (e instanceof RecallDbTimeoutError) console.log("timed out after", e.timeoutMs, "ms");
  else if (e instanceof RecallDbException) console.log(e.statusCode, e.errorCode, e.errorMessage);
  else throw e;
}
```

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

The test harness runs 160+ integration tests and outputs results in this format:

```
=========================================
  RecallDB Integration Test Harness
  (JavaScript SDK)
=========================================
  Endpoint : http://127.0.0.1:8600
  API Key  : recalldbadmin
=========================================

  [PASS] Connectivity: GET / (15 ms)
  [PASS] Connectivity: HEAD / (4 ms)
  ...

=========================================
  Test Summary
=========================================
  Total    : 165
  Passed   : 165
  Failed   : 0
  Runtime  : 3892 ms
  Result   : PASS
=========================================
```

The process exits with code `0` on success or `1` if any tests fail.
