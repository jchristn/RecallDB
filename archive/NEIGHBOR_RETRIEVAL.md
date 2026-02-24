# Neighbor Chunk Retrieval — Implementation Plan

> **Status: COMPLETED** — All phases implemented on 2026-02-24.

## Overview

When searching via vector similarity, full-text relevance, or hybrid search, users can request neighboring chunks (before and after each matched chunk) to be included in the results. This provides surrounding context for each match, which is critical for RAG pipelines where isolated chunks lose meaning without their adjacent content.

**Behavior**: When `IncludeNeighbors` is set to `N`, each matched document in `SearchResult.Documents` will have its `Neighbors` property populated with up to `N` chunks before and `N` chunks after the matched chunk's position within the same `DocumentId`. Neighbors are ordered by `Position` ascending. Neighbors that fall outside the document's position range are simply absent (no error). If two matched chunks are close together in the same document, their neighbor lists may overlap — each match carries its own self-contained context window.

**Non-goal**: Neighbors do not affect scoring, filtering, pagination, or `TotalRecords`. They are a post-search enrichment step only.

---

## Phase 1: Core Backend (C# Server + Core Library)

### 1.1 Add `IncludeNeighbors` to `SearchQuery` (Core model)

- [x] **File**: `src/RecallDb.Core/Models/SearchQuery.cs` — DONE
- [x] Add property `IncludeNeighbors` (`int?`, default `null`) — DONE
- [x] Clamp to valid range in setter: `0–10` (values outside range are clamped; `null` and `0` both mean "no neighbors") — DONE
- [x] Add XML doc comment explaining the property — DONE

### 1.2 Add `Neighbors` to `DocumentRecord` (Core model)

