namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using Npgsql;

    using RecallDb.Core.Database.Postgresql;
    using RecallDb.Core.Database.Postgresql.Queries;
    using RecallDb.Core.Settings;

    using Touchstone.Core;

    using static Test.Shared.TestHelpers;

    /// <summary>
    /// Tests for the per-collection index naming, atomic collection creation, duplicate-name handling, id validation,
    /// URL-encoded document keys, and the catalog-driven startup schema repair.
    ///
    /// The pure-unit cases (identifier uniqueness, id validation, name-length limits) need no server. The integration
    /// cases exercise the REST surface. The repair and rollback cases need direct database access and run only when
    /// RECALLDB_TEST_DB holds an Npgsql connection string for the database the server under test uses; otherwise they
    /// return without asserting, matching the convention in HybridSearchSuites.
    /// </summary>
    public static class CollectionIntegritySuites
    {
        #region Shared-State

        private static HttpClient _Client = null;
        private static readonly List<string> _CollectionsToDelete = new List<string>();

        private static string TestDbConnectionString
        {
            get
            {
                string env = Environment.GetEnvironmentVariable("RECALLDB_TEST_DB");
                return string.IsNullOrWhiteSpace(env) ? null : env;
            }
        }

        #endregion

        #region Suite

        /// <summary>
        /// The collection-integrity test suite.
        /// </summary>
        public static TestSuiteDescriptor Suite { get; } = Build();

        private static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "RecallDbCollectionIntegrity",
                displayName: "RecallDB Collection Integrity Tests",
                beforeSuiteAsync: ct =>
                {
                    _Client = CreateHttpClient(ApiKey);
                    return ValueTask.CompletedTask;
                },
                afterSuiteAsync: async ct =>
                {
                    if (_Client == null) return;
                    foreach (string id in _CollectionsToDelete.Distinct().ToList())
                    {
                        try { using HttpResponseMessage r = await DeleteAsync(_Client, "/v1.0/tenants/default/collections/" + Uri.EscapeDataString(id)).ConfigureAwait(false); }
                        catch (HttpRequestException) { }
                    }
                    _Client.Dispose();
                    _Client = null;
                },
                cases: new List<TestCaseDescriptor>
                {
                    // ---------- Pure-unit: index identifier ----------

                    Case("IndexIdentifierDistinctForSameMillisecond", "Index id: two ids sharing a timestamp yield different, disjoint index names", ct =>
                    {
                        // The historical bug kept only the base36 millisecond component, so these two ids collided.
                        string a = "col_mufvqeof_AaaaaaaaaaaaaaaaaaaaaaaaaaA";
                        string b = "col_mufvqeof_BbbbbbbbbbbbbbbbbbbbbbbbbbB";

                        AssertTrue(DynamicTableQueries.GetIndexIdentifier(a) != DynamicTableQueries.GetIndexIdentifier(b),
                            "Index identifiers for two same-millisecond ids must differ");

                        HashSet<string> namesA = ExpectedNameSet(a);
                        HashSet<string> namesB = ExpectedNameSet(b);
                        AssertTrue(namesA.Count > 0, "Expected a non-empty index set");
                        AssertTrue(!namesA.Overlaps(namesB), "Index name sets for two same-millisecond ids must be disjoint");
                        return Task.CompletedTask;
                    }),

                    Case("IndexNamesWithinPostgresLimit", "Index id: every generated name stays within 63 bytes, including a 256-char id (hash branch)", ct =>
                    {
                        foreach (string id in new[] { "default", "col_mug74c9r_1zRmmrArVYPxVEltD5RaSwflGe6", new string('a', 256) })
                        {
                            foreach (DynamicTableQueries.CollectionIndexSpec spec in DynamicTableQueries.GetExpectedIndexes(id, includeTsv: true, includeLegacyFts: true))
                                AssertTrue(Encoding.UTF8.GetByteCount(spec.Name) <= 63, "Index name '" + spec.Name + "' exceeds 63 bytes for id length " + id.Length);
                        }

                        // A 256-character id cannot fit in the 48-char identifier, so it must fall back to the short hash.
                        string longId = new string('a', 256);
                        string ident = DynamicTableQueries.GetIndexIdentifier(longId);
                        AssertTrue(ident.StartsWith("h", StringComparison.Ordinal) && ident.Length <= 25, "A long id should use the short hash identifier, got '" + ident + "'");
                        return Task.CompletedTask;
                    }),

                    // ---------- Pure-unit: id validation ----------

                    Case("ResourceIdValidationAcceptsAndRejects", "Id validation: plain identifiers pass; anything that could break out of an identifier fails", ct =>
                    {
                        foreach (string ok in new[] { "default", "col_mug74c9r_1zRmmrArVYPxVEltD5RaSwflGe6", "a", new string('z', 48) })
                            AssertTrue(DynamicTableQueries.IsValidResourceId(ok), "Should accept valid id '" + ok + "'");

                        foreach (string bad in new[] { null, "", "x;drop", "x(a int)", "a b", "a'b", "a\"b", new string('z', 49) })
                            AssertTrue(!DynamicTableQueries.IsValidResourceId(bad), "Should reject invalid id '" + (bad ?? "<null>") + "'");

                        // The table-name sanitizer must reject the same dangerous input rather than silently altering it.
                        bool threw = false;
                        try { DynamicTableQueries.SanitizeTableName("x;drop table y;--"); }
                        catch (ArgumentException) { threw = true; }
                        AssertTrue(threw, "SanitizeTableName should throw on characters outside [A-Za-z0-9_-.]");
                        return Task.CompletedTask;
                    }),

                    // ---------- Integration: create validation and duplicate handling ----------

                    Case("CreateRejectsInvalidId", "Create: a client-supplied id that is not a plain identifier is rejected with 400", async ct =>
                    {
                        foreach (object body in new object[]
                        {
                            new { Id = "x(a int); create table pwn(b int); --", Name = "bad-id-1", Dimensionality = 3 },
                            new { Id = new string('z', 49), Name = "bad-id-2", Dimensionality = 3 }
                        })
                        {
                            using HttpResponseMessage r = await PutAsync(_Client, "/v1.0/tenants/default/collections", body).ConfigureAwait(false);
                            AssertStatusCode(r, HttpStatusCode.BadRequest);
                        }
                    }),

                    Case("CreateDuplicateNameReturns409", "Create: a second collection with the same name returns 409 with the existing id, and no second row is created", async ct =>
                    {
                        string name = "integrity-dup-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                        string firstId = await CreateCollection(name, 3).ConfigureAwait(false);

                        using HttpResponseMessage r = await PutAsync(_Client, "/v1.0/tenants/default/collections", new { Name = name, Dimensionality = 3 }).ConfigureAwait(false);
                        AssertStatusCode(r, HttpStatusCode.Conflict);
                        System.Text.Json.JsonElement err = await ReadResponse<System.Text.Json.JsonElement>(r).ConfigureAwait(false);
                        string context = err.TryGetProperty("Context", out System.Text.Json.JsonElement c) ? c.GetString() : "";
                        AssertTrue(context != null && context.Contains(firstId), "The 409 body should name the existing collection id " + firstId + ", got: " + context);

                        if (TestDbConnectionString != null)
                        {
                            long count = await ScalarLongAsync("SELECT count(*) FROM collections WHERE tenant_id='default' AND name='" + name + "'", ct).ConfigureAwait(false);
                            AssertEqual(1L, count, "Exactly one collection row should exist for the duplicated name");
                        }
                    }),

                    // ---------- Integration: concurrent creation (the original bug) ----------

                    Case("ConcurrentCreateAllHealthy", "Create: 32 concurrent creates with distinct names all succeed and each gets its full index set", async ct =>
                    {
                        const int n = 32;
                        string prefix = "integrity-burst-" + Guid.NewGuid().ToString("N").Substring(0, 6) + "-";

                        // Release all requests together so their ids are as likely as possible to share a millisecond.
                        TaskCompletionSource barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        List<Task<(HttpStatusCode status, string id)>> tasks = new List<Task<(HttpStatusCode, string)>>();
                        for (int i = 0; i < n; i++)
                        {
                            string name = prefix + i;
                            tasks.Add(Task.Run(async () =>
                            {
                                await barrier.Task.ConfigureAwait(false);
                                using HttpResponseMessage r = await PutAsync(_Client, "/v1.0/tenants/default/collections", new { Name = name, Dimensionality = 3 }).ConfigureAwait(false);
                                string id = null;
                                if (r.StatusCode == HttpStatusCode.Created)
                                {
                                    System.Text.Json.JsonElement col = await ReadResponse<System.Text.Json.JsonElement>(r).ConfigureAwait(false);
                                    id = col.GetProperty("Id").GetString();
                                }
                                return (r.StatusCode, id);
                            }, ct));
                        }
                        barrier.SetResult();
                        (HttpStatusCode status, string id)[] results = await Task.WhenAll(tasks).ConfigureAwait(false);

                        foreach ((HttpStatusCode status, string id) in results)
                        {
                            AssertStatusCode2(status, HttpStatusCode.Created, "Every concurrent create should return 201");
                            if (id != null) _CollectionsToDelete.Add(id);
                        }

                        if (TestDbConnectionString != null)
                        {
                            foreach ((HttpStatusCode status, string id) in results)
                            {
                                if (id == null) continue;
                                await AssertFullIndexSetAsync(id, ct).ConfigureAwait(false);
                            }
                        }
                    }),

                    // ---------- Integration: URL-encoded document keys ----------

                    Case("DocumentKeySpecialCharactersRoundTrip", "Documents: keys containing # ? / % space and non-ASCII round-trip through create/read/exists/update/delete", async ct =>
                    {
                        string collectionId = await CreateCollection("integrity-keys-" + Guid.NewGuid().ToString("N").Substring(0, 8), 3).ConfigureAwait(false);
                        string docs = "/v1.0/tenants/default/collections/" + Uri.EscapeDataString(collectionId) + "/documents";

                        string[] keys = new[] { "mem123#0", "a/b/c", "50%off", "hello world", "q?x=1", "café_über" };
                        foreach (string key in keys)
                        {
                            object body = new { DocumentKey = key, Content = "content for " + key, ContentType = "Text", Embeddings = new List<float> { 0.1f, 0.2f, 0.3f } };
                            using (HttpResponseMessage cr = await PutAsync(_Client, docs, body).ConfigureAwait(false))
                                AssertStatusCode(cr, HttpStatusCode.Created);

                            string encoded = docs + "/" + Uri.EscapeDataString(key);

                            using (HttpResponseMessage gr = await GetAsync(_Client, encoded).ConfigureAwait(false))
                            {
                                AssertStatusCode(gr, HttpStatusCode.OK);
                                System.Text.Json.JsonElement doc = await ReadResponse<System.Text.Json.JsonElement>(gr).ConfigureAwait(false);
                                AssertEqual(key, doc.GetProperty("DocumentKey").GetString(), "Round-tripped document key");
                            }
                            using (HttpResponseMessage hr = await HeadAsync(_Client, encoded).ConfigureAwait(false))
                                AssertStatusCode(hr, HttpStatusCode.OK);
                            using (HttpResponseMessage ur = await PutAsync(_Client, encoded, new { DocumentKey = key, Content = "updated " + key, ContentType = "Text", Embeddings = new List<float> { 0.3f, 0.2f, 0.1f } }).ConfigureAwait(false))
                                AssertStatusCode(ur, HttpStatusCode.OK);
                            using (HttpResponseMessage dr = await DeleteAsync(_Client, encoded).ConfigureAwait(false))
                                AssertStatusCode(dr, HttpStatusCode.NoContent);
                            using (HttpResponseMessage gr2 = await GetAsync(_Client, encoded).ConfigureAwait(false))
                                AssertStatusCode(gr2, HttpStatusCode.NotFound);
                        }
                    }),

                    // ---------- Schema (direct database access; opt-in via RECALLDB_TEST_DB) ----------

                    Case("CreateRollsBackOnDdlFailure", "Create: a DDL failure mid-creation leaves no collections row and no tables", async ct =>
                    {
                        if (TestDbConnectionString == null) return;

                        string id = "col_rollback_" + Guid.NewGuid().ToString("N").Substring(0, 12);
                        // A dimensionality above pgvector's 16000-column limit makes the documents-table DDL fail, so the
                        // whole creation transaction (row + tables + indexes) must roll back.
                        using HttpResponseMessage r = await PutAsync(_Client, "/v1.0/tenants/default/collections",
                            new { Id = id, Name = "integrity-rollback-" + Guid.NewGuid().ToString("N").Substring(0, 8), Dimensionality = 20000 }).ConfigureAwait(false);
                        AssertTrue(r.StatusCode != HttpStatusCode.Created, "An oversized dimensionality must not succeed, got " + (int)r.StatusCode);

                        long rows = await ScalarLongAsync("SELECT count(*) FROM collections WHERE id='" + id + "'", ct).ConfigureAwait(false);
                        AssertEqual(0L, rows, "No collections row should survive a rolled-back create");
                        AssertTrue(!await TableExistsAsync("collection_" + id, ct).ConfigureAwait(false), "No documents table should survive a rolled-back create");
                        AssertTrue(!await TableExistsAsync("collection_" + id + "_labels", ct).ConfigureAwait(false), "No labels table should survive a rolled-back create");
                        AssertTrue(!await TableExistsAsync("collection_" + id + "_tags", ct).ConfigureAwait(false), "No tags table should survive a rolled-back create");
                    }),

                    Case("StartupRepairFixesDamagedCollection", "Repair: the startup pass renames old-scheme indexes, builds missing indexes, and creates missing side tables", async ct =>
                    {
                        if (TestDbConnectionString == null) return;

                        string id = "col_dmgclean_" + Guid.NewGuid().ToString("N").Substring(0, 12);
                        string table = "collection_" + id;
                        await BuildDamagedCollectionAsync(id, includeOldHnsw: true, duplicateKeys: false, ct).ConfigureAwait(false);
                        try
                        {
                            using (PostgresqlDatabaseDriver driver = new PostgresqlDatabaseDriver(SettingsFromConnectionString(TestDbConnectionString), null))
                                await driver.EnsureAllCollectionSchemasAsync(ct).ConfigureAwait(false);

                            // Every expected index exists under the new scheme, on all three tables.
                            List<string> docIx = await ListIndexesAsync(table, ct).ConfigureAwait(false);
                            foreach (DynamicTableQueries.CollectionIndexSpec spec in DynamicTableQueries.GetExpectedIndexes(id, includeTsv: true, includeLegacyFts: false))
                            {
                                List<string> onTable = await ListIndexesAsync(spec.Table, ct).ConfigureAwait(false);
                                AssertTrue(onTable.Contains(spec.Name), "Expected index " + spec.Name + " on " + spec.Table + " after repair");
                            }
                            // The old-scheme index name is gone (renamed into the new scheme).
                            AssertTrue(!docIx.Contains("idx_col_dmgtest_hnsw"), "The old-scheme hnsw index should have been renamed away");
                            AssertTrue(await TableExistsAsync(table + "_labels", ct).ConfigureAwait(false), "Labels table should exist after repair");
                            AssertTrue(await TableExistsAsync(table + "_tags", ct).ConfigureAwait(false), "Tags table should exist after repair");
                        }
                        finally
                        {
                            await ExecuteSqlAsync("DROP TABLE IF EXISTS " + table + "_tags; DROP TABLE IF EXISTS " + table + "_labels; DROP TABLE IF EXISTS " + table + "; DELETE FROM collections WHERE id='" + id + "';", ct).ConfigureAwait(false);
                        }
                    }),

                    Case("StartupRepairSkipsUniqueIndexOnDuplicateKeys", "Repair: a duplicate document_key leaves the unique index unbuilt (with the other indexes built)", async ct =>
                    {
                        if (TestDbConnectionString == null) return;

                        string id = "col_dmgdup_" + Guid.NewGuid().ToString("N").Substring(0, 12);
                        string table = "collection_" + id;
                        await BuildDamagedCollectionAsync(id, includeOldHnsw: false, duplicateKeys: true, ct).ConfigureAwait(false);
                        try
                        {
                            using (PostgresqlDatabaseDriver driver = new PostgresqlDatabaseDriver(SettingsFromConnectionString(TestDbConnectionString), null))
                                await driver.EnsureAllCollectionSchemasAsync(ct).ConfigureAwait(false);

                            List<string> docIx = await ListIndexesAsync(table, ct).ConfigureAwait(false);
                            string dkey = "idx_col_" + DynamicTableQueries.GetIndexIdentifier(id) + "_dkey";
                            string hnsw = "idx_col_" + DynamicTableQueries.GetIndexIdentifier(id) + "_hnsw";
                            AssertTrue(!docIx.Contains(dkey), "The unique document_key index must be skipped while duplicates exist");
                            AssertTrue(docIx.Contains(hnsw), "Non-unique indexes should still be built when the unique one is skipped");
                        }
                        finally
                        {
                            await ExecuteSqlAsync("DROP TABLE IF EXISTS " + table + "_tags; DROP TABLE IF EXISTS " + table + "_labels; DROP TABLE IF EXISTS " + table + "; DELETE FROM collections WHERE id='" + id + "';", ct).ConfigureAwait(false);
                        }
                    })
                });
        }

        #endregion

        #region Private-Methods

        private static TestCaseDescriptor Case(string caseId, string displayName, Func<CancellationToken, Task> execute)
        {
            return new TestCaseDescriptor(
                suiteId: "RecallDbCollectionIntegrity",
                caseId: caseId,
                displayName: displayName,
                executeAsync: execute);
        }

        private static HashSet<string> ExpectedNameSet(string id)
        {
            return new HashSet<string>(
                DynamicTableQueries.GetExpectedIndexes(id, includeTsv: true, includeLegacyFts: true).Select(s => s.Name),
                StringComparer.Ordinal);
        }

        private static async Task<string> CreateCollection(string name, int dimensionality)
        {
            using HttpResponseMessage r = await PutAsync(_Client, "/v1.0/tenants/default/collections", new { Name = name, Dimensionality = dimensionality }).ConfigureAwait(false);
            AssertStatusCode(r, HttpStatusCode.Created);
            System.Text.Json.JsonElement col = await ReadResponse<System.Text.Json.JsonElement>(r).ConfigureAwait(false);
            string id = col.GetProperty("Id").GetString();
            AssertNotNullOrEmpty(id, "created collection id");
            _CollectionsToDelete.Add(id);
            return id;
        }

        private static void AssertStatusCode2(HttpStatusCode actual, HttpStatusCode expected, string message)
        {
            if (actual != expected) throw new Exception(message + " (got " + (int)actual + " " + actual + ")");
        }

        private static DatabaseSettings SettingsFromConnectionString(string connectionString)
        {
            NpgsqlConnectionStringBuilder builder = new NpgsqlConnectionStringBuilder(connectionString);
            DatabaseSettings settings = new DatabaseSettings();
            settings.Hostname = builder.Host;
            settings.Port = builder.Port;
            settings.DatabaseName = builder.Database;
            settings.Username = builder.Username;
            settings.Password = builder.Password;
            if (!string.IsNullOrEmpty(builder.SearchPath)) settings.Schema = builder.SearchPath;
            settings.MigrateFullTextColumn = true;
            return settings;
        }

        private static async Task AssertFullIndexSetAsync(string id, CancellationToken token)
        {
            foreach (DynamicTableQueries.CollectionIndexSpec spec in DynamicTableQueries.GetExpectedIndexes(id, includeTsv: true, includeLegacyFts: false))
            {
                List<string> onTable = await ListIndexesAsync(spec.Table, token).ConfigureAwait(false);
                AssertTrue(onTable.Contains(spec.Name), "Concurrently-created collection " + id + " is missing index " + spec.Name);
            }
        }

        private static async Task BuildDamagedCollectionAsync(string id, bool includeOldHnsw, bool duplicateKeys, CancellationToken token)
        {
            string table = "collection_" + id;
            StringBuilder sql = new StringBuilder();
            sql.Append("INSERT INTO collections (id, tenant_id, name, dimensionality, active) VALUES ('")
               .Append(id).Append("','default','damaged',3,true);");
            sql.Append("CREATE TABLE ").Append(table).Append(" (")
               .Append("id BIGSERIAL PRIMARY KEY, document_key VARCHAR(256) NOT NULL, document_id VARCHAR(256), ")
               .Append("content_length BIGINT NOT NULL DEFAULT 0, etag VARCHAR(64), sha256 VARCHAR(64), ")
               .Append("position INTEGER NOT NULL DEFAULT 0, content_type VARCHAR(32) NOT NULL DEFAULT 'Text', ")
               .Append("content TEXT, binary_data BYTEA, embeddings vector(3), ")
               .Append("created_utc TIMESTAMPTZ(6) NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'), ")
               .Append("content_tsv tsvector GENERATED ALWAYS AS (to_tsvector('english', COALESCE(content, ''))) STORED);");
            // A leftover old-scheme index to exercise the rename path (the historical short identifier "dmgtest").
            if (includeOldHnsw)
                sql.Append("CREATE INDEX idx_col_dmgtest_hnsw ON ").Append(table).Append(" USING hnsw (embeddings vector_cosine_ops);");
            if (duplicateKeys)
                sql.Append("INSERT INTO ").Append(table).Append(" (document_key, content) VALUES ('dup','a'),('dup','b');");
            else
                sql.Append("INSERT INTO ").Append(table).Append(" (document_key, content) VALUES ('k1','a'),('k2','b');");
            await ExecuteSqlAsync(sql.ToString(), token).ConfigureAwait(false);
        }

        private static async Task ExecuteSqlAsync(string sql, CancellationToken token)
        {
            using NpgsqlConnection connection = new NpgsqlConnection(TestDbConnectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);
            using NpgsqlCommand command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }

        private static async Task<long> ScalarLongAsync(string sql, CancellationToken token)
        {
            using NpgsqlConnection connection = new NpgsqlConnection(TestDbConnectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);
            using NpgsqlCommand command = new NpgsqlCommand(sql, connection);
            object result = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt64(result);
        }

        private static async Task<bool> TableExistsAsync(string table, CancellationToken token)
        {
            using NpgsqlConnection connection = new NpgsqlConnection(TestDbConnectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);
            using NpgsqlCommand command = new NpgsqlCommand("SELECT to_regclass('" + table + "') IS NOT NULL", connection);
            object result = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
            return result != null && result != DBNull.Value && (bool)result;
        }

        private static async Task<List<string>> ListIndexesAsync(string table, CancellationToken token)
        {
            using NpgsqlConnection connection = new NpgsqlConnection(TestDbConnectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);
            using NpgsqlCommand command = new NpgsqlCommand(
                "SELECT indexname FROM pg_indexes WHERE schemaname = current_schema() AND tablename = '" + table.ToLowerInvariant() + "' ORDER BY indexname", connection);
            using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            List<string> names = new List<string>();
            while (await reader.ReadAsync(token).ConfigureAwait(false)) names.Add(reader.GetString(0));
            return names;
        }

        #endregion
    }
}
