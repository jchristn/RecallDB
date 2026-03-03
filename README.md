<p align="center">
  <img src="assets/logo-black.png" alt="RecallDB" style="max-width: 256px; max-height: 256px;" width="256" height="256">
</p>

<h3 align="center">The persistence and retrieval layer your RAG pipeline is missing.</h3>

<p align="center">
  <a href="#quick-start">Quick Start</a> &middot;
  <a href="REST_API.md">API Docs</a> &middot;
  <a href="#sdks">SDKs</a> &middot;
  <a href="#search">Search</a> &middot;
  <a href="CHANGELOG.md">Changelog</a>
</p>

---

## The Problem

You're building a RAG pipeline. You need to store embeddings, but you also need the raw content, chunk positions, content types, labels, tags, hashes, and ETags. You need multi-tenant isolation. You need filtering that goes beyond "nearest neighbor."

So you bolt pgvector onto Postgres, write migration scripts, hand-roll a chunking schema, build an API layer, add auth, wire up metadata tables, and six weeks later you have a fragile bespoke system that solves one project's needs.

**RecallDB is the architecture you'd build if you had the time to do it right.**

## Why RecallDB

Most vector databases store embeddings and call it a day. RecallDB stores the **complete context** your AI application needs to retrieve, rank, and act on information:

| What you store | Why it matters |
|---|---|
| **Vector embeddings** | Semantic similarity search via pgvector with HNSW indexing |
| **Raw content** | Full text, code, tables, lists, hyperlinks, binary data, images |
| **Content types** | 9 typed content categories so your retrieval pipeline knows *what* it's looking at |
| **Chunk positions** | Ordered document segments with `document_id` + `position` grouping |
| **Labels** | Categorical filters with AND/AND-NOT logic for scoped retrieval |
| **Key-value tags** | Structured metadata with 10 filter operators (equals, contains, range, null checks) |
| **SHA256 hashes + ETags** | Deduplication, cache invalidation, and change detection out of the box |
| **Content length** | Token budget awareness without recomputing |
| **Full-text search** | TF-IDF-like relevance scoring with ts_rank &mdash; find documents by lexical relevance, not just semantic similarity |

This isn't a thin wrapper around pgvector. It's an **opinionated persistence schema** that normalizes how AI-ready data is stored, indexed, and retrieved, so you stop reinventing the storage layer for every project.

## What You Get

- **Multi-tenant isolation** &mdash; tenants, users, credentials, and collections are fully scoped. One deployment serves many clients.
- **Per-collection vector tables** &mdash; each collection gets its own Postgres table with dedicated HNSW indexes (`m=16`, `ef_construction=64`). No noisy-neighbor problems.
- **5 distance metrics** &mdash; cosine similarity, cosine distance, Euclidean similarity, Euclidean distance, inner product. Pick what fits your embedding model.
- **Three search modes** &mdash; vector similarity (nearest-neighbor), full-text relevance (ts_rank/ts_rank_cd scored), and hybrid (blended vector + full-text scoring with configurable weights). Mix and match in a single request.
- **Compound search queries** &mdash; combine any search mode with label filters, tag conditions, content term matching, and date ranges in a single request.
- **Bring your own embeddings** &mdash; no vendor lock-in to any embedding provider. Use OpenAI, Cohere, Ollama, or anything that outputs a float array.
- **40+ REST endpoints** &mdash; full CRUD for tenants, users, credentials, collections, documents, labels, and tags. Includes batch delete by keys and filter-based delete for bulk operations.
- **SDKs in C#, Python, and JavaScript** &mdash; typed clients ready to drop into your stack.
- **React dashboard** &mdash; manage tenants, collections, and documents visually. Search with a query builder.
- **Docker Compose deployment** &mdash; Postgres + pgvector, API server, and dashboard in one command.
- **MIT licensed** &mdash; use it however you want.

## Quick Start

```bash
cd docker
docker compose up
```

