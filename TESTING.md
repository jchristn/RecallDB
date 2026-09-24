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

## Suites

`RecallDbSuites.All` runs three suites in order:

- **RecallDb**: the main REST suite. Its search cases run against a 10-document
  seed set (`SearchDataSetup`) whose vector-test baselines stay fixed.
- **RecallDbHybridSearch** (`HybridSearchSuites`): full-text match modes, the
  hybrid strategies (`Rrf`, `Linear`, `Filter`), search input validation, and
  the stored `content_tsv` schema. It creates its own collection in the
  `default` tenant and deletes it at the end.
- **RecallDbMcp**: the MCP tool surface, including a hybrid `search/query` case.

The hybrid suite's 11 seed documents exist to make the new semantics
observable, so each has a job. `hy-guide` is the best semantic match for the
question "how do I run the test suite" but shares none of its keywords, which
is the regression the Rrf strategy fixes (the legacy engine returned nothing
for it). `hy-run` and `hy-harness` match three and one of the query terms, so
ranking under `MatchMode = Any` is observable. `hy-chunk` and `hy-keychunk`
are a phrase-order pair ("chunk key" vs "key chunk") for `Phrase` and
`WebSearch`. `hy-hidden-v` and `hy-hidden-t` carry a `hidden` label, and are a
strong vector match and a strong text match respectively, so a label filter can
be shown to apply to both hybrid legs.

Two schema cases (`CollectionHasStoredTsVector`, `SchemaBackfillIdempotent`)
need direct database access. They run only when `RECALLDB_TEST_DB` holds an
Npgsql connection string for the database the server under test uses, and
return without asserting otherwise. The backfill case drops the collection's
`content_tsv` column, recreates the legacy `_fts` index, runs the driver's
startup schema pass twice, and checks that the first pass migrates the table
and the second leaves it untouched (same `relfilenode`, same indexes).

```bash
RECALLDB_TEST_DB="Host=127.0.0.1;Port=5432;Username=recalldb;Password=recalldb;Database=recalldb" \
  dotnet run --project src/Test.Automated
```

## Retrieval-quality and performance verification

Search-ranking changes are verified once before sign-off, outside the unit
suites, and the numbers go in the PR description.

- **Retrieval quality.** Run the Isis benchmark harness against a local build
  (`dotnet run --project C:\Code\AgentMemory\src\Test.Benchmark -- retrieval --dataset ...`)
  on isis-live, SciFact, and LongMemEval-S, and record Keyword and Hybrid
  recall@5 and nDCG@10 before and after. Targets are in `HYBRID_SEARCH_FIX.md` section 9.
- **Query plans.** On a large collection, `EXPLAIN (ANALYZE, BUFFERS)` the
  full-text query (expect a Bitmap Index Scan on `idx_col_<id>_tsv`), the
  hybrid vector leg (an Index Scan on `idx_col_<id>_hnsw`), and the hybrid text
  leg (a Bitmap Index Scan on `_tsv`). Set `Database.LogQueries` to capture the
  generated SQL. Long statements are split across log lines, so rebuild the
  full text from the builder in `SearchMethods` when they are.
- **Latency.** Measure p50/p95 of `SearchResult.TotalMs` for vector-only,
  full-text, and hybrid searches at 10k and 100k documents.

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
