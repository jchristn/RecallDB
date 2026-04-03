# Testing

Requires a running RecallDB server (with PostgreSQL/pgvector behind it).

## Test.Console

Console runner with colored output. Default endpoint: `http://127.0.0.1:8600`.

```bash
dotnet run --project src/Test.Console

# Custom endpoint and API key
dotnet run --project src/Test.Console -- --endpoint http://127.0.0.1:8600 --apikey recalldbadmin

# Export results to JSON
dotnet run --project src/Test.Console -- --results results.json
```

## Test.Xunit

xUnit runner for CI pipelines and `dotnet test`.

```bash
dotnet test src/Test.Xunit
```

## RecallDb.Sdk.TestHarness

SDK integration tests (standalone console app).

```bash
dotnet run --project sdk/csharp/RecallDb.Sdk.TestHarness
```
