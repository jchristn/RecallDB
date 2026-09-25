# Bug to Resolve: Collection Index Names Collide, Leaving Half-Created Collections

Found on 2026-09-24 while benchmarking Isis (C:\Code\AgentMemory) against RecallDB `v0.2.1` (Docker image `jchristn77/recalldb-server:v0.2.1`, PostgreSQL 15.4 with pgvector 0.5.1). The code paths below were re-read in the working tree at commit `b00b8c5` and are unchanged there, so the bug is live on `main`.

## Summary

Every per-collection index name is built from `GetIndexIdentifier(collectionId)`, which keeps only the time component of the collection id. Collection ids come from `PrettyId.GenerateKSortable("col_", 40)`, whose time component is Unix milliseconds in base36. Two collections created in the same millisecond therefore get the same index names (`idx_col_<time>_dkey`, `_did`, `_didp`, `_crt`, `_hnsw`, `_trgm`, `_tsv`, `_fts`, and the `_l_*` and `_t_*` label and tag indexes). PostgreSQL index names are unique per schema, not per table, so the second collection cannot have them.

What happens next depends on timing, and neither outcome is acceptable:

- **Concurrent creation (observed).** Both sessions pass the `IF NOT EXISTS` check, one wins, and the loser's `CREATE UNIQUE INDEX` fails with `23505` on `pg_class_relname_nsp_index`. The DDL batch is not transactional and stops at the first error, so the losing collection keeps its documents table with only the primary key, and its `_labels` and `_tags` tables are never created. The `collections` row was already committed in its own transaction, so the API returns 500 but the collection exists. A client that retries with the same name then gets `23505` on `idx_collections_tenant_name` forever.
- **Sequential creation with the same identifier (reproduced in PostgreSQL, not yet through the API).** `CREATE INDEX IF NOT EXISTS` with a name owned by another table is a silent `NOTICE ... already exists, skipping`. Every statement "succeeds", the API returns 201, and the new collection has no unique `document_key` index, no HNSW index, no trigram index, and no full-text index. Nothing logs an error.

The startup repair (`EnsureAllCollectionSchemasAsync`) cannot fix either case while the other collection holds the names, because it re-runs the same `IF NOT EXISTS` statements with the same colliding names.

## Severity

**High.** The concurrent case corrupts state on a normal workload (Isis provisioning scopes in parallel), returns a 500 for a request whose side effect persisted, and leaves a collection whose document deletes fail. The sequential case is quieter and worse: it silently removes the uniqueness guarantee on `document_key` and the vector index that makes search usable at scale. Isis lost 183 of 2,891 LongMemEval ingests in one benchmark round to this bug before it added a workaround.

## Evidence

### PostgreSQL log, 2026-09-24 18:42 UTC

The benchmark container `isis-bench-postgres` logged four index-name collisions during one parallel provisioning burst (three at 18:42:53.058 and one at 18:42:55.539):

```
ERROR:  duplicate key value violates unique constraint "pg_class_relname_nsp_index"
DETAIL:  Key (relname, relnamespace)=(idx_col_mufvqeof_dkey, 2200) already exists.
STATEMENT:  CREATE UNIQUE INDEX IF NOT EXISTS idx_col_mufvqeof_dkey ON collection_col_mufvqeof_rUUOv9gjkfbyslncYSngTRznLIp (document_key)
```

The counts per identifier were 2 for `mufvqeof`, 1 for `mufvqeoh`, and 1 for `mufvqglk`. Exactly 183 errors on `idx_collections_tenant_name` followed in the same minute (18:42), which is Isis retrying the create for the three affected scope names roughly every 40 ms. Decoding `mufvqeof` as base36 gives 1790275373007 ms, which is 2026-09-24 18:42:53.007 UTC. The identifier really is a millisecond timestamp.

### State of the benchmark database afterwards (read-only queries)

Of the 69 collections in the `recalldb` database, four have a documents table with exactly one index (the primary key) and no `_labels` or `_tags` table:

| Collection id | Indexes on documents table | `_labels` | `_tags` |
|---|---|---|---|
| `col_mufvqeof_oPphcLdmvjHvRWbY7fdGrYBhDCQ` | 1 | missing | missing |
| `col_mufvqeof_rUUOv9gjkfbyslncYSngTRznLIp` | 1 | missing | missing |
| `col_mufvqeoh_kQihbROmBHzOuyOfVh8l2HedxSr` | 1 | missing | missing |
| `col_mufvqglk_wLyx6hGreltuapHQbNkQ0PRqMc3` | 1 | missing | missing |

