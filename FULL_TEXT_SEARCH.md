# Full-Text Search with Relevance Scoring — Proposal

## 1. Executive Summary

RecallDB currently supports vector similarity search as its primary ranking mechanism and term-based content filtering (`Terms.Required` / `Terms.Excluded`) as a binary include/exclude filter using PostgreSQL `ILIKE`. This proposal introduces **scored full-text search** — a first-class search mode that ranks documents by textual relevance using PostgreSQL's native full-text search engine with `ts_rank` scoring (a TF-IDF-like relevance model).

The goal is to enable consumers to:

1. **Issue a standalone full-text search** — query by natural language text, receive documents ranked by textual relevance score.
2. **Combine full-text search with vector search in a single query** — produce a hybrid score that blends semantic similarity (vector) with lexical relevance (full-text), all within one API call.
3. **Apply exact substring filtering on any search mode** — the existing `Terms` filter (required/excluded) works as an orthogonal constraint across vector, full-text, and hybrid searches.

This enables consuming applications to emit either two separate searches (vector + full-text) and merge results client-side, or a single hybrid search that RecallDB merges server-side.

---

## 2. Problem Statement

### 2.1 Current Behavior

The existing `Terms` filter (`TermsFilter.Required` / `TermsFilter.Excluded`) operates as a **binary predicate**:

```sql
-- Terms.Required: every term must appear (AND logic)
content ILIKE '%term1%' AND content ILIKE '%term2%'

-- Terms.Excluded: no term may appear
content NOT ILIKE '%term3%'
```

This is backed by a GIN trigram index (`gin_trgm_ops`) on the `content` column. It answers the question *"does this document contain these substrings?"* but says nothing about **how relevant** the document is to those terms. There is no scoring, no term frequency weighting, no inverse document frequency, and no length normalization.

### 2.2 What Consumers Actually Need

When a consuming application searches for documents related to a user's question, it typically:

1. Generates an embedding for the query.
2. Sends a vector search request to RecallDB (cosine similarity).
3. Receives documents ranked by semantic similarity.

This misses documents that are **lexically relevant** but **semantically distant** — a common problem with embedding models. For example, searching for "OAuth2 PKCE flow configuration" may find documents about "authentication setup" (semantically similar) but miss a document titled "OAuth2 PKCE Configuration Reference" that uses the exact terminology.

Full-text search with relevance scoring solves this by finding documents that contain the actual query terms, ranked by how important those terms are to each document (term frequency, inverse document frequency, document length normalization).

### 2.3 Desired Consumer Patterns

**Pattern A — Two Separate Searches (Client-Side Fusion)**
```
Consumer → Vector Search → [doc1: 0.92, doc3: 0.87, doc5: 0.81]
Consumer → Full-Text Search → [doc2: 0.95, doc1: 0.78, doc4: 0.72]
Consumer → Merge & re-rank → [doc1, doc2, doc3, doc5, doc4]
```

**Pattern B — Single Hybrid Search (Server-Side Fusion)**
```
Consumer → Hybrid Search (vector + full-text) → [doc1: 0.88, doc2: 0.85, doc3: 0.79, ...]
```

Both patterns should be supported.

---

## 3. PostgreSQL Full-Text Search Capabilities

### 3.1 Native FTS Engine

PostgreSQL provides a built-in full-text search engine with the following components:

| Component | Purpose |
|-----------|---------|
| `to_tsvector(config, text)` | Converts document text into a searchable token vector (stems words, removes stop words) |
| `to_tsquery(config, text)` | Converts a search query into a query object (supports AND, OR, NOT, phrase, prefix) |
| `plainto_tsquery(config, text)` | Converts plain text into a query (AND-joined terms, no special syntax needed) |
| `websearch_to_tsquery(config, text)` | Converts web-search-style syntax (quotes, minus, OR) into a query |
| `@@` operator | Matches a `tsvector` against a `tsquery` (returns boolean) |
| `ts_rank(vector, query)` | Scores relevance based on term frequency, with configurable length normalization |
| `ts_rank_cd(vector, query)` | Scores relevance using cover density ranking (rewards term proximity) |

### 3.2 Scoring Model — `ts_rank`

PostgreSQL's `ts_rank` function implements a scoring model that considers:

- **Term Frequency (TF)**: How often the query terms appear in the document.
- **Inverse Document Frequency (IDF)**: Implicitly handled — rare terms that match carry more weight because they appear in fewer `tsvector` positions.
- **Length Normalization**: Configurable via a bitmask flag:
  - `0` (default): No normalization.
  - `1`: Divide by `1 + log(document_length)`.
  - `2`: Divide by document length.
  - `4`: Divide by mean harmonic distance between extents.
  - `8`: Divide by number of unique words.
  - `16`: Divide by `1 + log(unique_words)`.
  - `32`: Divide by itself + 1 (normalization to 0–1 range).

The recommended normalization for RAG workloads is **`1` (log normalization)** or **`32` (self-normalization to 0–1)** — these prevent long documents from dominating results while maintaining sensitivity to term frequency.

### 3.3 `ts_rank_cd` (Cover Density Ranking)

An alternative scorer that additionally rewards **term proximity** — documents where query terms appear close together score higher. This is particularly useful for multi-term queries in a RAG context where you want documents that discuss the query concepts together, not scattered throughout.

### 3.4 Existing Infrastructure

RecallDB already enables the `pg_trgm` extension and creates GIN trigram indexes on `content`. For native FTS, we need an additional **GIN index on `to_tsvector('english', content)`**. The `pg_trgm` index remains useful for the existing `Terms` filter (ILIKE-based substring matching), so both indexes will coexist.

### 3.5 Text Search Configurations

PostgreSQL ships with text search configurations for many languages. The `english` configuration handles stemming (e.g., "running" → "run"), stop word removal (e.g., "the", "is"), and normalization. For multi-language support, we could allow the configuration to be specified per-collection or per-query. The initial implementation will default to `english` with an option to specify an alternative.

