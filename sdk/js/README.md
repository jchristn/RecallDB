# RecallDB JavaScript SDK

A JavaScript (Node.js) client library for interacting with the RecallDB vector database REST API.

## Overview

The RecallDB JavaScript SDK provides a simple interface for all RecallDB operations including:

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

- Node.js 18+ (requires native `fetch` API)

## Installation

No external dependencies required. The SDK uses the native `fetch` API available in Node.js 18+.

```bash
# No npm install needed
```

## Quick Start

```javascript
const { RecallDbClient, Capabilities } = require("./recalldb-sdk");

const client = new RecallDbClient("http://127.0.0.1:8600", "your-bearer-token");

// Check server health
const health = await client.health();
console.log(health.Name); // RecallDB

// Authenticate
const auth = await client.authenticate({ BearerToken: "your-bearer-token" });
console.log(auth.Success); // true

// Create a collection
const collection = await client.createCollection("tenant-id", {
  Name: "my-collection",
  Dimensionality: 384,
});

// Create a document
const doc = await client.createDocument("tenant-id", collection.Id, {
  DocumentKey: "doc-1",
  Content: "Hello world",
  ContentType: "Text",
  Embeddings: [0.1, 0.2, 0.3 /* ... */],
});

// Search
const results = await client.search("tenant-id", collection.Id, {
  Vector: {
    SearchType: "CosineSimilarity",
    Embeddings: [0.1, 0.2, 0.3 /* ... */],
  },
  MaxResults: 10,
});
```

### Full-text and hybrid search

```javascript
// Full-text only: matches documents containing any of the terms (MatchMode "Any" is the default)
const textResults = await client.search("tenant-id", collection.Id, {
  FullText: { Query: "how do I run the test suite", MatchMode: "Any" },
  MaxResults: 10,
});

// Hybrid: vector and text legs combined with reciprocal rank fusion
const hybridResults = await client.search("tenant-id", collection.Id, {
  Vector: { SearchType: "CosineSimilarity", Embeddings: [0.1, 0.2, 0.3 /* ... */] },
  FullText: { Query: "run the test suite", TextWeight: 0.5 },
  Hybrid: { Strategy: "Rrf", RrfK: 60, CandidatePool: 100 },
  MaxResults: 10,
});

for (const d of hybridResults.Documents) {
  console.log(d.DocumentKey, d.Score, d.VectorRank, d.TextRank);
}

// Legacy hybrid: text match required, raw scores blended
const legacyResults = await client.search("tenant-id", collection.Id, {
  Vector: { SearchType: "CosineSimilarity", Embeddings: [0.1, 0.2, 0.3 /* ... */] },
  FullText: { Query: "machine learning", MatchMode: "All" },
  Hybrid: { Strategy: "Filter" },
  MaxResults: 10,
});
```

### Single-call hybrid search with collapse and recency

One request can fuse the vector and text legs, add a recency leg, and return one hit per parent document. Guard the
newer fields with `supports()`: older servers silently ignore fields they do not know, so without the check you would
get uncollapsed results with no error.

```javascript
const {
  RecallDbClient, Capabilities, HybridStrategies, CollapseFields, VectorSearchTypes,
} = require("./recalldb-sdk");

const client = new RecallDbClient("http://127.0.0.1:8600", "your-bearer-token");

const query = {
  Vector: { SearchType: VectorSearchTypes.CosineSimilarity, Embeddings: queryVector },
  FullText: { Query: "quarterly revenue forecast" },
  Hybrid: { Strategy: HybridStrategies.Rrf },
  MaxResults: 10,
};

if (await client.supports(Capabilities.HybridRecency)) {
  query.Hybrid.RecencyWeight = 0.2; // 0.0-1.0; Rrf only
}
if (await client.supports(Capabilities.Collapse)) {
  // One hit per value of the parentKey tag; MaxResults, TotalRecords, and continuation count groups
  query.Collapse = { Field: CollapseFields.Tag, TagKey: "parentKey" };
}

const result = await client.search("tenant-id", collection.Id, query);
if (result.Notice) console.log("Notice:", result.Notice);
for (const d of result.Documents) {
  console.log(d.GroupKey, d.GroupHits, d.Score, d.VectorRank, d.TextRank, d.RecencyRank);
}
```

Notes:

- `Hybrid.RecencyWeight` (0.0 to 1.0, default 0) adds a newest-first leg to reciprocal rank fusion. Only `Rrf` uses
  it; `Linear` and `Filter` ignore it and return a `Notice`. Hits then carry `RecencyRank`.
- `Collapse` takes `Field` (`DocumentId` or `Tag`), `TagKey` (required when `Field` is `Tag`), and an optional
  `CandidatePool` (1 to 10000). Each hit carries `GroupKey` and `GroupHits`. Collapse is rejected with HTTP 400 when
  `Hybrid.Strategy` is `Filter` or when the query has neither a vector nor a text query.
- `FullText.MinimumShouldMatch` (1 to 3, default 1) requires that many distinct query terms, and is only valid with
  `MatchMode` `Any`.

### Stored vectors (IncludeEmbeddings)

