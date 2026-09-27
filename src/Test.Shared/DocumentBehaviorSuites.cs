namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    using static Test.Shared.TestHelpers;

    /// <summary>
    /// Integration tests for write-path status codes and filter semantics: duplicate keys (409, and 400 inside one
    /// batch), updating a missing document (404), update keeping omitted labels and tags, embedding and collection
    /// dimensionality checks (400), LabelFilter.RequiredMode (All by default, or Any), and VectorQuery.EfSearch.
    /// The suite creates its own 3-dimension collection in the default tenant and deletes it at the end.
    /// </summary>
    public static class DocumentBehaviorSuites
    {
        #region Shared-State

        private static HttpClient _Client = null;
        private static string _CollectionId = null;
        private static readonly List<float> _Vec = new List<float> { 1.0f, 0.0f, 0.0f };

        #endregion

        #region Suite

        /// <summary>
        /// The document behavior integration test suite.
        /// </summary>
        public static TestSuiteDescriptor Suite { get; } = Build();

        private static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "RecallDbDocumentBehavior",
                displayName: "RecallDB Write Status Codes, Label Modes, and EfSearch Tests",
                beforeSuiteAsync: async ct =>
                {
                    _Client = CreateHttpClient(ApiKey);
                    await Task.CompletedTask.ConfigureAwait(false);
                },
                afterSuiteAsync: async ct =>
                {
                    if (_Client == null) return;
                    if (!string.IsNullOrEmpty(_CollectionId))
                    {
                        try
                        {
                            using HttpResponseMessage response = await DeleteAsync(_Client, "/v1.0/tenants/default/collections/" + _CollectionId).ConfigureAwait(false);
                        }
                        catch (HttpRequestException)
                        {
                        }
                    }
                    _Client.Dispose();
                    _Client = null;
                },
                cases: new List<TestCaseDescriptor>
                {
                    Case("DocBehaviorSetup", "Behavior: create collection and seed labeled documents", async ct =>
                    {
                        object colBody = new { Name = "DocumentBehaviorTestCollection", Dimensionality = 3 };
                        using HttpResponseMessage colResp = await PutAsync(_Client, "/v1.0/tenants/default/collections", colBody).ConfigureAwait(false);
                        AssertStatusCode(colResp, HttpStatusCode.Created);
                        _CollectionId = (await ReadResponse<JsonElement>(colResp).ConfigureAwait(false)).GetProperty("Id").GetString();

                        // Labels for the RequiredMode cases: la has a, lb has b, lab has a and b, lnone has neither.
                        List<object> seed = new List<object>
                        {
                            Doc("la", "label a only", new[] { "a" }, null),
                            Doc("lb", "label b only", new[] { "b" }, null),
                            Doc("lab", "labels a and b", new[] { "a", "b" }, null),
                            Doc("lnone", "no labels", null, null)
                        };
                        using HttpResponseMessage r = await PostAsync(_Client, Path("/documents/batch"), seed).ConfigureAwait(false);
                        AssertStatusCode(r, HttpStatusCode.Created);
                    }),

                    // ----- Duplicate keys -----

                    Case("DocCreateUniqueKey", "Create: a new key is created (201)", async ct =>
                    {
                        using HttpResponseMessage r = await PutAsync(_Client, Path("/documents"), Doc("dup-1", "first", null, null)).ConfigureAwait(false);
                        AssertStatusCode(r, HttpStatusCode.Created);
                    }),

                    Case("DocCreateDuplicateKeyConflict", "Create: an existing key is a 409 naming the key, and the stored document is unchanged", async ct =>
                    {
                        using HttpResponseMessage r = await PutAsync(_Client, Path("/documents"), Doc("dup-1", "second", null, null)).ConfigureAwait(false);
                        AssertStatusCode(r, HttpStatusCode.Conflict);
                        AssertTrue((await r.Content.ReadAsStringAsync().ConfigureAwait(false)).Contains("dup-1"), "The 409 names the key");
                        JsonElement stored = await ReadDoc("dup-1").ConfigureAwait(false);
                        AssertEqual("first", stored.GetProperty("Content").GetString(), "The original document is untouched");
                    }),

                    Case("DocBatchDuplicateWithinBatch", "Batch: a key repeated inside the batch is a 400 and nothing is stored", async ct =>
                    {
                        using HttpResponseMessage r = await PostAsync(_Client, Path("/documents/batch"), new List<object> { Doc("bd-1", "one", null, null), Doc("bd-1", "two", null, null) }).ConfigureAwait(false);
                        AssertStatusCode(r, HttpStatusCode.BadRequest);
                        AssertTrue((await r.Content.ReadAsStringAsync().ConfigureAwait(false)).Contains("bd-1"), "The 400 names the key");
                        await AssertMissing("bd-1").ConfigureAwait(false);
                    }),

                    Case("DocBatchConflictWithExisting", "Batch: a key that already exists is a 409 naming it, and the batch is rolled back", async ct =>
                    {
                        using HttpResponseMessage r = await PostAsync(_Client, Path("/documents/batch"), new List<object> { Doc("bc-new", "new", null, null), Doc("dup-1", "clash", null, null) }).ConfigureAwait(false);
                        AssertStatusCode(r, HttpStatusCode.Conflict);
                        string body = await r.Content.ReadAsStringAsync().ConfigureAwait(false);
                        AssertTrue(body.Contains("dup-1"), "The 409 names the existing key");
                        AssertTrue(!body.Contains("bc-new"), "The 409 does not name the new key");
                        await AssertMissing("bc-new").ConfigureAwait(false);
                    }),

                    Case("DocBatchUniqueKeysCreated", "Batch: distinct new keys are created (201)", async ct =>
                    {
                        using HttpResponseMessage r = await PostAsync(_Client, Path("/documents/batch"), new List<object> { Doc("bu-1", "one", null, null), Doc("bu-2", "two", null, null) }).ConfigureAwait(false);
                        AssertStatusCode(r, HttpStatusCode.Created);
                        await ReadDoc("bu-1").ConfigureAwait(false);
                        await ReadDoc("bu-2").ConfigureAwait(false);
                    }),

                    // ----- Update -----

                    Case("DocUpdateMissingNotFound", "Update: a key that does not exist is a 404 and nothing is created", async ct =>
                    {
                        using HttpResponseMessage r = await PutAsync(_Client, Path("/documents/no-such-doc"), new { Content = "x" }).ConfigureAwait(false);
                        AssertStatusCode(r, HttpStatusCode.NotFound);
                        await AssertMissing("no-such-doc").ConfigureAwait(false);
                    }),

                    Case("DocUpdateExistingOk", "Update: an existing key is updated (200) and keeps its CreatedUtc", async ct =>
                    {
                        JsonElement before = await ReadDoc("bu-1").ConfigureAwait(false);
                        using HttpResponseMessage r = await PutAsync(_Client, Path("/documents/bu-1"), new { Content = "one, revised", Embeddings = _Vec }).ConfigureAwait(false);
                        AssertStatusCode(r, HttpStatusCode.OK);
                        JsonElement after = await ReadDoc("bu-1").ConfigureAwait(false);
                        AssertEqual("one, revised", after.GetProperty("Content").GetString(), "Content updated");
                        AssertEqual(before.GetProperty("CreatedUtc").GetString(), after.GetProperty("CreatedUtc").GetString(), "CreatedUtc unchanged");
                    }),

                    Case("DocUpdateOmittedLabelsTagsKept", "Update: omitting Labels and Tags keeps the existing ones", async ct =>
                    {
                        using HttpResponseMessage c = await PutAsync(_Client, Path("/documents"), Doc("up-keep", "v1", new[] { "keep" }, new Dictionary<string, string> { { "t", "v" } })).ConfigureAwait(false);
                        AssertStatusCode(c, HttpStatusCode.Created);
                        using HttpResponseMessage r = await PutAsync(_Client, Path("/documents/up-keep"), new { Content = "v2", Embeddings = _Vec }).ConfigureAwait(false);
                        AssertStatusCode(r, HttpStatusCode.OK);
                        JsonElement doc = await ReadDoc("up-keep").ConfigureAwait(false);
                        AssertEqual("keep", string.Join(",", Labels(doc)), "Labels kept");
                        AssertEqual("v", doc.GetProperty("Tags").GetProperty("t").GetString(), "Tags kept");
                    }),

                    Case("DocUpdateProvidedLabelsTagsReplace", "Update: provided Labels and Tags replace the existing ones", async ct =>
                    {
                        using HttpResponseMessage r = await PutAsync(_Client, Path("/documents/up-keep"), new { Content = "v3", Embeddings = _Vec, Labels = new[] { "new" }, Tags = new Dictionary<string, string> { { "u", "w" } } }).ConfigureAwait(false);
                        AssertStatusCode(r, HttpStatusCode.OK);
                        JsonElement doc = await ReadDoc("up-keep").ConfigureAwait(false);
                        AssertEqual("new", string.Join(",", Labels(doc)), "Labels replaced");
                        AssertTrue(!doc.GetProperty("Tags").TryGetProperty("t", out _), "Old tag removed");
                        AssertEqual("w", doc.GetProperty("Tags").GetProperty("u").GetString(), "New tag stored");
                    }),

                    Case("DocUpdateEmptyLabelsTagsClear", "Update: empty Labels and Tags remove them", async ct =>
                    {
                        using HttpResponseMessage r = await PutAsync(_Client, Path("/documents/up-keep"), new { Content = "v4", Embeddings = _Vec, Labels = new string[0], Tags = new Dictionary<string, string>() }).ConfigureAwait(false);
                        AssertStatusCode(r, HttpStatusCode.OK);
                        JsonElement doc = await ReadDoc("up-keep").ConfigureAwait(false);
                        AssertEqual(0, Labels(doc).Count, "Labels cleared");
                        AssertEqual(0, doc.GetProperty("Tags").EnumerateObject().Count(), "Tags cleared");
                    }),

                    Case("DocUpdateWrongDimensions", "Update: embeddings of the wrong length are a 400 and the document is unchanged", async ct =>
                    {
                        using HttpResponseMessage r = await PutAsync(_Client, Path("/documents/up-keep"), new { Content = "v5", Embeddings = new List<float> { 1.0f, 0.0f } }).ConfigureAwait(false);
                        AssertStatusCode(r, HttpStatusCode.BadRequest);
                        AssertEqual("v4", (await ReadDoc("up-keep").ConfigureAwait(false)).GetProperty("Content").GetString(), "Document unchanged");
                    }),

                    // ----- Dimensionality -----

                    Case("SearchWrongDimensions", "Search: a query vector of the wrong length is a 400; the right length is a 200", async ct =>
                    {
                        using HttpResponseMessage bad = await PostAsync(_Client, Path("/search"), new { Vector = new { Embeddings = new List<float> { 1.0f, 0.0f } }, MaxResults = 5 }).ConfigureAwait(false);
                        AssertStatusCode(bad, HttpStatusCode.BadRequest);
                        AssertTrue((await bad.Content.ReadAsStringAsync().ConfigureAwait(false)).Contains("dimensions"), "The 400 explains the dimension mismatch");
                        using HttpResponseMessage good = await PostAsync(_Client, Path("/search"), new { Vector = new { Embeddings = _Vec }, MaxResults = 5 }).ConfigureAwait(false);
                        AssertStatusCode(good, HttpStatusCode.OK);
                    }),

                    Case("CollectionDimensionalityLimit", "Collection: 2000 dimensions are accepted; 2001 and 3072 are a 400", async ct =>
                    {
                        string stamp = DateTime.UtcNow.Ticks.ToString();
                        using HttpResponseMessage ok = await PutAsync(_Client, "/v1.0/tenants/default/collections", new { Name = "DimLimitOk-" + stamp, Dimensionality = 2000 }).ConfigureAwait(false);
                        AssertStatusCode(ok, HttpStatusCode.Created);
                        string okId = (await ReadResponse<JsonElement>(ok).ConfigureAwait(false)).GetProperty("Id").GetString();
                        using (HttpResponseMessage del = await DeleteAsync(_Client, "/v1.0/tenants/default/collections/" + okId).ConfigureAwait(false)) { }

                        foreach (int dims in new[] { 2001, 3072 })
                        {
                            using HttpResponseMessage bad = await PutAsync(_Client, "/v1.0/tenants/default/collections", new { Name = "DimLimitBad-" + dims + "-" + stamp, Dimensionality = dims }).ConfigureAwait(false);
                            AssertStatusCode(bad, HttpStatusCode.BadRequest);
                            AssertTrue((await bad.Content.ReadAsStringAsync().ConfigureAwait(false)).Contains("2000"), "The 400 states the limit");
                        }
                    }),

                    // ----- LabelFilter.RequiredMode -----

                    Case("LabelRequiredDefaultIsAll", "Labels: Required with no mode needs every label", async ct =>
                    {
                        List<string> keys = await SearchKeys(new { Required = new[] { "a", "b" } }).ConfigureAwait(false);
                        AssertEqual("lab", string.Join(",", keys), "Only the document with both labels");
                    }),

                    Case("LabelRequiredModeAll", "Labels: RequiredMode All needs every label", async ct =>
                    {
                        List<string> keys = await SearchKeys(new { Required = new[] { "a", "b" }, RequiredMode = "All" }).ConfigureAwait(false);
                        AssertEqual("lab", string.Join(",", keys), "Only the document with both labels");
                    }),

                    Case("LabelRequiredModeAny", "Labels: RequiredMode Any needs at least one label", async ct =>
                    {
                        List<string> keys = await SearchKeys(new { Required = new[] { "a", "b" }, RequiredMode = "Any" }).ConfigureAwait(false);
                        AssertEqual("la,lab,lb", string.Join(",", keys), "Every document with a or b");
                    }),

                    Case("LabelRequiredSingleSameInBothModes", "Labels: a single required label behaves the same in both modes", async ct =>
                    {
                        string all = string.Join(",", await SearchKeys(new { Required = new[] { "a" }, RequiredMode = "All" }).ConfigureAwait(false));
                        string any = string.Join(",", await SearchKeys(new { Required = new[] { "a" }, RequiredMode = "Any" }).ConfigureAwait(false));
                        AssertEqual("la,lab", all, "All with one label");
                        AssertEqual(all, any, "Any with one label");
                    }),

                    Case("LabelRequiredDuplicateLabelsAll", "Labels: repeating a label in Required does not make All impossible", async ct =>
                    {
                        List<string> keys = await SearchKeys(new { Required = new[] { "a", "a", "b" } }).ConfigureAwait(false);
                        AssertEqual("lab", string.Join(",", keys), "Duplicates are counted once");
                    }),

                    Case("LabelRequiredAllWithExcluded", "Labels: All combines with Excluded", async ct =>
                    {
                        List<string> keys = await SearchKeys(new { Required = new[] { "a" }, Excluded = new[] { "b" } }).ConfigureAwait(false);
                        AssertEqual("la", string.Join(",", keys), "a but not b");
                    }),

                    Case("LabelRequiredModeEnumerate", "Labels: enumeration honors RequiredMode", async ct =>
                    {
                        string all = string.Join(",", await EnumerateKeys(new { Required = new[] { "a", "b" } }).ConfigureAwait(false));
                        string any = string.Join(",", await EnumerateKeys(new { Required = new[] { "a", "b" }, RequiredMode = "Any" }).ConfigureAwait(false));
                        AssertEqual("lab", all, "Enumerate All");
                        AssertEqual("la,lab,lb", any, "Enumerate Any");
                    }),

                    Case("LabelRequiredModeInvalid", "Labels: an unknown RequiredMode is a 400", async ct =>
                    {
                        using HttpResponseMessage r = await PostAsync(_Client, Path("/search"), new { Vector = new { Embeddings = _Vec }, LabelFilter = new { Required = new[] { "a" }, RequiredMode = "Some" } }).ConfigureAwait(false);
                        AssertStatusCode(r, HttpStatusCode.BadRequest);
                    }),

                    Case("LabelRequiredModeDeleteByFilter", "Labels: delete-by-filter honors RequiredMode All", async ct =>
                    {
                        List<object> extra = new List<object> { Doc("del-a", "x", new[] { "da" }, null), Doc("del-ab", "x", new[] { "da", "db" }, null) };
                        using (HttpResponseMessage c = await PostAsync(_Client, Path("/documents/batch"), extra).ConfigureAwait(false)) AssertStatusCode(c, HttpStatusCode.Created);
                        using HttpResponseMessage r = await PostAsync(_Client, Path("/documents/delete/filter"), new { LabelFilter = new { Required = new[] { "da", "db" } } }).ConfigureAwait(false);
                        AssertStatusCode(r, HttpStatusCode.OK);
                        AssertEqual(1L, (await ReadResponse<JsonElement>(r).ConfigureAwait(false)).GetProperty("DocumentsDeleted").GetInt64(), "Only the document with both labels is deleted");
                        await ReadDoc("del-a").ConfigureAwait(false);
                        await AssertMissing("del-ab").ConfigureAwait(false);
                    }),

                    // ----- EfSearch and vector-only depth -----

                    Case("VectorEfSearchSeedMany", "EfSearch: seed 120 more documents", async ct =>
                    {
                        List<object> many = new List<object>();
                        for (int i = 0; i < 120; i++)
                        {
                            double angle = i * 0.01;
                            many.Add(new { DocumentKey = "ef-" + i, Content = "vector " + i, ContentType = "Text", Embeddings = new List<float> { (float)Math.Cos(angle), (float)Math.Sin(angle), 0.0f } });
                        }
                        using HttpResponseMessage r = await PostAsync(_Client, Path("/documents/batch"), many).ConfigureAwait(false);
                        AssertStatusCode(r, HttpStatusCode.Created);
                    }),

                    Case("VectorOnlyBeyondForty", "EfSearch: a vector-only search returns more than 40 hits and pages past 40", async ct =>
                    {
                        JsonElement page1 = await Search(new { Vector = new { Embeddings = _Vec }, MaxResults = 100 }).ConfigureAwait(false);
                        AssertEqual(100, GetDocs(page1).GetArrayLength(), "MaxResults 100 returns 100 hits");
                        AssertTrue(!page1.GetProperty("EndOfResults").GetBoolean(), "More pages remain");

                        JsonElement page2 = await Search(new { Vector = new { Embeddings = _Vec }, MaxResults = 40, ContinuationToken = "40" }).ConfigureAwait(false);
                        AssertEqual(40, GetDocs(page2).GetArrayLength(), "The page at offset 40 is full");
                    }),

                    Case("VectorEfSearchAcceptedAndClamped", "EfSearch: in-range values are accepted, and out-of-range values are clamped rather than rejected", async ct =>
                    {
                        foreach (int ef in new[] { 1, 40, 200, 1000, 0, -5, 5000 })
                        {
                            JsonElement json = await Search(new { Vector = new { Embeddings = _Vec, EfSearch = ef }, MaxResults = 10 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(json).GetArrayLength() > 0, "EfSearch " + ef + " returns hits");
                        }
                        JsonElement hybrid = await Search(new { Vector = new { Embeddings = _Vec, EfSearch = 500 }, FullText = new { Query = "vector" }, MaxResults = 10 }).ConfigureAwait(false);
                        AssertTrue(GetDocs(hybrid).GetArrayLength() > 0, "Hybrid honors EfSearch");
                        JsonElement collapsed = await Search(new { Vector = new { Embeddings = _Vec, EfSearch = 500 }, Collapse = new { Field = "DocumentId" }, MaxResults = 10 }).ConfigureAwait(false);
                        AssertTrue(GetDocs(collapsed).GetArrayLength() > 0, "Collapsed search honors EfSearch");
                    }),

                    Case("VectorEfSearchNotANumber", "EfSearch: a non-numeric value is a 400", async ct =>
                    {
                        using HttpResponseMessage r = await PostAsync(_Client, Path("/search"), new { Vector = new { Embeddings = _Vec, EfSearch = "lots" } }).ConfigureAwait(false);
                        AssertStatusCode(r, HttpStatusCode.BadRequest);
                    }),

                    Case("DocBehaviorCleanup", "Behavior: delete collection", async ct =>
                    {
                        if (string.IsNullOrEmpty(_CollectionId)) return;
                        using HttpResponseMessage response = await DeleteAsync(_Client, "/v1.0/tenants/default/collections/" + _CollectionId).ConfigureAwait(false);
                        AssertStatusCode(response, HttpStatusCode.NoContent);
                        _CollectionId = null;
                    })
                });
        }

        #endregion

        #region Private-Methods

        private static TestCaseDescriptor Case(string caseId, string displayName, Func<CancellationToken, Task> execute)
        {
            return new TestCaseDescriptor(
                suiteId: "RecallDbDocumentBehavior",
                caseId: caseId,
                displayName: displayName,
                executeAsync: execute);
        }

        private static object Doc(string key, string content, string[] labels, Dictionary<string, string> tags)
        {
            return new
            {
                DocumentKey = key,
                Content = content,
                ContentType = "Text",
                Embeddings = _Vec,
                Labels = labels,
                Tags = tags
            };
        }

        private static string Path(string suffix)
        {
            AssertNotNullOrEmpty(_CollectionId, "Behavior collection (DocBehaviorSetup must run first)");
            return "/v1.0/tenants/default/collections/" + _CollectionId + suffix;
        }

        private static async Task<JsonElement> ReadDoc(string key)
        {
            using HttpResponseMessage r = await GetAsync(_Client, Path("/documents/" + Uri.EscapeDataString(key))).ConfigureAwait(false);
            AssertStatusCode(r, HttpStatusCode.OK);
            return await ReadResponse<JsonElement>(r).ConfigureAwait(false);
        }

        private static async Task AssertMissing(string key)
        {
            using HttpResponseMessage r = await HeadAsync(_Client, Path("/documents/" + Uri.EscapeDataString(key))).ConfigureAwait(false);
            AssertStatusCode(r, HttpStatusCode.NotFound);
        }

        private static async Task<JsonElement> Search(object body)
        {
            using HttpResponseMessage r = await PostAsync(_Client, Path("/search"), body).ConfigureAwait(false);
            AssertStatusCode(r, HttpStatusCode.OK);
            return await ReadResponse<JsonElement>(r).ConfigureAwait(false);
        }

        private static async Task<List<string>> SearchKeys(object labelFilter)
        {
            // Only the seeded label documents carry labels a and b, so the result is exactly the label match set.
            JsonElement json = await Search(new { Vector = new { Embeddings = _Vec }, LabelFilter = labelFilter, MaxResults = 100 }).ConfigureAwait(false);
            return GetDocs(json).EnumerateArray().Select(d => d.GetProperty("DocumentKey").GetString()).OrderBy(k => k, StringComparer.Ordinal).ToList();
        }

        private static async Task<List<string>> EnumerateKeys(object labelFilter)
        {
            using HttpResponseMessage r = await PostAsync(_Client, Path("/documents/enumerate"), new { LabelFilter = labelFilter, MaxResults = 100 }).ConfigureAwait(false);
            AssertStatusCode(r, HttpStatusCode.OK);
            JsonElement json = await ReadResponse<JsonElement>(r).ConfigureAwait(false);
            return GetObjects(json).EnumerateArray().Select(d => d.GetProperty("DocumentKey").GetString()).OrderBy(k => k, StringComparer.Ordinal).ToList();
        }

        private static List<string> Labels(JsonElement doc)
        {
            if (!doc.TryGetProperty("Labels", out JsonElement labels) || labels.ValueKind != JsonValueKind.Array) return new List<string>();
            return labels.EnumerateArray().Select(l => l.GetString()).ToList();
        }

        #endregion
    }
}