---

## 4. Design

### 4.1 Overview of Changes

| Area | Change |
|------|--------|
| **Database Schema** | Add a GIN `tsvector` index on each collection table's `content` column |
| **Models** | Add `FullTextQuery` model; add `FullTextQuery` property to `SearchQuery`; add `TextScore` property to `DocumentRecord` |
| **Enums** | Add `TextSearchTypeEnum` for ranking function selection; extend `SortOrderEnum` with `TextScoreDescending` / `TextScoreAscending` |
| **Search Implementation** | Extend `SearchMethods.SearchAsync` to build FTS SQL clauses when `FullTextQuery` is present |
| **API Endpoint** | No new endpoint needed — the existing search endpoint is extended |
| **SDKs** | Update C#, Python, and JavaScript SDKs with new model properties |

### 4.2 New Model — `FullTextQuery`

```csharp
// File: src/RecallDb.Core/Models/FullTextQuery.cs

public class FullTextQuery
{
    /// <summary>
    /// The search text to match against document content.
    /// Processed by PostgreSQL's text search parser (stemming, stop word removal).
    /// </summary>
    public string Query { get; set; }

    /// <summary>
    /// Text search ranking function to use.
    /// Default: TsRank.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TextSearchTypeEnum SearchType { get; set; } = TextSearchTypeEnum.TsRank;

    /// <summary>
    /// PostgreSQL text search configuration to use (e.g., "english", "simple", "spanish").
    /// Default: "english".
    /// </summary>
    public string Language { get; set; } = "english";

    /// <summary>
    /// Normalization option for ts_rank scoring (PostgreSQL normalization bitmask).
    /// 0 = none, 1 = log(length), 2 = length, 32 = self+1 (0-1 range).
    /// Default: 32 (normalized to 0-1 range).
    /// </summary>
    public int Normalization { get; set; } = 32;

    /// <summary>
    /// Minimum text relevance score threshold for results.
    /// Documents scoring below this value are excluded.
    /// </summary>
    public double? MinimumScore { get; set; }

    /// <summary>
    /// Weight to apply to the text score when combining with vector score
    /// in hybrid search mode. Must be between 0.0 and 1.0.
    /// The vector weight is computed as (1.0 - TextWeight).
    /// Default: 0.5 (equal weighting).
    /// Only used when both Vector and FullText queries are present.
    /// </summary>
    public double TextWeight { get; set; } = 0.5;
}
```

### 4.3 New Enum — `TextSearchTypeEnum`

```csharp
// File: src/RecallDb.Core/Enums/TextSearchTypeEnum.cs

public enum TextSearchTypeEnum
{
    /// <summary>
    /// Standard ts_rank scoring (term frequency with length normalization).
    /// </summary>
    [EnumMember(Value = "TsRank")]
    TsRank,

    /// <summary>
    /// Cover density ranking (ts_rank_cd) — rewards term proximity.
    /// </summary>
    [EnumMember(Value = "TsRankCd")]
    TsRankCd
}
```

### 4.4 Model Changes — `SearchQuery`

Add a new optional property to `SearchQuery`:

```csharp
// Add to SearchQuery.cs

/// <summary>
/// Full-text search query parameters for content relevance scoring.
/// When provided without Vector, performs a standalone full-text search.
/// When provided with Vector, performs a hybrid search combining both scores.
/// </summary>
public FullTextQuery FullText { get; set; }
```

### 4.5 Model Changes — `DocumentRecord`

Add a new transient property to `DocumentRecord`:

```csharp
// Add to DocumentRecord.cs

/// <summary>
/// Full-text relevance score (transient, populated during search).
/// Only present when a FullText query is used.
/// </summary>
public double? TextScore { get; set; }
```

### 4.6 Enum Changes — `SortOrderEnum`

Add new sort options:

```csharp
// Add to SortOrderEnum.cs

/// <summary>
/// Sort by text relevance score ascending.
/// </summary>
[EnumMember(Value = "TextScoreAscending")]
TextScoreAscending,

/// <summary>
/// Sort by text relevance score descending.
/// </summary>
[EnumMember(Value = "TextScoreDescending")]
TextScoreDescending
```

### 4.7 Database Schema Changes

#### 4.7.1 New Index — `tsvector` GIN Index

Add a new index to each per-collection table. This is a **functional index** that computes the `tsvector` at index time, so no schema change to the table itself is needed.

Add to `DynamicTableQueries.GetCreateCollectionIndexes()`:

```sql
CREATE INDEX IF NOT EXISTS idx_col_{ixId}_fts
    ON collection_{tableName}
    USING gin (to_tsvector('english', COALESCE(content, '')));
```

Add the same index to `recalldb_factory.sql` for the default collection:

```sql
CREATE INDEX IF NOT EXISTS idx_col_default_fts
    ON collection_default
    USING gin (to_tsvector('english', COALESCE(content, '')));
```

**Note on `COALESCE`**: The `content` column is nullable (binary documents may have `content = NULL`). Using `COALESCE(content, '')` ensures the index handles NULL content gracefully by treating it as empty text.

#### 4.7.2 Language-Specific Indexes

The initial implementation uses the `english` text search configuration in the index. If a consumer specifies a different `Language` in their query, PostgreSQL will fall back to a sequential scan for that query (the GIN index only accelerates queries matching the indexed configuration). This is acceptable for the initial release — a future enhancement could allow configuring the text search configuration per-collection at creation time, which would then be used in both the index and queries.

#### 4.7.3 Migration Strategy

For **existing** collections that were created before this feature:

1. On server startup, during the existing table verification/creation flow, add the new index if it does not exist (`CREATE INDEX IF NOT EXISTS`).
2. PostgreSQL builds GIN indexes in the background with `CONCURRENTLY` option (recommended for production).
3. No data migration is needed — the index is computed from existing `content` column values.

### 4.8 Search Implementation Changes