Healthy collections have 8 indexes on the documents table. The collisions' winners have since been deleted (no `idx_col_mufvqeof_*`, `idx_col_mufvqeoh_*`, or `idx_col_mufvqglk_*` index exists anywhere now), so the losers are orphaned half-collections that nothing will repair until a restart. Even then, the two `mufvqeof` losers would compete for the same names again, and only the first one iterated would get indexes.

### Silent skip, demonstrated

Run inside a transaction that was rolled back (no footprint on the benchmark database):

```sql
BEGIN;
CREATE TABLE collection_col_aaaa_one (id bigserial primary key, document_key varchar(256) not null);
CREATE TABLE collection_col_aaaa_two (id bigserial primary key, document_key varchar(256) not null);
CREATE UNIQUE INDEX IF NOT EXISTS idx_col_aaaa_dkey ON collection_col_aaaa_one (document_key);
CREATE UNIQUE INDEX IF NOT EXISTS idx_col_aaaa_dkey ON collection_col_aaaa_two (document_key);
-- NOTICE:  relation "idx_col_aaaa_dkey" already exists, skipping
INSERT INTO collection_col_aaaa_two (document_key) VALUES ('k1'), ('k1');
-- INSERT 0 2   (duplicate document_key accepted)
ROLLBACK;
```

## Reproduction

The race needs two creates whose ids share a millisecond. Server-side ids are generated when the request body is deserialized into `CollectionMetadata` (the field initializer at `src\RecallDb.Core\Models\CollectionMetadata.cs:146`), so concurrent PUTs arriving together are enough.

1. Start RecallDB against an empty database.
2. Fire 16 to 32 concurrent `PUT /v1.0/tenants/default/collections` requests with distinct names and the same dimensionality. With the SDK:

   ```csharp
   RecallDbClient client = new RecallDbClient("http://localhost:8600", "<admin bearer token>");
   IEnumerable<Task> creates = Enumerable.Range(0, 32).Select(i =>
       client.CreateCollectionAsync("default", new CollectionMetadata { Name = "race-" + i, Dimensionality = 384 }));
   try { await Task.WhenAll(creates); } catch { /* some calls fail with 500 */ }
   ```

   `RecallDbClient(string endpoint, string bearerToken)` and `CreateCollectionAsync(string tenantId, CollectionMetadata collection, ...)` are at `sdk\csharp\RecallDb.Sdk\RecallDbClient.cs:38` and `:353`.
3. Look for collisions and half-created collections:

   ```sql
   SELECT split_part(id, '_', 2) AS ix_id, count(*) FROM collections GROUP BY 1 HAVING count(*) > 1;

   SELECT c.id,
          (SELECT count(*) FROM pg_indexes i WHERE i.tablename = 'collection_' || lower(c.id)) AS doc_indexes,
          to_regclass('collection_' || lower(c.id) || '_labels') IS NOT NULL AS has_labels,
          to_regclass('collection_' || lower(c.id) || '_tags') IS NOT NULL AS has_tags
   FROM collections c ORDER BY doc_indexes;
   ```

4. Retry a failed create with the same name and observe `23505` on `idx_collections_tenant_name` surfacing as another 500.

Several runs may be needed on a fast machine, because the window is one millisecond. Raising concurrency or adding a barrier so all requests are released at once makes it reliable. A deterministic unit-level reproduction is easier: call `DynamicTableQueries.GetCreateCollectionIndexes` for `col_mufvqeof_A...` and `col_mufvqeof_B...` and assert the names differ (they do not today).

## Root cause

### The identifier keeps only the timestamp

`src\RecallDb.Core\Database\Postgresql\Queries\DynamicTableQueries.cs:243-258`:

```csharp
private static string GetIndexIdentifier(string collectionId)
{
    string sanitized = SanitizeTableName(collectionId);

    if (sanitized.StartsWith("col_") && sanitized.Length > 4)
    {
        int nextUnderscore = sanitized.IndexOf('_', 4);
        if (nextUnderscore > 4)
        {
            return sanitized.Substring(4, nextUnderscore - 4);
        }
    }

    if (sanitized.Length <= 8) return sanitized;
    return sanitized.Substring(sanitized.Length - 8);
}
```

