# Retrieval

How data comes back out of RecallDB: the search modes and how each one ranks, the filters, grouping and context
expansion, what the numbers in a response mean, performance, and the limits to design around. For how data gets in,
see [INGESTION.md](INGESTION.md); for the storage and index layout, see [ARCHITECTURE.md](ARCHITECTURE.md#5-storage-layout).
Field-by-field formats are in [REST_API.md](../REST_API.md#search).

---

## 1. Search at a glance

Every search is one request to one collection:

```http
POST /v1.0/tenants/{tid}/collections/{cid}/search
```

The body is a `SearchQuery`. Its parts are independent and combine freely:

| Part | Fields | Effect |
|---|---|---|
| Vector leg | `Vector.Embeddings`, `Vector.SearchType`, `Vector.EfSearch`, vector thresholds | Rank by similarity to a query vector ([section 3](#3-vector-search)) |
| Text leg | `FullText.Query`, `MatchMode`, `MinimumShouldMatch`, `SearchType`, `Language`, `Normalization`, `TextWeight` | Rank by lexical relevance ([section 4](#4-full-text-search)) |
| Fusion | `Hybrid.Strategy`, `RrfK`, `CandidatePool`, `RecencyWeight` | How the two legs combine when both are present ([section 5](#5-hybrid-search)) |
| Filters | `LabelFilter`, `TagFilter`, `Terms`, `CreatedAfter`, `CreatedBefore`, `DocumentIds` | Restrict which documents can match ([section 6](#6-filters)) |
| Grouping | `Collapse` | One hit per document or per tag value instead of per chunk ([section 7](#7-grouping-and-context)) |
| Context | `IncludeNeighbors`, `IncludeEmbeddings` | Add surrounding chunks or stored vectors to each hit |
| Shape | `SortOrder`, `MaxResults`, `ContinuationToken`, `MinimumScore`, `MaximumScore`, `MinimumDistance`, `MaximumDistance` | Ordering, paging, thresholds ([section 8](#8-results-scores-and-paging)) |

A single request can therefore say "hybrid-rank these 1536-dimension embeddings and this question, only within
documents labeled `published` and tagged `lang = en` from the last 90 days, one hit per source document, newest
sources slightly favored, with one chunk of context on each side".

---

## 2. Choosing a mode

The server decides the mode from what the request contains, not from a mode field:

| `Vector.Embeddings` non-empty | `FullText.Query` non-blank | Mode | Scored by |
|---|---|---|---|
| yes | no | **Vector** | Similarity to the query vector |
| no | yes | **Full-text** | `ts_rank` or `ts_rank_cd` |
| yes | yes | **Hybrid** | Fusion of both legs (`Rrf` by default) |
| no | no | Filter-only | Nothing: matching documents come back with `Score` 0 in no useful order. Use [enumeration](#12-enumeration-unranked-retrieval) instead. |

A `FullText` object with a blank `Query` counts as absent: next to a vector it is ignored (with a `Notice`); on its own
it is a 400. `Hybrid` options on a single-leg search are ignored, with a `Notice`.

**Which one to use:**

- **Hybrid** is the best default for natural-language questions over prose. The vector leg finds paraphrases and
  answers that share no words with the question; the text leg catches names, codes, identifiers, and rare terms that
  embeddings blur. Rank fusion keeps a result that is strong in either leg.
- **Vector** alone suits similarity lookups (near-duplicate detection, "more like this", classification by neighbor)
  and content where words are not meaningful (code embeddings, images with vector-only descriptions). With selective
  filters, read [section 6.5](#65-filters-and-the-vector-index) on `Vector.EfSearch`.
- **Full-text** alone suits keyword lookups, exact-term audits, and callers that have no embedding model at query time.

---

## 3. Vector search

### 3.1 Metrics

`Vector.SearchType` picks the pgvector operator and how `Score` is derived from the raw distance:

| `SearchType` | Operator | `Distance` | `Score` | Better is |
|---|---|---|---|---|
| `CosineSimilarity` (default) | `<=>` | cosine distance, 0 to 2 | `1 - distance` (cosine similarity, -1 to 1) | higher score |
| `CosineDistance` | `<=>` | cosine distance | the distance itself | lower score |
| `EuclideanSimilarity` | `<->` | L2 distance, 0 and up | `1 - distance` (unbounded below) | higher score |
| `EuclideanDistance` | `<->` | L2 distance | the distance itself | lower score |
| `InnerProduct` | `<#>` | negative inner product | the inner product | higher score |

Use the metric your embedding model was trained for. For normalized embeddings (most text models), cosine and inner
product rank identically, and cosine is the one the index serves (section 3.2).

With the two `...Distance` types `Score` is a distance, so sort with `DistanceAscending` to get nearest first in every
code path. (An uncollapsed vector-only search treats `ScoreDescending` as nearest-first for every metric, but a
collapsed one sorts groups by the literal `Score`.)

### 3.2 How it runs

Each collection has one HNSW index, built with the **cosine** operator class (`m = 16`, `ef_construction = 64`):

- `CosineSimilarity` and `CosineDistance` searches use it. The vector-only statement orders by the raw distance
  operator (`ORDER BY embeddings <=> $q LIMIT n`), which is the only form the index can satisfy.
- `EuclideanSimilarity`, `EuclideanDistance`, and `InnerProduct` are correct but cannot use it, so PostgreSQL computes
  the distance for every row that passes the filters. On a 13,700-chunk collection they took 48 and 69 ms against
  16 ms for cosine ([section 9](#9-performance)); the gap grows with collection size.

HNSW is approximate: it returns very good neighbors quickly, not a guaranteed exact top-k. With pgvector 0.5.1 an HNSW
scan also returns at most `hnsw.ef_search` rows, and pgvector's own default is 40. RecallDB sets `ef_search` for each
query (with `SET LOCAL`, so it lasts only for that statement):

| Search | `ef_search` when `Vector.EfSearch` is not set |
|---|---|
| Vector-only | `min(max((offset + MaxResults) * 4, 100), 1000)`: four times the rows the page needs, at least 100 |
| Hybrid (vector leg) | The candidate pool, at most 1000 |
| Collapsed vector | The candidate pool, at most 1000 |

`Vector.EfSearch` (1 to 1000; values outside the range are clamped, not rejected) replaces that default for the query.
Raise it for better recall, deeper pages, or selective filters ([section 6.5](#65-filters-and-the-vector-index)); lower it
to trade recall for latency. A value below what the page needs caps the page: `EfSearch: 20` with `MaxResults: 40`
returns 20 hits. Because pgvector 0.5.1 cannot go above 1000, a vector-only search can page through at most the 1000
nearest neighbors.

### 3.3 Thresholds

`Vector.MinimumScore` / `MaximumScore` / `MinimumDistance` / `MaximumDistance` (or the same fields at the top level of
the query, which take precedence) drop hits outside the range.

- In an **uncollapsed vector-only** search they are applied to the page after it is fetched, so a page can be shorter
  than `MaxResults` while `TotalRecords` still counts every document that passed the filters.
- In **collapsed** vector searches they are applied in SQL to each group's representative, so pages are full and
  `TotalRecords` reflects them.
- In **hybrid** searches `Vector.*` thresholds restrict the vector leg's candidates, and the top-level
  `MinimumScore` / `MaximumScore` apply to the fused score.

---

## 4. Full-text search

### 4.1 Indexing and languages

Every document's `content` is tokenized into the stored `content_tsv` column with PostgreSQL's `english` text search
configuration (stemming, stop-word removal), and a GIN index covers it. A query in `english` (the default) matches
against that column through the index.

`FullText.Language` may name any configuration installed in PostgreSQL (`simple`, `spanish`, `german`, `french`, ...;
see `pg_ts_config`). For anything other than `english` the server tokenizes each row's content on the fly with that
configuration, which is correct but cannot use the index, and the response carries a `Notice` saying so. An unknown
language is a 400. For a collection that is mostly in another language, expect full-text search to be slower, or rely
more on the vector leg.

### 4.2 Match modes

`FullText.MatchMode` decides which documents match:

| Mode | Built with | Matches |
|---|---|---|
| `Any` (default) | the OR of the lexemes `to_tsvector` produces from the query | Documents containing any meaningful query term. Operators typed in the query are treated as text. Documents with more (and rarer) terms rank higher. |
| `All` | `plainto_tsquery` | Documents containing every term |
| `Phrase` | `phraseto_tsquery` | The terms adjacent and in order |
| `WebSearch` | `websearch_to_tsquery` | Search-box syntax: `"quoted phrase"`, `or`, `-exclude` |

`Any` is the right mode for questions ("how do I run the test suite"): requiring every word of a question returns
nothing surprisingly often. Use `All` or `Phrase` for precise lookups.

A query made only of stop words ("the and of") produces no terms. That is not an error: a full-text search returns no
hits, a hybrid search ranks by the vector leg alone, and either way the response carries a `Notice`.

### 4.3 Minimum should match

With `MatchMode = Any`, `FullText.MinimumShouldMatch` (1 to 3, default 1) requires that many distinct query terms in a
document. The query becomes the OR of every 2-term (or 3-term) conjunction of its first 16 terms; a query with fewer
terms than the value requires all of them. It trims weak single-term matches and shrinks the set of rows to rank.

It is not free. The conjunction query grows with the square (or cube) of the term count and every matching row is
checked and ranked against it. On a 13,700-chunk collection, `2` made 5- to 7-term queries 17-61% faster, and a 16-term
query 2.6 times slower. Measure with your own queries, and consider it for short queries only.

### 4.4 Ranking

`FullText.SearchType` picks the rank function: `TsRank` (default; term frequency) or `TsRankCd` (cover density, which
rewards query terms appearing close together). `FullText.Normalization` is PostgreSQL's rank normalization bitmask
(0-63). The default, 32, divides the rank by itself plus one, which keeps `TextScore` in [0, 1); 1 or 2 divide by the
document length (log or plain), which stops long chunks winning just by being long.

`TextScore` is the raw rank. In a full-text search `Score` equals `TextScore`, thresholds (`FullText.MinimumScore`,
top-level `MinimumScore` / `MaximumScore`) are applied in SQL, and `TotalRecords` is the exact number of matches.

### 4.5 Full-text versus the `Terms` filter

`Terms` ([section 6.3](#63-terms)) is a case-insensitive **substring** filter: no stemming, no ranking, and it matches
inside words ("art" matches "article"). `FullText` is ranked lexical search over stemmed words. Use `Terms` to require or
exclude an exact string; use `FullText` to find and rank relevant text.

---

## 5. Hybrid search

A hybrid search runs the vector leg and the text leg as two ranked candidate lists, each under the same filters, and
fuses them in one SQL statement.

### 5.1 Candidates

Each leg retrieves its own top `Hybrid.CandidatePool` documents: the vector leg through the HNSW index, the text leg
through the GIN index. The default pool is `max(MaxResults * 4, 100)`, capped at 1000; it can be set from 1 to 10000.
The union of the two lists is the candidate set, so `TotalRecords` is at most `2 * CandidatePool`, and paging stays
within it: raise the pool to page deeper, or to let fusion see more of each leg.

`FullText.MinimumScore` gates only the text leg, and `Vector.*` thresholds only the vector leg, so a document that
fails one leg's threshold can still come back on the other leg's strength.

### 5.2 Strategies

`FullText.TextWeight` (`w`, 0.0 to 1.0, default 0.5) is the text leg's share in every strategy; the vector leg gets
`1 - w`. `w = 0` ranks by vector only and `w = 1` by text only.

**`Rrf`** (default): weighted Reciprocal Rank Fusion. Only ranks matter, not raw scores, so the two legs' incompatible
scales (cosine similarity versus `ts_rank`) never have to be reconciled. With `k = Hybrid.RrfK` (default 60):

```
Score = ((1 - w) / (k + VectorRank) + w / (k + TextRank)) * (k + 1)
```

A leg the document is missing from contributes 0. The `k + 1` factor puts `Score` in [0, 1]: first in both legs scores
1.0; first in one leg only scores 0.5 at the default weight. Smaller `k` sharpens the advantage of top ranks; larger `k`
flattens it. Worked example with defaults: a document ranked 1st by vector and 3rd by text scores
`(0.5/61 + 0.5/63) * 61 = 0.984`; one ranked 1st by vector and absent from the text leg scores `0.5/61 * 61 = 0.5`.

**`Linear`**: a weighted blend of normalized scores over the same union:
`Score = (1 - w) * vectorNorm + w * textNorm`. For `CosineSimilarity`, `vectorNorm` is the similarity clamped to
[0, 1]; for other metrics it is min-max normalized within the candidates. `textNorm` is `TextScore` divided by the best
`TextScore` among the candidates (0 for non-matches). Use it when the magnitude of a similarity matters, not just its
rank; it is more sensitive to score distributions than `Rrf`.

**`Filter`** (legacy): the text query becomes a required filter, and matches are ranked by
`(1 - w) * vectorScore + w * textScore` on raw scales. Only text matches come back. It exists for callers that relied on
the old behavior; it cannot be combined with `Collapse` or recency.

### 5.3 What a hybrid hit carries

| Field | Meaning |
|---|---|
| `Score` | The fused score |
| `VectorScore` | Raw similarity in the metric's units, computed for every candidate (also those the vector leg did not return) |
| `VectorRank` | 1-based rank in the vector leg; null when the document was not among its candidates |
| `TextScore`, `TextRank` | Raw text rank and 1-based rank; null when the document did not match the text query |
| `RecencyRank` | With recency on (section 5.4) |

`VectorRank` and `TextRank` explain every result: a hit with only a `VectorRank` was found by meaning alone, one with
only a `TextRank` by keywords alone.

### 5.4 Recency

`Hybrid.RecencyWeight` (`r`, 0.0 to 1.0, default 0 = off; `Rrf` only) adds a third ranked list: how recently each
candidate was written. Candidates are grouped by a recency key (the collapse group when `Collapse` is set, otherwise
the document), keys are ranked newest `CreatedUtc` first (ties broken by the key in byte order), and every candidate in
a key shares that `RecencyRank`:

```
raw   = (1 - w) / (k + VectorRank) + w / (k + TextRank) + r / (k + RecencyRank)
Score = raw * (k + 1) / ((1 - w) + w + r)
```

The divisor keeps `Score` in [0, 1]. With `r = 0` nothing changes, not even the generated SQL. Because the signal is a
rank, a small weight (0.05 to 0.1) mostly breaks near-ties in favor of newer content, which is what you want when newer
material supersedes older (policies, memories, changelogs); a weight of 1.0 counts recency as much as both legs
together. Recency ranks only the candidates the two legs retrieved; it never brings in a document that neither leg found.
`Linear` and `Filter` ignore it with a `Notice`.

Recency is based on `CreatedUtc`, which an update does not change; see [INGESTION.md](INGESTION.md#5-keeping-data-current)
for how to make a rewrite count as new.

---

## 6. Filters

Filters restrict which documents can match. Every filter present must hold (they are ANDed), and they apply before
ranking, to both legs of a hybrid search, and to enumeration and delete-by-filter as well as search.

### 6.1 Labels

```json
"LabelFilter": { "Required": ["published", "reviewed"], "RequiredMode": "All", "Excluded": ["draft"] }
```

- `Required` with `RequiredMode: "All"` (the default): the document must carry **every** listed label.
- `Required` with `RequiredMode: "Any"`: the document must carry **at least one** of them.
- `Excluded`: the document must carry **none** of the listed labels.
- Labels are exact, case-sensitive strings. A label repeated in `Required` counts once. With a single required label
  both modes are the same.
- The same rules apply to enumeration and delete-by-filter, so a delete with several required labels removes only the
  documents that carry all of them unless you ask for `Any`.

### 6.2 Tags

```json
"TagFilter": {
  "Required": [ { "Key": "lang", "Condition": "Equals", "Value": "en" },
                { "Key": "year", "Condition": "GreaterThan", "Value": "2023" } ],
  "Excluded": [ { "Key": "status", "Condition": "Equals", "Value": "retracted" } ]
}
```

Each `Required` condition must hold and each `Excluded` condition must not. Values are strings and every comparison is
a string comparison:

| Condition | A `Required` condition holds when the document... |
|---|---|
| `Equals` | has the key with exactly this value |
| `NotEquals` | does not have the key with this value (a document without the key passes) |
| `GreaterThan`, `LessThan` | has the key with a value that sorts after / before this one **as text**: `"9" > "50"`, `"100" < "50"`. Zero-pad numbers (`"0042"`) or use ISO-8601 dates (`"2025-03-01"`) so text order matches numeric or date order |
| `Contains`, `ContainsNot` | has the key with a value that contains / does not contain the substring (case-sensitive; a document without the key fails both) |
| `StartsWith`, `EndsWith` | has the key with a value that starts / ends with the string (case-sensitive) |
| `IsNull` | does not have the key, or has it with an empty value |
| `IsNotNull` | has the key with a non-empty value |

An `Excluded` condition removes the documents for which the same condition would hold.

### 6.3 Terms

```json
"Terms": { "Required": ["kubernetes"], "Excluded": ["deprecated"] }
```

Case-insensitive substring match on `content` (`ILIKE '%term%'`, which the trigram index can serve). Every `Required` term must
appear; no `Excluded` term may. `%` and `_` are matched literally.

### 6.4 Dates and document ids

- `CreatedAfter` / `CreatedBefore`: `CreatedUtc` strictly after / before the given instant (ISO-8601, UTC).
- `DocumentIds`: only chunks whose `DocumentId` is in the list. This is how to search within one or a few source
  documents.

### 6.5 Filters and the vector index

With the HNSW index in use, pgvector 0.5.1 applies filters to the index's candidates, not before it: the index yields
its `ef_search` nearest rows and the filters then drop the ones that do not match. When a filter keeps only a small
share of the collection, the nearest rows may contain fewer matches than the page asks for. Measured on 13,716 chunks,
top-10 vector searches filtered by a term:

| Chunks matching the filter | Default `ef_search` (100) | `EfSearch: 1000` | Collapse, pool 1000 |
|---|---|---|---|
| 2,003 to 2,175 | 10 | 10 | 10 |
| 990 | 9 | 10 | 10 |
| 271 | 8 | 10 | 10 |
| 147 | 10 | 10 | 10 |
| 95 | 0 | 9 | 10 |

So for selective filters, raise `Vector.EfSearch` (up to 1000), or add `Collapse` with a large `CandidatePool`. Very
selective filters (a few `DocumentIds`, a rare tag value) usually lead PostgreSQL to skip the vector index and compute
exact distances over the few matching rows, which returns complete results. Hybrid searches are less exposed, because
the text leg finds matching documents independently.

---

## 7. Grouping and context

### 7.1 Collapse

When long sources are stored as chunks, a plain search can return five chunks of the same document in the top ten.
`Collapse` returns one hit per group instead:

```json
"Collapse": { "Field": "DocumentId" }
"Collapse": { "Field": "Tag", "TagKey": "parentKey", "CandidatePool": 200 }
```

- `Field = DocumentId` groups by the `DocumentId` column; `Field = Tag` groups by the value of the tag named `TagKey`.
  A document with no value is its own group, keyed by its `DocumentKey`.
- The server retrieves a candidate pool, forms groups within it, and keeps each group's best-scoring chunk (ties by
  `Id`) as the representative. The hit carries the representative's own content, position, and scores, plus
  `GroupKey` and `GroupHits` (how many candidates the group had).
- `MaxResults`, `TotalRecords`, `RecordsRemaining`, and continuation tokens count groups.
- Thresholds apply to representatives. `IncludeNeighbors` and `IncludeEmbeddings` work on them.
- The pool is `Collapse.CandidatePool` for single-leg searches and `Hybrid.CandidatePool` (or else
  `Collapse.CandidatePool`) for hybrid; the default is `max(MaxResults * 4, 100)` capped at 1000. Groups can only be
  formed from chunks in the pool, so when chunks per group are many, raise the pool. When a leg filled the pool but the
  pool held fewer groups than the page needed, the response carries a `Notice`.
- Supported with vector, full-text, and hybrid `Rrf` and `Linear`; not with hybrid `Filter` (400).

Collapse sets the vector index's candidate limit to the pool (unless `Vector.EfSearch` is set), so a collapsed vector
search can consider up to 1000 candidates.

### 7.2 Neighbor retrieval

`IncludeNeighbors: N` (1 to 10) attaches, to each hit that has a `DocumentId`, the chunks at positions `Position - N`
to `Position + N` of the same `DocumentId`, excluding the hit itself, in position order:

- Neighbors are fetched after ranking and do not affect scores, filters, or paging. They are not filtered: a neighbor is
  returned even if it would not pass the search's label or tag filters.
- Windows of hits in the same document are fetched together, so overlapping windows cost one read.
- Hits without a `DocumentId` get no neighbors. Positions must be gap-free for windows to be complete.
- Neighbors are full document records, including their stored vectors, so large `N` with large vectors makes responses
  big.

Use it to hand a model a readable passage around each matched chunk instead of an isolated fragment.

### 7.3 Stored vectors

Search hits omit their stored embedding by default. `IncludeEmbeddings: true` adds it, for client-side work such as
MMR diversification, clustering, or similarity checks between results. Expect roughly 10 to 20 bytes of JSON per
dimension per hit.

---

## 8. Results, scores, and paging

### 8.1 The response

```json
{
  "Success": true, "MaxResults": 10, "TotalRecords": 148, "RecordsRemaining": 138,
  "EndOfResults": false, "ContinuationToken": "10", "Notice": null, "TotalMs": 17.4,
  "Documents": [ { "DocumentKey": "...", "Score": 0.98, "VectorRank": 1, "TextRank": 3, "Labels": [...], "Tags": {...}, ... } ]
}
```

Each hit is a full document record (without `Embeddings` unless asked) plus the search fields in section 5.3, and its
labels and tags.

### 8.2 What `TotalRecords` counts

| Search | `TotalRecords` |
|---|---|
| Vector-only, uncollapsed | Every document passing the filters, regardless of thresholds. Pages can only reach the `ef_search` nearest neighbors (at most 1000) |
| Full-text | Exact number of matches after thresholds |
| Hybrid `Rrf` / `Linear` | Size of the fused candidate set after thresholds, at most `2 * CandidatePool` |
| Hybrid `Filter` | Exact number of text matches after thresholds |
| Any collapsed search | Number of groups in the candidate pool after thresholds |

### 8.3 Sorting

`SortOrder` defaults to `ScoreDescending` (best first). Others: `ScoreAscending`, `DistanceAscending`,
`DistanceDescending`, `TextScoreAscending`, `TextScoreDescending` (non-matches last in hybrid), `CreatedAscending`,
`CreatedDescending`. Ties break on `Id`. A non-score sort reorders the same result set; it does not change which
documents match (in hybrid and collapsed searches it reorders the candidate set).

### 8.4 Paging

`MaxResults` is 1 to 1000 (default 10; larger values are clamped). The `ContinuationToken` in a response is the offset of
the next page; send it back unchanged. Paging re-runs the query each time, so writes between pages can shift results.
Hybrid and collapsed searches page within their candidate pool.

### 8.5 Notices

`Notice` explains how a search ran when that is not obvious: stop-word-only queries, a language served without the
index, `Hybrid` options ignored, recency ignored, a collapse pool too small. Notices are informational and their wording
may change; do not branch on them. The full list is in [REST_API.md](../REST_API.md#search-modes).

---

## 9. Performance

Measured on the development machine (Docker, default PostgreSQL settings) over SciFact split into 13,716 chunks with
384-dimension vectors; server-side `TotalMs`, median of five warm runs:

| Search | Median |
|---|---|
| Vector, `CosineSimilarity`, top 10 (default `ef_search` 100) | 16 ms |
| Vector, `CosineSimilarity`, top 10, `EfSearch: 1000` | 26 ms |
| Vector, `InnerProduct`, top 10 (no index) | 48 ms |
| Vector, `EuclideanDistance`, top 10 (no index) | 69 ms |
| Full-text, 4-term query, `Any` | 10 ms |
| Hybrid `Rrf`, default pool | 17 ms |
| Hybrid `Rrf` + collapse by tag + recency 0.1 | 17 ms |

Query plans for the hybrid and collapsed statements (captured with `auto_explain`) use only index access on the
documents table: an HNSW index scan for the vector leg, a GIN bitmap scan for the text leg, primary-key lookups for the
join, and the tags table's `document_key` index for collapse by tag. No sequential scans.

Where the time goes: in hybrid searches the text leg is usually the largest cost (31-82% of the statement across five
SciFact queries), because PostgreSQL ranks every document that matches any query term. Common questions match
thousands of chunks. The vector leg took 1 to 3 ms, and recency and collapse added no measurable cost.

Levers, in order of effect:

1. **Use a cosine metric** so vector search uses the index.
2. **Leave `Vector.EfSearch` at its default** unless recall or filtering needs more; 1000 cost about 10 ms more than
   the default here.
3. **Keep the candidate pool proportionate.** The default (`max(4 * MaxResults, 100)`) is fine for top-10 retrieval;
   pools in the thousands cost more in both legs.
4. **Narrow the text leg** with filters (labels, tags, dates) when the application knows the scope, and consider
   `MinimumShouldMatch: 2` for short queries (section 4.3).
5. **Batch writes** ([INGESTION.md](INGESTION.md#33-writing-many-documents)); single-row writes spend most of their time
   on round trips.

---

## 10. Limits and known issues

Verified against the current build (server v0.2.1, pgvector 0.5.1):

| Issue | Effect | Workaround |
|---|---|---|
| Vector search can page through at most the 1000 nearest neighbors | pgvector 0.5.1 caps `ef_search` at 1000 | Narrow with filters, or use hybrid or full-text for exhaustive listing; use enumeration to list everything |
| Selective filters on vector search can return short pages | Filters apply to the index's candidates (section 6.5) | Raise `Vector.EfSearch` toward 1000, or collapse with a large pool |
| Only cosine metrics use the vector index | Euclidean and inner-product searches scan every row that passes the filters | Use cosine (identical ranking to inner product for normalized vectors) |
| Vector-only thresholds are applied after paging | Short pages; `TotalRecords` ignores thresholds | Use collapse, or apply the threshold in the client |
| Tag comparisons are text comparisons | `GreaterThan`/`LessThan` on numbers compare as strings | Zero-pad numbers; use ISO-8601 dates |
| `MinimumShouldMatch` on long queries | Can be slower than plain `Any` | Use it for short queries only |
| Non-English full-text | No index for other languages | Rely on the vector leg, or keep content English |
| pgvector 0.5.1 | No iterative index scans; `ef_search` at most 1000; at most 2000 dimensions | A newer pgvector image lifts these; it is not the shipped default |

---

## 11. Recipes

**RAG over chunked documents: a few passages with context.**

```json
{
  "Vector": { "Embeddings": [...] },
  "FullText": { "Query": "how do I rotate the signing key" },
  "Collapse": { "Field": "DocumentId" },
  "IncludeNeighbors": 1,
  "MaxResults": 5
}
```

One passage per source, the best chunk of each with one chunk on either side, ranked by meaning and keywords together.

**Agent memory: newest version wins near-ties.** Memories stored as chunks tagged `parentKey`, scoped by a category
label:

```json
{
  "Vector": { "Embeddings": [...] },
  "FullText": { "Query": "what is the deploy freeze policy", "TextWeight": 0.5 },
  "Hybrid": { "Strategy": "Rrf", "RecencyWeight": 0.1 },
  "Collapse": { "Field": "Tag", "TagKey": "parentKey" },
  "LabelFilter": { "Required": ["cat_ops"] },
  "MaxResults": 10
}
```

**Exact identifier lookup.** `{ "FullText": { "Query": "ERR_CERT_DATE_INVALID", "MatchMode": "Phrase" } }`, or
`Terms.Required` when the identifier is not a clean word.

**Scoped similarity: "more like this" within one source.**
`{ "Vector": { "Embeddings": [...] }, "DocumentIds": ["handbook-12"], "MaxResults": 5 }`

**Recent material first, still relevant.** `SortOrder: "CreatedDescending"` with a hybrid query reorders the top
candidates by time; `Hybrid.RecencyWeight` instead blends time into relevance.

**Vector search with a narrow filter.** Give the index room to find enough matches:
`{ "Vector": { "Embeddings": [...], "EfSearch": 1000 }, "TagFilter": { "Required": [ { "Key": "team", "Condition": "Equals", "Value": "payments" } ] }, "MaxResults": 10 }`.

**Documents with all of several labels, or any of them.**
`"LabelFilter": { "Required": ["security", "published"] }` needs both; add `"RequiredMode": "Any"` for either.

---

## 12. Enumeration: unranked retrieval

`POST /v1.0/tenants/{tid}/collections/{cid}/documents/enumerate` lists documents without ranking: the same filters
(labels, tags, terms, dates, `DocumentIds`), ordered by `CreatedUtc` (`CreatedDescending` default, or
`CreatedAscending`), 1 to 1000 per page (default 100), with exact `TotalRecords` and offset continuation tokens. Use it
to export, audit, sync, or feed a filter-only view. `POST .../documents/delete/filter` deletes exactly what the same
filter would enumerate.

Other direct reads (by key, by `DocumentId` and position, stats) are listed in
[INGESTION.md](INGESTION.md#7-reading-back-what-was-written).

---

## 13. Searching through the SDKs and MCP

The SDKs send the `SearchQuery` as-is (`SearchAsync`, `search`) and expose the result fields; they also provide
constants for every enum string and a capability check (`SupportsAsync(Capabilities.Collapse)`, ...) so a client can
confirm a server honors collapse, recency, embeddings, or minimum-should-match before relying on them. See
[sdk/](../sdk/).

Agents search with the MCP `search/query` tool, which takes the same `SearchQuery` as a JSON string and returns the
same `SearchResult`. See [MCP_API.md](../MCP_API.md#search).

The dashboard's Search page builds the same requests interactively, including hybrid ranking, recency, collapse, minimum
terms to match, and embeddings, and shows each hit's ranks and group.
