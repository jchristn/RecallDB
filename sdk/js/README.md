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
- **Search** - Vector similarity/distance search with label, tag, terms, date, and pagination filters

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
