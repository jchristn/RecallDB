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

`RecallDbSuites.All` runs these suites in order:

- **RecallDb**: the main REST suite. Its search cases run against a 10-document
  seed set (`SearchDataSetup`) whose vector-test baselines stay fixed.
- **RecallDbHybridSearch** (`HybridSearchSuites`): full-text match modes, the
  hybrid strategies (`Rrf`, `Linear`, `Filter`), search input validation, and
  the stored `content_tsv` schema. It creates its own collection in the
  `default` tenant and deletes it at the end.
- **RecallDbSearchGrouping** (`SearchGroupingSuites`): the hybrid recency signal
  (`Hybrid.RecencyWeight`), result collapse (`SearchQuery.Collapse`),
  `FullText.MinimumShouldMatch`, and their validation. It creates its own
  collection in the `default` tenant, so the fixed baselines of the other suites
  are unaffected, and deletes it at the end.
- **RecallDbDocumentBehavior** (`DocumentBehaviorSuites`): write status codes and
  filter semantics, each in both directions: a duplicate `DocumentKey` is 409
  (and a key repeated inside a batch is 400, with nothing stored), updating a
  missing key is 404, update keeps omitted labels and tags and replaces or
  clears sent ones, wrong-length vectors and collections above 2000 dimensions
  are 400, `LabelFilter.RequiredMode` (`All` by default, `Any`) in search,
  enumeration, and delete-by-filter, and `Vector.EfSearch` (clamped values,
  vector-only paging past 40 hits). It uses its own collection. Its small
  collection is usually searched without the HNSW index, so the index's
  candidate limit itself is verified on a large collection (see below).
- **RecallDbCollectionIntegrity** (`CollectionIntegritySuites`): collection
  creation, index naming, and the startup schema repair.
- **RecallDbMcp**: the MCP tool surface, including hybrid and collapsed,
  recency-weighted `search_query` cases and the `server_info` capability list.

The main suite also checks that `GET /` reports every capability in
`RecallDb.Core.SearchCapabilities` (`HealthReportsCapabilities`), and that the
OpenAPI document's document-write examples carry client fields such as
`Content` but none of the search-only response fields (`Score`, `VectorRank`,
`TextRank`, `RecencyRank`, `GroupKey`, `GroupHits`), so the API Explorer does
not prefill them (`OpenApiDocumentExampleOmitsResponseFields`).

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

The grouping suite's seed is two chunked parents with explicit `CreatedUtc`
values: `p-old` (2 chunks, January) and `p-new` (3 chunks, June), tagged
`parentKey` and with `DocumentId` equal to the parent. The first two `p-new`
chunks duplicate `p-old`'s in vector and text, and `p-old` is written first, so
without recency `p-old` wins every tie on id, and recency can be shown to flip
it. Because fusion is rank-based, the duplicate trails by one rank in each leg,
so the tie-break case uses `RrfK = 1` and `RecencyWeight = 1.0`; the formula case
checks every score against `((1-w)/(k+vr) + w/(k+tr) + r/(k+rr)) * (k+1)/(1+r)`.
`p-loose` has neither a `DocumentId` nor the tag, to exercise the
`DocumentKey` fallback, and `m-two` / `m-one` share two and one terms of
"alpha beta" for `MinimumShouldMatch`.