#### 4.8.1 Three Search Modes

The search endpoint will support three modes based on which query objects are present:

| `Vector` | `FullText` | Mode | Score Semantics |
|----------|------------|------|-----------------|
| Present | Absent | **Vector-only** (current behavior) | `Score` = vector similarity |
| Absent | Present | **Full-text-only** (new) | `Score` = text relevance, `TextScore` = text relevance |
| Present | Present | **Hybrid** (new) | `Score` = weighted blend, `TextScore` = text relevance |

#### 4.8.2 SQL Generation — Full-Text-Only Mode

When only `FullText` is provided (no `Vector`):

```sql
SELECT
    id, document_key, document_id, content_length, etag, sha256, position,
    content_type, content, binary_data, embeddings::text as embeddings, created_utc,
    ts_rank(
        to_tsvector('english', COALESCE(content, '')),
        plainto_tsquery('english', 'user search terms'),
        32
    ) AS score,
    ts_rank(
        to_tsvector('english', COALESCE(content, '')),
        plainto_tsquery('english', 'user search terms'),
        32
    ) AS text_score,
    0.0 AS distance
FROM collection_default
WHERE to_tsvector('english', COALESCE(content, '')) @@ plainto_tsquery('english', 'user search terms')
    -- additional filters (labels, tags, dates, Terms, etc.) applied here as AND conditions
ORDER BY score DESC
LIMIT 10 OFFSET 0;
```

Key details:
- The `@@` operator in the WHERE clause ensures only matching documents are returned (uses the GIN index).
- `ts_rank` in the SELECT clause scores each matching document.
- `distance` is set to `0.0` as a placeholder (not meaningful for text search).
- `text_score` is always populated when `FullText` is provided.
- All existing filters (`Terms`, `LabelFilter`, `TagFilter`, `DocumentIds`, `CreatedBefore/After`) continue to work as AND conditions.

#### 4.8.3 SQL Generation — Hybrid Mode

When both `Vector` and `FullText` are provided:

```sql
SELECT
    id, document_key, document_id, content_length, etag, sha256, position,
    content_type, content, binary_data, embeddings::text as embeddings, created_utc,
    (embeddings <=> '[0.1,0.2,...]'::vector) AS distance,
    (1.0 - (embeddings <=> '[0.1,0.2,...]'::vector)) AS vector_score,
    ts_rank(
        to_tsvector('english', COALESCE(content, '')),
        plainto_tsquery('english', 'user search terms'),
        32
    ) AS text_score,
    (
        (1.0 - 0.5) * (1.0 - (embeddings <=> '[0.1,0.2,...]'::vector))
        + 0.5 * ts_rank(
            to_tsvector('english', COALESCE(content, '')),
            plainto_tsquery('english', 'user search terms'),
            32
        )
    ) AS score
FROM collection_default
WHERE to_tsvector('english', COALESCE(content, '')) @@ plainto_tsquery('english', 'user search terms')
    -- additional filters applied here
ORDER BY score DESC
LIMIT 10 OFFSET 0;
```

Key details:
- The hybrid `score` is computed as: `(1 - TextWeight) * vector_score + TextWeight * text_score`.
- `TextWeight` defaults to `0.5` (equal blend). Consumers can tune this — e.g., `0.3` for 70% vector / 30% text.
- The `WHERE` clause includes the `@@` match predicate, which means only documents that match the text query are returned. This is intentional — in hybrid mode, the full-text query acts as both a **filter** and a **scorer**. Documents that don't match the text query at all are excluded.
- `text_score` is populated on each `DocumentRecord` for transparency.

**Alternative: Hybrid without text filtering (union behavior)**

Some consumers may want hybrid search where documents can match *either* vector similarity *or* text relevance (not requiring both). This can be achieved with a future enhancement using UNION queries or LEFT JOIN patterns. For the initial implementation, hybrid mode requires the text query to match (AND semantics), which is the more common and performant pattern.

#### 4.8.4 Full-Text Query Construction

The `FullTextQuery.Query` string is processed by PostgreSQL's `plainto_tsquery()`, which:

- Splits text on whitespace/punctuation.
- Applies stemming (e.g., "running" → "run").
- Removes stop words (e.g., "the", "is", "and").
- Joins remaining terms with AND logic.

For example:
- `"OAuth2 PKCE configuration"` → `'oauth2' & 'pkce' & 'configur'`
- `"how to set up authentication"` → `'set' & 'authent'` (stop words "how", "to", "up" are removed)

This is the safest choice for API consumers because it requires no special syntax knowledge. The query string is treated as plain natural language.

**Input sanitization**: The query text must be sanitized via `_Driver.Sanitize()` before being embedded in SQL, consistent with the existing pattern for Terms filtering.

#### 4.8.5 Implementation in `SearchMethods.cs`

The changes to `SearchMethods.SearchAsync()` follow the existing pattern of building SQL dynamically:

```csharp
// After existing vector setup code (lines 70-73)

// Full-text search setup
string ftsScoreExpression = null;
string ftsMatchCondition = null;
bool hasFullText = query.FullText != null && !string.IsNullOrWhiteSpace(query.FullText.Query);

if (hasFullText)
{
    string sanitizedQuery = _Driver.Sanitize(query.FullText.Query);
    string language = _Driver.Sanitize(query.FullText.Language ?? "english");
    string normalization = query.FullText.Normalization.ToString(CultureInfo.InvariantCulture);
    string rankFunction = query.FullText.SearchType == TextSearchTypeEnum.TsRankCd
        ? "ts_rank_cd" : "ts_rank";

    string tsvector = "to_tsvector('" + language + "', COALESCE(content, ''))";
    string tsquery = "plainto_tsquery('" + language + "', '" + sanitizedQuery + "')";

    ftsScoreExpression = rankFunction + "(" + tsvector + ", " + tsquery + ", " + normalization + ")";
    ftsMatchCondition = tsvector + " @@ " + tsquery;
}

// Modify SELECT clause based on mode
bool hasVector = query.Vector != null && query.Vector.Embeddings != null
    && query.Vector.Embeddings.Count > 0;

if (hasVector && hasFullText)
{
    // Hybrid mode
    double textWeight = Math.Clamp(query.FullText.TextWeight, 0.0, 1.0);
    double vectorWeight = 1.0 - textWeight;
    string vectorScoreExpr = GetScoreExpression(query.Vector, vectorOperator, vectorLiteral);

    sb.Append("SELECT " + _SelectColumns + ", ");
    sb.Append(distanceExpression + " AS distance, ");
    sb.Append(ftsScoreExpression + " AS text_score, ");
    sb.Append("(" + vectorWeight.ToString(CultureInfo.InvariantCulture) + " * " + vectorScoreExpr
        + " + " + textWeight.ToString(CultureInfo.InvariantCulture) + " * " + ftsScoreExpression
        + ") AS score ");
}
else if (hasFullText)
{
    // Full-text-only mode
    sb.Append("SELECT " + _SelectColumns + ", ");
    sb.Append("0.0 AS distance, ");
    sb.Append(ftsScoreExpression + " AS text_score, ");
    sb.Append(ftsScoreExpression + " AS score ");
}
else
{
    // Vector-only mode (existing behavior, unchanged)
    sb.Append("SELECT " + _SelectColumns + ", ");
    sb.Append(distanceExpression + " AS distance, ");
    sb.Append(scoreExpression + " AS score ");
}

sb.Append("FROM " + tableName);

// Add FTS match to conditions
if (hasFullText)
{
    conditions.Add(ftsMatchCondition);
}

// ... rest of existing WHERE clause building, ORDER BY, LIMIT/OFFSET ...
```

#### 4.8.6 Reading `text_score` from Results

Update `DocumentRecord.FromDataRow()` to read the `text_score` column when present:

```csharp
if (row.Table.Columns.Contains("text_score"))
{
    doc.TextScore = DataTableHelper.GetDoubleValue(row, "text_score");
}
```

#### 4.8.7 Post-Query Filtering for Text Score

After results are retrieved, apply `FullTextQuery.MinimumScore` threshold:

```csharp
if (hasFullText && query.FullText.MinimumScore.HasValue)
{
    documents = documents
        .Where(d => d.TextScore.HasValue && d.TextScore.Value >= query.FullText.MinimumScore.Value)
        .ToList();
}
```

### 4.9 API Endpoint

No new endpoint is required. The existing search endpoint is extended:

```
POST /v1.0/tenants/{tid}/collections/{cid}/search
```

The request body is the existing `SearchQuery`, which now accepts an optional `FullText` property alongside the existing `Vector` property.

Update the OpenAPI metadata:
- Summary: `"Search"` (from `"Vector search"`)
- Description: Updated to reflect full-text and hybrid search capabilities.

### 4.10 Backward Compatibility

This design is **fully backward compatible**:

| Scenario | Behavior |
|----------|----------|
| Consumer sends `Vector` only (no `FullText`) | Identical to current behavior. No SQL changes. No `text_score` column. |
| Consumer sends `Terms` filter | Continues to work as ILIKE-based binary filtering (unchanged). |
| Consumer sends `FullText` only | New full-text search mode with relevance scoring. |
| Consumer sends both `Vector` and `FullText` | New hybrid mode with blended scoring. |
| Consumer sends `FullText` + `Terms` | Full-text scoring + ILIKE filtering. Both apply. |
| Consumer sends neither `Vector` nor `FullText` | Returns all documents (sorted by `SortOrder`), filters only. Same as current behavior when no vector is provided (uses zero vector, effectively random distance scores). |

The `TextScore` property on `DocumentRecord` is nullable and will serialize as `null` (or be absent from JSON, depending on serializer settings) when not populated, so existing consumers parsing the response will not break.

---

## 5. API Usage Examples

### 5.1 Full-Text Search Only

```json
POST /v1.0/tenants/default/collections/default/search

{
    "FullText": {
        "Query": "OAuth2 PKCE flow configuration",
        "SearchType": "TsRank",
        "Language": "english",
        "Normalization": 32,
        "MinimumScore": 0.01
    },
    "MaxResults": 10,
    "SortOrder": "ScoreDescending"
}
```

Response:
```json
{
    "Success": true,
    "Documents": [
        {
            "DocumentKey": "doc-abc-001",
            "DocumentId": "oauth2-reference",
            "Content": "OAuth2 PKCE Configuration Reference...",
            "Score": 0.62,
            "TextScore": 0.62,
            "Labels": ["documentation"],
            "Tags": { "category": "auth" }
        },
        {
            "DocumentKey": "doc-abc-002",
            "DocumentId": "auth-setup-guide",
            "Content": "Setting up OAuth2 with PKCE flow...",
            "Score": 0.45,
            "TextScore": 0.45,
            "Labels": ["guide"],
            "Tags": { "category": "auth" }
        }
    ],
    "TotalRecords": 2,
    "MaxResults": 10,
    "EndOfResults": true,
    "TotalMs": 12.34
}
```

### 5.2 Hybrid Search (Vector + Full-Text)

```json
{
    "Vector": {
        "SearchType": "CosineSimilarity",
        "Embeddings": [0.0123, -0.0456, ...]
    },
    "FullText": {
        "Query": "OAuth2 PKCE flow",
        "TextWeight": 0.3
    },
    "MaxResults": 10
}
```

This produces a blended score: `0.7 * cosine_similarity + 0.3 * ts_rank`.

### 5.3 Full-Text Search with Existing Filters

```json
{
    "FullText": {
        "Query": "database migration",
        "SearchType": "TsRankCd"
    },
    "LabelFilter": {
        "Required": ["documentation"]
    },
    "CreatedAfter": "2025-01-01T00:00:00Z",
    "Terms": {
        "Excluded": ["deprecated"]
    },
    "MaxResults": 20
}
```