API at `http://localhost:8600`, dashboard at `http://localhost:8601`.

### Default Credentials

| Credential | Value |
|---|---|
| Admin API Key | `recalldbadmin` |
| User | `admin@recall` / `password` |
| Bearer Token | `default` |

### Store a Document

```bash
curl -X PUT http://localhost:8600/v1.0/tenants/ten_default/collections/col_default/documents \
  -H "Authorization: Bearer recalldbadmin" \
  -H "Content-Type: application/json" \
  -d '{
    "DocumentId": "readme-guide",
    "Position": 0,
    "ContentType": "Text",
    "Content": "RecallDB stores embeddings alongside rich metadata.",
    "Embeddings": [0.1, 0.2, 0.3],
    "Labels": ["documentation", "guide"],
    "Tags": [
      { "Key": "source", "Value": "readme" },
      { "Key": "version", "Value": "1.0" }
    ]
  }'
```

### Search

```bash
curl -X POST http://localhost:8600/v1.0/tenants/ten_default/collections/col_default/search \
  -H "Authorization: Bearer recalldbadmin" \
  -H "Content-Type: application/json" \
  -d '{
    "Vector": {
      "SearchType": "CosineSimilarity",
      "Embeddings": [0.1, 0.2, 0.3],
      "MinimumScore": 0.7
    },
    "LabelFilter": {
      "Required": ["documentation"]
    },
    "Terms": {
      "Required": ["metadata"]
    },
    "MaxResults": 10
  }'
```

### Full-Text Search

```bash
curl -X POST http://localhost:8600/v1.0/tenants/default/collections/default/search \
  -H "Authorization: Bearer default" \
  -H "Content-Type: application/json" \
  -d '{
    "FullText": {
      "Query": "machine learning neural networks",
      "SearchType": "TsRank",
      "MinimumScore": 0.01
    },
    "MaxResults": 10
  }'
```

## Search

RecallDB search goes well beyond nearest-neighbor. A single query can combine any of these filters:

**Vector** &mdash; similarity or distance search across 5 metrics with score/distance thresholds.

**Labels** &mdash; require or exclude categorical labels with boolean logic.

**Tags** &mdash; filter on key-value metadata using `Equals`, `NotEquals`, `GreaterThan`, `LessThan`, `Contains`, `ContainsNot`, `StartsWith`, `EndsWith`, `IsNull`, `IsNotNull`.

**Terms** &mdash; case-insensitive substring matching on document content. Require terms, exclude terms, or both.

**Full-Text Search** &mdash; scored full-text search powered by Postgres `tsvector` and `tsquery`. Ranking uses `ts_rank` or `ts_rank_cd` for TF-IDF-like relevance scoring. Includes stemming, stop word removal, and configurable language support. Set a `MinimumScore` threshold to filter low-relevance results. This is distinct from the `Terms` substring filter: `Terms` does literal substring matching with no ranking, while `FullText` scores and ranks results by lexical relevance.

**Hybrid Search** &mdash; combine vector similarity and full-text relevance in a single query with configurable blending weights. The final score is a weighted sum of the normalized vector score and full-text rank, letting you tune how much semantic vs. lexical relevance influences result ordering.

**Date ranges** &mdash; `CreatedBefore` and `CreatedAfter` for temporal scoping.

**Neighbor retrieval** &mdash; include surrounding chunks for contextual windows (`IncludeNeighbors: N` returns up to N chunks before and after each match).

**Pagination** &mdash; `MaxResults` (1-1000) with continuation tokens for large result sets.

**Sort** &mdash; by score, distance, or creation date in ascending or descending order.

## SDKs

### C\#

