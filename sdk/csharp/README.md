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

Install the `RecallDb.Sdk` package (0.2.2 or later for everything below):

```bash
dotnet add package RecallDb.Sdk --version 0.2.2
```

Or add a project reference to `RecallDb.Sdk.csproj`:

```xml
<ItemGroup>
  <ProjectReference Include="path/to/RecallDb.Sdk/RecallDb.Sdk.csproj" />
</ItemGroup>
```

## Quick Start

```csharp
using RecallDb.Sdk;
using RecallDb.Sdk.Models;

using var client = new RecallDbClient("http://127.0.0.1:8600", "your-bearer-token");

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
- String values have named constants in `RecallDb.Sdk.Constants`: `HybridStrategies`, `FullTextMatchModes`, `FullTextSearchTypes`, `VectorSearchTypes`, `SortOrders`, `CollapseFields`, and `Capabilities`.

### Check server capabilities first

Servers that report the same version can support different search features, and a server that does not know a request field ignores it silently. Check before relying on one:

```csharp
using RecallDb.Sdk.Constants;

ServerInfo info = await client.GetServerInfoAsync();          // Name, Version, UptimeMs, Capabilities
bool canCollapse = await client.SupportsAsync(Capabilities.Collapse);
bool hasRecency = await client.SupportsAsync(Capabilities.HybridRecency);
```

`SupportsAsync` reads the capability list once and caches it for the client's lifetime; pass `refresh: true` to re-read it after a server upgrade. `Capabilities` is empty for servers that predate it.

### One call for chunked documents: collapse and recency

When each logical record is stored as several chunk documents, `Collapse` returns one hit per group (its best-scoring chunk), and `MaxResults`, `TotalRecords`, and continuation tokens count groups rather than chunks. `Hybrid.RecencyWeight` adds a third, recency signal to `Rrf` fusion that ranks groups by their newest `CreatedUtc`.

```csharp
SearchQuery query = new SearchQuery
{
    Vector = new VectorQuery { SearchType = VectorSearchTypes.CosineSimilarity, Embeddings = queryVector },
    FullText = new FullTextQuery { Query = "how do I rotate the signing key", TextWeight = 0.5 },
    Hybrid = new HybridQuery { Strategy = HybridStrategies.Rrf, RrfK = 60, CandidatePool = 40, RecencyWeight = 0.1 },
    Collapse = new CollapseQuery { Field = CollapseFields.Tag, TagKey = "parentKey" },
    MaxResults = 10
};

if (await client.SupportsAsync(Capabilities.Collapse) && await client.SupportsAsync(Capabilities.HybridRecency))
{
    SearchResult result = await client.SearchAsync("tenant-id", collection.Id, query);
    foreach (DocumentRecord d in result.Documents)
    {
        Console.WriteLine(d.GroupKey + " hits=" + d.GroupHits + " score=" + d.Score
            + " vectorRank=" + d.VectorRank + " textRank=" + d.TextRank + " recencyRank=" + d.RecencyRank);
    }
}
```

- `Collapse.Field` is `DocumentId` (the default) or `Tag` with `TagKey`. A document without a group value is its own group, keyed by its `DocumentKey`.
- Collapse works with vector-only, full-text-only, and hybrid `Rrf` and `Linear` searches; hybrid `Filter` rejects it with 400. For single-leg searches, `Collapse.CandidatePool` sets how many candidates are grouped. A `Notice` says when the pool filled up with fewer groups than you asked for.
- `RecencyWeight` is 0.0 to 1.0 and off by default. Ranks decide the fused score, so a small weight breaks near-ties rather than reordering strong matches.
- `FullText.MinimumShouldMatch` (1 to 3, `MatchMode` `Any` only) requires that many distinct query terms, which cuts ranking work on large collections.

### Stored vectors

Set `SearchQuery.IncludeEmbeddings = true` to get each hit's stored vector in `DocumentRecord.Embeddings`, for example for result diversity or duplicate detection. Vectors are sent as JSON numbers, about 4 KB per hit at 384 dimensions and 8 KB at 768, so ask for them only when you use them. They are off by default.

## Connections, timeouts, and retries

`new RecallDbClient(endpoint, token)` creates and owns its own `HttpClient`. Two other constructors let you control the transport:

```csharp
// Share one HttpClient (and its connection pool) across clients. The caller owns it; Dispose() leaves it open,
// and the client never changes its default headers (the bearer token is sent on each request).
HttpClient shared = new HttpClient();
using var a = new RecallDbClient("http://127.0.0.1:8600", tokenA, shared);
using var b = new RecallDbClient("http://127.0.0.1:8600", tokenB, shared);

// Send through your own handler, for example retries, logging, or telemetry. The caller owns the handler.
using var c = new RecallDbClient("http://127.0.0.1:8600", token, new RetryHandler(new SocketsHttpHandler()));
c.Timeout = TimeSpan.FromSeconds(30);   // per request, including reading the body; default 100 s
```

A request that exceeds `Timeout` throws `TimeoutException`; cancelling your own `CancellationToken` throws `OperationCanceledException` as usual. The SDK does not retry on its own. A handler that retries 429, 502, and 503 with backoff:

```csharp
public sealed class RetryHandler : DelegatingHandler
{
    public RetryHandler(HttpMessageHandler inner) : base(inner) { }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        for (int attempt = 0; ; attempt++)
        {
            HttpResponseMessage response = await base.SendAsync(request, token);
            int status = (int)response.StatusCode;
            if (attempt >= 3 || (status != 429 && status != 502 && status != 503)) return response;
            response.Dispose();
            await Task.Delay(TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt)), token);
        }
    }
}
```

## Errors

A failed call throws `RecallDbException` with `StatusCode` and the raw `ResponseBody`. When the body is the server's error object, `ErrorCode` holds its `Error` field and `ErrorMessage` its most specific message, and the exception message reads like `RecallDB API returned 400 (BadRequest): RrfK must be between 1 and 100000.`

**Behavior change in 0.2.2:** the `...ExistsAsync` methods return `true` for 200 and `false` for 404, and throw `RecallDbException` for anything else (401, 403, 429, 5xx). Earlier versions returned `false` for every non-200 status, so an unauthorized or failing server read as "does not exist".

All path segments (tenant, user, credential, collection, label, tag, document ids and keys) are URL-encoded, so keys containing `#`, `?`, `/`, `%`, or spaces round-trip. The client sends compact JSON, and its public surface carries nullable annotations: a nullable member is one the server may omit or that you may leave unset.

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