This combines: text relevance ranking + label filtering + date filtering + term exclusion.

### 5.4 Client-Side Fusion Pattern

A consuming application can issue two separate requests and merge:

**Request 1 — Vector Search:**
```json
{
    "Vector": {
        "SearchType": "CosineSimilarity",
        "Embeddings": [0.0123, -0.0456, ...]
    },
    "MaxResults": 20
}
```

**Request 2 — Full-Text Search:**
```json
{
    "FullText": {
        "Query": "OAuth2 PKCE flow configuration"
    },
    "MaxResults": 20
}
```

**Client-side merge:** Deduplicate by `DocumentKey`, apply Reciprocal Rank Fusion (RRF) or weighted score blending, take top-K.

---

## 6. Implementation Plan

### Phase 1 — Core: Enums and Models

- [ ] **1.1** Create `src/RecallDb.Core/Enums/TextSearchTypeEnum.cs` — new enum with `TsRank`, `TsRankCd` values and `[EnumMember]` attributes
- [ ] **1.2** Create `src/RecallDb.Core/Models/FullTextQuery.cs` — new model class with properties: `Query`, `SearchType`, `Language`, `Normalization`, `MinimumScore`, `TextWeight`
- [ ] **1.3** Update `src/RecallDb.Core/Models/SearchQuery.cs` — add optional `FullText` property (type `FullTextQuery`, default `null`) with backing field and XML doc
- [ ] **1.4** Update `src/RecallDb.Core/Models/DocumentRecord.cs` — add nullable `TextScore` property (type `double?`, default `null`) with backing field and XML doc
- [ ] **1.5** Update `src/RecallDb.Core/Enums/SortOrderEnum.cs` — add `TextScoreAscending` and `TextScoreDescending` values with `[EnumMember]` attributes

### Phase 2 — Core: Database Schema

- [ ] **2.1** Update `src/RecallDb.Core/Database/Postgresql/Queries/DynamicTableQueries.cs` — add `tsvector` GIN index to `GetCreateCollectionIndexes()` array: `CREATE INDEX IF NOT EXISTS idx_col_{ixId}_fts ON collection_{tableName} USING gin (to_tsvector('english', COALESCE(content, '')));`
- [ ] **2.2** Update `docker/factory/recalldb_factory.sql` — add `tsvector` GIN index for the default collection: `CREATE INDEX IF NOT EXISTS idx_col_default_fts ON collection_default USING gin (to_tsvector('english', COALESCE(content, '')));`

### Phase 3 — Core: Search Engine

- [ ] **3.1** Update `src/RecallDb.Core/Database/Postgresql/Implementations/SearchMethods.cs` — add full-text search setup logic: detect `hasFullText`, build `ftsScoreExpression` and `ftsMatchCondition` strings using `ts_rank`/`ts_rank_cd`, `to_tsvector`, `plainto_tsquery`
- [ ] **3.2** Update `SearchMethods.cs` — implement three-mode SELECT clause generation: vector-only (unchanged), full-text-only (new), hybrid (new with weighted blend)
- [ ] **3.3** Update `SearchMethods.cs` — add `ftsMatchCondition` to WHERE clause conditions list when full-text is active
- [ ] **3.4** Update `SearchMethods.cs` — add `GetOrderByClause` cases for `TextScoreAscending` and `TextScoreDescending` sort orders
- [ ] **3.5** Update `DocumentRecord.FromDataRow()` — read `text_score` column when present in the DataTable
- [ ] **3.6** Update `SearchMethods.cs` — add post-query filtering for `FullTextQuery.MinimumScore` threshold on `TextScore`

### Phase 4 — Core: Server

- [ ] **4.1** Update `src/RecallDb.Server/RecallDbServer.cs` — update OpenAPI metadata for the search route: change summary from `"Vector search"` to `"Search"`, update description to reflect vector, full-text, and hybrid search capabilities
- [ ] **4.2** Verify the search route handler (`SearchRoute`) requires no changes (it passes `SearchQuery` through — the new `FullText` property deserializes automatically)

### Phase 5 — Dashboard

- [ ] **5.1** Update `dashboard/src/views/SearchQuery.jsx` — add `SORT_ORDERS` entries for `TextScoreDescending` and `TextScoreAscending`
- [ ] **5.2** Update `SearchTab` component — add new state variables for full-text search: `fullTextQuery` (string), `fullTextSearchType` (dropdown: TsRank/TsRankCd), `fullTextLanguage` (text input, default "english"), `fullTextNormalization` (number input, default 32), `fullTextMinScore` (number input), `fullTextWeight` (number input, default 0.5)
- [ ] **5.3** Update `SearchTab` component — add a "Full-Text Search" form section (between the Vector Search section and Labels section) with inputs for the fields above; use a `CollapsibleSection` or inline layout consistent with the existing UI patterns
- [ ] **5.4** Update `buildQuery()` function — when `fullTextQuery` is non-empty, add a `FullText` object to the query with `Query`, `SearchType`, `Language`, `Normalization`, `MinimumScore`, and `TextWeight` fields
- [ ] **5.5** Update `SearchTab` component — remove the hard requirement that embeddings must be provided; allow search to proceed when either embeddings or full-text query (or both) are present
- [ ] **5.6** Update `resultColumns` in `SearchTab` — add a `TextScore` column that displays `d.TextScore` as a badge when present (similar to the existing `Score` column)
- [ ] **5.7** Verify the `QueryTab` component (enumeration) requires no changes (it does not use the search endpoint)

### Phase 6 — C# SDK

