# Testing

Requires a running RecallDB server (with PostgreSQL/pgvector behind it).

The integration test cases are defined once, centrally, in **Test.Shared**
(`RecallDbSuites`) using [Touchstone](https://www.nuget.org/packages/Touchstone).
That single source of truth is then exposed through three runners:

| Project          | Role                              | Touchstone package        |
|------------------|-----------------------------------|---------------------------|
| `Test.Shared`    | Central source of truth (suites)  | `Touchstone.Core`         |
| `Test.Automated` | Console/CLI runner                | `Touchstone.Cli`          |
| `Test.Xunit`     | xUnit adapter (`dotnet test`)     | `Touchstone.XunitAdapter` |
| `Test.Nunit`     | NUnit adapter (`dotnet test`)     | `Touchstone.NunitAdapter` |

All runners honor the `RECALLDB_ENDPOINT` and `RECALLDB_APIKEY` environment
variables. `Test.Automated` additionally accepts `--endpoint` / `--apikey`
command-line flags. The defaults are `http://127.0.0.1:8600` and `recalldbadmin`.

## Test.Automated (CLI runner)

Console runner with colored output. Default endpoint: `http://127.0.0.1:8600`.

```bash
dotnet run --project src/Test.Automated

# Custom endpoint and API key
dotnet run --project src/Test.Automated -- --endpoint http://127.0.0.1:8600 --apikey recalldbadmin

# Export results to JSON
dotnet run --project src/Test.Automated -- --results results.json
```

## Test.Xunit

xUnit runner for CI pipelines and `dotnet test`.

```bash
dotnet test src/Test.Xunit

# Target a non-default endpoint
RECALLDB_ENDPOINT=http://127.0.0.1:8600 RECALLDB_APIKEY=recalldbadmin dotnet test src/Test.Xunit
```

## Test.Nunit

NUnit runner for CI pipelines and `dotnet test`.

```bash
dotnet test src/Test.Nunit

# Target a non-default endpoint
RECALLDB_ENDPOINT=http://127.0.0.1:8600 RECALLDB_APIKEY=recalldbadmin dotnet test src/Test.Nunit
```

## RecallDb.Sdk.TestHarness

SDK integration tests (standalone console app).

```bash
dotnet run --project sdk/csharp/RecallDb.Sdk.TestHarness
```