For `col_mufvqeof_rUUOv9gjkfbyslncYSngTRznLIp` it returns `mufvqeof` and throws away the 27 random characters that actually make the id unique. The fallback for ids without the `col_` prefix keeps the last 8 characters, which is also collision-prone. The format comes from `IdGenerator.NewCollectionId()` (`src\RecallDb.Core\Helpers\IdGenerator.cs:100-103`), which calls `PrettyId.IdGenerator.GenerateKSortable("col_", 40)`; the PrettyId 2.0.1 XML docs describe the format as `{timestamp}_{random}` with the timestamp in Unix milliseconds, base36.

Every index statement in the file uses that identifier: lines 46-51 (`_dkey` UNIQUE, `_did`, `_didp`, `_crt`, `_hnsw`, `_trgm`), 78 (`_tsv`), 92 (`_fts`), 104 (`DROP INDEX IF EXISTS idx_col_<id>_fts`), 166-169 (labels), and 204-208 (tags).

### Creation is two independent steps, and the second is not atomic

`src\RecallDb.Core\Database\Postgresql\Implementations\CollectionMethods.cs:71-72`:

```csharp
await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);          // INSERT INTO collections, committed
await _Driver.CreateCollectionTablesAsync(collection.Id, collection.Dimensionality, token).ConfigureAwait(false);
```

The INSERT commits in its own transaction before any DDL runs. `CreateCollectionTablesInternalAsync` (`src\RecallDb.Core\Database\Postgresql\PostgresqlDatabaseDriver.cs:539-551`) then builds one list (documents table, its indexes, labels table and indexes, tags table and indexes) and runs it with `isTransaction: false` at line 549. The loop in `ExecuteQueriesAsync` (lines 440-458) executes statements one by one and rethrows on the first failure, so a failure on the `_dkey` statement (the second statement) skips everything after it, including both side tables. `EnsureFullTextSchemaAsync` (lines 553-592) runs afterwards as further separate batches.

`CollectionService.CreateAsync` (`src\RecallDb.Server\Services\CollectionService.cs:111-126`) has no handling for a unique violation, so the retry's `23505` on `idx_collections_tenant_name` (defined at `src\RecallDb.Core\Database\Postgresql\Queries\TableQueries.cs:140`) propagates as a 500 rather than a 409.

### The startup repair repeats the collision

`EnsureAllCollectionSchemasAsync` (`PostgresqlDatabaseDriver.cs:136-162`, called from `src\RecallDb.Server\RecallDbServer.cs:168`) loops over every collection and calls the same `CreateCollectionTablesInternalAsync`. For a loser whose names are held by a live winner, every `CREATE INDEX IF NOT EXISTS` becomes the silent NOTICE shown above. The pass creates the missing `_labels` and `_tags` tables (their `CREATE TABLE IF NOT EXISTS` statements are keyed on the table name, which is unique) but none of the indexes, and it counts the collection as "ensured". It also runs `GetDropLegacyFullTextIndex`, which targets `idx_col_<time>_fts` by name, so a loser's repair could drop another collection's legacy full-text index. That last effect needs a legacy collection sharing the identifier and is unlikely in practice, but it shows how far the shared name reaches.

The test suite mirrors the same logic (`src\Test.Shared\HybridSearchSuites.cs:593-604`, "Mirrors DynamicTableQueries.GetIndexIdentifier", used at line 469), which is why no test catches it.

## Impact

The damage splits by outcome. On the concurrent path, the caller sees a 500, and the collection row is persisted anyway, so the name is taken and every retry fails with `idx_collections_tenant_name`. Isis saw that as a retry storm: 183 errors in under a second from three scope names. The half-created collection has no `_labels` or `_tags` table, so `DocumentService.DeleteAsync` (`src\RecallDb.Server\Services\DocumentService.cs:220-221`) and update with labels or tags (lines 190-199) fail with "relation does not exist" every time. The Isis scopes that depended on those collections could not store memories.

The sequential path is the one that should worry an operator most, because it never reports anything. A collection created that way accepts duplicate `document_key` values, which breaks the assumption behind key-based reads, updates, and deletes (a delete by key removes every duplicate, and a read returns an arbitrary one). Vector search runs as a sequential scan because there is no HNSW index, and full-text search loses both the trigram and `content_tsv` GIN indexes. On a large collection that is the difference between milliseconds and seconds per query, with no error to explain it.