Each feature is covered in both directions. Positive cases include recency on
and off, collapse by tag and by `DocumentId` for vector-only, full-text-only,
hybrid `Rrf`, and hybrid `Linear`, and the edges of every range
(`SearchValidationBoundariesAccepted`: `RecencyWeight` 0 and 1, collapse pool 1
and 10000, a 256-character `TagKey`, `MinimumShouldMatch` 1 and 3). Negative
cases are rejected with 400: `RecencyWeight` below 0, above 1, or not a number;
collapse by tag with a missing, null, or blank `TagKey`; an unknown collapse
field; a collapse pool of 0 or 10001; a 257-character `TagKey`; collapse with
the `Filter` strategy or with no query; `MinimumShouldMatch` 0 or 4, or above 1
with `All`, `Phrase`, or `WebSearch`. Two ignored-input cases check that
`RecencyWeight` has no effect on `Linear` (with a `Notice`) or on single-leg
searches.

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
  leg (a Bitmap Index Scan on `_tsv`). Collapse by tag adds an Index Scan on
  `_t_dkey`. The simplest way to see the exact statements the server runs is
  `auto_explain` on a throwaway test database, never a shared one:
  `ALTER DATABASE recalldb SET session_preload_libraries = 'auto_explain'`, then
  `auto_explain.log_min_duration = 0`, `log_analyze = on`, `log_buffers = on`,
  and `log_format = 'json'`. Restart the server so its pooled connections pick
  the settings up, read the plans from the PostgreSQL log (`docker logs`), and
  finish with `ALTER DATABASE recalldb RESET ALL`. `Database.LogQueries` also
  shows the SQL, but it splits long statements across log lines.
- **Vector index candidate limit.** On a collection large enough for the HNSW
  index to be used (for example SciFact split into about 13,700 chunks), check
  that a vector-only search with `MaxResults` 100 returns 100 hits, that the
  page at offset 40 is full, that `Vector.EfSearch: 20` caps a 40-hit page at
  20, and how many hits filtered top-10 searches return at the default and at
  `EfSearch: 1000`. Results are in docs/RETRIEVAL.md, section 6.5.
- **Latency.** Measure p50/p95 of `SearchResult.TotalMs` for vector-only,
  full-text, and hybrid searches at 10k and 100k documents.

## Dashboard unit tests

The Search view's request builder and form validation live in
`dashboard/src/views/searchRequest.js`, without React, so they can be tested
with Node's built-in runner and no extra dependencies:

```bash
cd dashboard
npm test
```

The cases (`dashboard/test/searchRequest.test.js`) check what each control
sends and when it is left out (recency weight, collapse field, tag key, and
pool, minimum terms to match, include embeddings), that the validation accepts
the edges of each server range and rejects the values the server rejects, that
hidden fields are neither validated nor sent, that a plain hybrid search builds
the same body as before, which result columns appear, and that the API
Explorer's extra search examples are offered only for the search operation and
pass the same validation.

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

## SDK harnesses

Each SDK has a standalone integration harness. All three take the endpoint and
bearer token as positional arguments (defaults `http://127.0.0.1:8600` and
`recalldbadmin`), create their own tenant or collections, and clean up.

```bash
dotnet run --project sdk/csharp/RecallDb.Sdk.TestHarness -- http://127.0.0.1:8600 recalldbadmin
node sdk/js/test-harness.js http://127.0.0.1:8600 recalldbadmin
PYTHONDONTWRITEBYTECODE=1 python sdk/python/test_harness.py http://127.0.0.1:8600 recalldbadmin
```

Besides the CRUD, enumeration, and search cases that mirror Test.Automated, each
harness covers the 0.2.2 SDK surface: server info and `supports`, the hybrid
round trip fields, a search `Notice`, `IncludeEmbeddings`, collapse by tag with
recency, vector-only collapse, hybrid `Linear` collapse, `MinimumShouldMatch`,
rejected collapse and `MinimumShouldMatch` requests (400), label `RequiredMode`
`All`/`Any`, `EfSearch`, duplicate keys (409), updating a missing key (404),
update keeping labels and tags, a wrong-length query vector (400), structured errors for both
server error shapes, a request timeout (a delaying test handler in C#, a local
stub server in JavaScript and Python), cancellation, a document key and id
containing `#`, `?`, `/`, `%`, and a space, and `exists` throwing on 401 while
returning false on 404. The C# harness also checks that an injected
`HttpClient` is left usable and unmodified, and that a `DelegatingHandler` sees
compact JSON and a per-request `Authorization` header. These cases need a server
that reports the `search.collapse`, `search.hybrid.recency`, and
`search.fulltext.minimum-should-match` capabilities.
