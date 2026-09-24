# Full-Text and Hybrid Search Fix: Product Plan

**Status:** proposed
**Target version:** v0.2.1. No version bump; this rolls into the existing release. See [§11 Release notes](#11-release-and-versioning-notes) for what that means for immutable package registries.
**Author:** plan derived from the Isis benchmark investigation (2026-09-24).

---

## 1. Summary

RecallDB's full-text search is effectively unusable for natural-language queries, and its hybrid search inherits the
defect while hiding the vector ranking it is supposed to add. There are three root causes, all in one method,
`SearchMethods.SearchAsync` (`src/RecallDb.Core/Database/Postgresql/Implementations/SearchMethods.cs`):

1. **Full-text requires every term.** The text query is built with `plainto_tsquery(...)`, which ANDs every lexeme. A
   question such as *"how do I run the test suite"* matches only documents that contain *run* **and** *test* **and**
   *suite*.
2. **Hybrid applies the text query as a required filter.** The full-text match condition is added to the `WHERE`
   clause in hybrid mode too (L223-227). Hybrid therefore returns only documents that satisfy the all-terms text
   match, and the vector ranking is applied only inside that filtered set. When no document contains every term,
   hybrid returns **zero results**, which is strictly worse than a vector-only search.
3. **The hybrid blend mixes incomparable scales.** `score = (1 − w) · vectorScore + w · ts_rank` adds a cosine
   similarity in [0, 1] to a `ts_rank` value that is usually 0.01–0.1 (and ~0–0.5 after normalization 32). `TextWeight`
   therefore does not mean what it says, and the blended score cannot be used as a threshold.

This plan fixes all three:

- **New `MatchMode`, defaulting to `Any`.** Full-text defaults to OR semantics ranked by relevance. `All`, `Phrase`
  and `WebSearch` are available explicitly.
- **Hybrid is a union, fused by weighted Reciprocal Rank Fusion (RRF) by default.** The vector and text legs are
  retrieved independently and fused in one statement. Both legs can use their indexes (HNSW for vector, GIN for
  text). A text match is no longer required.
- **The legacy behaviors remain opt-in.** `MatchMode = All` restores all-terms matching. `HybridStrategy = Filter`
  restores "vector ranking within text matches". A corrected `Linear` blend is also offered.
- **A stored, indexed `tsvector` column.** Ranking stops re-tokenizing every candidate row, and the index always
  matches the query expression.
- **Input validation for `FullText`.** Language is checked against an allowlist, which also closes an injection
  surface, along with Normalization, TextWeight and the hybrid parameters.

### Evidence

Measured by the Isis benchmark harness (`C:\Code\AgentMemory\benchmarks`) against RecallDB v0.2.1:

| Dataset | Keyword recall@5 (today) | Vector recall@5 | Keyword recall@5 with OR semantics (simulated) |
|---|---|---|---|
| isis-live (24 agent memories, 90 answerable questions) | **0.072** | 0.941 | **0.859** |
| SciFact / BEIR (5,183 abstracts, 300 queries) | **0.056** | 0.732 | not simulated; see §9 targets |
| LongMemEval-S (60 multi-session haystacks) | **0.098** | 0.947 | not simulated |

On isis-live, simulating OR semantics plus RRF fusion (k = 60) moved hybrid from nDCG@10 0.848 to **0.867** and
MRR@10 from 0.814 to **0.838**. Isis had to work around finding 2 client-side: it issues two separate RecallDB
searches and fuses them itself (`Isis.Core/Stores/RecallDb/RecallDbMemoryStore.cs`). That is extra round trips and
duplicated logic every RecallDB client would otherwise have to reinvent.

All SQL in this plan was validated against PostgreSQL 15.4, the version in the `ankane/pgvector:v0.5.1` image
RecallDB ships with.

---

## 2. Goals and non-goals

**Goals**

- A natural-language text query returns documents that match *any* of its meaningful terms, ranked so that documents
  matching more (and rarer) terms come first.
- Hybrid search never returns fewer relevant results than the better of its two legs, and it surfaces documents that
  match only semantically or only lexically.
- `TextWeight` has a well-defined meaning in every hybrid strategy.
- Hybrid scores are normalized to [0, 1] so thresholds are meaningful.
- Both legs of hybrid use their indexes (HNSW for vector, GIN for text). Neither forces a sequential scan.
- Existing callers can restore today's behavior with one field (`MatchMode = All`, `HybridStrategy = Filter`).
- Everything ships in v0.2.1: server, Core, SDKs (C#, JS, Python), MCP, dashboard, tests, docs.

**Non-goals** (tracked in §12)

- BM25 scoring. Postgres' `ts_rank`/`ts_rank_cd` stay the lexical scorers; BM25 needs an extension (for example
  `pg_search`/ParadeDB) and is a separate decision.
- Per-collection text-search language configuration. The index language stays `english`; other languages still work
  through the expression path, just without the index.
- Learned or cross-encoder reranking.
- Fixing post-`LIMIT` vector score thresholds and `TotalRecords` under vector thresholds. This is a pre-existing and
  adjacent defect, and it is noted here only where full-text touches it.

---

## 3. Current behavior (reference)

Paths are relative to the repository root. Line numbers are from commit `d5d4e30`.

| Concern | Location | Behavior today |
|---|---|---|
| tsquery construction | `SearchMethods.cs` L92-102 | `plainto_tsquery('<lang>', '<query>')`; `Sanitize` only doubles single quotes (`PostgresqlDatabaseDriver.cs` L397-401) |
| Hybrid score | `SearchMethods.cs` L110-122 | `(1−w)·vectorScore + w·ts_rank(...)`, raw scales |
| Required text match | `SearchMethods.cs` L223-227 | `conditions.Add(ftsMatchCondition)` in both full-text **and** hybrid |
| ORDER BY | `SearchMethods.cs` L264-274 | HNSW-friendly `distance ASC` only for vector-only; hybrid orders by the blended score, so HNSW cannot be used |
| Count | `SearchMethods.cs` L293-299 | Same `WHERE`, so `TotalRecords` counts only documents that match every text term |
| Text threshold | `SearchMethods.cs` L344-349 | `FullText.MinimumScore` is applied in C# **after** `LIMIT`, so pages come back short and `TotalRecords` is wrong |
| Index | `Queries/DynamicTableQueries.cs` L51 | `GIN (to_tsvector('english', COALESCE(content,'')))`; used only when `Language == "english"`. `ts_rank` recomputes `to_tsvector` for every candidate |
| Validation | `src/RecallDb.Server/Services/SearchService.cs` L52-183 | No `FullText` validation. `DeriveSearchMode` (L203-212) counts `FullText != null` as full-text even when `Query` is blank, which disagrees with `SearchMethods` L79 |
| Enum | `src/RecallDb.Core/Enums/TextSearchTypeEnum.cs` | `TsRank`, `TsRankCd` |
| Model | `src/RecallDb.Core/Models/FullTextQuery.cs` | `Query`, `SearchType`, `Language` ("english"), `Normalization` (32), `MinimumScore`, `TextWeight` (0.5) |

---

## 4. Design

### 4.1 Match modes (`FullTextQuery.MatchMode`)

Add `TextMatchModeEnum` (`src/RecallDb.Core/Enums/TextMatchModeEnum.cs`), serialized as a string:

| Value | tsquery | Semantics | Use for |
|---|---|---|---|
| `Any` (**default**) | OR of the query's normalized lexemes (below) | Matches documents containing any meaningful term; `ts_rank` rewards more and denser matches | Natural-language questions; the default for agents and RAG |
| `All` | `plainto_tsquery(lang, q)` | Every term required (**today's behavior**) | Precise keyword filtering |
| `Phrase` | `phraseto_tsquery(lang, q)` | Terms adjacent and in order | Exact phrases, identifiers split by the parser |
| `WebSearch` | `websearch_to_tsquery(lang, q)` | Google-style syntax: `"quoted phrase"`, `or`, `-exclude` | Power users and UI search boxes |

**`Any` construction.** Build the OR query from the lexemes `to_tsvector` produces, so the query and the index agree
exactly on stemming and stop-word removal:

```sql
(SELECT COALESCE(NULLIF(string_agg(quote_literal(lexeme), ' | '), ''), '')
   FROM unnest(to_tsvector('<lang>', '<query>')))::tsquery
```

This construction was validated on PostgreSQL 15.4:

- *"How do I run the tests? Chunk-key separators!"* produces `'chunk' | 'chunk-key' | 'key' | 'run' | 'separ' | 'test'`.
  Stop words are gone, the terms are stemmed, and hyphenated compounds keep both forms.
- The input `x' | 'y & !z` produces `'x' | 'y' | 'z'`. User-supplied tsquery operators are neutralized, because the
  input only ever passes through `to_tsvector`.
- A stop-words-only query produces an empty tsquery (`numnode = 0`), which matches nothing without raising an error.

> Do **not** OR together `plainto_tsquery` calls made per word. That still ANDs the lexemes *within* one word
> (`'run' & 'test' | …`), as the validation showed.

**Empty tsquery handling.** When the effective tsquery is empty (`numnode(tsq) = 0`):

- **Full-text-only:** return a successful empty result (`TotalRecords = 0`) and add a `Notice` string to
  `SearchResult`: *"The text query contained no searchable terms."*
- **Hybrid:** the text leg contributes nothing, so the result is the vector ranking. This is not an error.

### 4.2 A single tsquery CTE and the stored tsvector column

Today the tsquery expression is inlined twice, once in the score and once in the match, and `to_tsvector(content)`
is recomputed per row for ranking. Instead:

- Compute the tsquery once in a `params` CTE and reference it everywhere.
- Add a stored generated column and a GIN index on it, to every collection table:

  ```sql
  ALTER TABLE collection_<id> ADD COLUMN IF NOT EXISTS content_tsv tsvector
    GENERATED ALWAYS AS (to_tsvector('english', COALESCE(content, ''))) STORED;
  CREATE INDEX IF NOT EXISTS idx_col_<id>_tsv ON collection_<id> USING gin (content_tsv);
  ```

  This was validated on PostgreSQL 15.4, including the idempotent re-run (`ADD COLUMN IF NOT EXISTS` skips an
  existing column).
- When `Language` resolves to `english` and the column exists, the query uses `content_tsv` for both match and rank.
  For any other language it falls back to `to_tsvector('<lang>', COALESCE(content,''))`, which is correct but
  unindexed. The fallback is documented, and the response's `Notice` says so.
- **Drop the old expression index.** The old `idx_col_<id>_fts` expression index becomes redundant. Drop it in the
  same schema pass, after `_tsv` exists, to save write amplification and space.
- **Column-existence cache.** `SearchMethods` needs to know whether a collection has `content_tsv`. Cache this per
  collection id in the driver after the startup schema pass, and invalidate it when a collection is created.

### 4.3 Hybrid strategies (`SearchQuery.Hybrid`)

Add `HybridQuery` (`src/RecallDb.Core/Models/HybridQuery.cs`) as `SearchQuery.Hybrid`. It is optional and only used
when both `Vector` and `FullText` are present:

| Field | Type | Default | Meaning |
|---|---|---|---|
| `Strategy` | `HybridStrategyEnum` | `Rrf` | `Rrf`, `Linear`, or `Filter` |
| `RrfK` | int | 60 | RRF constant k (≥ 1) |
| `CandidatePool` | int? | `max(MaxResults × 4, 100)`, capped at 1000 | Candidates retrieved per leg before fusion |

`FullText.TextWeight` (w) keeps its meaning in every strategy: the text leg's share; the vector leg gets 1 − w.

**`Rrf` (default).** Retrieve the top `CandidatePool` from each leg independently, with the same filters (labels,
tags, dates, ids, terms), then fuse:

```
raw(d)   = (1 − w) / (k + rank_vector(d)) + w / (k + rank_text(d))     (a missing rank contributes 0)
score(d) = raw(d) · (k + 1)                                              normalized to [0, 1]
```

A document ranked first in both legs scores 1.0. A document ranked first in only one leg with w = 0.5 scores 0.5.
The fusion is a single statement, validated on PostgreSQL 15.4:

```sql
WITH params AS (SELECT '<vector>'::vector AS qv, <tsquery expr> AS tsq),
vec AS (   -- HNSW-satisfiable: raw distance ORDER BY + LIMIT
  SELECT t.id, t.embeddings <=> p.qv AS distance
  FROM collection_<id> t, params p
  WHERE <filters>
  ORDER BY t.embeddings <=> p.qv
  LIMIT <pool>),
vr AS (SELECT id, distance, ROW_NUMBER() OVER (ORDER BY distance) AS rnk FROM vec),
txt AS (   -- GIN-satisfiable
  SELECT t.id, ts_rank(t.content_tsv, p.tsq, <norm>) AS text_score
  FROM collection_<id> t, params p
  WHERE <filters> AND numnode(p.tsq) > 0 AND t.content_tsv @@ p.tsq
  ORDER BY text_score DESC
  LIMIT <pool>),
tr AS (SELECT id, text_score, ROW_NUMBER() OVER (ORDER BY text_score DESC) AS rnk FROM txt),
fused AS (
  SELECT COALESCE(vr.id, tr.id) AS id, vr.distance, tr.text_score,
         vr.rnk AS vector_rank, tr.rnk AS text_rank,
         (COALESCE(<1-w> / (<k> + vr.rnk), 0) + COALESCE(<w> / (<k> + tr.rnk), 0)) * (<k> + 1) AS score
  FROM vr FULL OUTER JOIN tr ON vr.id = tr.id)
SELECT <columns>, f.distance, f.text_score, f.vector_rank, f.text_rank, f.score
FROM fused f JOIN collection_<id> d ON d.id = f.id
WHERE <score thresholds, if any>
ORDER BY f.score DESC, f.id
LIMIT <MaxResults> OFFSET <offset>;
```

- The vector expression follows the collection's operator (`<=>`, `<->`, `<#>`), exactly as `GetVectorOperator`
  does today. The `vec` CTE keeps the HNSW-friendly `ORDER BY distance` form introduced in `b6f1af5`.
- `score` thresholds (`SearchQuery.MinimumScore`/`MaximumScore`) and `FullText.MinimumScore` (on `text_score`) move
  into SQL for the hybrid and full-text paths, so pagination is correct. See §4.5.
- `TotalRecords` in RRF is the size of the fused candidate set. It is bounded by 2 × `CandidatePool`, and that bound
  is documented. Continuation tokens page within it.

**`Linear` (corrected blend).** Same candidate union as RRF, fused by normalized scores instead of ranks:

```
vector_norm = the vector similarity score (already 0..1 for cosine similarity);
              min-max within the candidate set for other metrics
text_norm   = text_score / max(text_score over candidates)     (0 when the document is not a text match)
score       = (1 − w) · vector_norm + w · text_norm
```

A text match is not required, so non-matching documents score on the vector alone. Offered for callers who want
score-proportional blending; RRF is the recommended default.

**`Filter` (legacy).** Today's semantics, kept deliberately: the text query is a required `WHERE` condition, and
documents are ranked by the old `(1 − w)·vector + w·text` blend. Combined with `MatchMode = All`, it reproduces
today's results exactly. Documented as "vector ranking within keyword matches".

### 4.4 Full-text-only path

Same `params` CTE. Match on `content_tsv @@ tsq` (or the expression fallback), score with `ts_rank`/`ts_rank_cd`, and
apply the new default `MatchMode = Any`. `SortOrder` handling is unchanged. `FullText.MinimumScore` moves into
`WHERE text_score >= x`, using a subquery or `HAVING`-style wrapper so the alias is usable.

### 4.5 Thresholds and counts (full-text and hybrid only)

- `FullText.MinimumScore` is applied in SQL for the full-text and hybrid paths, so `TotalRecords`, `EndOfResults`
  and `ContinuationToken` agree with the returned pages.
- In hybrid, `SearchQuery.MinimumScore`/`MaximumScore` apply to the normalized fused `score` in SQL.
- Vector-only thresholds keep today's post-`LIMIT` behavior (non-goal; §12).

### 4.6 Response additions (`DocumentRecord`)

These are additive and nullable, so existing clients are unaffected:

| Field | Type | Set when | Meaning |
|---|---|---|---|
| `VectorScore` | double? | vector or hybrid | The leg's similarity score (cosine similarity etc.). In hybrid, `Score` becomes the fused score, so this preserves the raw similarity |
| `VectorRank` | int? | hybrid | 1-based rank in the vector leg (null if absent) |
| `TextRank` | int? | hybrid | 1-based rank in the text leg (null if absent) |

`TextScore` keeps its meaning (the raw `ts_rank`). In hybrid it is null for documents that are not text matches;
today every hybrid hit has a non-null `TextScore`, because a match is required.

`SearchResult` gains `Notice` (string?) for the empty-tsquery and non-indexed-language cases.

### 4.7 Validation (`SearchService.SearchAsync`)

Reject with **400** and a field-specific message:

| Field | Rule |
|---|---|
| `FullText.Query` | When `FullText` is present, a blank `Query` is treated as "no full-text" consistently in both `DeriveSearchMode` and `SearchMethods`. Recommended: a present-but-blank `FullText` is a 400 in full-text-only mode and ignored in hybrid (documented) |
| `FullText.Language` | Must be in an allowlist loaded once from `SELECT cfgname FROM pg_ts_config` at startup. This removes the reliance on quote-escaping for an identifier interpolated into SQL |
| `FullText.Normalization` | 0–63 (the ts_rank bitmask range) |
| `FullText.TextWeight` | 0.0–1.0; reject instead of silently clamping |
| `FullText.MatchMode` | A valid enum value |
| `Hybrid.RrfK` | ≥ 1 |
| `Hybrid.CandidatePool` | 1–10,000 |
| `Hybrid` without both legs | Allowed but ignored; `Notice` says so |

`DeriveSearchMode` must use the same predicate as `SearchMethods` (`FullText != null && !IsNullOrWhiteSpace(Query)`)
so the metrics and traces tagged with `search.mode` are correct.

### 4.8 Performance expectations

- **Text leg:** a GIN bitmap index scan on `content_tsv`. Ranking reads the stored vector, so it no longer
  re-tokenizes every candidate. Expect a large reduction in full-text CPU on medium and large collections. Verify with
  `EXPLAIN (ANALYZE, BUFFERS)` in §7.4.
- **Vector leg:** unchanged HNSW path. Hybrid no longer sequential-scans to compute a blended score for every row.
- **One round trip:** callers that fused client-side (Isis) drop to a single request.
- **OR query cost:** a query of many common words matches many documents. The text leg is bounded by `LIMIT <pool>`
  after ranking, but ranking still runs over all matches. `CandidatePool` caps output, not the match set. Document
  the trade-off; if profiling shows a problem, a later option is to rank only the top-N by a cheaper `ts_rank_cd`
  pre-pass.
- **Filtered HNSW:** with the shipped pgvector 0.5.1, a selective filter can return fewer than `<pool>` vector
  candidates. This behaves the same as today's vector-only path, and the pgvector ≥ 0.8 `hnsw.iterative_scan` setting
  is the eventual remedy (§12).

---

## 5. Backend changes (file by file)

### RecallDb.Core

| File | Change |
|---|---|
| `Enums/TextMatchModeEnum.cs` (new) | `Any`, `All`, `Phrase`, `WebSearch`, with `JsonStringEnumConverter` |
| `Enums/HybridStrategyEnum.cs` (new) | `Rrf`, `Linear`, `Filter` |
| `Models/FullTextQuery.cs` | Add `MatchMode` (default `Any`), with an XML doc on each value. Keep the existing defaults. Setter guards for `Normalization` (0–63) and `TextWeight` (0–1) that throw `ArgumentOutOfRangeException`, matching the model-guard style used elsewhere |
| `Models/HybridQuery.cs` (new) | `Strategy`, `RrfK`, `CandidatePool`, with guarded setters |
| `Models/SearchQuery.cs` | Add `Hybrid` (HybridQuery?, default null, which means RRF defaults) |
| `Models/DocumentRecord.cs` | Add `VectorScore`, `VectorRank`, `TextRank`. Map the `vector_score`/`vector_rank`/`text_rank` columns in `FromDataTable` when present (alongside `text_score`, L367-370) |
| `Models/SearchResult.cs` | Add `Notice` |
| `Database/Postgresql/Implementations/SearchMethods.cs` | Refactor `SearchAsync` into `BuildTsQueryExpression(matchMode, language, query)`, `BuildFilterConditions(query)` (the shared filters, reused by both legs), `BuildVectorOnlySql`, `BuildFullTextSql`, `BuildHybridRrfSql`, `BuildHybridLinearSql` and `BuildHybridFilterSql` (which keeps today's SQL). Move `FullText.MinimumScore` and the hybrid score thresholds into SQL. Keep the vector-only path byte-for-byte identical to avoid regressions in the HNSW optimization from `b6f1af5` |
| `Database/Postgresql/Queries/DynamicTableQueries.cs` | Add the `content_tsv` generated column to `CREATE TABLE` for new collections. Replace the `_fts` expression index with `_tsv` on the column |
| `Database/Postgresql/PostgresqlDatabaseDriver.cs` | In `EnsureAllCollectionSchemasAsync` (L136-163), run for each existing collection: `ALTER TABLE … ADD COLUMN IF NOT EXISTS content_tsv …`, then `CREATE INDEX IF NOT EXISTS …_tsv`, then `DROP INDEX IF EXISTS …_fts`. Log per-collection progress and timing, because adding a stored generated column rewrites the table (see §8). Populate the column-existence cache. Add `ListTextSearchConfigurationsAsync()` for the language allowlist |
| `docker/factory/recalldb_factory.sql` L100-101 | Same column and index changes for the factory image |

### RecallDb.Server

| File | Change |
|---|---|
| `Services/SearchService.cs` | Validation per §4.7; fix `DeriveSearchMode`; tag `search.match_mode` and `search.hybrid_strategy` on the search activity and metrics; carry `Notice` through |
| `RecallDbServer.cs` | Update the search route's OpenAPI description (L1272: "ts_rank", hybrid semantics). Update the `JsonBody<SearchQuery>` request examples (L479-520) with `MatchMode` and a hybrid RRF example. `_Version` stays `0.2.1` |
| `Mcp/Registrations/SearchRegistrations.cs` | The `search/query` tool's description lists the new fields and adds a hybrid example, because the `search` parameter is a JSON-string `SearchQuery` |
| Observability | New tags on the existing search histograms: `match_mode`, `hybrid_strategy`. Update the Grafana "Search" dashboard so panels can break down by them |

---

## 6. Frontend changes (dashboard)

`dashboard/src/views/SearchQuery.jsx` (route `/search`):

- Add a **Match mode** select (`Any` default, `All`, `Phrase`, `WebSearch`) with one-line help for each, next to the
  rank-function select (L50-51).
- Add a **Hybrid strategy** select (`RRF` default, `Linear`, `Filter (legacy)`), shown only when both a vector and a
  text query are supplied, plus **RRF k** and **Candidate pool** numeric inputs.
- **Fix the falsy-default bugs** in the query builder (L112-121). `parseFloat(w) || 0.5` turns a weight of **0** into
  0.5, and `parseInt(norm) || 32` turns 0 into 32. Use explicit `Number.isFinite` checks, so a text weight of 0
  (vector only) and normalization 0 are expressible.
- Results table (TextScore column, L335-337): add **Vector score**, **Vector rank** and **Text rank** columns for
  hybrid, and show `Notice` above the results.
- Validation (L290-296): mirror the server ranges so errors show before submit.
- `dashboard/src/views/Search.jsx` (vector-only, per collection) needs no change.
- API Explorer examples (from `198b764`): add the new fields to the search example bodies.

---

## 7. Tests

The suites live in `src/Test.Shared/RecallDbSuites.cs` (Touchstone) and run over HTTP against real Postgres/pgvector
through `Test.Automated`, `Test.Xunit` and `Test.Nunit`. Seed data is the 10-document set in `SearchDataSetup`
(L807-830). Extend the seed set with documents that make the new semantics observable:

- a document containing only *some* of a multi-term query's words
- a document that is a strong semantic match with **no** shared keywords
- a phrase-order pair ("key chunk" vs "chunk key")

### 7.1 Existing tests to update

| Test (line) | Why | Change |
|---|---|---|
| `SearchHybridBasic` (L1433) | Asserts `TextScore > 0` on **every** hit, which is no longer true because the text match is optional | Assert at least one hit has `TextScore > 0`, and that hits without a text match are allowed and have `TextScore == null` |
| `SearchHybridBlendedScoreFormula` (L1648) | Validates the legacy linear formula | Set `Hybrid.Strategy = Filter` explicitly; add a separate `Linear` formula test (§7.2) |
| `SearchHybridWithTerms` (L1470), `SearchFullTextWithTerms` (L1540) | **Existing test bug:** they send `TermsFilter = …`, but the property is `Terms`, so the filter is silently ignored and the tests assert nothing about it | Use `Terms`, and assert the filter's effect |
| `SearchFullTextBasic`, `TsRank`, `TsRankCd`, `SortDescending`/`Ascending`, `Pagination`, `MaxResults`, `With{Label,Tag,DateRange,DocumentIds}` (L1357-1562) | Queries may now match more documents under `Any` | Re-baseline the expected counts, or pin `MatchMode = All` where a test is specifically about AND filtering |
| `SearchFullTextNoMatch` (L1384) | A nonsense term still matches nothing under `Any` | None; keep it as the negative control |
| `SearchFullTextMinimumScore` (L1392) | The threshold moves into SQL | Also assert that `TotalRecords` equals the post-threshold count |
| `SearchHybridWeightZero` / `WeightOne` (L1604/L1626) | Still valid under RRF: w = 1 means text rank only, w = 0 means vector rank only | None; keep as RRF weight-boundary tests |
| `NeighborHybridSearch` / `NeighborFullTextSearch` (L1766/L1784) | May return more primary hits | Re-baseline |

### 7.2 New tests

**Full-text match modes**

- `SearchFullTextAnyMatchesPartialTerms`: `"alpha nonexistentterm"` returns the alpha documents under `Any`, and
  nothing under `All`.
- `SearchFullTextAnyRanksMoreTermsHigher`: a document matching 3 of 3 terms outranks one matching 1 of 3.
- `SearchFullTextAllPreservesLegacy`: `MatchMode = All` returns exactly what v0.2.1-before-fix returned for a fixed
  query set.
- `SearchFullTextPhrase`: "chunk key" matches the in-order document and not the reversed one.
- `SearchFullTextWebSearchSyntax`: covers a quoted phrase, `or`, and `-exclusion`.
- `SearchFullTextStopwordsOnly`: `"the and of"` returns success with 0 results and a `Notice`, not an error.
- `SearchFullTextOperatorInjection`: `x' | 'y & !z` under `Any` is treated as the plain terms x, y, z with no error or
  syntax leak. Under `WebSearch`, it is parsed by Postgres' safe parser.
- `SearchFullTextNonEnglishLanguage`: `Language = "simple"` works through the expression fallback and sets `Notice`.

**Hybrid strategies**

- `SearchHybridRrfIncludesVectorOnlyHits`: **the key regression test.** A natural-language query sharing no keyword
  with the best semantic document still returns it at or near rank 1. This fails before the fix.
- `SearchHybridRrfIncludesTextOnlyHits`: a document outside the vector candidate pool but a strong text match is
  returned.
- `SearchHybridRrfScoreNormalized`: every `Score` is in [0, 1], and a document ranked first in both legs scores ≈ 1.0.
- `SearchHybridRrfRanksPopulated`: `VectorRank`/`TextRank` are set consistently with each leg.
- `SearchHybridNeverFewerThanVector`: for a fixed query set, hybrid result count ≥ vector-only result count.
- `SearchHybridLinearNormalized`: scores are in [0, 1], and non-matching text documents are included with
  `text_norm = 0`.
- `SearchHybridFilterLegacy`: `Strategy = Filter` with `MatchMode = All` reproduces pre-fix results.
- `SearchHybridPaginationWithinPool`: continuation pages don't overlap, and `TotalRecords ≤ 2 × CandidatePool`.
- `SearchHybridWithFiltersAppliedToBothLegs`: a label filter excludes a document from both the vector and text legs.

**Validation (400s)**

- `SearchValidationLanguageNotAllowed` (`"english'); drop table x;--"`), `NormalizationOutOfRange` (64),
  `TextWeightOutOfRange` (1.5), `RrfKZero`, `CandidatePoolTooLarge`, `BlankFullTextQueryFullTextOnly`.

**Schema**

- `CollectionHasStoredTsVector`: a new collection has `content_tsv` and `idx_col_<id>_tsv`, and no `_fts` index.
- `SchemaBackfillIdempotent`: running `EnsureAllCollectionSchemasAsync` twice is a no-op the second time, and a
  collection created with the old DDL (created directly in the test database) gains the column and index.

**MCP**

- `McpSearchHybridRrf` in `RecallDbMcpSuites.cs` (next to `McpSearch`, L215): a hybrid query through `search/query`
  returns fused results with ranks.

### 7.3 SDK test harnesses

Add match-mode, hybrid RRF and validation cases to all three SDK harnesses, and update each harness's hybrid
assertion that assumes every hit has a text score:

| SDK | Harness | Existing full-text calls |
|---|---|---|
| C# | `sdk/csharp/RecallDb.Sdk.TestHarness/Program.cs` | L214-224, `MakeFullTextQuery` L1506 |
| JS | `sdk/js/test-harness.js` | L677-712, L1181-1186 |
| Python | `sdk/python/test_harness.py` | L869-913, L1553-1558 |

### 7.4 Retrieval-quality and performance verification

These are not unit tests; they are run once before sign-off and recorded in the PR description.

- **Retrieval quality.** Run the Isis harness against a local RecallDB build:
  `dotnet run --project C:\Code\AgentMemory\src\Test.Benchmark -- retrieval --dataset …` on isis-live, SciFact and
  LongMemEval-S. Record Keyword and Hybrid recall@5 and nDCG@10 before and after. Targets are in §9.
- **Query plans.** Capture `EXPLAIN (ANALYZE, BUFFERS)` on a 100k-document collection for:
  - the full-text query: expect a Bitmap Index Scan on `_tsv`
  - the hybrid `vec` CTE: expect an Index Scan on the HNSW index
  - the hybrid `txt` CTE: expect a Bitmap Index Scan on `_tsv`
- **Latency.** Measure p50/p95 for vector-only, full-text and hybrid at 10k and 100k documents. The Isis
  `load --stub` harness or a RecallDB-native equivalent can drive this.

---

## 8. Migration and rollout

- **New collections:** created with the column and index directly.
- **Existing collections:** migrated at startup by `EnsureAllCollectionSchemasAsync`. `ADD COLUMN … GENERATED …
  STORED` **rewrites the table while holding an `ACCESS EXCLUSIVE` lock**. On a large collection that can take
  seconds to minutes, and startup blocks until it is done.
  - Log each collection's name, row count and elapsed time.
  - Add a settings flag, `Database.MigrateFullTextColumn` (default `true`), with a `RECALLDB_DB_MIGRATE_FTS_COLUMN`
    environment override. It lets operators with very large collections skip the startup rewrite and run it in a
    maintenance window.
  - When the column is absent, the query path falls back to the expression index, so correctness never depends on
    the migration having run. Only speed does.
- **Drop order:** drop the `_fts` expression index only after `_tsv` is built, so there is never a window without a
  text index.
- **Rollback:** the column and new index are harmless to older binaries, which ignore them. Rolling back the code
  restores `plainto_tsquery`, and the column can be dropped manually if desired.
- **Behavior change inside v0.2.1** (see §11): `Any` and `Rrf` become the defaults. Callers that depend on today's
  semantics set `MatchMode = All` and `Hybrid.Strategy = Filter`. The CHANGELOG entry calls this out.

---

## 9. Acceptance criteria

1. **Keyword quality.** On isis-live, full-text (Keyword) recall@5 is ≥ **0.80** (today 0.072; 0.859 simulated). On
   SciFact, full-text nDCG@10 is ≥ **0.50** (today 0.057). For reference, BM25 is ~0.66 on SciFact; `ts_rank` is
   expected to land below BM25.
2. **Hybrid quality.** On isis-live, SciFact and LongMemEval-S, hybrid (RRF) nDCG@10 is ≥ vector-only nDCG@10 on each
   dataset, and on isis-live it reaches ≥ **0.86** (today 0.848).
3. **No empty hybrid.** Across all benchmark queries, hybrid result count ≥ vector-only result count
   (`SearchHybridNeverFewerThanVector`).
4. **Index use.** The `EXPLAIN` checks in §7.4 show index scans on both hybrid legs and on full-text.
5. **Latency.** Vector-only p95 is unchanged (within noise). Hybrid p95 at 100k documents is ≤ 2× vector-only p95.
   Full-text p95 is no worse than today.
6. **Legacy parity.** `MatchMode = All` plus `Strategy = Filter` reproduces the pre-fix results on the full test seed
   set.
7. **Tests.** All suites pass (Automated, Xunit, Nunit, MCP) and all three SDK harnesses pass.
8. **Docs.** Every doc listed in §10 is updated, and the README's wrong enum reference is fixed.

---

## 10. Documentation, SDKs, and release artifacts

### SDKs

| SDK | Files | Change |
|---|---|---|
| C# | `sdk/csharp/RecallDb.Sdk/Models/FullTextQuery.cs` | Add `MatchMode` (string, default `"Any"`, matching the SDK's existing string-typed `SearchType`) |
| | `Models/HybridQuery.cs` (new), `Models/SearchQuery.cs` | Add the `Hybrid` property |
| | `Models/DocumentRecord.cs` | Add `VectorScore`, `VectorRank`, `TextRank` |
| | `Models/SearchResult.cs` | Add `Notice` |
| | `README.md`, `GETTING_STARTED.md` | Add a hybrid RRF example |
| JS | `sdk/js/recalldb-sdk.js` (`search()` JSDoc, L545-573) | Document `MatchMode`, `Hybrid`, and the new response fields |
| | `README.md`, `GETTING_STARTED.md` | Add examples |
| Python | `sdk/python/recalldb_sdk.py` (`search()` docstring, L679-712) | Same documentation |
| | `README.md`, `GETTING_STARTED.md` | Add examples |

Clients stay backward compatible because every new field is optional with a server-side default.

### Repository docs

| File | Change |
|---|---|
| `REST_API.md` | Request example (L1340-1363): add `MatchMode` and a `Hybrid` block. Rewrite mode semantics and the hybrid formula (L1451-1463) for RRF / Linear / Filter, with the normalized-score definition and the candidate-pool bound on `TotalRecords`. `FullText` row and fields table (L1531, L1546-1555): add `MatchMode`. New `Hybrid` fields table. New enum sections `TextMatchModeEnum` and `HybridStrategyEnum` next to `TextSearchTypeEnum` (L1622-1627). New `DocumentRecord` fields and `SearchResult.Notice`. Validation error table |
| `README.md` | Update the feature bullets (L41, L50), the example (L119-130), the full-text description (L147, now "any-term by default"), and the hybrid description (L149, now "rank-fused union"). Fix L196, which references a non-existent `FullTextSearchTypeEnum.TsRank`; the SDK field is the string `SearchType`, and the enum is `TextSearchTypeEnum` |
| `MCP_API.md` (L243, `search/query`) | Document the new fields and add a hybrid example |
| `RecallDB.postman_collection.json` | Update the full-text body (L1981) with `MatchMode`, the hybrid bodies (L1992, L2023) with `Hybrid.Strategy`, and the combined example (L2065). Add requests for "Full-text: phrase", "Full-text: web search" and "Hybrid: legacy filter" |
| `TESTING.md` | Describe the new seed documents and the benchmark verification step (§7.4) |
| OpenAPI | Generated at runtime; covered by the `RecallDbServer.cs` description and example changes above |
| Grafana | "Search" dashboard panels by `match_mode` and `hybrid_strategy` |

### CHANGELOG.md

Append to the existing **v0.2.1** entry. No new version heading:

```markdown
- Full-text search now matches ANY query term by default (ranked by relevance) instead of requiring every term;
  new `FullText.MatchMode` (`Any` default, `All` = previous behavior, `Phrase`, `WebSearch`)
- Hybrid search is now a rank-fused union of the vector and text legs (weighted Reciprocal Rank Fusion, k=60)
  instead of a vector ranking inside required text matches; new `SearchQuery.Hybrid` (`Strategy` = `Rrf` default,
  `Linear` = normalized score blend, `Filter` = previous behavior; `RrfK`; `CandidatePool`). Hybrid scores are
  normalized to [0, 1]; results include `VectorScore`, `VectorRank`, and `TextRank`
- Collections gain a stored `content_tsv` column with a GIN index (migrated at startup; opt out with
  `Database.MigrateFullTextColumn`), so text ranking no longer re-tokenizes every candidate row
- `FullText` inputs are validated (language allowlist from `pg_ts_config`, normalization 0-63, weights 0-1);
  `FullText.MinimumScore` and hybrid score thresholds are applied in SQL so pagination and `TotalRecords` are exact
- Dashboard search: match-mode and hybrid-strategy controls; a text weight or normalization of 0 is no longer
  replaced by the default
- Behavior change: callers relying on all-terms matching or text-filtered hybrid should set `MatchMode = All` and
  `Hybrid.Strategy = Filter`
```

(The two most recent commits, `b6f1af5` "HNSW-friendly ORDER BY" and `d5d4e30` "IncludeEmbeddings off by default",
are also missing from the v0.2.1 entry and should be added in the same edit.)

---

## 11. Release and versioning notes

The request is to roll this into v0.2.1 without a version change. That is straightforward for artifacts that can be
re-published, but **not** for immutable package registries:

- **Can be re-published:** Docker images (`jchristn77/recalldb-server:v0.2.1`, dashboard) can be re-pushed under the
  same tag. The source tree, docs and CHANGELOG are fine.
- **Cannot be overwritten:** NuGet (`RecallDb.Sdk` 0.2.1 is already published and consumed by Isis), npm and PyPI all
  refuse to overwrite a published version.
- **What that means for the SDKs:** the SDK *model additions* (`MatchMode`, `Hybrid`, new response fields) cannot
  reach consumers under 0.2.1 through those registries. Because every new field is optional and defaulted
  server-side, old 0.2.1 SDKs keep working against the new server and get the new *defaults* (Any / RRF)
  automatically. They just can't set the new options or read the new response fields. Treat this as an open
  decision before release: accept that, or publish an SDK-only patch.
- **Same tag, new behavior:** re-pushing `:v0.2.1` changes search results for anyone who pulls it. The CHANGELOG
  behavior-change bullet is the mitigation; consider announcing it.
- **Pre-existing version drift** (not introduced here; flag only): `sdk/js/package.json` is 0.2.0 and
  `RecallDbMcpSuites.cs` L61 asserts 0.2.0, while everything else is 0.2.1.

---

## 12. Out of scope and follow-ups

- **BM25 scoring** via a Postgres extension (for example ParadeDB `pg_search`), expected to lift lexical quality
  further (SciFact BM25 ≈ 0.66 nDCG@10).
- **Per-collection text-search language**, so the stored column and index use the collection's language rather than
  `english`.
- **pgvector ≥ 0.8** with `hnsw.iterative_scan`, so selective filters still fill the vector candidate pool.
- **Vector-only thresholds in SQL** (today they are applied post-`LIMIT`, so pages come back short and `TotalRecords`
  is inaccurate).
- **Downstream (Isis):** once this ships, `RecallDbMemoryStore.SearchAsync` can replace its client-side two-query
  RRF with one server-side `Hybrid.Strategy = Rrf` call. Its Keyword mode improves immediately just from the new
  `Any` default. Chunk-to-memory rollup stays client-side.
- **`RecallDbClient` path-segment encoding** (found in Isis): document keys containing `#` or `?` break delete and
  exists calls because keys are concatenated into URLs unencoded. It is unrelated to search, but it is in the same
  SDK, so worth fixing in the same pass.

---

## 13. Work breakdown (suggested order)

1. **Core models, enums and validation**: §4.1, §4.3 models, §4.7, `DeriveSearchMode` fix. Include the unit-level
   validation tests.
2. **`SearchMethods` refactor**: the shared filter builder, match modes, the full-text path with SQL thresholds, and
   an unchanged vector path (regression-checked).
3. **Hybrid strategies**: RRF (default), Linear, Filter (legacy); response fields; `Notice`.
4. **Stored tsvector column**: DDL, the startup migration with a flag, the column cache, the `_fts` drop, factory SQL.
5. **Tests**: update the existing tests (§7.1), add new ones (§7.2), fix the `TermsFilter` test bug, extend the seed
   set; then the MCP test.
6. **SDKs**: C#, JS and Python models, docs and harnesses (§7.3).
7. **Dashboard**: controls, falsy-default fixes, result columns, API Explorer examples.
8. **Docs**: REST_API, README, MCP_API, Postman, TESTING, Grafana, CHANGELOG.
9. **Verification** (§7.4) against the §9 acceptance criteria, with results recorded in the PR.