Isis works around the bug today by serializing collection creation per tenant (`C:\Code\AgentMemory\src\Isis.Server\Services\MemoryService.cs:473-480`). The lock lives in one Isis process and is keyed by tenant, so two Isis instances, two tenants provisioning at once, or any other RecallDB client can still collide. The fix has to be in RecallDB.

## Proposed fix

### 1. Collision-free index identifier

Derive the identifier from the whole id. PostgreSQL truncates identifiers to 63 bytes (NAMEDATALEN - 1), and the longest name built today is `idx_col_` + identifier + `_l_dkey` (15 characters of framing), so the identifier can be up to 48 characters. A default collection id is 40 characters, so the full sanitized, lower-cased id fits.

Recommended shape:

```csharp
private static string GetIndexIdentifier(string collectionId)
{
    string sanitized = SanitizeTableName(collectionId).ToLowerInvariant();
    if (sanitized.Length <= MaxIndexIdentifierLength) return sanitized;   // 48
    // Longer ids (client supplied, or a future longer format): deterministic short hash.
    byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(collectionId));
    return "h" + Convert.ToHexString(hash, 0, 12).ToLowerInvariant();     // 25 characters
}
```

Readable names keep `\di` useful for operators, and the hash branch keeps the 63-byte limit safe for any id. Names change shape (`idx_col_col_mufvqeof_ruuov9..._dkey`), so the prefix could drop the redundant `col_` (`idx_` + identifier), but either is fine as long as it is decided once and documented. Unquoted identifiers are folded to lower case, which means two ids differing only in case would still collide. The table names already have that property, so the lower-casing here only makes the existing behavior explicit, and with 27 random characters the risk is negligible.

Worth confirming while in this code: `CollectionMetadata.Id` has a public setter and no `[JsonIgnore]`, and `_ServerManagedMembers` in `RecallDbServer.cs:497-500` only hides `Id` from the generated API examples. If a client-supplied `Id` in the create body is honored, it flows unescaped into every DDL statement through `SanitizeTableName`, which only replaces `-` and `.`. I did not verify this end to end. If it is honored, validate ids against `^[A-Za-z0-9_]{1,48}$` (or ignore a supplied `Id` on create) as part of this fix.

### 2. Atomic creation

PostgreSQL DDL is transactional, so the INSERT, both `CREATE TABLE` statements for side tables, the documents table, and every index can run in one transaction on one connection. Add a driver method (for example `CreateCollectionAsync(CollectionMetadata, CancellationToken)`) that builds the INSERT plus all DDL, including the `content_tsv` index that `EnsureFullTextSchemaAsync` adds today (a new table already has the `content_tsv` column from `GetCreateCollectionTable`, so the migration branch is not needed at creation), and runs it through `ExecuteQueriesAsync(queries, isTransaction: true, _Settings.SchemaCommandTimeoutSeconds, token)`. Any failure rolls back the row and every table. Index builds on an empty table are fast, so holding the transaction open costs nothing.

### 3. Idempotent create, or a clean 409

Catch `PostgresException` with `SqlState == "23505"` and `ConstraintName == "idx_collections_tenant_name"` in `CollectionService.CreateAsync` and return `409 Conflict` with a body that names the existing collection (id and name), so a client can adopt it instead of retrying. Returning the existing collection with 200 is tempting, but it would hide a dimensionality mismatch, so 409 with the existing id is the safer contract. With creation atomic, a 409 can only mean a fully built collection exists.

### 4. Startup repair that detects and fixes damage

Replace the blind `IF NOT EXISTS` replay with a check against the catalog:

1. For each collection row, compute the expected index names under the new identifier and read the actual indexes from `pg_indexes WHERE schemaname = current_schema() AND tablename IN (<documents>, <labels>, <tags>)`.
2. Create any missing table (the side tables are missing on the observed losers).
3. Before creating the UNIQUE `document_key` index on a collection that lacked it, check for duplicates (`SELECT document_key FROM <table> GROUP BY 1 HAVING count(*) > 1 LIMIT 10`). If any exist, `CREATE UNIQUE INDEX` will fail; log the collection id and the duplicate keys at warning level, create the other indexes, and leave remediation to the operator rather than deleting data automatically.
4. Create each missing index under the new name. The HNSW build on a populated table can take minutes, so run these under `SchemaCommandTimeoutSeconds` and log per-collection timing, the way the `content_tsv` migration already does.
5. Once a collection has every index under the new name, drop the indexes on that same table whose names match the old pattern (`idx_col_<old identifier>_%`), selected from `pg_indexes` by table name so a shared old name can never cause a drop on another table.
6. Log a one-line summary: collections checked, repaired, still unhealthy.

