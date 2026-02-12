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
- **Search** - Vector similarity/distance search with label, tag, terms, date, and pagination filters

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