```csharp
var client = new RecallDbClient("http://localhost:8600", "recalldbadmin");

// Vector search
var results = await client.SearchAsync("ten_default", "col_default", new SearchQuery
{
    Vector = new VectorQuery
    {
        SearchType = SearchTypeEnum.CosineSimilarity,
        Embeddings = new List<float> { 0.1f, 0.2f, 0.3f },
        MinimumScore = 0.7
    },
    MaxResults = 10
});

// Vector search with neighbor retrieval
var neighborResults = await client.SearchAsync("ten_default", "col_default", new SearchQuery
{
    Vector = new VectorQuery
    {
        SearchType = SearchTypeEnum.CosineSimilarity,
        Embeddings = new List<float> { 0.1f, 0.2f, 0.3f }
    },
    IncludeNeighbors = 2,
    MaxResults = 10
});

// Full-text search
var ftResults = await client.SearchAsync("ten_default", "col_default", new SearchQuery
{
    FullText = new FullTextQuery
    {
        Query = "machine learning neural networks",
        SearchType = FullTextSearchTypeEnum.TsRank,
        MinimumScore = 0.01
    },
    MaxResults = 10
});
```

### Python

```python
from recalldb_sdk import RecallDbClient

client = RecallDbClient("http://localhost:8600", "recalldbadmin")
results = client.search("ten_default", "col_default", {
    "Vector": {
        "SearchType": "CosineSimilarity",
        "Embeddings": [0.1, 0.2, 0.3],
        "MinimumScore": 0.7
    },
    "MaxResults": 10
})

# Vector search with neighbor retrieval
neighbor_results = client.search("ten_default", "col_default", {
    "Vector": {
        "SearchType": "CosineSimilarity",
        "Embeddings": [0.1, 0.2, 0.3]
    },
    "IncludeNeighbors": 2,
    "MaxResults": 10
})
```

### JavaScript

```javascript
import { RecallDbClient } from 'recalldb-sdk';

const client = new RecallDbClient('http://localhost:8600', 'recalldbadmin');
const results = await client.search('ten_default', 'col_default', {
  Vector: {
    SearchType: 'CosineSimilarity',
    Embeddings: [0.1, 0.2, 0.3],
    MinimumScore: 0.7,
  },
  MaxResults: 10,
});

// Vector search with neighbor retrieval
const neighborResults = await client.search('ten_default', 'col_default', {
  Vector: {
    SearchType: 'CosineSimilarity',
    Embeddings: [0.1, 0.2, 0.3],
  },
  IncludeNeighbors: 2,
  MaxResults: 10,
});
```

## Configuration

Server settings live in `recalldb.json`. Environment variables override database connection settings:

| Variable | Description |
|---|---|
| `RECALLDB_DB_HOST` | PostgreSQL hostname |
| `RECALLDB_DB_PORT` | PostgreSQL port |
| `RECALLDB_DB_NAME` | Database name |
| `RECALLDB_DB_USER` | Database username |
| `RECALLDB_DB_PASS` | Database password |

## Architecture

```
┌─────────────┐      ┌─────────────────┐      ┌──────────────────────────┐
│  Your App   │────> |  RecallDB API   │────> │  PostgreSQL + pgvector   │
│  (SDK)      │      │  (REST, Auth)   │      │                          │
└─────────────┘      └─────────────────┘      │  tenants                 │
                     ┌─────────────────┐      │  users / credentials     │
                     │   Dashboard     │────> │  collections             │
                     │   (React SPA)   │      │  collection_{id}  [HNSW] │
                     └─────────────────┘      │  collection_{id}_labels  │
                                              │  collection_{id}_tags    │
                                              └──────────────────────────┘
```

Each collection creates its own Postgres tables with a dedicated HNSW vector index. Labels and tags are stored in separate relational tables and joined at query time, keeping the vector index lean and the metadata queryable.

## Building from Source

```bash
dotnet restore src/RecallDb.sln
dotnet build src/RecallDb.sln
```

## API Reference

See [REST_API.md](REST_API.md) for the complete endpoint reference and request/response examples.

A [Postman collection](RecallDB.postman_collection.json) is included for interactive exploration.

## License

MIT &mdash; see [LICENSE.md](LICENSE.md).
