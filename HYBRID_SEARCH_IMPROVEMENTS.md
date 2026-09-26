# Hybrid Search Improvements: Single-Call Hybrid for Isis

**Status:** implemented (2026-09-26) except the dashboard (6.3), the Grafana panels, and the verification runs (13, Verification)
**Target version:** v0.2.1. No version change; the work folds into the existing v0.2.1 entry. See [section 11](#11-release-and-versioning) for what that means for the package registries.
**Date:** 2026-09-25
**Predecessor:** `archive/HYBRID_SEARCH_FIX.md` (implemented in `d8ce32c`, "Full-text any-term matching and rank-fused hybrid search")
**Downstream consumer:** Isis (`C:\Code\AgentMemory`), which uses RecallDB as its memory store

---

## 1. Summary

Isis still runs every hybrid search as two RecallDB calls, one vector-only and one full-text-only, and fuses them
itself. It does that for two reasons. The first is historical: before `d8ce32c`, RecallDB's hybrid mode treated the text
query as a required filter, so a question with no literal keyword overlap came back empty. The second is still true
today: RecallDB's server-side hybrid does most of what Isis needs, but not all of it, and the part it does do is not
reachable from the published C# SDK.

After reading the current server, the SDKs and Isis side by side, the picture is narrower than the original request
assumed. Weighted RRF with a configurable k, text and vector weights, per-hit vector and text scores and ranks,
normalization to [0, 1], label filters on both legs, a bounded candidate pool, and `IncludeEmbeddings` are all already
implemented in the server and in the C# SDK *source*. Three things are actually missing:

1. **A recency signal.** Isis fuses a third reciprocal-rank signal that ranks parent memories by write time. RecallDB
   has no equivalent, so a single call would change Isis's ordering.
2. **Collapsing chunks to their parent.** Isis stores each memory as one or more chunk documents tagged `parentKey`,
   and rolls hits up so `topK` counts distinct memories. RecallDB returns chunks, so `MaxResults` counts chunks and
   pagination and `TotalRecords` are in the wrong unit for Isis.
3. **A published SDK that exposes any of this.** `RecallDb.Sdk` 0.2.1 on NuGet predates `d8ce32c`. It has no
   `Hybrid`, no `IncludeEmbeddings`, no `MatchMode`, no `VectorScore`/`VectorRank`/`TextRank`, and no `Notice`.

There is also a fourth, smaller gap that matters for rollout: a server running the new code reports the same version
(`0.2.1`) as one running the old code, so Isis has no way to tell whether a single call will be honored. This plan adds
a capability list to the health response to fix that.

On performance, the honest expectation is modest. Isis already runs its two legs concurrently, so hybrid latency today
is roughly the slower of the two calls, not their sum. A single call saves one HTTP exchange, one authentication, one
collection lookup, and one pair of label and tag lookups. On SciFact, where p50 is about 89 ms, most of the cost is
the text leg ranking every any-term match, and a single call does not change that. Section 4.7 treats that cost
separately with an opt-in `FullText.MinimumShouldMatch` and a measurement step, rather than pretending the round-trip
change fixes it.

---

## 2. What already works (verified against the code)

Line numbers are from `0d022e6`.

| Isis needs | RecallDB today | Where |
|---|---|---|
| One statement for both legs | `SearchHybridFusedAsync` builds one CTE statement (`vec`, `vr`, `txt`, `tr`, `cand`, `scored`, `fused`). A second statement runs only when the page is empty, to get a count | `src/RecallDb.Core/Database/Postgresql/Implementations/SearchMethods.cs` L287-387 |
| Weighted RRF with configurable k | `GetRrfFusionSql`: `((1-w)/(k+vr) + w/(k+tr)) * (k+1)`, a missing rank contributes 0 | `SearchMethods.cs` L652-659; `HybridQuery.RrfK` (1-100000, default 60) |
| Text weight in [0, 1], vector weight its complement | `FullText.TextWeight`, guarded setter, default 0.5 | `src/RecallDb.Core/Models/FullTextQuery.cs` |
| Fused score normalized to [0, 1] | The `(k+1)` factor divides by the best achievable score when weights sum to 1 | `SearchMethods.cs` L657-658 |
| Per-hit vector score, text score, vector rank, text rank, fused score | Projected as `vector_score`, `text_score`, `vector_rank`, `text_rank`, `score` and mapped in `DocumentRecord.FromDataTable` | `SearchMethods.cs` L353; `src/RecallDb.Core/Models/DocumentRecord.cs` L422-427 |
| Label filter on both legs | `BuildFilterConditions` is called separately for the vector leg and the text leg | `SearchMethods.cs` L310, L317, L536-565 |
| Candidate pool per leg | `HybridQuery.CandidatePool`, default `max(MaxResults x 4, 100)` capped at 1000 | `SearchMethods.cs` L297 |
| HNSW index on the vector leg, GIN on the text leg | Vector leg orders by the raw distance operator; `hnsw.ef_search` raised to the pool with `SET LOCAL` | `SearchMethods.cs` L322-325, L644-650 |
| Stored embeddings in results | `SearchQuery.IncludeEmbeddings` appends `embeddings::text` to the projection, including on the fused path | `src/RecallDb.Core/Models/SearchQuery.cs` L305; `SearchMethods.cs` L707-719 |
| Tags on each hit (Isis reads `parentKey`, `title`) | `SearchService` attaches labels and tags with one batched query each | `src/RecallDb.Server/Services/SearchService.cs` L120; `DocumentService.cs` L417-430 |
| SDK surface in source | `sdk/csharp/RecallDb.Sdk/Models/HybridQuery.cs`, `SearchQuery.Hybrid`, `SearchQuery.IncludeEmbeddings`, `DocumentRecord.VectorScore/VectorRank/TextRank`, `SearchResult.Notice`; JS and Python document the same fields | `sdk/csharp/RecallDb.Sdk/Models/*`, `sdk/js/recalldb-sdk.js` L545-596, `sdk/python/recalldb_sdk.py` L680-747 |

One consequence of that table is worth stating plainly: if Isis did not use recency or chunk rollup, it could switch to
a single call today, provided it had an SDK build from source. The new server work in this plan is the recency signal
and collapse. Everything else is release plumbing, docs, and tests.

---

## 3. Gaps

| # | Gap | Evidence | Effect on Isis |
|---|---|---|---|
| G1 | No recency signal in fusion | `GetRrfFusionSql` has two terms only; `HybridQuery` has `Strategy`, `RrfK`, `CandidatePool` and nothing else | Isis's default `RecencyWeight` is 0.1 (`MemorySearchQuery.cs`). A single call without it reorders near-ties, which is exactly the supersession case recency was added for (Isis round 2) |
| G2 | No server-side collapse by a tag or column | Results are per chunk document; `MaxResults`, `TotalRecords` and continuation tokens count chunks (`SearchMethods.cs` L351-358, `BuildResult` L618-642) | Isis must over-fetch (`fetch = max(topK x 4, 20)`, `RecallDbMemoryStore.cs` L353) and roll up client-side (`GroupByParent`, L509-523). It cannot page by memory at all |
| G3 | Published `RecallDb.Sdk` 0.2.1 lacks every field added since `d5d4e30` | Byte search of `~/.nuget/packages/recalldb.sdk/0.2.1/lib/net10.0/RecallDb.Sdk.dll` finds no `HybridQuery`, `IncludeEmbeddings`, `VectorRank`, `TextRank`, `VectorScore`, `Notice`, `MatchMode`, or `CandidatePool` (it does find `IncludeNeighbors` and `TextScore`) | Isis cannot express a hybrid request, cannot request embeddings (blocking Isis items "stored vectors in results" for MMR and the similarity check), and cannot read ranks |
| G4 | No way to detect server capability | `GET /` returns only `Name`, `Version`, `UptimeMs` (`src/RecallDb.Server/RecallDbServer.cs` L1456-1465). Old and new servers both say `0.2.1` | Isis cannot decide between single-call and its fallback. A server built before `d8ce32c` treats a hybrid request as text-filtered and silently ignores unknown fields such as `Collapse` |
| G5 | The text leg ranks every any-term match | `txt` CTE is `WHERE content_tsv @@ tsq ... ORDER BY text_score DESC LIMIT pool` (`SearchMethods.cs` L327-330). `LIMIT` caps output, not the rows ranked | SciFact (about 11,000 chunk documents) ranks thousands of matches per query; hybrid p50 about 89 ms (Isis `benchmarks/RESULTS.md`, Latency) |
| G6 | Isis's vector leg is silently truncated at 40 rows | Isis's separate vector-only call does not raise `hnsw.ef_search`, so with pgvector 0.5.1 an HNSW scan returns at most 40 rows even when Isis asks for `fetch` > 40 (any `topK` above 10). The fused path already raises it (`GetHnswSetupStatements`) | Not a RecallDB defect, but it means single-call parity with Isis can only be exact for `fetch` <= 40. Single call fixes it as a side effect |
| G7 | `VectorScore` semantics differ slightly | The fused path computes `vector_score` for every candidate, including text-only hits (`scored` CTE, L335-337). Isis's `FusedDocument.VectorScore` is null for a hit the vector leg did not return | Harmless, and arguably more useful, but the parity check and Isis's mapping must account for it (section 8) |
| G8 | Tie-breaks differ | RecallDB breaks fused-score ties by `id ASC`; `HybridFusion.Fuse` breaks them by `DocumentKey` ordinal | Parity is defined up to ties (section 9) |

Two things from the original list turned out not to be gaps: label filters already apply to both legs, and fused scores
are already normalized. The only change there is that normalization must include the recency weight when recency is
on, exactly as `HybridFusion.Fuse` does.

---

## 4. Design

### 4.1 Recency signal (`Hybrid.RecencyWeight`)

Add `RecencyWeight` to `HybridQuery`: a double in [0.0, 1.0], default 0.0 (off). It is used by the `Rrf` strategy only.
The formula matches `HybridFusion.Fuse` term for term:

```
raw(d)   = (1 - w) / (k + vectorRank(d)) + w / (k + textRank(d)) + r / (k + recencyRank(d))
score(d) = raw(d) * (k + 1) / ((1 - w) + w + r)            a missing rank contributes 0
```

`w` is `FullText.TextWeight`, `r` is `Hybrid.RecencyWeight`. With `r = 0` the expression reduces to today's
`raw * (k + 1)`, so existing scores are unchanged to the last bit; implement it so the recency term and the divisor are
only emitted when `r > 0`, which keeps the generated SQL for existing callers identical.

**Recency rank.** Among the fused candidate set (the union of both legs, after the text-leg `MinimumScore` gate and
before score thresholds), group candidates by their *recency key*, take `MAX(created_utc)` per key, and rank keys
newest first with ties broken by the key in byte order (`COLLATE "C"`, so it matches Isis's `StringComparer.Ordinal`).
Every candidate in a key shares that rank. The recency key is the collapse key when `Collapse` is set (section 4.2) and
the document's own `id` otherwise. Isis always uses collapse, so its recency is per memory, as it is today.

`created_utc` is the right column for Isis: its upsert deletes and recreates every chunk of a memory, so all chunks of
a memory carry the write time. Other callers who want "last modified" recency can come later (section 12).

**Other strategies.** `Linear` and `Filter` ignore `RecencyWeight` and add a `Notice`
(`"Hybrid.RecencyWeight applies only to the Rrf strategy and was ignored."`). Vector-only and full-text-only searches
ignore `Hybrid` entirely, as they do today (`SearchService.NoticeHybridIgnored`).

### 4.2 Collapse (`SearchQuery.Collapse`)

Add `SearchQuery.Collapse` (`CollapseQuery`, default null). When set, results are collapsed so that each group
contributes exactly one hit, its best-scoring candidate, and `MaxResults`, `TotalRecords`, `RecordsRemaining` and
continuation tokens all count groups.

| Field | Type | Default | Meaning |
|---|---|---|---|
| `Field` | `CollapseFieldEnum` | `DocumentId` | `DocumentId`: group by the `document_id` column. `Tag`: group by the value of the tag named `TagKey` |
| `TagKey` | string | null | Required when `Field` is `Tag`; 1-256 characters (the `_tags.key` column is `VARCHAR(256)`) |
| `CandidatePool` | int? | null, meaning `max(MaxResults x 4, 100)` capped at 1000 | Candidates retrieved before collapsing, for vector-only and full-text-only searches. Hybrid searches use `Hybrid.CandidatePool` when it is set, and this value otherwise. 1-10000 |

**Group key.** `Field = DocumentId` uses `COALESCE(document_id, document_key)`. `Field = Tag` uses
`COALESCE(NULLIF(<tag value>, ''), document_key)`. Falling back to `document_key` keeps an untagged document as its own
group, which matches Isis's `ParentKey` fallback (`RecallDbMemoryStore.cs` L529-533). The tag value is read with a
scalar subquery (`SELECT MIN(value) FROM <table>_tags WHERE document_key = d.document_key AND key = '<TagKey>'`)
because `_tags` has no uniqueness constraint on `(document_key, key)`; the existing `_t_dkey` index serves it.

Isis could in principle use `Field = DocumentId`, because it writes the memory slug to `DocumentId`. It should use
`Tag` with `parentKey` anyway: the slug is a display name that can change on rename, while `parentKey` is the memory id
Isis already treats as the identity. Supporting both costs one `CASE` in the SQL builder.

**Representative.** Within a group, the representative is the candidate with the highest `score`, ties broken by `id`.
For vector-only that is the smallest distance; for full-text-only the highest `text_score`. The representative's own
fields (content, position, per-leg scores and ranks) are returned, which is what `GroupByParent` does today: it keeps
the first chunk seen in best-first order.

**Ordering and thresholds.** Groups are ordered by the requested `SortOrder` applied to their representatives.
`SearchQuery.MinimumScore` and `MaximumScore` apply to the representative, after collapse. `FullText.MinimumScore` keeps
its current meaning (it gates the text leg before fusion).

**Pool shortfall.** When the pool holds fewer distinct groups than `MaxResults`, the response carries a `Notice`
(`"Collapse found N groups in a candidate pool of P; raise the candidate pool for more."`). It is not an error.

**Supported combinations.** Collapse works with vector-only, full-text-only, hybrid `Rrf` and hybrid `Linear`. With
hybrid `Filter` it returns 400 (`"Collapse is not supported with Hybrid.Strategy Filter."`). `Filter` is the legacy
whole-table path, and nobody has asked for it there. `IncludeNeighbors` works unchanged on the representative chunk.

### 4.3 Response additions

All additive and nullable, so older clients are unaffected.

`DocumentRecord`:

| Field | Type | Set when | Meaning |
|---|---|---|---|
| `RecencyRank` | int? | hybrid `Rrf` with `RecencyWeight > 0` | 1-based rank of the hit's recency key among the candidates, 1 = newest |
| `GroupKey` | string | `Collapse` is set | The group this hit represents |
| `GroupHits` | int? | `Collapse` is set | Number of candidates in the group, including the representative |

`SearchResult` gains nothing new; it already has `Notice`. When `Collapse` is set, `TotalRecords` is the number of
groups in the candidate set after thresholds, bounded by the pool (at most 2 x pool for hybrid).

`GET /` (and the MCP `server/info` tool) gains `Capabilities`, an array of strings (section 4.6).

### 4.4 SQL

The fused hybrid statement grows by four CTEs. Shown here with collapse by tag and recency on, cosine operator,
placeholders in angle brackets. Parts that exist today are unchanged.

```sql
WITH vec AS (                                   -- unchanged: HNSW-satisfiable
  SELECT id, (embeddings <=> <qv>) AS distance FROM <t>
  WHERE <filters> [AND <vector thresholds>]
  ORDER BY (embeddings <=> <qv>) LIMIT <pool>),
vr  AS (SELECT id, ROW_NUMBER() OVER (ORDER BY distance, id) AS rnk FROM vec),
txt AS (                                        -- unchanged: GIN-satisfiable
  SELECT id, <rank expr> AS text_score FROM <t>
  WHERE <filters> AND <match> [AND <rank expr> >= <FullText.MinimumScore>]
  ORDER BY text_score DESC, id LIMIT <pool>),
tr  AS (SELECT id, text_score, ROW_NUMBER() OVER (ORDER BY text_score DESC, id) AS rnk FROM txt),
cand AS (
  SELECT COALESCE(vr.id, tr.id) AS id, tr.text_score, vr.rnk AS vector_rank, tr.rnk AS text_rank
  FROM vr FULL OUTER JOIN tr ON vr.id = tr.id),
scored AS (                                     -- adds created_utc and group_key
  SELECT c.id, c.text_score, c.vector_rank, c.text_rank,
         (d.embeddings <=> <qv>) AS distance, d.created_utc,
         COALESCE(NULLIF((SELECT MIN(g.value) FROM <t>_tags g
                          WHERE g.document_key = d.document_key AND g.key = '<TagKey>'), ''),
                  d.document_key) AS group_key
  FROM cand c JOIN <t> d ON d.id = c.id),
grp AS (                                        -- new: one row per group (recency and GroupHits)
  SELECT group_key, MAX(created_utc) AS newest, COUNT(*) AS group_hits
  FROM scored GROUP BY group_key),
rec AS (                                        -- new, only when RecencyWeight > 0
  SELECT group_key, group_hits,
         ROW_NUMBER() OVER (ORDER BY newest DESC, group_key COLLATE "C") AS recency_rank
  FROM grp),
fused AS (
  SELECT s.*, r.recency_rank, r.group_hits,
         (1.0 - s.distance) AS vector_score,
         ((COALESCE(CAST(<1-w> AS double precision) / (<k> + s.vector_rank), 0)
         + COALESCE(CAST(<w>   AS double precision) / (<k> + s.text_rank),   0)
         + COALESCE(CAST(<r>   AS double precision) / (<k> + r.recency_rank), 0))
         * (<k> + 1) / <(1-w) + w + r>) AS score
  FROM scored s JOIN rec r ON r.group_key = s.group_key),
best AS (                                       -- new, only when Collapse is set
  SELECT f.*, ROW_NUMBER() OVER (PARTITION BY f.group_key ORDER BY f.score DESC, f.id) AS group_pos
  FROM fused f)
SELECT <columns of d>, b.distance, b.text_score, b.vector_score, b.vector_rank, b.text_rank,
       b.recency_rank, b.group_key, b.group_hits, b.score,
       COUNT(*) OVER () AS total_count, numnode(<tsq>) AS term_count
FROM best b JOIN <t> d ON d.id = b.id
WHERE b.group_pos = 1 [AND <score thresholds on b.score>]
ORDER BY <GetHybridOrderByClause(SortOrder)>
LIMIT <MaxResults> OFFSET <offset>;
```

Notes for the implementer:

- Without `Collapse`, `group_key` is `CAST(d.id AS text)` and `best` is skipped (the final `FROM` reads `fused`).
  `grp` is emitted when either recency or collapse is on; `rec` only when recency is on. With collapse and no recency,
  `fused` joins `grp` for `group_hits` and the recency term is omitted.
- `vector_score` is written as `1.0 - s.distance` above for cosine only; generate it with the existing
  `GetScoreFromDistanceSql`, as the fused path does today. Build this with small helpers rather than string flags scattered
  through `SearchHybridFusedAsync`: `BuildGroupKeySql(tableName, collapse)`, `BuildRecencyCtes(recencyOn)`,
  `BuildCollapseCte(collapse)`, and a `GetRrfFusionSql(vectorWeight, textWeight, recencyWeight, k)` overload that
  emits exactly today's text when `recencyWeight == 0`.
- The count-fallback statement (run only when the page is empty) must count `best WHERE group_pos = 1` when
  collapsing, so `TotalRecords` stays in groups.
- `TagKey` goes through `_Driver.Sanitize`, as tag filter keys already do (`BuildTagConditionClause`, L875-949).
- The `Linear` strategy gets `best` and `group_key` the same way; its score expression is unchanged.

**Single-leg collapse.** Vector-only with `Collapse` cannot use today's plain `ORDER BY distance LIMIT MaxResults`
(`SearchVectorOnlyAsync`), because a page of chunks is not a page of groups. It becomes a pool-then-collapse
statement, and the plain path stays byte-for-byte as it is when `Collapse` is null:

```sql
WITH vec AS (
  SELECT id, (embeddings <=> <qv>) AS distance FROM <t>
  WHERE <filters> ORDER BY (embeddings <=> <qv>) LIMIT <pool>),
scored AS (SELECT v.id, v.distance, <group key expr> AS group_key FROM vec v JOIN <t> d ON d.id = v.id),
best AS (
  SELECT s.*, COUNT(*) OVER (PARTITION BY s.group_key) AS group_hits,
         ROW_NUMBER() OVER (PARTITION BY s.group_key ORDER BY s.distance, s.id) AS group_pos
  FROM scored s)
SELECT <columns of d>, b.distance, (1.0 - b.distance) AS score, b.group_key, b.group_hits,
       COUNT(*) OVER () AS total_count
FROM best b JOIN <t> d ON d.id = b.id
WHERE b.group_pos = 1 [AND <score and distance thresholds>]
ORDER BY <sort> , b.id LIMIT <MaxResults> OFFSET <offset>;
```

It runs with `GetHnswSetupStatements(pool)` so the pool can fill, and it applies vector thresholds in SQL (a side
benefit: on this new path, pages are never short). Full-text-only with `Collapse` follows the same shape over the
existing `txt` expression. Vector score conversion uses `GetScoreFromDistanceSql` so non-cosine metrics keep working.

### 4.5 Validation (`SearchService.SearchAsync` and model setters)

Model setters throw `ArgumentOutOfRangeException`, which the server already turns into 400 (CHANGELOG v0.2.1, "Request
bodies whose values are rejected by a model's range check now return 400"). Cross-field rules live in
`SearchService.SearchAsync`, next to the existing `FullText.Query` check (L89-97).

| Field | Rule | Response |
|---|---|---|
| `Hybrid.RecencyWeight` | 0.0-1.0 and finite | 400 via setter |
| `Hybrid.RecencyWeight > 0` with `Strategy` `Linear` or `Filter` | Ignored | 200 with `Notice` |
| `Collapse.Field` | A defined `CollapseFieldEnum` value | 400 via setter |
| `Collapse.Field = Tag` with a null or blank `TagKey` | Rejected | 400, `"Collapse.TagKey is required when Collapse.Field is Tag."` |
| `Collapse.TagKey` | At most 256 characters | 400 via setter |
| `Collapse.CandidatePool` | 1-10000 | 400 via setter |
| `Collapse` with `Hybrid.Strategy = Filter` (both legs present) | Rejected | 400 |
| `Collapse` with neither a vector nor a text query | Rejected (there is nothing to rank) | 400, `"Collapse requires a vector or a full-text query."` |
| `FullText.MinimumShouldMatch` | 1-3; only with `MatchMode = Any` | 400 via setter; 400 for other match modes |

### 4.6 Capability advertisement

Add `Capabilities` (list of strings) to `HealthInfo` (`src/RecallDb.Core/Models/HealthInfo.cs`), return it from
`HealthGetRoute` (`RecallDbServer.cs` L1456-1465, which currently returns an anonymous object; switch it to
`HealthInfo`), and add it to `McpServerInfo` for the MCP `server/info` tool. Values for this work:

| Capability | Meaning |
|---|---|
| `search.hybrid.rrf` | Hybrid is the rank-fused union from `d8ce32c` (not the legacy text filter) |
| `search.hybrid.recency` | `Hybrid.RecencyWeight` is honored |
| `search.collapse` | `SearchQuery.Collapse` is honored |
| `search.include-embeddings` | `SearchQuery.IncludeEmbeddings` is honored |
| `search.fulltext.minimum-should-match` | `FullText.MinimumShouldMatch` is honored |

Keep the list in one static place (`SearchCapabilities` in `RecallDb.Core`, one class per file) so REST, MCP, and tests
share it. The version string cannot do this job: the owner has chosen to keep `0.2.1`, and old and new builds are both
in the wild under that tag.

### 4.7 Performance

The goal is one HTTP exchange per Isis search, one SQL statement for the ranking itself, both indexes used, and no
new sequential scans. Most of that exists; this section is about what to measure and the one lever worth adding.

**What a single call saves.** Per search, Isis stops paying for a second HTTP request, a second bearer-token
authentication, a second `Collections.ReadAsync`, and a second pair of label and tag batch queries. Because Isis runs
its legs concurrently, the saving is the overhead of the slower call plus contention, not half the latency. At
concurrency the saving is larger, because RecallDB is Isis's throughput ceiling (about 140 operations per second in
the Isis load test) and each search currently costs it two requests.

**What it does not save.** The text leg still ranks every row that matches any query lexeme. The Isis round-2 result
names this directly: SciFact is slower "because any-term text matching over 11,000 chunk documents ranks thousands of
matches per query; the full-text index is used, and the cost is the ranking itself."

**Step 1, measure (required).** Before changing anything, capture `EXPLAIN (ANALYZE, BUFFERS)` of the fused statement
on a SciFact-sized collection (the Isis harness creates one) for five representative SciFact queries. Record the time
in the `txt` CTE versus the `vec` CTE versus the join back to the table. If `txt` is not the majority, stop here and
record why.

**Step 2, `FullText.MinimumShouldMatch` (opt-in, default unchanged).** An integer from 1 to 3, default 1 (today's
`Any`). With m >= 2, the `Any` tsquery becomes the OR of every m-lexeme conjunction instead of the OR of lexemes, so the
GIN index returns only documents with at least m distinct query terms and far fewer rows reach `ts_rank`. Built in SQL
from the same lexemes as today, so stemming and operator neutralization are unchanged:

```sql
(WITH lex AS (
   SELECT DISTINCT '''' || replace(replace(lexeme, '\', '\\'), '''', '''''') || '''' AS q
   FROM unnest(to_tsvector('<lang>', '<query>')))
 SELECT CASE
   WHEN (SELECT COUNT(*) FROM lex) < 2      -- fewer lexemes than m: require all of them
     THEN (SELECT COALESCE(string_agg(q, ' & '), '') FROM lex)
   ELSE (SELECT string_agg('(' || a.q || ' & ' || b.q || ')', ' | ') FROM lex a JOIN lex b ON a.q < b.q)
 END)::tsquery
```

(m = 3 adds a third join.) Cap the lexemes considered at 16. Choosing the rarest 16 would need term statistics RecallDB
does not keep, so keep the first 16 in query order and document it; 16 lexemes give 120 pairs, well within what GIN
handles. In hybrid
mode the vector leg still covers semantic matches, so the recall risk is confined to documents that share exactly one
term with the query and are not semantically close. Whether it is worth turning on for Isis is a benchmark question:
run SciFact and isis-live with m = 1 and m = 2 and compare nDCG@10 and p50. The default stays 1 regardless of the
result, because changing it would change results for existing callers inside the same version.

**Step 3, optional.** If trace spans show the two label and tag batch queries are material (more than 10% of server
time at p50), fold them into the search statement as `json_agg` subselects over the final page. This is secondary and
should only happen with numbers behind it.

**Not in this plan.** BM25 (ParadeDB `pg_search`) or a RUM index would let Postgres return the top text matches
without ranking all of them. Both are new extensions in the image and belong in their own decision (section 12).

### 4.8 Backward compatibility

- Every new request field is optional and defaults to off. A request without `Hybrid.RecencyWeight`, `Collapse`, or
  `FullText.MinimumShouldMatch` produces the same SQL text and the same results as today. Add a test that asserts the
  generated SQL is unchanged for the existing hybrid, vector-only, and full-text-only paths (section 7.1).
- The vector-only path without `Collapse` keeps its HNSW-friendly `ORDER BY distance ASC` exactly (commit `b6f1af5`).
- Current `Hybrid` callers see no change. `Filter` stays as it is; `Linear` gains collapse support only.
- Old SDKs keep working against the new server: new response fields are ignored by their deserializers, and they never
  send the new request fields.
- New SDKs against an old server: the old server ignores `Collapse` and `RecencyWeight` silently (System.Text.Json
  skips unknown members). That is why clients must check `Capabilities` first, and why each SDK README says so.

---

## 5. API reference

### 5.1 Request

`POST /v1.0/tenants/{tid}/collections/{cid}/search`, exactly what Isis will send for a hybrid search with the default
`topK` of 10:

```json
{
  "Vector": {
    "SearchType": "CosineSimilarity",
    "Embeddings": [0.0123, -0.0456, 0.0789]
  },
  "FullText": {
    "Query": "how do I rotate the signing key",
    "MatchMode": "Any",
    "TextWeight": 0.5
  },
  "Hybrid": {
    "Strategy": "Rrf",
    "RrfK": 60,
    "CandidatePool": 40,
    "RecencyWeight": 0.1
  },
  "Collapse": {
    "Field": "Tag",
    "TagKey": "parentKey"
  },
  "LabelFilter": { "Required": ["cat_4kQ9mZ2x"] },
  "IncludeEmbeddings": false,
  "MaxResults": 10
}
```

Semantic-only and keyword-only searches send one leg plus `Collapse` (with `Collapse.CandidatePool` set to Isis's
`fetch`), and no `Hybrid`.

### 5.2 Response

```json
{
  "Success": true,
  "MaxResults": 10,
  "TotalRecords": 23,
  "RecordsRemaining": 13,
  "EndOfResults": false,
  "ContinuationToken": "10",
  "Notice": null,
  "TotalMs": 31.4,
  "Documents": [
    {
      "Id": 4182,
      "DocumentKey": "mem_7Tn2QwX-c2",
      "DocumentId": "signing-key-rotation",
      "Position": 2,
      "ContentType": "Text",
      "Content": "Rotate the signing key with ...",
      "CreatedUtc": "2026-09-20T14:02:11.482113Z",
      "Distance": 0.2144,
      "Score": 0.9800,
      "VectorScore": 0.7856,
      "TextScore": 0.0613,
      "VectorRank": 1,
      "TextRank": 3,
      "RecencyRank": 5,
      "GroupKey": "mem_7Tn2QwX",
      "GroupHits": 3,
      "Embeddings": null,
      "Labels": ["cat_4kQ9mZ2x"],
      "Tags": { "parentKey": "mem_7Tn2QwX", "ordinal": "2", "title": "Signing key rotation" }
    }
  ]
}
```

The `Score` is worked out from the formula: `0.5/61 + 0.5/63 + 0.1/65 = 0.017672`, the best achievable is
`1.1/61 = 0.018033`, and their ratio is 0.9800.

### 5.3 Health

```json
{
  "Name": "RecallDB",
  "Version": "0.2.1",
  "UptimeMs": 812345.6,
  "Capabilities": [
    "search.hybrid.rrf",
    "search.hybrid.recency",
    "search.collapse",
    "search.include-embeddings",
    "search.fulltext.minimum-should-match"
  ]
}
```

---

## 6. Changes by area

### 6.1 RecallDb.Core

| File | Change |
|---|---|
| `Models/HybridQuery.cs` | Add `RecencyWeight` (double, default 0.0, guarded 0.0-1.0 and finite, XML doc with default, range, and "Rrf only") |
| `Models/CollapseQuery.cs` (new) | `Field` (`CollapseFieldEnum`, default `DocumentId`, `JsonStringEnumConverter`), `TagKey` (string, at most 256), `CandidatePool` (int?, 1-10000). Guarded setters throw `ArgumentOutOfRangeException` |
| `Enums/CollapseFieldEnum.cs` (new) | `DocumentId`, `Tag` |
| `Models/SearchQuery.cs` | Add `Collapse` (CollapseQuery, default null) |
| `Models/FullTextQuery.cs` | Add `MinimumShouldMatch` (int, default 1, guarded 1-3) |
| `Models/DocumentRecord.cs` | Add `RecencyRank` (int?), `GroupKey` (string), `GroupHits` (int?). Map `recency_rank`, `group_key`, `group_hits` in `FromDataTable` next to L422-427, tolerating absent columns |
| `Models/HealthInfo.cs`, `Models/McpServerInfo.cs` | Add `Capabilities` (List<string>, never null) |
| `SearchCapabilities.cs` (new) | Static list of the capability strings in 4.6 |
| `Database/Postgresql/Implementations/SearchMethods.cs` | Section 4.4: recency CTEs, collapse CTE, `GetRrfFusionSql` overload, `BuildGroupKeySql`, collapse-aware count fallback, new `SearchVectorCollapsedAsync` and `SearchFullTextCollapsedAsync` (the uncollapsed methods stay as they are), `MinimumShouldMatch` in `BuildTsQueryExpression`, notices for recency-ignored and pool shortfall. New notice constants next to L27-32 |

### 6.2 RecallDb.Server

| File | Change |
|---|---|
| `Services/SearchService.cs` | Cross-field validation (4.5). Derive and tag `search.collapse` (`none`, `documentid`, `tag`) and `search.recency` (`on`, `off`) on the activity and metrics |
| `Observability/ServerTelemetry.cs` | `RecordSearch` (L299) gains `collapse` and `recency` label parameters with `"none"`/`"off"` defaults, so existing callers compile unchanged |
| `RecallDbServer.cs` | `HealthGetRoute` returns `HealthInfo` with `Capabilities`; update the health OpenAPI description (L590). Update the search route description (L1296-1305) with recency, collapse, and `MinimumShouldMatch`. Add a second request example built by `BuildSearchCollapsedExample()` next to `BuildSearchExample()` (L513-524), showing the section 5.1 body. `_Version` stays `0.2.1` |
| `Mcp/Registrations/SearchRegistrations.cs` | `search/query` description (L31-48) documents `Hybrid.RecencyWeight`, `Collapse`, `FullText.MinimumShouldMatch`, and adds a collapse example |
| `Mcp/Registrations/ServerRegistrations.cs` | `server/info` returns `Capabilities` |
| `docker/observability/grafana/dashboards/RecallDB-Search/search.json` | Rate and latency panels broken down by `collapse` and `recency` |

### 6.3 Dashboard

`dashboard/src/views/SearchQuery.jsx`:

- A **Recency weight** number input (0-1, step 0.05, default 0) shown when the search is hybrid and the strategy is
  `Rrf`, next to RRF k and Candidate pool (state near L322-324, builder near L178-183). Use the same explicit
  finite-number handling as `fullTextWeight`, so 0 stays 0.
- A **Collapse** group: a select (None, Document ID, Tag), a **Tag key** input shown for Tag, and a **Collapse
  candidate pool** input shown when the search is not hybrid. Client-side validation mirrors 4.5.
- A **Min terms to match** select (1, 2, 3) shown when match mode is `Any`.
- An **Include embeddings** checkbox (the view has `IncludeNeighbors` at L163 and L732 but no embeddings toggle).
- Result table (columns near L416-447): **Group**, **Group hits**, and **Recency rank** columns, shown when any hit has
  them. Show the `Notice` as today.
- `dashboard/src/views/ApiExplorer.jsx`: add the section 5.1 body as a search example.
- `dashboard/src/views/Search.jsx` (vector-only per collection) needs no change.

### 6.4 SDKs

The SDKs are thin: the C# SDK models use strings for enums and ignore nulls on write
(`JsonIgnoreCondition.WhenWritingNull`, `RecallDbClient.cs` L48-51); JS and Python pass dictionaries through. The work
is models, documentation, and harness cases.

| SDK | File | Change |
|---|---|---|
| C# | `sdk/csharp/RecallDb.Sdk/Models/HybridQuery.cs` | Add `double? RecencyWeight` (null so it is omitted on the wire unless set; XML doc gives the server default 0.0 and range) |
| | `Models/CollapseQuery.cs` (new) | `string Field` (default `"DocumentId"`), `string TagKey`, `int? CandidatePool` |
| | `Models/SearchQuery.cs` | Add `CollapseQuery Collapse` |
| | `Models/FullTextQuery.cs` | Add `int? MinimumShouldMatch` |
| | `Models/DocumentRecord.cs` | Add `int? RecencyRank`, `string GroupKey`, `int? GroupHits` |
| | `RecallDbClient.cs` | Add `GetCapabilitiesAsync(CancellationToken)` returning `List<string>` (empty when the server predates the field), built on the existing `HealthAsync` (L66) |
| | `README.md`, `GETTING_STARTED.md` | A single-call hybrid example with collapse and recency, and a paragraph on checking capabilities first |
| JS | `sdk/js/recalldb-sdk.js` | `search()` JSDoc (L545-596): `Hybrid.RecencyWeight`, `Collapse`, `FullText.MinimumShouldMatch`, `RecencyRank`, `GroupKey`, `GroupHits`. Add `getCapabilities()` |
| | `README.md`, `GETTING_STARTED.md` | Same example |
| Python | `sdk/python/recalldb_sdk.py` | `search()` docstring (L680-747) with the same fields; add `get_capabilities()` |
| | `README.md`, `GETTING_STARTED.md` | Same example |

Pre-existing drift, flagged and not changed here: `sdk/js/package.json` says `0.2.0` while everything else says `0.2.1`.

### 6.5 Repository docs

| File | Change |
|---|---|
| `REST_API.md` | Search request example (near L1348-1403): add the collapse and recency body. Search Modes (L1469): a "Recency" and a "Collapse" subsection with the formula from 4.1 and the group semantics from 4.2 (representative, `TotalRecords` in groups, pool shortfall). SearchQuery Fields (L1602): `Collapse`. HybridQuery Fields (L1637): `RecencyWeight`. New CollapseQuery Fields table. FullText fields: `MinimumShouldMatch`. Search Hit Fields (L1661): `RecencyRank`, `GroupKey`, `GroupHits`. Search Validation Errors (L1524): the rows from 4.5. Enumerations (near L1756): `CollapseFieldEnum`. Health section: `Capabilities` |
| `README.md` | Feature bullet (L50): recency and collapse. Hybrid Search section (L136-173): one paragraph on collapsing chunked documents and the recency signal, with the example |
| `MCP_API.md` | `search/query` (L246) new fields and example; `server/info` gains `Capabilities` |
| `RecallDB.postman_collection.json` | New requests in the Search folder (near L1863-2208): "Hybrid: collapse by tag with recency", "Vector: collapse by document id", "Full-text: minimum should match". Update the health request description with `Capabilities`. Use the collection's existing variables |
| `TESTING.md` | Describe the new seed documents and cases (7.1), and the verification step (7.4) |
| `CHANGELOG.md` | Append to the v0.2.1 entry (section 11) |

---

## 7. Tests

### 7.1 Server suites (`src/Test.Shared/HybridSearchSuites.cs`)

The hybrid suite seeds 11 single-chunk documents (L96-109). Collapse and recency need chunked parents with controlled
write times, so extend the seed with a small chunked set in the same collection:

- Parent `p-old` with chunks `p-old-c0`, `p-old-c1` (tag `parentKey = p-old`), written first.
- Parent `p-new` with chunks `p-new-c0`, `p-new-c1`, `p-new-c2` (tag `parentKey = p-new`), written after a short
  delay so `created_utc` differs. Its chunks are near-duplicates of `p-old`'s in vector and text, so recency decides
  the order between them.
- One untagged document `p-loose`, to exercise the `document_key` fallback.
- `DocumentId` on the chunks equals the parent name, so `Field = DocumentId` groups the same way.

New cases, all through HTTP like the rest of the suite:

| Case | Asserts |
|---|---|
| `SearchHybridRecencyZeroUnchanged` | `RecencyWeight = 0` returns the same ids, order, and scores (to 1e-12) as a request without the field |
| `SearchHybridRecencyBreaksTie` | With `RecencyWeight = 0.1`, `p-new`'s best chunk ranks above `p-old`'s; with 0 the order follows the legs |
| `SearchHybridRecencyNormalized` | Every score is in [0, 1]; a document first in vector, text, and recency scores 1.0 |
| `SearchHybridRecencyRankShared` | All candidate chunks of one parent share a `RecencyRank`; ranks are dense from 1 |
| `SearchHybridRecencyIgnoredForLinear` | `Linear` with `RecencyWeight > 0` succeeds, has no `RecencyRank`, and carries the notice |
| `SearchCollapseTagOneHitPerParent` | Hybrid with `Collapse` by `parentKey`: no two hits share `GroupKey`; `GroupHits` equals the number of that parent's chunks in the candidate set |
| `SearchCollapseRepresentativeIsBest` | The returned chunk is the highest-scoring chunk of its group (checked against an uncollapsed search) |
| `SearchCollapseCountsGroups` | `MaxResults = 1` returns one group; `TotalRecords` equals the distinct group count; pages do not overlap and cover every group once |
| `SearchCollapseUntaggedFallsBack` | `p-loose` is returned with `GroupKey` equal to its document key |
| `SearchCollapseDocumentIdMatchesTag` | `Field = DocumentId` and `Field = Tag` give the same ordered group list on the seed |
| `SearchCollapseVectorOnly`, `SearchCollapseFullTextOnly` | Single-leg collapse returns one hit per group, ordered best-first, thresholds applied in SQL |
| `SearchCollapsePoolShortfallNotice` | `CandidatePool = 2`, `MaxResults = 10`: fewer groups and the notice |
| `SearchCollapseWithNeighbors` | `IncludeNeighbors = 1` with collapse attaches neighbors to the representative |
| `SearchCollapseIncludeEmbeddings` | `IncludeEmbeddings = true` with collapse returns the representative's vector; false returns null |
| `SearchFullTextMinimumShouldMatch` | m = 2 excludes a document sharing one term and keeps one sharing two; m = 1 equals today's `Any`; a one-lexeme query with m = 2 requires that lexeme |
| `SearchSqlUnchangedWithoutNewFields` | A unit-level check (build the SQL through an internal builder exposed to `Test.Shared` with `InternalsVisibleTo`) that the vector-only, full-text-only, `Rrf`, `Linear`, and `Filter` statements are identical to golden strings captured before the change |
| `SearchValidationRecencyWeightOutOfRange` | 1.5 and -0.1 are 400 |
| `SearchValidationCollapseTagKeyMissing` | `Field = Tag` without `TagKey` is 400 |
| `SearchValidationCollapseWithFilter` | `Collapse` with `Strategy = Filter` is 400 |
| `SearchValidationCollapseNoQuery` | `Collapse` with neither leg is 400 |
| `SearchValidationMinimumShouldMatch` | 0 and 4 are 400; 2 with `MatchMode = All` is 400 |
| `HealthReportsCapabilities` (in `RecallDbSuites.cs`) | `GET /` returns every string in `SearchCapabilities` |

Existing hybrid cases stay as they are; `SearchHybridRrfScoreNormalized` and `SearchHybridRrfRanksPopulated` are the
regression guard for the unchanged formula.

### 7.2 MCP (`src/Test.Shared/RecallDbMcpSuites.cs`)

- `McpSearchHybridCollapseRecency`: a `search/query` call with the section 5.1 shape returns `GroupKey` and
  `RecencyRank`.
- `McpServerInfoCapabilities`: `server/info` returns the capability list.

### 7.3 SDK harnesses

| SDK | Harness | New cases |
|---|---|---|
| C# | `sdk/csharp/RecallDb.Sdk.TestHarness/Program.cs` (hybrid cases near L220-234, `MakeFullTextQuery` helper near L1565) | Collapse by tag with recency (one hit per parent, ranks present); vector-only collapse; `IncludeEmbeddings` returns vectors; `GetCapabilitiesAsync` contains `search.collapse`; recency weight 1.5 raises `RecallDbException` with 400 |
| JS | `sdk/js/test-harness.js` (hybrid cases near L727-831, runner near L1305-1315) | Same five |
| Python | `sdk/python/test_harness.py` (hybrid cases near L917-1025, runner near L1669-1679) | Same five |

All three already use `127.0.0.1` for loopback, per the repository requirements; keep it that way in new code.

### 7.4 Verification and regression check

RecallDB has no benchmark suite of its own (no `benchmarks/` or `src/Test.Benchmark/`), and building one to the
`BENCHMARKING.md` standard is a separate project (section 12). For this change the regression check is the Isis
harness, which is black-box against RecallDB through Isis and already has baselines, plus a query-plan capture. Results
go in the PR description.

1. **Query plans.** `EXPLAIN (ANALYZE, BUFFERS)` of the collapsed, recency-on hybrid statement on the SciFact collection
   the Isis harness creates. Expect an Index Scan on `_hnsw` in `vec` and a Bitmap Index Scan on `_tsv` in `txt`, and
   no Seq Scan on the documents table. Capture the same for single-leg collapse.
2. **Isis retrieval baseline, before and after.** From `C:\Code\AgentMemory`, with Isis switched to the single-call
   path (section 8) and pointed at the new RecallDB:
   `dotnet run --project src/Test.Benchmark -c Release -- retrieval --dataset <each of isis-live, atlas, scifact, longmemeval-s-60>`,
   then `compare --baseline <round-5 report> --candidate <new report> --tolerance 0.01 --latency-tolerance 0.25`.
3. **MinimumShouldMatch sweep.** The same retrieval runs on SciFact and isis-live with m = 2, labelled `msm2`, to decide
   whether Isis should opt in (4.7 step 2).
4. **Load.** `load --stub --stub-latency-ms 5 --corpus-size 10000 --concurrency 1,4,16,64 --duration 30 --dataset benchmarks/data/scifact.json`,
   compared with the round-1 numbers in Isis `benchmarks/RESULTS.md`.

---

## 8. Isis integration

What follows is what Isis will change after RecallDB ships this work. It is guidance for the Isis owner; none of it is
part of the RecallDB change, and no Isis code changes as part of this plan.

**Capability detection.** `RecallDbMemoryStore` reads `Capabilities` once per RecallDB client (via `HealthAsync`, whose
dictionary result already exposes new keys without an SDK change, or the new `GetCapabilitiesAsync`) and caches it for
the process lifetime, refreshing on a failed search. Single-call hybrid requires both `search.hybrid.recency` and
`search.collapse`; embeddings in results require `search.include-embeddings`.

**Hybrid path (`SearchAsync`, today L364-397).** When capable, build one `SearchQuery`:

- `Vector` with the query embedding, `FullText` with `Query = query.QueryText` and `TextWeight = query.TextWeight`.
- `Hybrid = { Strategy = "Rrf", RrfK = HybridFusion.DefaultRrfK, CandidatePool = fetch, RecencyWeight = query.RecencyWeight }`.
  Passing `fetch` as the pool keeps the ranks and the recency candidate set identical to today's two calls, which is
  what makes parity testable. Raising the pool to the server default later is a separate, benchmarked change.
- `Collapse = { Field = "Tag", TagKey = "parentKey" }`, `LabelFilter` as today, `MaxResults = topK`.
- `IncludeEmbeddings = true` only when `query.Diversity > 0` or the caller is the write-time similarity check.

Map each returned `DocumentRecord` to a `FusedDocument` with `Score`, `TextScore`, `VectorRank`, `TextRank`, and
`RecencyRank` copied across. Copy `VectorScore` only when `VectorRank` has a value, so Isis's meaning ("null when the
vector leg did not return it") is preserved (gap G7). Keep a final `GroupByParent` pass over the result: it is a no-op
on collapsed results and a safety net if the server ever returns duplicates.

**Semantic and Keyword paths (today L398-423).** Send the single leg with `Collapse` and `Collapse.CandidatePool = fetch`,
`MaxResults = topK`. The client-side rollup becomes the same safety-net pass.

**Fallback.** When the capabilities are absent, keep today's two-call path, `HybridFusion`, and `FusedDocument` exactly
as they are. `HybridFusion` stays the reference implementation that the parity test compares against, so it is not
dead code.

**Snippet budget.** Unchanged: it runs on the returned representative chunk's `Content`, as it does now.

**Embeddings for diversity and similarity.** With `IncludeEmbeddings`, `SearchDiversifier` and the similarity check can
compare the representative chunk's vector instead of words. Whole-memory vectors (for example a mean over all chunks)
are a follow-up (section 12).

**Parity test (Isis `src/Test.Shared`, new `RecallDbSingleCallParity` suite).** Against a live RecallDB, for every
question in `benchmarks/datasets/isis-live.json` and a 100-question SciFact sample, run both paths with the same
embedding, `TextWeight`, `RecencyWeight`, `topK`, and `fetch`, then compare:

- The same ordered list of parent keys, except where adjacent fused scores differ by less than 1e-9 (ties, gap G8).
- Fused scores equal within 1e-9 per parent; `VectorRank`, `TextRank`, and `RecencyRank` equal.
- For isis-live (small enough that HNSW returns exact neighbors) the match must be exact for every question. For
  SciFact, report the agreement rate, which must be at least 98%; the expected source of mismatches is the 40-row
  truncation of the two-call vector leg when `fetch` > 40 (gap G6), so also run the sample at `topK = 10` (`fetch` = 40),
  where agreement must be 100%.

---

## 9. Acceptance criteria

1. **Single call.** An Isis hybrid search on a capable server makes exactly one RecallDB HTTP request (verified in the
   parity suite by counting requests through a `DelegatingHandler`, or in RecallDB's request history).
2. **Parity.** The Isis parity suite passes as defined in section 8.
3. **Formula.** With `RecencyWeight = 0`, RecallDB's fused scores are bit-identical to today's; with it on, they equal
   `HybridFusion.Fuse` within 1e-9 on the section 7.1 seed.
4. **Collapse semantics.** One hit per group, representative is the best chunk, `MaxResults`, `TotalRecords`, and
   continuation tokens count groups, and pages neither overlap nor skip groups.
5. **Compatibility.** `SearchSqlUnchangedWithoutNewFields` passes; every existing test in `Test.Automated`,
   `Test.Xunit`, `Test.Nunit`, and the three SDK harnesses passes unchanged.
6. **Index use.** The plans in 7.4 show HNSW and GIN index scans and no sequential scan of the documents table.
7. **Quality.** Isis `compare` against the round-5 reports shows no nDCG@10 regression beyond 0.01 on isis-live, atlas,
   scifact, or longmemeval-s-60.
8. **Latency.** Isis Hybrid p50 without a reranker is lower on every dataset than the latest no-rerank baseline run on
   the same machine immediately before the switch, and at least 10%
   lower on SciFact; no p95 regresses beyond the 0.25 latency tolerance. At concurrency 16 in the load test, throughput
   is at least the round-1 figure (139 operations per second). If the 10% SciFact target is missed, the step 1 plan
   capture in 4.7 must show where the time goes before the work is accepted.
9. **Capabilities.** `GET /` and MCP `server/info` return the list in 4.6; an older server returns none and Isis falls
   back.
10. **Docs.** Every file in 6.5 and the SDK READMEs is updated; no document in the change contains an em-dash.

---

## 10. Observability

`RecordSearch` gains two low-cardinality labels, `recalldb_search_collapse` (`none`, `documentid`, `tag`) and
`recalldb_search_recency` (`on`, `off`), alongside the existing `recalldb_search_mode`, `match_mode`, and
`hybrid_strategy`. The Grafana Search dashboard breaks rate and latency down by both. The search activity carries
`search.collapse`, `search.recency`, the pool size, and the number of groups returned, so a slow collapsed search can be
told apart from a slow ranked one in Tempo.

---

## 11. Release and versioning

Following `C:\code\agents\requirements\VERSIONING.md`: no version number changes. `_Version` in `RecallDbServer.cs`,
`<Version>` in `RecallDb.Sdk.csproj`, the JS and Python package metadata, and the MCP test's version assertion stay as
they are. The CHANGELOG gets bullets appended to the existing **v0.2.1** entry and no new heading:

```markdown
- Hybrid `Rrf` search can fuse a third, recency signal: new `Hybrid.RecencyWeight` (0.0-1.0, default 0, off) ranks
  candidates by newest `created_utc` per collapse group (or per document) and the fused score stays normalized to [0, 1];
  hits carry `RecencyRank`. Ignored with a `Notice` for `Linear` and `Filter`
- New `SearchQuery.Collapse` (`Field` = `DocumentId` or `Tag` with `TagKey`; `CandidatePool` for single-leg searches)
  returns one hit per group, its best-scoring chunk, with `GroupKey` and `GroupHits`; `MaxResults`, `TotalRecords`, and
  continuation tokens count groups. Works with vector-only, full-text-only, and hybrid `Rrf` and `Linear`
- New opt-in `FullText.MinimumShouldMatch` (1-3, default 1) for `MatchMode = Any`: only documents containing at least
  that many distinct query terms match, which cuts ranking work on large collections
- `GET /` and MCP `server/info` report `Capabilities`, so clients can detect these features on servers that share the
  same version
- SDKs (C#, JS, Python): the new request and response fields, and a capabilities helper
- Dashboard search: recency weight, collapse, minimum terms to match, include-embeddings controls, and group and recency
  columns
```

**Registries.** The source tree, Docker images, and docs can carry this under v0.2.1. NuGet, npm, and PyPI cannot:
`RecallDb.Sdk` 0.2.1 is already published and immutable, and it is the package Isis consumes. Gap G3 exists precisely
because of that. Getting the C# SDK changes (from `d8ce32c` onward, plus this plan) to Isis through NuGet needs a
publish, and whether and how to publish is the owner's call under VERSIONING.md section 4; this plan does not make it.
Until then, Isis can validate the work against a project reference to `sdk/csharp/RecallDb.Sdk` in a local branch, and
the capability check keeps production Isis on its fallback.

**Same tag, different behavior.** Re-pushing `jchristn77/recalldb-server:v0.2.1` gives existing callers new capabilities
but no behavior change, because every new field defaults to off. That is the reason for making `RecencyWeight`
default 0 and `MinimumShouldMatch` default 1 rather than matching Isis's defaults.

---

## 12. Out of scope and follow-ups

- **BM25 or RUM for the text leg.** Returns the top text matches without ranking all of them. Needs a new extension in
  the database image and its own evaluation; it is the real fix for gap G5 if 4.7 step 2 is not enough.
- **pgvector 0.8 or later**, for `hnsw.iterative_scan` (filtered searches fill the pool) and to lift the 1000 cap on
  `ef_search`. The shipped image is pgvector 0.5.1.
- **Whole-group embeddings**, for example `Collapse.IncludeMeanEmbedding` returning the mean of all chunk vectors in
  the group, so Isis diversity compares whole memories. Needs the pgvector `avg(vector)` aggregate confirmed on the
  shipped image and a decision on whether the mean is over all chunks or only candidates.
- **Recency by a tag timestamp** (for example "last modified") instead of `created_utc`, and recency for vector-only and
  full-text-only searches.
- **A RecallDB benchmark suite** under `benchmarks/` and `src/Test.Benchmark/` to the `BENCHMARKING.md` standard, so the
  next search change is gated by RecallDB's own numbers instead of Isis's.
- **Vector-only thresholds in SQL on the uncollapsed path.** Still applied after `LIMIT` (`SearchMethods.cs` L198-222);
  the new collapsed path does not have the problem.
- **HNSW index for non-cosine metrics.** The only HNSW index is `vector_cosine_ops` (`DynamicTableQueries.cs` L66), so
  Euclidean and inner-product searches scan. Unrelated to Isis, which uses cosine, but found while reading.

---

## 13. Implementation checklist

Annotate in place. Status values: `todo`, `doing`, `done`, `blocked`, `skipped`.

### Core and SQL

| # | Item | Ref | Status | Owner | Notes |
|---|---|---|---|---|---|
| C1 | `HybridQuery.RecencyWeight` with guarded setter and XML docs | 4.1, 6.1 | done | |  |
| C2 | `CollapseFieldEnum`, `CollapseQuery`, `SearchQuery.Collapse` | 4.2, 6.1 | done | |  |
| C3 | `FullTextQuery.MinimumShouldMatch` | 4.7, 6.1 | done | |  |
| C4 | `DocumentRecord.RecencyRank`, `GroupKey`, `GroupHits` and `FromDataTable` mapping | 4.3 | done | |  |
| C5 | Capture golden SQL for all existing paths before touching `SearchMethods` | 4.8, 7.1 | done | | Do this first; Captured from the server query log before and after on the full suite; SQL identical for every existing search path, instead of an InternalsVisibleTo golden test |
| C6 | `GetRrfFusionSql` overload with recency term and divisor, unchanged text at r = 0 | 4.1, 4.4 | done | |  |
| C7 | `scored`, `grp`, `rec`, `best` CTEs and collapse-aware count fallback in `SearchHybridFusedAsync` | 4.4 | done | |  |
| C8 | Collapse for `Linear` | 4.2 | done | |  |
| C9 | `SearchVectorCollapsedAsync` with `ef_search` setup and SQL thresholds | 4.4 | done | |  |
| C10 | `SearchFullTextCollapsedAsync` | 4.4 | done | |  |
| C11 | `MinimumShouldMatch` in `BuildTsQueryExpression`, 16-lexeme cap | 4.7 | done | |  |
| C12 | Notices: recency ignored, pool shortfall | 4.1, 4.2 | done | | Pool-shortfall notice only when a leg filled the pool |
| C13 | `SearchCapabilities`, `HealthInfo.Capabilities`, `McpServerInfo.Capabilities` | 4.6 | done | |  |

### Server, REST, MCP, observability

| # | Item | Ref | Status | Owner | Notes |
|---|---|---|---|---|---|
| S1 | Cross-field validation in `SearchService.SearchAsync` | 4.5 | done | |  |
| S2 | `HealthGetRoute` returns `HealthInfo` with capabilities; OpenAPI description | 4.6, 6.2 | done | |  |
| S3 | Search route description and `BuildSearchCollapsedExample` | 6.2 | done | | Descriptions updated; the OpenAPI helper takes one example, so no second example |
| S4 | MCP `search/query` description and example; `server/info` capabilities | 6.2 | done | |  |
| S5 | Telemetry labels and span attributes; Grafana Search dashboard panels | 10 | doing | | Labels and span attributes done; Grafana panels not added |

### Dashboard

| # | Item | Ref | Status | Owner | Notes |
|---|---|---|---|---|---|
| D1 | Recency weight input (Rrf only) | 6.3 | todo | |  |
| D2 | Collapse controls and validation | 6.3 | todo | |  |
| D3 | Min terms to match select | 6.3 | todo | |  |
| D4 | Include embeddings checkbox | 6.3 | todo | |  |
| D5 | Group, Group hits, Recency rank result columns | 6.3 | todo | |  |
| D6 | API Explorer example | 6.3 | todo | |  |

### SDKs

| # | Item | Ref | Status | Owner | Notes |
|---|---|---|---|---|---|
| K1 | C# models and `GetCapabilitiesAsync` | 6.4 | done | | See SDK_IMPROVEMENTS.md |
| K2 | C# README and GETTING_STARTED | 6.4 | done | |  |
| K3 | JS JSDoc, `getCapabilities()`, README, GETTING_STARTED | 6.4 | done | |  |
| K4 | Python docstring, `get_capabilities()`, README, GETTING_STARTED | 6.4 | done | |  |
| K5 | Harness cases in all three SDKs | 7.3 | done | |  |

### Tests

| # | Item | Ref | Status | Owner | Notes |
|---|---|---|---|---|---|
| T1 | Chunked seed documents with staggered writes | 7.1 | done | | In a new SearchGroupingSuites with its own collection, so the hybrid suite baselines are untouched |
| T2 | Recency cases | 7.1 | done | |  |
| T3 | Collapse cases (hybrid, single-leg, pagination, fallback, neighbors, embeddings) | 7.1 | done | |  |
| T4 | `MinimumShouldMatch` case | 7.1 | done | |  |
| T5 | `SearchSqlUnchangedWithoutNewFields` | 7.1 | skipped | | Replaced by the query-log comparison in C5 |
| T6 | Validation cases | 7.1 | done | |  |
| T7 | `HealthReportsCapabilities` | 7.1 | done | |  |
| T8 | MCP cases | 7.2 | done | |  |
| T9 | Run Automated, Xunit, Nunit, and all SDK harnesses | 9 | done | | Test.Automated 270/270; SDK harnesses pass |

### Docs and release

| # | Item | Ref | Status | Owner | Notes |
|---|---|---|---|---|---|
| R1 | `REST_API.md` | 6.5 | done | |  |
| R2 | `README.md` | 6.5 | done | |  |
| R3 | `MCP_API.md` | 6.5 | done | |  |
| R4 | Postman collection | 6.5 | done | |  |
| R5 | `TESTING.md` | 6.5 | done | |  |
| R6 | CHANGELOG bullets under v0.2.1 | 11 | done | | No version change; Server bullets under v0.2.1; SDK bullets under a new SDKs v0.2.2 heading |
| R7 | Em-dash sweep of every changed file | 9 | done | |  |
| R8 | Owner decision on publishing the SDKs | 11 | done | | Owner only; Owner set the SDKs to 0.2.2; RecallDb.Sdk 0.2.2 published to NuGet |

### Verification

| # | Item | Ref | Status | Owner | Notes |
|---|---|---|---|---|---|
| V1 | `EXPLAIN (ANALYZE, BUFFERS)` capture, before and after | 4.7, 7.4 | todo | | |
| V2 | Isis retrieval runs on four datasets and `compare` | 7.4 | todo | | |
| V3 | `MinimumShouldMatch` sweep and recommendation for Isis | 7.4 | todo | | |
| V4 | Isis load test | 7.4 | todo | | |
| V5 | Isis parity suite (in a local Isis branch against a project-referenced SDK) | 8 | todo | | |

---

## 14. Verified, and not verified

Verified by reading the code at `0d022e6`: everything in sections 2 and 3, including the fused SQL, the RRF
normalization, label filters on both legs, `IncludeEmbeddings` on the fused path, the SDK source models, the health
route's fields, the `_tags` schema, and Isis's fusion, rollup, and query model. The published `RecallDb.Sdk` 0.2.1 was
checked by searching the DLL in the local NuGet cache for member names, not by decompiling it.

Not verified, and worth a check before or during implementation:

- The SQL in 4.4 and 4.7 has not been executed. It follows the shape of the statement `d8ce32c` validated on
  PostgreSQL 15.4, but the new CTEs, the `COLLATE "C"` tie-break, and the m-lexeme tsquery should be run against the
  shipped `ankane/pgvector:v0.5.1` image first.
- Where the SciFact hybrid time actually goes. The Isis results attribute it to text ranking; 4.7 step 1 exists to
  confirm that before anyone optimizes it.
- The 40-row truncation of Isis's vector leg (gap G6) is inferred from pgvector 0.5.1's documented HNSW behavior and
  the comment at `SearchMethods.cs` L57-60; it has not been reproduced against Isis.
- Whether any deployed Isis instance points at a RecallDB built before `d8ce32c`. The capability check makes the answer
  irrelevant to correctness, but it decides how long the fallback path stays warm.
- The 10% SciFact latency target in section 9 is a judgment, not a measurement. Adjust it after V1 if the plan capture
  says the round trip was never the expensive part.