Search hits do not include stored vectors by default. Set `IncludeEmbeddings: true` to receive each hit's vector in
`Embeddings`. Vectors are sent as JSON numbers, about 4 KB per hit at 384 dimensions and 8 KB at 768, so request them
only when you need them (for example for client-side reranking or deduplication).

### Server info and capabilities

```javascript
const info = await client.getServerInfo(); // { Name, Version, UptimeMs, Capabilities }
console.log(info.Version, info.Capabilities); // Capabilities is [] on older servers

await client.supports(Capabilities.Collapse);                   // cached per client instance
await client.supports(Capabilities.Collapse, { refresh: true }); // re-fetches GET /
```

The SDK also exports frozen constant objects for string values: `Capabilities`, `HybridStrategies`,
`FullTextMatchModes`, `FullTextSearchTypes`, `VectorSearchTypes`, `SortOrders`, and `CollapseFields`, plus `VERSION`.

Full-text matching notes:

- `FullText.MatchMode` controls how the query text is matched. The default is `Any`: a document matches if it contains any of the query's terms (after stemming and stop word removal), ranked by relevance. `All` requires every term (the previous default behavior), `Phrase` requires the terms adjacent and in order, and `WebSearch` accepts web-search syntax (`"quoted phrase"`, `or`, `-exclude`).
- When both a vector and a text query are supplied, the two legs are combined with reciprocal rank fusion by default (`Hybrid.Strategy` `Rrf`). Each leg retrieves its own candidates, so a document does not need to match the text query to be returned; `Score` is the fused score in the range 0 to 1, and `VectorRank`, `TextRank`, and `VectorScore` explain where each result came from. `TextScore` is absent for results that did not match the text query.
- To restore the previous hybrid behavior (the text query is a required filter and raw scores are blended), set `Hybrid.Strategy` to `Filter` and `FullText.MatchMode` to `All`.
- `FullText.TextWeight` must be between 0.0 and 1.0, `FullText.Normalization` between 0 and 63, and `FullText.Language` must be a text search configuration installed on the server. Out-of-range values are rejected with HTTP 400.
- The search result may include a `Notice` explaining how the search was evaluated, for example when the text query contained only stop words or hybrid options were ignored.

## Timeouts, cancellation, and custom fetch

The constructor takes an optional third `options` argument:

```javascript
const client = new RecallDbClient("http://127.0.0.1:8600", "your-bearer-token", {
  timeoutMs: 30000, // default 100000; 0 or null disables the timeout
  fetch: myFetch,   // optional custom fetch (proxies, instrumentation, tests); default is the global fetch
});
```

- The timeout covers the whole request, including reading the response body. A timed-out call rejects with a
  `RecallDbTimeoutError` (exported; `name` is `"TimeoutError"`, `timeoutMs` holds the configured value).
- Every method accepts an optional trailing `{ signal }` argument. The caller's `AbortSignal` is combined with the
  timeout; when it aborts, the call rejects with the signal's reason (by default a `DOMException` named `"AbortError"`).

```javascript
const controller = new AbortController();
setTimeout(() => controller.abort(), 2000);
const doc = await client.getDocument("tenant-id", "collection-id", "doc-1", { signal: controller.signal });
```

## Errors

HTTP failures reject with `RecallDbException`:

| Field | Description |
|-------|-------------|
| `statusCode` | HTTP status code |
| `responseBody` | Raw response body, unchanged (empty for HEAD requests) |
| `errorCode` | The body's `Error` field, e.g. `BadRequest` or `NotAuthorized`; null when the body is not a JSON object |
| `errorMessage` | First non-empty of the body's `Context`, `Message`, and `Description`; null when the body is not a JSON object |

When the body is parsed, the message reads
`RecallDB API returned 400 (BadRequest): RrfK must be between 1 and 100000. (Parameter 'RrfK')`; otherwise it is
`RecallDB API returned <status>: <body>`.

```javascript
try {
  await client.search("tenant-id", "collection-id", { /* ... */ Hybrid: { RrfK: 0 } });
} catch (e) {
  if (e instanceof RecallDbException && e.statusCode === 400) console.log(e.errorCode, e.errorMessage);
  else throw e;
}
```

## Behavior changes in 0.2.2

- **Exists calls** (`tenantExists`, `userExists`, `credentialExists`, `collectionExists`, `documentExists`) return
  `true` for 200 and `false` for 404, and now throw `RecallDbException` for any other status (401, 403, 429, 5xx).
  Previously any non-200 status, including an authentication failure or a server error, was reported as `false`.
- **Every caller-supplied path segment is URL-encoded** (tenant, user, credential, collection, label, tag, and
  document identifiers). Identifiers containing `#`, `?`, `/`, `%`, or spaces now reach the server intact. If you were
  encoding identifiers yourself before passing them in, stop, or they will be encoded twice.
- **Requests time out after 100 seconds by default.** Pass `timeoutMs: 0` to restore the previous unlimited behavior.

## Files

| File | Description |
|------|-------------|
| `recalldb-sdk.js` | SDK client library |
| `test-harness.js` | Integration test harness (mirrors Test.Automated) |
| `package.json` | Node.js package metadata |

## Running Tests

See [GETTING_STARTED.md](GETTING_STARTED.md) for instructions on running the integration test harness.

## License

MIT