- [x] **File**: `src/RecallDb.Core/Models/DocumentRecord.cs` — DONE
- [x] Add property `Neighbors` (`List<DocumentRecord>`, default `null`) — DONE
- [x] Add private backing field `_Neighbors` initialized to `null` — DONE
- [x] Add XML doc comment: "Neighboring chunks surrounding this document in positional order. Populated when IncludeNeighbors is specified in the search query. Null when not requested." — DONE
- [x] Ensure `Neighbors` is **not** populated by `FromDataRow` (it's a post-search enrichment, not a database column) — DONE (not touched in FromDataRow)

### 1.3 Add range-query method to `IDocumentMethods` / `DocumentMethods`

- [x] **File**: `src/RecallDb.Core/Database/Interfaces/IDocumentMethods.cs` — DONE
- [x] Add method: `Task<List<DocumentRecord>> ReadByDocumentIdAndPositionRangeAsync(string collectionId, string documentId, int minPosition, int maxPosition, CancellationToken token = default)` — DONE
- [x] **File**: `src/RecallDb.Core/Database/Postgresql/Implementations/DocumentMethods.cs` — DONE
- [x] Implement the method with SQL: `SELECT {columns} FROM collection_{id} WHERE document_id = :docId AND position BETWEEN :min AND :max ORDER BY position ASC` — DONE
- [x] This leverages the existing composite index `(document_id, position)` — no schema migration needed — CONFIRMED

### 1.4 Implement neighbor retrieval in the search route

- [x] **File**: `src/RecallDb.Server/RecallDbServer.cs` — DONE
- [x] In `SearchRoute`, after `SearchAsync` returns and after `AttachLabelsAndTagsAsync`, add neighbor enrichment logic — DONE
- [x] Implementation approach: — DONE
  1. Check if `query.IncludeNeighbors` is set and > 0
  2. For each document in `result.Documents`, compute the position range: `(doc.Position - N, doc.Position + N)`
  3. **Batch by DocumentId**: group matched documents by `DocumentId` to minimize queries. For each unique `DocumentId`, compute the union of all position ranges, then issue a single `ReadByDocumentIdAndPositionRangeAsync` per `DocumentId` (or merge overlapping ranges into a single query)
  4. For each matched document, filter the fetched chunks to those within its `[Position - N, Position + N]` range, excluding the matched chunk itself (by `Position`), and assign to `doc.Neighbors`
  5. Call `AttachLabelsAndTagsAsync` on the collected neighbor documents as well
- [x] Ensure neighbors do **not** include the matched chunk itself — DONE
- [x] Ensure neighbors are ordered by `Position` ascending — DONE (ordered by SQL ORDER BY position ASC)

### 1.5 Optimize batching for neighbor queries

- [x] When multiple matched chunks share the same `DocumentId` and their neighbor ranges overlap, merge the ranges into a single SQL query to avoid redundant fetches — DONE
- [x] Example: if chunks at position 5 and 8 both match with `IncludeNeighbors: 3`, the ranges `[2,8]` and `[5,11]` merge to `[2,11]` — one query instead of two — DONE

---

## Phase 2: C# SDK

### 2.1 Update SDK `SearchQuery` model

- [x] **File**: `sdk/csharp/RecallDb.Sdk/Models/SearchQuery.cs` — DONE
- [x] Add property: `public int? IncludeNeighbors { get; set; }` — DONE
- [x] Add XML doc comment — DONE

### 2.2 Update SDK `DocumentRecord` model

- [x] **File**: `sdk/csharp/RecallDb.Sdk/Models/DocumentRecord.cs` — DONE
- [x] Add property: `public List<DocumentRecord> Neighbors { get; set; }` — DONE
- [x] Add XML doc comment — DONE

### 2.3 No changes needed to `RecallDbClient.cs`

- [x] Verify: `SearchAsync` already serializes the full `SearchQuery` object and deserializes the full `SearchResult`/`DocumentRecord` — no method signature changes required — CONFIRMED

---

## Phase 3: Python SDK

### 3.1 Update SDK documentation and examples

- [x] **File**: `sdk/python/recalldb_sdk.py` — DONE
- [x] Update the `search()` method docstring to document `IncludeNeighbors` as an optional integer parameter on the query dict — DONE
- [x] Document that response documents may contain a `Neighbors` list when `IncludeNeighbors` is set — DONE
- [x] No structural code changes needed (Python SDK uses dicts, so new fields are automatically supported) — CONFIRMED

---

## Phase 4: JavaScript SDK

### 4.1 Update SDK documentation and examples

- [x] **File**: `sdk/js/recalldb-sdk.js` — DONE
- [x] Update the `search()` method JSDoc to document `@param {number} [query.IncludeNeighbors]` — number of neighbor chunks before/after each match to include (0-10) — DONE
- [x] Document that response documents may contain a `Neighbors` array when `IncludeNeighbors` is set — DONE
- [x] No structural code changes needed (JavaScript SDK uses plain objects) — CONFIRMED

---

## Phase 5: Dashboard

### 5.1 Add `IncludeNeighbors` input to the search form

- [x] **File**: `dashboard/src/views/SearchQuery.jsx` — DONE
- [x] In the `SearchTab` component, add state: `const [includeNeighbors, setIncludeNeighbors] = useState('')` — DONE
- [x] Add a numeric input field in the "Results options & submit" row (alongside Sort Order and Max Results): label "Include Neighbors", type number, min 0, max 10, placeholder "0" — DONE
- [x] Update the `buildQuery` function to include `IncludeNeighbors: parseInt(includeNeighbors) || undefined` when the value is > 0 — DONE

### 5.2 Display neighbors in search results

- [x] **File**: `dashboard/src/views/SearchQuery.jsx` — DONE
- [x] Modify the results `DataTable` or add an expandable row mechanism to show neighbors when present — DONE (added expandable/renderExpanded props to DataTable)
- [x] Approach: add an expand/collapse toggle on each result row. When expanded, render the neighbor chunks below the matched row in a visually distinct sub-table or indented list, showing Position, ContentType, and Content (preview) for each neighbor — DONE
- [x] Neighbors should be visually distinguishable from matched results (e.g., lighter background, indentation, or a "context" label) — DONE (bg-tertiary background, indented sub-table, "Neighbors" label)
- [x] The "View JSON" modal (`JsonModal`) already shows the full document object — neighbors will appear naturally since they're part of the serialized `DocumentRecord` — CONFIRMED

---

## Phase 6: Test Harnesses

### 6.1 C# automated test harness

- [x] **File**: `src/Test.Automated/Program.cs` — DONE
- [x] Add test: **Vector search with IncludeNeighbors** — DONE (TestNeighborVectorSearch)
- [x] Add test: **Hybrid search with IncludeNeighbors** — DONE (TestNeighborHybridSearch)
- [x] Add test: **Full-text search with IncludeNeighbors** — DONE (TestNeighborFullTextSearch)
- [x] Add test: **IncludeNeighbors with boundary chunks** — DONE (TestNeighborBoundaryChunk)
- [x] Add test: **IncludeNeighbors with single-chunk document** — DONE (TestNeighborSingleChunkDocument)
- [x] Add test: **IncludeNeighbors = 0 or null** — DONE (TestNeighborNullAndZero)
- [x] Add test: **IncludeNeighbors with overlapping results** — DONE (TestNeighborOverlapping)
- [x] Add test: **Neighbor labels and tags** — DONE (TestNeighborLabelsAndTags)

### 6.2 Python test harness

- [x] **File**: `sdk/python/test_harness.py` — DONE
- [x] Add test: search with `IncludeNeighbors` set, assert `Neighbors` present in response documents — DONE (test_neighbor_search_with_neighbors)
- [x] Add test: search without `IncludeNeighbors`, assert `Neighbors` is absent or null — DONE (test_neighbor_search_without_neighbors)

### 6.3 JavaScript test harness

- [x] **File**: `sdk/js/test-harness.js` — DONE
- [x] Add test: search with `IncludeNeighbors` set, assert `Neighbors` present in response documents — DONE (testNeighborSearchWithNeighbors)
- [x] Add test: search without `IncludeNeighbors`, assert `Neighbors` is absent or null — DONE (testNeighborSearchWithoutNeighbors)

---

## Phase 7: Postman Collection

### 7.1 Update search request examples

- [x] **File**: `RecallDB.postman_collection.json` — DONE
- [x] Add a new request example under the Search folder: "Search with Neighbor Retrieval" showing `IncludeNeighbors: 3` in the request body alongside a vector or hybrid query — DONE
- [x] Update the existing search request body examples to show `IncludeNeighbors` as an optional field (commented or set to null) so users discover it — DONE (new dedicated example added; JSON format doesn't support comments)
- [x] Update the response example to show the `Neighbors` array on a document — N/A (Postman collection has empty response arrays; response examples live in REST_API.md which was updated)

---

## Phase 8: Documentation

### 8.1 REST_API.md

- [x] **File**: `REST_API.md` — DONE
- [x] In the SearchQuery Fields reference table, add a row for `IncludeNeighbors` — DONE
- [x] In the response example, add a `Neighbors` array to one of the example document objects showing before/after chunks — DONE
- [x] Add a new subsection "Neighbor Retrieval" under the Search section explaining:
  - What neighbors are and when to use them — DONE
  - That neighbors are scoped to the same `DocumentId` as the matched chunk — DONE
  - That neighbors do not affect scoring, filtering, or pagination — DONE
  - Example request and response — DONE
  - Edge cases: first/last chunks, single-chunk documents, overlapping windows — DONE

### 8.2 README.md

- [x] **File**: `README.md` — DONE
- [x] In the Search section, add a bullet point mentioning neighbor retrieval — DONE
- [x] In the SDK examples section, add `IncludeNeighbors` usage examples in all three SDKs (C#, Python, JavaScript) — DONE

---

## File Change Summary

| File | Change Type | Phase | Status |
|------|------------|-------|--------|
| `src/RecallDb.Core/Models/SearchQuery.cs` | Add property | 1.1 | DONE |
| `src/RecallDb.Core/Models/DocumentRecord.cs` | Add property | 1.2 | DONE |
| `src/RecallDb.Core/Database/Interfaces/IDocumentMethods.cs` | Add method | 1.3 | DONE |
| `src/RecallDb.Core/Database/Postgresql/Implementations/DocumentMethods.cs` | Add method | 1.3 | DONE |
| `src/RecallDb.Server/RecallDbServer.cs` | Add post-search enrichment | 1.4 | DONE |
| `sdk/csharp/RecallDb.Sdk/Models/SearchQuery.cs` | Add property | 2.1 | DONE |
| `sdk/csharp/RecallDb.Sdk/Models/DocumentRecord.cs` | Add property | 2.2 | DONE |
| `sdk/python/recalldb_sdk.py` | Update docstring | 3.1 | DONE |
| `sdk/js/recalldb-sdk.js` | Update JSDoc | 4.1 | DONE |
| `dashboard/src/views/SearchQuery.jsx` | Add input + result display | 5.1, 5.2 | DONE |
| `dashboard/src/components/DataTable.jsx` | Add expandable row support | 5.2 | DONE |
| `src/Test.Automated/Program.cs` | Add test cases | 6.1 | DONE |
| `sdk/python/test_harness.py` | Add test cases | 6.2 | DONE |
| `sdk/js/test-harness.js` | Add test cases | 6.3 | DONE |
| `RecallDB.postman_collection.json` | Add examples | 7.1 | DONE |
| `REST_API.md` | Add docs | 8.1 | DONE |
| `README.md` | Add mention + example | 8.2 | DONE |

## Notes

- **No database schema migration required.** The existing `(document_id, position)` composite index on each collection table already supports the range query needed for neighbor retrieval.
- **No breaking changes.** `IncludeNeighbors` defaults to null (no neighbors). The `Neighbors` property on `DocumentRecord` defaults to null and is only populated when requested. Existing API consumers are unaffected.
- **Performance consideration**: Neighbor retrieval adds one SQL query per unique `DocumentId` in the result set (after range merging). For a typical search returning 10 results from 10 different documents, this adds 10 indexed lookups. The composite index makes each lookup fast (index scan on `document_id` + range scan on `position`).
- **Build verified**: All C# projects (Core, Server, SDK, Test.Automated) compile successfully with 0 warnings, 0 errors.
