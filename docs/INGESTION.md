# Ingestion

How data gets into RecallDB, what the server does with it, and how it is changed and removed. For the storage layout
behind this, see [ARCHITECTURE.md](ARCHITECTURE.md#5-storage-layout); for how stored data is queried, see
[RETRIEVAL.md](RETRIEVAL.md). Exact request and response bodies are in [REST_API.md](../REST_API.md#documents).

---

## 1. The pipeline and who does what

```
source content ──▶ [caller] chunk ──▶ [caller] embed ──▶ [caller] attach metadata ──▶ RecallDB write API
                                                                                          │
                     ┌────────────────────────────────────────────────────────────────────┘
                     ▼
     validate ─▶ insert row(s) ─▶ PostgreSQL maintains content_tsv, HNSW, GIN, B-tree indexes ─▶ insert label/tag rows
```

| Step | Done by | Notes |
|---|---|---|
| Split source content into chunks | Caller | RecallDB stores chunks as given. Choose sizes that suit your embedding model and your context budget. |
| Compute embedding vectors | Caller | Any model. Every vector in a collection must have the collection's dimensionality, and query vectors must come from the same model. |
| Assign `DocumentKey`, `DocumentId`, `Position` | Caller (key optional) | These decide how chunks can be grouped, ordered, and expanded at query time (section 4). |
| Attach labels and tags | Caller | Sent inline with the document or added later through the label and tag endpoints. |
| Validate, store, index | RecallDB | Section 3. |
| Tokenize for full-text search | RecallDB (PostgreSQL) | The stored `content_tsv` column is generated from `content` on every insert and update, with the `english` configuration. |

---

## 2. Collections

A collection is the unit of storage and search: its own documents, labels, and tags tables, its own indexes, and a
fixed vector **dimensionality**. Searches never cross collections.

```http
PUT /v1.0/tenants/{tid}/collections
{ "Name": "product-docs", "Description": "Docs site, chunked", "Dimensionality": 1536 }
```

- **Dimensionality** is fixed for the collection's lifetime. To change embedding models, create a new collection and
  re-ingest. It must be at least 1 and **at most 2000**, because pgvector 0.5.1 (the shipped image) cannot build the
  HNSW index above that; a larger value is rejected with 400. Models with larger outputs (for example 3072-dimension
  embeddings) need their vectors shortened (many models support a `dimensions` parameter).
- **Name** must be unique within the tenant; a duplicate name is rejected with 409 and the existing collection's id.
- **Id** is generated (`col_...`) unless supplied. A supplied id may use 1 to 48 letters, digits, `_`, `-`, or `.`.
- Creation is atomic: the `collections` row, all three tables, and every index are created in one transaction.
- Creating a collection needs an admin API key or a tenant-admin user's token.
- Deleting a collection drops its tables, with every document, label, and tag in it.

`GET /v1.0/tenants/{tid}/collections/{cid}/stats` reports the collection's chunk count (`DocumentCount`), distinct
`DocumentId` count (`UniqueDocumentCount`), total content length, and label and tag counts.

---

## 3. Documents

### 3.1 The document record

A document is one row: usually one chunk of a larger source.

| Field | Required | What it is for |
|---|---|---|
| `DocumentKey` | No; generated (`doc_...`) when omitted | The unique handle for this row within the collection (at most 256 characters). Use a deterministic key (for example `<source-id>-c<n>`) if you will update or delete by key later. |
| `DocumentId` | No | Groups the chunks of one source document (at most 256 characters). Needed for neighbor retrieval, collapse by `DocumentId`, the `DocumentIds` filter, read-by-position, and per-document stats. |
| `Position` | No; default 0 | The chunk's order within its `DocumentId`. Neighbor retrieval reads adjacent positions, so number chunks 0, 1, 2, ... without gaps. |
| `Content` | No | The chunk text. It is what full-text search ranks and what the `Terms` filter matches. |
| `ContentType` | No; default `Text` | `Text`, `List`, `Table`, `Binary`, `Image`, `Code`, `Hyperlink`, `Meta`, or `Unknown`. Informational: stored and returned, not used in matching. Any other value is a 400. |
| `BinaryData` | No | Raw bytes, base64-encoded in JSON. Stored and returned; never searched. |
| `Embeddings` | No, but needed for vector search | Must have exactly the collection's dimensionality, or the write is rejected with 400. A document without a vector is still stored and is still found by full-text search (section 8). |
| `Labels` | No | List of strings. Stored in the labels table. |
| `Tags` | No | Object of string keys to string values. Stored in the tags table. |
| `CreatedUtc` | No; defaults to the time of the request | May be supplied, for example to preserve the source's own timestamp. It drives date filters, `Created*` sorting, and the hybrid recency signal. |
| `ContentLength` | No | Computed as the UTF-8 byte length of `Content` (or the length of `BinaryData`) when omitted or 0 on create. |
| `Sha256`, `Etag` | No | Stored and returned exactly as supplied. **The server does not compute or verify them**, and does not deduplicate on them; compute them in the caller if you want change detection (section 6). |

The server also returns `Id` (the internal row id).

### 3.2 Writing one document

```http
PUT /v1.0/tenants/{tid}/collections/{cid}/documents
{
  "DocumentKey": "handbook-12-c3",
  "DocumentId": "handbook-12",
  "Position": 3,
  "ContentType": "Text",
  "Content": "Rotate the signing key every quarter ...",
  "Embeddings": [0.012, -0.044, ...],
  "Labels": ["security", "published"],
  "Tags": { "source": "handbook", "section": "keys", "parentKey": "handbook-12" }
}
```

What happens, in order:

1. The body is deserialized; a bad `ContentType`, an empty `DocumentKey`, or a malformed body is a 400.
2. The collection is read (404 when missing) and, when `Embeddings` is present, its length is checked against the
   collection's dimensionality (400 on a mismatch).
3. One `INSERT` writes the row and returns the generated `Id` and the stored `CreatedUtc`. PostgreSQL computes
   `content_tsv` and updates every index on the table in the same statement.
4. One row per label and one row per tag are inserted into the side tables.
5. The response (201) is the stored document with its labels and tags.

If a document with the same `DocumentKey` already exists, the request fails with **409 Conflict** naming the key, and
nothing is written.

The document row is written before its labels and tags, in separate statements, so a failure between them can leave a
document without some of its labels or tags.

### 3.3 Writing many documents

```http
POST /v1.0/tenants/{tid}/collections/{cid}/documents/batch
[ { ...document... }, { ...document... } ]
```

- Every document's embedding length is checked first; one mismatch rejects the whole batch with a 400 naming the key.
- A `DocumentKey` that appears twice in the batch is rejected up front with a 400 naming it.
- All rows go in **one multi-row `INSERT`**, so the batch is atomic: if any row fails, none of them is stored. A key
  that already exists in the collection fails the batch with **409 Conflict**, listing the existing keys (up to ten).
- Labels and tags are then written per document, after the rows commit.
- The response (201) lists every created document with its `Id`, `CreatedUtc`, labels, and tags.

Batching is much faster than single writes. A batch is one request body and one SQL statement, so keep batches to a
few hundred documents; with 384-dimension vectors, 250 documents per batch loaded a 13,700-chunk collection in about
two minutes on a laptop, index maintenance included.

### 3.4 Updating a document

```http
PUT /v1.0/tenants/{tid}/collections/{cid}/documents/{docKey}
```

An update replaces the document's **stored fields**, but leaves its **labels and tags** alone unless you send them:

- Every column is set from the request body. A field left out is written as its default: `Embeddings` becomes null
  (the document drops out of vector search), `DocumentId` null, `Position` 0, `Content` null, `ContentType` `Text`.
- `Labels` and `Tags` left out (or null) keep the document's current labels and tags. Sent as a list or object, they
  replace them; an empty list or object removes them all.
- `Embeddings`, when sent, must match the collection's dimensionality (400 otherwise).
- `ContentLength` is not recomputed on update; send it, or it is stored as 0.
- `CreatedUtc` is not changed by an update, so the recency signal and date filters keep the original write time. If
  "newest" should mean "last modified", delete and re-create the document instead (section 5).
- A key that does not exist is a **404**, and nothing is created.

To change one stored field, read the document, change the field, and send the record back (without `Id`, which the
server manages); the labels and tags can be left out.

### 3.5 Labels and tags after the fact

Labels and tags can also be managed on their own:

| Operation | Endpoint |
|---|---|
| Add a label to a document | `PUT /v1.0/tenants/{tid}/collections/{cid}/labels` with `{ "DocumentKey": "...", "Label": "..." }` |
| Add a tag | `PUT /v1.0/tenants/{tid}/collections/{cid}/tags` with `{ "DocumentKey": "...", "Key": "...", "Value": "..." }` |
| Remove one | `DELETE .../labels/{id}` or `DELETE .../tags/{id}` (the label or tag record's own id) |
| List, or list distinct values | `GET .../labels`, `GET .../labels/distinct`, `GET .../tags`, `GET .../tags/distinct` |

These endpoints do not check that the document exists, and they do not prevent duplicates: adding the same label twice,
or a tag key a document already has, creates another row. When a document carries one tag key twice, reads show one
value for it, and collapse by tag uses the smallest value.

### 3.6 Deleting

| Operation | Endpoint | Notes |
|---|---|---|
| One document | `DELETE .../documents/{docKey}` | 204, also when the key does not exist. Removes the document's labels and tags. |
| Several by key | `POST .../documents/batch/delete` with `{ "DocumentKeys": [...] }` | One `DELETE ... IN (...)` for the rows, after their labels and tags. |
| Everything matching a filter | `POST .../documents/delete/filter` with an enumeration filter | Same filters as enumeration (labels, tags, terms, dates, `DocumentIds`). Returns `DocumentsDeleted`. An empty filter deletes every document in the collection. |
| A whole source document | `delete/filter` with `{ "DocumentIds": ["handbook-12"] }` | Removes every chunk of it. |
| The collection | `DELETE /v1.0/tenants/{tid}/collections/{cid}` | Drops the tables. |

---

## 4. Modeling chunked documents

Most retrieval features work on chunks, but several of them need to know which chunks belong together:

| Feature | Needs |
|---|---|
| Neighbor retrieval (`IncludeNeighbors`) | `DocumentId` and gap-free `Position` values |
| Collapse by `DocumentId` | `DocumentId` |
| Collapse by tag | A tag holding the parent's identity (for example `parentKey`) |
| Hybrid recency per parent | Collapse (recency is ranked per collapse group) |
| `DocumentIds` filter, read-by-position, document stats, delete a whole source | `DocumentId` |

A layout that supports all of them:

```json
[
  { "DocumentKey": "mem_7Tn2-c0", "DocumentId": "mem_7Tn2", "Position": 0, "Content": "...", "Embeddings": [...],
    "Tags": { "parentKey": "mem_7Tn2", "title": "Signing key rotation" }, "Labels": ["cat_security"] },
  { "DocumentKey": "mem_7Tn2-c1", "DocumentId": "mem_7Tn2", "Position": 1, "Content": "...", "Embeddings": [...],
    "Tags": { "parentKey": "mem_7Tn2", "title": "Signing key rotation" }, "Labels": ["cat_security"] }
]
```

- Put the same labels and tags on every chunk of a parent. Filters are evaluated per chunk, so a label on chunk 0 only
  does not make chunk 3 match.
- Prefer a stable identity (an id, not a title) for `DocumentId` or the grouping tag. Collapse by tag is useful when
  `DocumentId` holds something that can change, such as a slug.
- Keep one chunk per row even for short documents; a single-chunk document is simply a group of one.
- Write all chunks of a parent in one batch, so the parent is stored whole or not at all.

---

## 5. Keeping data current

RecallDB has no upsert. Common patterns:

- **Replace a source document.** Delete its chunks (`delete/filter` with its `DocumentId`), then batch-create the new
  chunks. The number of chunks can change, and the new rows get a fresh `CreatedUtc`, so recency reflects the rewrite.
  Between the two calls, searches will not see the document.
- **Edit one chunk in place.** Update by key with the complete stored record (section 3.4). The chunk keeps its
  original `CreatedUtc`, and its labels and tags unless you send new ones.
- **Idempotent retries.** Use deterministic `DocumentKey` values. Re-sending a create for a key that exists returns
  409 and stores nothing, so a retried write cannot duplicate rows, and a 409 on retry means the first attempt landed.

---

## 6. Change detection and deduplication

The `Sha256` and `Etag` columns exist so a caller can store its own content hash and version marker next to each chunk.
The server stores them verbatim; it does not hash content, compare hashes, or reject duplicates. A typical use:

1. Hash each chunk's content in the caller and send it as `Sha256`.
2. On the next sync, enumerate the document's chunks (`POST .../documents/enumerate` with its `DocumentId`) and compare
   hashes before deciding whether to rewrite.

The only uniqueness RecallDB enforces is on `DocumentKey`.

---

## 7. Reading back what was written

These read paths do not rank anything; [RETRIEVAL.md](RETRIEVAL.md) covers search.

| Need | Endpoint |
|---|---|
| One chunk by key | `GET .../documents/{docKey}` |
| One chunk by source and position | `GET .../documents/{docId}/{position}` |
| Does a key exist | `HEAD .../documents/{docKey}` (200 or 404) |
| Page through documents, filtered | `POST .../documents/enumerate` (newest or oldest first, 1-1000 per page) |
| First 100 documents | `GET .../documents` (the same paged result, first page only) |
| Size of one source document | `GET .../documents/stats/{docKey}` (chunk count, total content length, label and tag counts, across the key's `DocumentId`) |

Every document read attaches its labels and tags. Reads return the stored `Embeddings`; searches do not unless asked
(`IncludeEmbeddings`).

---

## 8. Behaviors and limits to know

Verified against the current build (v0.2.1 server):

| Situation | What happens |
|---|---|
| Create with a `DocumentKey` that already exists | 409 naming the key. Nothing is written. |
| Batch with a key repeated inside it | 400 naming the key. Nothing is written. |
| Batch with a key that already exists | 409 listing the existing keys. The whole batch is rolled back. |
| `Embeddings` with the wrong length | 400 on create, batch create, and update. |
| Update of a key that does not exist | 404. Nothing is created. |
| Update that omits stored fields | Those fields are cleared; omitted labels and tags are kept (section 3.4). |
| Delete of a key that does not exist | 204. |
| Collection with more than 2000 dimensions | 400. |
| Document without `Embeddings` | Stored. Found by full-text search. In a small collection, a vector search that scans the table can still list it last with `Score` 0, and a hybrid search can give it a trailing `VectorRank`; with the HNSW index in use it is skipped. Filter such documents out (for example with a label) if that matters. |
| `Content` in a language other than English | Stored and searchable, but the stored `content_tsv` column and its index use English stemming and stop words. Searching with another `FullText.Language` works without the index (see [RETRIEVAL.md](RETRIEVAL.md#4-full-text-search)). |
| Very long `Content` | `TEXT` has no practical limit, but PostgreSQL caps a `tsvector` at 1 MB, so an extremely large single chunk can fail to store. Chunk long sources; retrieval works best on chunks of a few hundred to a few thousand characters anyway. |
| Keys with `/`, `?`, `#`, `%`, spaces, non-ASCII | Supported; URL-encode them in paths (the SDKs do). |

---

## 9. Ingesting through the SDKs and MCP

The SDKs wrap every call above (`CreateDocumentAsync`, `CreateDocumentBatchAsync`, `UpdateDocumentAsync`,
`DeleteDocumentsByFilterAsync`, ... in C#; the same names in camelCase or snake_case in JavaScript and Python), encode
path segments, and surface errors as `RecallDbException` with the server's status and message. See the READMEs under
[sdk/](../sdk/).

Agents can ingest through MCP with the `document/create`, `document/batchCreate`, `document/update`, `document/delete`,
`document/batchDelete`, `document/deleteByFilter`, `label/create`, and `tag/create` tools; the behavior is identical
to REST. See [MCP_API.md](../MCP_API.md#document).