- [ ] **6.1** Create `sdk/csharp/RecallDb.Sdk/Models/FullTextQuery.cs` — new model class mirroring server-side `FullTextQuery` with properties: `Query` (string), `SearchType` (string, default "TsRank"), `Language` (string, default "english"), `Normalization` (int, default 32), `MinimumScore` (double?), `TextWeight` (double, default 0.5)
- [ ] **6.2** Update `sdk/csharp/RecallDb.Sdk/Models/SearchQuery.cs` — add `FullText` property (type `FullTextQuery`)
- [ ] **6.3** Update `sdk/csharp/RecallDb.Sdk/Models/DocumentRecord.cs` — add `TextScore` property (type `double?`)

### Phase 7 — Python SDK

- [ ] **7.1** Update `sdk/python/recalldb_sdk.py` — add docstring documentation for the `FullText` parameter in the `search()` method, describing the `FullTextQuery` dict structure: `Query` (str), `SearchType` (str), `Language` (str), `Normalization` (int), `MinimumScore` (float), `TextWeight` (float)
- [ ] **7.2** Update `sdk/python/recalldb_sdk.py` — update the `search()` method docstring to describe the `TextScore` field in the response `Documents` array

### Phase 8 — JavaScript SDK

- [ ] **8.1** Update `sdk/js/recalldb-sdk.js` — add JSDoc documentation for the `FullText` parameter in the `search()` method, describing the `FullTextQuery` object structure: `Query` (string), `SearchType` (string), `Language` (string), `Normalization` (number), `MinimumScore` (number), `TextWeight` (number)
- [ ] **8.2** Update `sdk/js/recalldb-sdk.js` — update the `search()` method JSDoc to describe the `TextScore` field in the response `Documents` array

### Phase 9 — C# SDK Test Harness

- [ ] **9.1** Update `sdk/csharp/RecallDb.Sdk.TestHarness/Program.cs` — add test: **full-text search only** — create test documents with known content, perform a full-text search with `FullText.Query`, assert results are returned, assert `Score > 0`, assert `TextScore > 0`, assert `TextScore == Score` (full-text-only mode)
- [ ] **9.2** Add test: **full-text search with TsRankCd** — same as 9.1 but with `SearchType = "TsRankCd"`, verify results returned and scored
- [ ] **9.3** Add test: **hybrid search** — perform search with both `Vector` (embeddings) and `FullText` (query text), assert results are returned, assert `Score` reflects weighted blend, assert `TextScore` is populated separately
- [ ] **9.4** Add test: **full-text search with minimum score threshold** — set `FullText.MinimumScore` to a value that excludes some results, verify all returned documents have `TextScore >= threshold`
- [ ] **9.5** Add test: **full-text search with Terms filter** — combine `FullText` query with `Terms.Required` and `Terms.Excluded`, verify both text relevance scoring and exact substring filtering apply
- [ ] **9.6** Add test: **full-text search with label filter** — combine `FullText` with `LabelFilter.Required`, verify label constraint is respected
- [ ] **9.7** Add test: **full-text search no match** — search for terms not present in any document, verify `TotalRecords == 0` and empty `Documents` list
- [ ] **9.8** Add test: **full-text sort orders** — verify `TextScoreDescending` and `TextScoreAscending` sort orders produce correctly ordered results
- [ ] **9.9** Add test: **backward compatibility** — verify existing vector-only search produces identical results (no `TextScore` populated, same `Score` values)

### Phase 10 — Python SDK Test Harness

- [ ] **10.1** Update `sdk/python/test_harness.py` — add test: **full-text search only** — search with `{"FullText": {"Query": "..."}}`, assert results returned with `Score > 0` and `TextScore > 0`
- [ ] **10.2** Add test: **hybrid search** — search with both `Vector` and `FullText`, assert `TextScore` is populated and `Score` reflects blend
- [ ] **10.3** Add test: **full-text search with filters** — combine `FullText` with `LabelFilter` and `Terms`, verify all constraints apply
- [ ] **10.4** Add test: **full-text search no match** — search for absent terms, verify empty results
- [ ] **10.5** Add test: **backward compatibility** — verify vector-only search still works identically

### Phase 11 — JavaScript SDK Test Harness

- [ ] **11.1** Update `sdk/js/test-harness.js` — add test: **full-text search only** — search with `{FullText: {Query: "..."}}`, assert results returned with `Score > 0` and `TextScore > 0`
- [ ] **11.2** Add test: **hybrid search** — search with both `Vector` and `FullText`, assert `TextScore` populated and `Score` reflects blend
- [ ] **11.3** Add test: **full-text search with filters** — combine `FullText` with `LabelFilter` and `Terms`, verify all constraints apply
- [ ] **11.4** Add test: **full-text search no match** — search for absent terms, verify empty results
- [ ] **11.5** Add test: **backward compatibility** — verify vector-only search still works identically

### Phase 12 — Test.Automated (Server Integration Tests)