Rolling this out needs a migration plan for existing deployments. Every existing collection will be renamed index by index on the first start after upgrade. Renaming with `ALTER INDEX ... RENAME TO` is instant and avoids rebuilds for healthy collections, so step 4 should rename an existing old-name index on the same table instead of building a new one, and only build from scratch where the index is missing. The CHANGELOG should say that the first start rebuilds indexes only for collections that were missing them, and that large damaged collections can delay startup. Add a SQL script under `migrations\` (next free number) that lists damaged collections with the queries from the Reproduction section, so operators can inspect before upgrading.

## Tests to add

The collision is invisible to the current suite because the helper in `HybridSearchSuites.cs:593-604` copies the buggy function. Delete that mirror and assert against the catalog instead, and add these to `src\Test.Shared` (and therefore to `Test.Automated`, `Test.Xunit`, and `Test.Nunit`):

- **Identifier uniqueness (pure unit).** Two ids with the same time component (`col_mufvqeof_A...`, `col_mufvqeof_B...`) produce disjoint index name sets; every generated name is at most 63 bytes, including for a 256-character id that takes the hash branch.
- **Concurrent creation.** Create 32 collections in parallel with a barrier (for example `Parallel.ForEachAsync` over tasks that wait on a shared `TaskCompletionSource`). Every call succeeds, and for each collection `pg_indexes` shows the full expected set on the documents, labels, and tags tables.
- **Forced same-millisecond ids.** If ids can be injected at the `CollectionMethods` layer, create two collections whose ids share a time component and assert both have all indexes, including a UNIQUE `document_key` (inserting a duplicate key must fail).
- **Rollback on failure.** Make one DDL statement fail (for example by pre-creating a table named like the collection's `_tags` table with an incompatible definition, or by passing an invalid dimensionality that the vector type rejects) and assert no `collections` row and no `collection_*` table remains.
- **409 on duplicate name.** A second create with the same tenant and name returns 409 with the existing id, and no second row exists.
- **Repair.** Build a damaged collection by hand (table with only the primary key, no side tables), run `EnsureAllCollectionSchemasAsync`, assert the full index set exists under the new names; repeat with duplicate `document_key` rows and assert the unique index is skipped with a warning while the other indexes are created.

## Secondary issue: the C# SDK does not URL-encode document keys

`sdk\csharp\RecallDb.Sdk\RecallDbClient.cs` builds document routes by plain concatenation:

- line 467, `GetDocumentAsync`: `"/v1.0/tenants/" + tenantId + "/collections/" + collectionId + "/documents/" + documentKey`
- line 486, `GetDocumentByPositionAsync`: same, with `documentId + "/" + position`
- line 506, `UpdateDocumentAsync`
- line 523, `DeleteDocumentAsync`
- line 541, `DocumentExistsAsync`

A key containing `#` has everything from the `#` onwards treated as a URI fragment, so the request goes to a different path. `?` would start a query string, `/` would add a path segment, `%` would be read as an escape. Isis hit the `#` case with chunk keys shaped `<memoryId>#<ordinal>` and had to switch to a URL-safe separator (`C:\Code\AgentMemory\src\Isis.Core\Stores\RecallDb\RecallDbMemoryStore.cs:536-540`). The same pattern applies to `tenantId` and `collectionId` in every route, but server-generated ids are URL-safe, so keys are the real exposure. I confirmed the concatenation in source; I did not re-run the `#` failure against a live server.

The fix is to wrap each caller-supplied path segment in `Uri.EscapeDataString` (keys, document ids, and for consistency tenant and collection ids), plus a round-trip test that creates, reads, checks, updates, and deletes documents whose keys contain `#`, `?`, `/`, `%`, a space, and a non-ASCII character. The server side should be checked in the same pass to confirm it decodes `%2F` in a segment rather than rejecting it, since some routers treat an encoded slash specially.

## Documentation and CHANGELOG notes

Record in `CHANGELOG.md` under the next release heading, without changing any version numbers here:

- Fixed: per-collection index names now derive from the full collection id, so collections created in the same millisecond no longer share index names. Previously the second collection could be left without its unique `document_key`, HNSW, trigram, and full-text indexes, silently or with a 500.
- Fixed: collection creation is atomic; a failure no longer leaves a `collections` row without its tables.
- Changed: creating a collection whose name already exists in the tenant returns 409 with the existing collection's id instead of 500.
- Changed (upgrade note): the first start renames existing indexes to the new scheme and builds any that are missing; damaged collections with duplicate `document_key` values are reported and keep working without the unique index until an operator resolves the duplicates.
- Fixed (SDK): document keys are URL-encoded in get, update, delete, and exists calls.

`README.md` already describes the startup schema pass near the `content_tsv` migration notes (around line 326); extend that paragraph to mention the index repair and the duplicate-key warning. `REST_API.md` needs the new 409 response on collection create.

## Checklist

Mark status with `[ ]` open, `[~]` in progress, `[x]` done, `[-]` dropped. Fill in owner and notes as work lands.

| # | Task | Status | Owner | Notes |
|---|---|---|---|---|
| 1 | Replace `GetIndexIdentifier` with a full-id or hashed identifier, 63-byte safe | [x] | | Full lower-cased id ≤48 chars, else `h`+SHA-256 prefix; `DynamicTableQueries.cs` |
| 2 | Decide and document the final index name prefix (`idx_col_` vs `idx_`) | [x] | | Kept `idx_col_` (no name churn beyond the identifier); documented in CHANGELOG/README |
| 3 | Confirm whether a client-supplied `Id` on create is honored; validate or ignore it | [x] | | Confirmed honored (reached DDL). Now validated `^[A-Za-z0-9_-.]{1,48}$` → 400 in `CollectionService.CreateAsync` |
| 4 | Single-transaction collection create (row, tables, all indexes including `_tsv`) | [x] | | `PostgresqlDatabaseDriver.CreateCollectionAtomicAsync` |
| 5 | Map `23505` on `idx_collections_tenant_name` to 409 with the existing id | [x] | | `DuplicateCollectionException` → 409 with existing id |
| 6 | Catalog-driven startup repair: create missing tables and indexes under new names | [x] | | `RepairCollectionSchemaAsync` |
| 7 | Repair: rename old-name indexes on the same table instead of rebuilding | [x] | | `ALTER INDEX ... RENAME` by suffix, per table |
| 8 | Repair: duplicate `document_key` detection and warning before the unique index | [x] | | `FindDuplicateDocumentKeysAsync`; unique index skipped + warned |
| 9 | Repair: drop leftover old-name indexes only by table, never by name alone | [x] | | Leftover `idx_col_*` dropped, selected from `pg_indexes` by table |
| 10 | Add `migrations\` inspection script for damaged collections | [x] | | `migrations/002_inspect_damaged_collections.sql` |
| 11 | Delete the `IndexId` mirror in `HybridSearchSuites.cs:593-604`; assert via `pg_indexes` | [x] | | Mirror now delegates to `DynamicTableQueries.GetIndexIdentifier` |
| 12 | Tests: identifier uniqueness, concurrent create, forced same-ms ids | [x] | | `CollectionIntegritySuites` (unit + 32-way barrier concurrent create) |
| 13 | Tests: rollback on DDL failure, 409 on duplicate name, repair of damaged collection | [x] | | `CollectionIntegritySuites` (DB-gated rollback + repair, incl. duplicate-key variant) |
| 14 | SDK: `Uri.EscapeDataString` on path segments in `RecallDbClient.cs` (lines 467, 486, 506, 523, 541 and peers) | [x] | | Done in C#, Python (`quote`), and JS (`encodeURIComponent`) SDKs |
| 15 | SDK test: round-trip keys containing `#`, `?`, `/`, `%`, space, non-ASCII | [x] | | Added to the C#, Python, and JS SDK harnesses |
| 16 | Verify server routing decodes encoded `/` in a document key segment | [x] | | Server now URL-decodes path params (`DecodeParam`); verified round-trip incl. `%2F` |
| 17 | CHANGELOG, README startup-pass paragraph, REST_API 409 | [x] | | All updated |
| 18 | Clean up the four orphaned half-collections in the Isis benchmark database | [-] | | Dropped: `isis-bench-*` is off-limits per local policy; the startup repair now fixes such collections on the next restart |
| 19 | Tell Isis when fixed so it can drop the per-tenant provisioning lock | [ ] | | Cross-repo (AgentMemory `MemoryService.cs:473-480`); not changed here |
