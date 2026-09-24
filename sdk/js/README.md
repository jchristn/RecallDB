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
const { RecallDbClient } = require("./recalldb-sdk");

const client = new RecallDbClient("http://localhost:8600", "your-bearer-token");

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

Full-text matching notes:

- `FullText.MatchMode` controls how the query text is matched. The default is `Any`: a document matches if it contains any of the query's terms (after stemming and stop word removal), ranked by relevance. `All` requires every term (the previous default behavior), `Phrase` requires the terms adjacent and in order, and `WebSearch` accepts web-search syntax (`"quoted phrase"`, `or`, `-exclude`).
- When both a vector and a text query are supplied, the two legs are combined with reciprocal rank fusion by default (`Hybrid.Strategy` `Rrf`). Each leg retrieves its own candidates, so a document does not need to match the text query to be returned; `Score` is the fused score in the range 0 to 1, and `VectorRank`, `TextRank`, and `VectorScore` explain where each result came from. `TextScore` is absent for results that did not match the text query.
- To restore the previous hybrid behavior (the text query is a required filter and raw scores are blended), set `Hybrid.Strategy` to `Filter` and `FullText.MatchMode` to `All`.
- `FullText.TextWeight` must be between 0.0 and 1.0, `FullText.Normalization` between 0 and 63, and `FullText.Language` must be a text search configuration installed on the server. Out-of-range values are rejected with HTTP 400.
- The search result may include a `Notice` explaining how the search was evaluated, for example when the text query contained only stop words or hybrid options were ignored.

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