- [ ] **12.1** Update `src/Test.Automated/Program.cs` — add search test data setup: ensure test documents have varied, realistic content suitable for full-text ranking (distinct term frequencies, multi-word content with overlapping and unique terms)
- [ ] **12.2** Add test: **"Search full-text: basic query"** — full-text search with a query matching known content, assert `Success == true`, `TotalRecords > 0`, documents returned with `Score > 0` and `TextScore > 0`
- [ ] **12.3** Add test: **"Search full-text: TsRank scoring"** — verify `SearchType = "TsRank"` returns scored results
- [ ] **12.4** Add test: **"Search full-text: TsRankCd scoring"** — verify `SearchType = "TsRankCd"` returns scored results
- [ ] **12.5** Add test: **"Search full-text: no match"** — search for terms absent from all documents, verify `TotalRecords == 0`
- [ ] **12.6** Add test: **"Search full-text: minimum score threshold"** — set `MinimumScore`, verify all returned docs have `TextScore >= threshold`
- [ ] **12.7** Add test: **"Search full-text: sort text score descending"** — verify `SortOrder = "TextScoreDescending"` produces descending `TextScore` order
- [ ] **12.8** Add test: **"Search full-text: sort text score ascending"** — verify `SortOrder = "TextScoreAscending"` produces ascending `TextScore` order
- [ ] **12.9** Add test: **"Search hybrid: vector + full-text"** — send both `Vector` and `FullText`, verify `Score` is a blended value and `TextScore` is populated
- [ ] **12.10** Add test: **"Search hybrid: custom text weight"** — set `TextWeight = 0.3`, verify score reflects 70/30 vector/text blend
- [ ] **12.11** Add test: **"Search hybrid: with label filter"** — combine hybrid search with `LabelFilter`, verify label constraint applies
- [ ] **12.12** Add test: **"Search hybrid: with tag filter"** — combine hybrid search with `TagFilter`, verify tag constraint applies
- [ ] **12.13** Add test: **"Search hybrid: with terms filter"** — combine hybrid search with `Terms.Required` and `Terms.Excluded`, verify exact substring constraints apply alongside full-text scoring
- [ ] **12.14** Add test: **"Search hybrid: with date range"** — combine hybrid search with `CreatedAfter`/`CreatedBefore`, verify temporal constraints apply
- [ ] **12.15** Add test: **"Search full-text: pagination"** — verify `ContinuationToken` works correctly with full-text search results
- [ ] **12.16** Add test: **"Search full-text: max results"** — verify `MaxResults` is respected
- [ ] **12.17** Add test: **"Search backward compat: vector-only unchanged"** — existing vector-only search test, verify `TextScore` is null/absent and `Score` matches prior behavior

### Phase 13 — REST_API.md

- [ ] **13.1** Update `REST_API.md` search endpoint description — change `"Perform vector similarity search"` to reflect vector, full-text, and hybrid search capabilities
- [ ] **13.2** Update request example — add `FullText` object to the example request JSON showing `Query`, `SearchType`, `Language`, `Normalization`, `MinimumScore`, `TextWeight`
- [ ] **13.3** Update response example — add `TextScore` field to the example `DocumentRecord` in the response JSON
- [ ] **13.4** Add `FullTextQuery Fields` reference table — document all fields: `Query` (string, required), `SearchType` (string, default `TsRank`), `Language` (string, default `english`), `Normalization` (int, default 32), `MinimumScore` (double, null), `TextWeight` (double, default 0.5)
- [ ] **13.5** Update `SearchQuery Fields` reference table — add row for `FullText` (type `FullTextQuery`, default `null`, description: full-text search parameters)
- [ ] **13.6** Update `SortOrderEnum` reference table — add `TextScoreAscending` and `TextScoreDescending` rows
- [ ] **13.7** Add `TextSearchTypeEnum` reference table — document `TsRank` (term frequency with normalization) and `TsRankCd` (cover density ranking, rewards term proximity)
- [ ] **13.8** Add a "Search Modes" section explaining the three modes: vector-only (when only `Vector` is provided), full-text-only (when only `FullText` is provided), and hybrid (when both are provided), with the score semantics for each mode

### Phase 14 — README.md

