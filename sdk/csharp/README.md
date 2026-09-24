# RecallDB C# SDK

A .NET client library for interacting with the RecallDB vector database REST API.

## Overview

The RecallDB C# SDK provides a strongly-typed interface for all RecallDB operations including:

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

- .NET 10.0 SDK

## Installation

Add a project reference to `RecallDb.Sdk.csproj`:

```xml
<ItemGroup>
  <ProjectReference Include="path/to/RecallDb.Sdk/RecallDb.Sdk.csproj" />
</ItemGroup>
```

## Quick Start

```csharp
using RecallDb.Sdk;
using RecallDb.Sdk.Models;

using var client = new RecallDbClient("http://localhost:8600", "your-bearer-token");

// Check server health
var health = await client.HealthAsync();
Console.WriteLine(health["Name"]); // RecallDB

// Authenticate
var auth = await client.AuthenticateAsync(new AuthenticateRequest { BearerToken = "your-bearer-token" });
Console.WriteLine(auth.Success); // True

// Create a collection
var collection = await client.CreateCollectionAsync("tenant-id", new CollectionMetadata
{
    Name = "my-collection",
    Dimensionality = 384
});

// Create a document
var doc = await client.CreateDocumentAsync("tenant-id", collection.Id, new DocumentRecord
{
    DocumentKey = "doc-1",
    Content = "Hello world",
    ContentType = "Text",
    Embeddings = new List<float> { 0.1f, 0.2f, 0.3f /* ... */ }
});

// Search
var results = await client.SearchAsync("tenant-id", collection.Id, new SearchQuery
{
    Vector = new VectorQuery
    {
        SearchType = "CosineSimilarity",
        Embeddings = new List<float> { 0.1f, 0.2f, 0.3f /* ... */ }
    },
    MaxResults = 10
});
```

### Full-text and hybrid search

```csharp
// Full-text only: matches documents containing any of the terms (MatchMode "Any" is the default)
SearchResult textResults = await client.SearchAsync("tenant-id", collection.Id, new SearchQuery
{
    FullText = new FullTextQuery { Query = "how do I run the test suite", MatchMode = "Any" },
    MaxResults = 10
});

// Hybrid: vector and text legs combined with reciprocal rank fusion
SearchResult hybridResults = await client.SearchAsync("tenant-id", collection.Id, new SearchQuery
{
    Vector = new VectorQuery
    {
        SearchType = "CosineSimilarity",
        Embeddings = new List<float> { 0.1f, 0.2f, 0.3f /* ... */ }
    },
    FullText = new FullTextQuery { Query = "run the test suite", TextWeight = 0.5 },
    Hybrid = new HybridQuery { Strategy = "Rrf", RrfK = 60, CandidatePool = 100 },
    MaxResults = 10
});

foreach (DocumentRecord d in hybridResults.Documents)
{
    Console.WriteLine(d.DocumentKey + " score=" + d.Score + " vectorRank=" + d.VectorRank + " textRank=" + d.TextRank);
}

// Legacy hybrid: text match required, raw scores blended
SearchResult legacyResults = await client.SearchAsync("tenant-id", collection.Id, new SearchQuery
{
    Vector = new VectorQuery
    {
        SearchType = "CosineSimilarity",
        Embeddings = new List<float> { 0.1f, 0.2f, 0.3f /* ... */ }
    },
    FullText = new FullTextQuery { Query = "machine learning", MatchMode = "All" },
    Hybrid = new HybridQuery { Strategy = "Filter" },
    MaxResults = 10
});
```

Full-text matching notes:

- `FullText.MatchMode` controls how the query text is matched. The default is `Any`: a document matches if it contains any of the query's terms (after stemming and stop word removal), ranked by relevance. `All` requires every term (the previous default behavior), `Phrase` requires the terms adjacent and in order, and `WebSearch` accepts web-search syntax (`"quoted phrase"`, `or`, `-exclude`).
- When both a vector and a text query are supplied, the two legs are combined with reciprocal rank fusion by default (`Hybrid.Strategy` `Rrf`). Each leg retrieves its own candidates, so a document does not need to match the text query to be returned; `Score` is the fused score in the range 0 to 1, and `VectorRank`, `TextRank`, and `VectorScore` explain where each result came from. `TextScore` is absent for results that did not match the text query.
- To restore the previous hybrid behavior (the text query is a required filter and raw scores are blended), set `Hybrid.Strategy` to `Filter` and `FullText.MatchMode` to `All`.
- `FullText.TextWeight` must be between 0.0 and 1.0, `FullText.Normalization` between 0 and 63, and `FullText.Language` must be a text search configuration installed on the server. Out-of-range values are rejected with HTTP 400.
- The search result may include a `Notice` explaining how the search was evaluated, for example when the text query contained only stop words or hybrid options were ignored.

## Project Structure

| Path | Description |
|------|-------------|
| `RecallDb.Sdk/` | SDK client library |
| `RecallDb.Sdk/Models/` | Typed model classes |
| `RecallDb.Sdk.TestHarness/` | Integration test harness (mirrors Test.Automated) |

## Running Tests

See [GETTING_STARTED.md](GETTING_STARTED.md) for instructions on running the integration test harness.

## License

MIT