- [ ] **14.1** Update the "Why RecallDB" table — add a row for **Full-text search** describing TF-IDF-like relevance scoring with `ts_rank` for lexical retrieval
- [ ] **14.2** Update the "What You Get" bullet list — update the "5 distance metrics" bullet or add a new bullet highlighting three search modes: vector similarity, full-text relevance, and hybrid (blended scoring)
- [ ] **14.3** Update the "Search" section — add a **Full-Text** entry describing scored full-text search with `ts_rank`/`ts_rank_cd` ranking, stemming, stop word removal, and language configuration; clarify that this is a ranked relevance search distinct from the existing `Terms` substring filter
- [ ] **14.4** Update the "Search" section — add a **Hybrid** entry describing combined vector + full-text search with configurable blending weights
- [ ] **14.5** Update the "Search" curl example — add a second example showing a full-text search request (without embeddings) to demonstrate the standalone full-text capability
- [ ] **14.6** Update SDK code examples — add a brief full-text search example for at least one SDK (C#, Python, or JavaScript) showing the `FullText` query object

### Phase 15 — Postman Collection

- [ ] **15.1** Update `RecallDB.postman_collection.json` — add a new request: **"Full-Text Search"** in the Search folder, with request body containing `FullText.Query`, `FullText.SearchType`, `MaxResults`, and `SortOrder`
- [ ] **15.2** Add a new request: **"Hybrid Search (Vector + Full-Text)"** in the Search folder, with request body containing both `Vector` (with embeddings) and `FullText` (with query text and `TextWeight`)
- [ ] **15.3** Add a new request: **"Full-Text Search with Filters"** in the Search folder, combining `FullText` with `LabelFilter`, `Terms`, and `CreatedAfter` to demonstrate filter composition
- [ ] **15.4** Update existing "Vector Search" request description — note that this is the vector-only search mode; full-text and hybrid modes are available via the `FullText` parameter

---

## 7. Performance Considerations

### 7.1 Index Cost

| Index | Type | Storage Overhead | Write Overhead |
|-------|------|-----------------|----------------|
| Existing: `gin_trgm_ops` on `content` | GIN trigram | ~30-50% of content size | Moderate |
| New: `to_tsvector('english', content)` | GIN tsvector | ~10-20% of content size | Low-moderate |

The `tsvector` GIN index is generally **smaller** than the trigram index because it stores stemmed lexemes rather than all 3-character substrings. The combined overhead of both indexes is acceptable for a search database.

### 7.2 Query Performance

| Operation | Expected Performance |
|-----------|---------------------|
| `@@` match with GIN index | < 10ms for most queries on tables up to ~1M rows |
| `ts_rank` computation | Negligible (per-row computation on already-filtered results) |
| Hybrid (vector + full-text) | ~2x single search (two index lookups, one per mode) |

The `@@` operator with a GIN index is highly optimized in PostgreSQL. The `ts_rank` function is computed only on matching rows (post-filter), so its cost is proportional to the result set, not the table size.

### 7.3 Hot Path Optimization

In hybrid mode, PostgreSQL's query planner can use either the HNSW vector index or the GIN text index as the primary access path, then apply the other condition as a filter. In practice, the FTS `@@` predicate in the WHERE clause will use the GIN index to pre-filter documents, and the vector distance will be computed on the filtered set. This is efficient because text matching typically reduces the candidate set significantly before vector distance computation.

### 7.4 Index Creation on Existing Data

Creating the new GIN index on an existing table with data will lock the table briefly. For production deployments with large datasets, use `CREATE INDEX CONCURRENTLY` to avoid blocking writes. The `DynamicTableQueries` code uses `CREATE INDEX IF NOT EXISTS` which does not use `CONCURRENTLY` by default; for the initial release this is acceptable since the index is created at collection creation time (empty table). The migration path for existing collections should document the use of `CONCURRENTLY`.

---

## 8. Edge Cases and Considerations

### 8.1 Empty or NULL Content

Documents with `content = NULL` (e.g., binary-only documents) will never match a full-text query because `COALESCE(content, '')` produces an empty `tsvector`. This is correct behavior — binary documents should not appear in text search results.

### 8.2 Short or Common Query Terms

If all query terms are stop words (e.g., "the and is"), `plainto_tsquery` produces an empty query. PostgreSQL's `@@` operator returns `false` for all documents against an empty query, so the search returns zero results. The implementation should detect this case and return an empty result set with a clear indication, or fall back to the Terms-style ILIKE matching as an alternative.

**Recommended approach**: Check if `plainto_tsquery` produces an empty result. If so, return an empty `SearchResult` with `Success = true` and `TotalRecords = 0`. The consumer can then fall back to vector search or adjust their query.

### 8.3 Very Long Query Strings

PostgreSQL handles long query strings gracefully — `plainto_tsquery` extracts and stems individual terms. There is no practical length limit beyond PostgreSQL's general query size limits. The implementation should still apply reasonable input validation (e.g., max 10,000 characters for the query string).

### 8.4 Special Characters and SQL Injection

The query string is sanitized via `_Driver.Sanitize()` (replaces `'` with `''`), consistent with the existing pattern for Terms. Additionally, `plainto_tsquery` treats all input as plain text — it does not interpret special `tsquery` operators (`&`, `|`, `!`, `<->`), so there is no risk of query injection through the text search parser.

### 8.5 Score Normalization in Hybrid Mode

Vector similarity scores (cosine) range from 0.0 to 1.0. With normalization flag `32`, `ts_rank` scores also range from 0.0 to ~1.0 (technically 0 to `rank / (rank + 1)`). This makes the weighted average meaningful — both scores are on comparable scales. Without normalization `32`, `ts_rank` scores have an unbounded upper range, which would cause the text score to dominate in hybrid mode. The default normalization of `32` is chosen specifically to enable fair blending.

### 8.6 Language Configuration

The initial implementation defaults to `english` and uses `english` in the GIN index. If a consumer specifies a different language (e.g., `"spanish"`), the query will still work but will not use the GIN index (PostgreSQL will fall back to a sequential scan with on-the-fly `tsvector` computation). This is acceptable for low-volume queries; high-volume multi-language deployments should create per-language indexes.

**Future enhancement**: Allow the text search configuration to be set at the collection level, so the index matches the query language.

### 8.7 Interaction with Existing `Terms` Filter

`FullText` and `Terms` serve different purposes and can coexist:

| Feature | `Terms` (existing) | `FullText` (new) |
|---------|---------------------|-------------------|
| Purpose | Binary filter (include/exclude) | Ranked relevance search |
| Matching | Substring (`ILIKE '%term%'`) | Stemmed token match (`@@`) |
| Scoring | None | `ts_rank` / `ts_rank_cd` |
| Index | GIN trigram | GIN tsvector |
| Example | Must contain "OAuth2" exactly | Find documents about "OAuth2" (matches "oauth", "OAuth2", etc.) |

Both can be used in the same query: `FullText` provides ranking, `Terms` provides exact substring filtering. They are combined with AND logic in the WHERE clause.

---

## 9. Future Enhancements

These are out of scope for the initial implementation but are natural extensions:

| Enhancement | Description |
|-------------|-------------|
| **Prefix search** | Use `to_tsquery` with `:*` suffix for autocomplete-style prefix matching |
| **Phrase search** | Use `phraseto_tsquery` for exact phrase matching with proximity |
| **Web search syntax** | Use `websearch_to_tsquery` for Google-style query syntax (quotes, minus, OR) |
| **Per-collection language** | Store text search configuration on the `collections` table; use it in index and query |
| **Highlight/snippet** | Use `ts_headline` to return highlighted matching fragments |
| **Trigram similarity scoring** | Use `similarity()` or `word_similarity()` from `pg_trgm` for fuzzy matching scores |
| **Hybrid UNION mode** | Return documents matching either vector OR text (not requiring both) via SQL UNION |
| **Reciprocal Rank Fusion (RRF)** | Alternative to weighted average for hybrid score merging |
| **Stored `tsvector` column** | Precompute and store the `tsvector` as a table column to avoid re-computation at query time (trade storage for query speed) |

---

## 10. Summary

This proposal extends RecallDB's search capabilities from vector-only ranking with binary text filtering to a full hybrid search engine. By leveraging PostgreSQL's native full-text search with `ts_rank` scoring — backed by a GIN `tsvector` index — we add relevance-ranked text search without introducing any external dependencies or new infrastructure.

The design is:
- **Backward compatible** — existing API consumers are unaffected.
- **Minimal surface area** — one new model (`FullTextQuery`), one new enum (`TextSearchTypeEnum`), two new properties on existing models, one new database index.
- **Leverages existing infrastructure** — PostgreSQL's built-in FTS engine, already-enabled extensions, existing index patterns.
- **Consumer-flexible** — supports standalone text search, hybrid search, and client-side fusion patterns.
- **Production-ready scoring** — normalized to 0–1 range by default for fair blending with vector similarity scores.
