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
    /// Integration tests for the hybrid recency signal (Hybrid.RecencyWeight), result collapse (SearchQuery.Collapse),
    /// and FullText.MinimumShouldMatch. The suite creates its own collection in the default tenant, so the fixed
    /// baselines of the main and hybrid suites are unaffected. The seed is two chunked parents with explicit write
    /// times: p-old (2 chunks) and p-new (3 chunks, written later), whose first two chunks are exact duplicates of
    /// p-old's in vector and text, so without recency p-old wins every tie (lower id) and with recency p-new does.
    /// p-loose has no DocumentId and no parentKey tag, to exercise the document_key fallback. m-two and m-one share
    /// two and one terms of the query "alpha beta", for MinimumShouldMatch.
    /// </summary>
    public static class SearchGroupingSuites
    {
        #region Shared-State

        private static HttpClient _Client = null;
        private static string _CollectionId = null;
        private static readonly List<float> _QueryVector = new List<float> { 1.0f, 0.0f, 0.0f };
        private const string _Query = "signing key rotation";

        #endregion

        #region Suite

        /// <summary>
        /// The recency, collapse, and minimum-should-match integration test suite.
        /// </summary>
        public static TestSuiteDescriptor Suite { get; } = Build();

        private static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "RecallDbSearchGrouping",
                displayName: "RecallDB Recency, Collapse, and Minimum-Should-Match Tests",
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
                    Case("SearchGroupingSetup", "Grouping: create collection and seed chunked parents", async ct =>
                    {
                        object colBody = new { Name = "SearchGroupingTestCollection", Description = "Recency, collapse, and minimum-should-match tests", Dimensionality = 3 };
                        using HttpResponseMessage colResp = await PutAsync(_Client, "/v1.0/tenants/default/collections", colBody).ConfigureAwait(false);
                        AssertStatusCode(colResp, HttpStatusCode.Created);
                        JsonElement col = await ReadResponse<JsonElement>(colResp).ConfigureAwait(false);
                        _CollectionId = col.GetProperty("Id").GetString();
                        AssertNotNullOrEmpty(_CollectionId, "Grouping collection Id");

                        DateTime older = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                        DateTime newer = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
                        DateTime oldest = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

                        // p-old first so its chunks get the lower ids and win exact ties when recency is off.
                        List<object> first = new List<object>
                        {
                            Chunk("p-old-c0", "p-old", 0, "Rotate the signing key every quarter", 0.9f, 0.1f, 0.0f, older),
                            Chunk("p-old-c1", "p-old", 1, "Signing key storage lives in the vault", 0.8f, 0.3f, 0.0f, older)
                        };
                        List<object> second = new List<object>
                        {
                            Chunk("p-new-c0", "p-new", 0, "Rotate the signing key every quarter", 0.9f, 0.1f, 0.0f, newer),
                            Chunk("p-new-c1", "p-new", 1, "Signing key storage lives in the vault", 0.8f, 0.3f, 0.0f, newer),
                            Chunk("p-new-c2", "p-new", 2, "Key rotation checklist for operators", 0.7f, 0.4f, 0.1f, newer),
                            new { DocumentKey = "p-loose", Content = "Loose notes about key rotation in gardening sheds", ContentType = "Text", Embeddings = new List<float> { 0.2f, 0.9f, 0.3f }, CreatedUtc = oldest },
                            new { DocumentKey = "m-two", DocumentId = "m-two", Content = "Alpha and beta release notes", ContentType = "Text", Embeddings = new List<float> { 0.0f, 0.0f, 1.0f }, CreatedUtc = oldest },
                            new { DocumentKey = "m-one", DocumentId = "m-one", Content = "Alpha only draft", ContentType = "Text", Embeddings = new List<float> { 0.0f, 0.1f, 1.0f }, CreatedUtc = oldest }
                        };

                        using HttpResponseMessage r1 = await PostAsync(_Client, GroupingPath("/documents/batch"), first).ConfigureAwait(false);
                        AssertStatusCode(r1, HttpStatusCode.Created);
                        using HttpResponseMessage r2 = await PostAsync(_Client, GroupingPath("/documents/batch"), second).ConfigureAwait(false);
                        AssertStatusCode(r2, HttpStatusCode.Created);
                    }),

                    // ----- Recency -----

                    Case("SearchHybridRecencyZeroUnchanged", "Recency: RecencyWeight 0 returns exactly what a request without it returns", async ct =>
                    {
                        JsonElement without = await Search(new { Vector = Vec(), FullText = new { Query = _Query }, MaxResults = 50 }).ConfigureAwait(false);
                        JsonElement zero = await Search(new { Vector = Vec(), FullText = new { Query = _Query }, Hybrid = new { RecencyWeight = 0.0 }, MaxResults = 50 }).ConfigureAwait(false);
                        AssertEqual(string.Join(",", Keys(without)), string.Join(",", Keys(zero)), "Same ids and order");
                        List<JsonElement> a = Docs(without);
                        List<JsonElement> b = Docs(zero);
                        for (int i = 0; i < a.Count; i++)
                        {
                            AssertTrue(Math.Abs(a[i].GetProperty("Score").GetDouble() - b[i].GetProperty("Score").GetDouble()) < 1e-12, "Same scores");
                            AssertTrue(!GetInt(b[i], "RecencyRank").HasValue, "No RecencyRank when recency is off");
                        }
                    }),

                    Case("SearchHybridRecencyBreaksTie", "Recency: a positive weight ranks the newer duplicate chunk first", async ct =>
                    {
                        JsonElement off = await Search(new { Vector = Vec(), FullText = new { Query = _Query }, MaxResults = 50 }).ConfigureAwait(false);
                        List<string> offKeys = Keys(off);
                        AssertTrue(offKeys.IndexOf("p-old-c0") < offKeys.IndexOf("p-new-c0"), "Without recency the older duplicate wins the tie, got " + string.Join(",", offKeys));

                        // The duplicate trails by one rank in each leg, so recency must outweigh both legs to flip it:
                        // with k = 1 and r = 1, p-old-c0 scores (0.25 + 0.25 + 1/5) and p-new-c0 (1/6 + 1/6 + 1/2).
                        JsonElement on = await Search(new { Vector = Vec(), FullText = new { Query = _Query }, Hybrid = new { RrfK = 1, RecencyWeight = 1.0 }, MaxResults = 50 }).ConfigureAwait(false);
                        List<string> onKeys = Keys(on);
                        AssertTrue(onKeys.IndexOf("p-new-c0") < onKeys.IndexOf("p-old-c0"), "With recency the newer duplicate wins, got " + string.Join(",", onKeys));
                        AssertTrue(GetInt(FindDoc(on, "p-new-c0"), "RecencyRank") < GetInt(FindDoc(on, "p-old-c0"), "RecencyRank"), "The newer chunk has the better recency rank");
                    }),

                    Case("SearchHybridRecencyNormalized", "Recency: scores follow the three-signal formula and stay in [0, 1]", async ct =>
                    {
                        double w = 0.5;
                        double r = 0.2;
                        int k = 60;
                        JsonElement json = await Search(new { Vector = Vec(), FullText = new { Query = _Query, TextWeight = w }, Hybrid = new { RrfK = k, RecencyWeight = r }, MaxResults = 50 }).ConfigureAwait(false);
                        foreach (JsonElement doc in Docs(json))
                        {
                            double score = doc.GetProperty("Score").GetDouble();
                            AssertTrue(score >= 0.0 && score <= 1.0 + 1e-9, "Score in [0, 1], got " + score);
                            int? vr = GetInt(doc, "VectorRank");
                            int? tr = GetInt(doc, "TextRank");
                            int? rr = GetInt(doc, "RecencyRank");
                            AssertTrue(rr.HasValue, "Every hit carries RecencyRank");
                            double raw = (vr.HasValue ? (1 - w) / (k + vr.Value) : 0) + (tr.HasValue ? w / (k + tr.Value) : 0) + r / (k + rr.Value);
                            double expected = raw * (k + 1) / (1 + r);
                            AssertTrue(Math.Abs(score - expected) < 1e-9, "Score should equal the formula (" + expected + "), got " + score);
                        }
                    }),

                    Case("SearchHybridRecencyRankShared", "Recency: collapsed groups have dense, distinct recency ranks from 1", async ct =>
                    {
                        JsonElement json = await Search(new { Vector = Vec(), FullText = new { Query = _Query }, Hybrid = new { RecencyWeight = 0.1 }, Collapse = new { Field = "Tag", TagKey = "parentKey" }, MaxResults = 50 }).ConfigureAwait(false);
                        List<int> ranks = Docs(json).Select(d => GetInt(d, "RecencyRank") ?? -1).OrderBy(x => x).ToList();
                        AssertEqual(string.Join(",", Enumerable.Range(1, ranks.Count)), string.Join(",", ranks), "Recency ranks are dense from 1");
                        AssertEqual(1, GetInt(FindGroup(json, "p-new"), "RecencyRank") ?? -1, "The newest group has RecencyRank 1");
                        AssertEqual(2, GetInt(FindGroup(json, "p-old"), "RecencyRank") ?? -1, "The next newest group has RecencyRank 2");
                    }),

                    Case("SearchHybridRecencyIgnoredForLinear", "Recency: Linear ignores RecencyWeight with a Notice", async ct =>
                    {
                        JsonElement json = await Search(new { Vector = Vec(), FullText = new { Query = _Query }, Hybrid = new { Strategy = "Linear", RecencyWeight = 0.3 }, MaxResults = 10 }).ConfigureAwait(false);
                        AssertTrue(Docs(json).Count > 0, "The search still runs");
                        AssertTrue(Docs(json).All(d => !GetInt(d, "RecencyRank").HasValue), "No RecencyRank for Linear");
                        AssertTrue(NoticeContains(json, "RecencyWeight applies only to the Rrf strategy"), "Notice should say recency was ignored");
                    }),

                    // ----- Collapse -----

                    Case("SearchCollapseTagOneHitPerParent", "Collapse: hybrid by tag returns one hit per parent with GroupHits", async ct =>
                    {
                        JsonElement json = await Search(new { Vector = Vec(), FullText = new { Query = _Query }, Collapse = new { Field = "Tag", TagKey = "parentKey" }, MaxResults = 50 }).ConfigureAwait(false);
                        List<string> groups = Docs(json).Select(d => d.GetProperty("GroupKey").GetString()).ToList();
                        AssertEqual(groups.Count, groups.Distinct(StringComparer.Ordinal).Count(), "No two hits share a GroupKey");
                        AssertEqual(3, GetInt(FindGroup(json, "p-new"), "GroupHits") ?? -1, "p-new has three candidate chunks");
                        AssertEqual(2, GetInt(FindGroup(json, "p-old"), "GroupHits") ?? -1, "p-old has two candidate chunks");
                        AssertEqual((long)groups.Count, json.GetProperty("TotalRecords").GetInt64(), "TotalRecords counts groups");
                    }),

                    Case("SearchCollapseRepresentativeIsBest", "Collapse: each group's hit is its highest-scoring chunk", async ct =>
                    {
                        JsonElement flat = await Search(new { Vector = Vec(), FullText = new { Query = _Query }, MaxResults = 50 }).ConfigureAwait(false);
                        JsonElement collapsed = await Search(new { Vector = Vec(), FullText = new { Query = _Query }, Collapse = new { Field = "Tag", TagKey = "parentKey" }, MaxResults = 50 }).ConfigureAwait(false);
                        foreach (JsonElement hit in Docs(collapsed))
                        {
                            string group = hit.GetProperty("GroupKey").GetString();
                            JsonElement best = Docs(flat).First(d => ParentOf(d) == group);
                            AssertEqual(best.GetProperty("DocumentKey").GetString(), hit.GetProperty("DocumentKey").GetString(), "Representative of " + group);
                            AssertTrue(Math.Abs(best.GetProperty("Score").GetDouble() - hit.GetProperty("Score").GetDouble()) < 1e-12, "Representative keeps its fused score");
                        }
                    }),

                    Case("SearchCollapseCountsGroups", "Collapse: MaxResults and continuation tokens page by group", async ct =>
                    {
                        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                        string token = null;
                        long total = -1;
                        for (int pages = 0; pages < 20; pages++)
                        {
                            JsonElement page = await Search(new { Vector = Vec(), FullText = new { Query = _Query }, Collapse = new { Field = "Tag", TagKey = "parentKey" }, MaxResults = 1, ContinuationToken = token }).ConfigureAwait(false);
                            AssertTrue(Docs(page).Count <= 1, "MaxResults 1 returns at most one group");
                            total = page.GetProperty("TotalRecords").GetInt64();
                            foreach (JsonElement d in Docs(page))
                                AssertTrue(seen.Add(d.GetProperty("GroupKey").GetString()), "Pages should not repeat a group");
                            if (page.GetProperty("EndOfResults").GetBoolean()) break;
                            token = page.GetProperty("ContinuationToken").GetString();
                        }
                        AssertEqual(total, (long)seen.Count, "Paging visits every group exactly once");
                        AssertTrue(seen.Contains("p-old") && seen.Contains("p-new"), "Both parents are visited");
                    }),

                    Case("SearchCollapseUntaggedFallsBack", "Collapse: a document without the tag is its own group keyed by DocumentKey", async ct =>
                    {
                        JsonElement json = await Search(new { Vector = Vec(), FullText = new { Query = _Query }, Collapse = new { Field = "Tag", TagKey = "parentKey" }, MaxResults = 50 }).ConfigureAwait(false);
                        JsonElement loose = FindGroup(json, "p-loose");
                        AssertEqual("p-loose", loose.GetProperty("DocumentKey").GetString(), "p-loose is its own group");
                        AssertEqual(1, GetInt(loose, "GroupHits") ?? -1, "p-loose GroupHits");
                    }),

                    Case("SearchCollapseDocumentIdMatchesTag", "Collapse: DocumentId and Tag grouping agree on the seed", async ct =>
                    {
                        JsonElement byTag = await Search(new { Vector = Vec(), FullText = new { Query = _Query }, Collapse = new { Field = "Tag", TagKey = "parentKey" }, MaxResults = 50 }).ConfigureAwait(false);
                        JsonElement byId = await Search(new { Vector = Vec(), FullText = new { Query = _Query }, Collapse = new { Field = "DocumentId" }, MaxResults = 50 }).ConfigureAwait(false);
                        string tagGroups = string.Join(",", Docs(byTag).Where(d => d.GetProperty("GroupKey").GetString().StartsWith("p-", StringComparison.Ordinal)).Select(d => d.GetProperty("GroupKey").GetString()));
                        string idGroups = string.Join(",", Docs(byId).Where(d => d.GetProperty("GroupKey").GetString().StartsWith("p-", StringComparison.Ordinal)).Select(d => d.GetProperty("GroupKey").GetString()));
                        AssertEqual(tagGroups, idGroups, "Same ordered group list");
                    }),

                    Case("SearchCollapseVectorOnly", "Collapse: vector-only returns one hit per group, nearest first, thresholds in SQL", async ct =>
                    {
                        JsonElement json = await Search(new { Vector = Vec(), Collapse = new { Field = "DocumentId" }, MaxResults = 50 }).ConfigureAwait(false);
                        List<JsonElement> docs = Docs(json);
                        List<string> groups = docs.Select(d => d.GetProperty("GroupKey").GetString()).ToList();
                        AssertEqual(groups.Count, groups.Distinct(StringComparer.Ordinal).Count(), "One hit per group");
                        for (int i = 1; i < docs.Count; i++)
                            AssertTrue(docs[i - 1].GetProperty("Score").GetDouble() >= docs[i].GetProperty("Score").GetDouble(), "Best-first order");
                        AssertEqual("p-old-c0", docs[0].GetProperty("DocumentKey").GetString(), "Nearest chunk (ties by id) represents the top group");
                        AssertTrue(GetDouble(docs[0], "VectorScore").HasValue, "Vector-only hits report VectorScore");

                        JsonElement thresholded = await Search(new { Vector = Vec(), Collapse = new { Field = "DocumentId" }, MinimumScore = 0.9, MaxResults = 50 }).ConfigureAwait(false);
                        foreach (JsonElement d in Docs(thresholded))
                            AssertTrue(d.GetProperty("Score").GetDouble() >= 0.9, "Threshold applies to representatives");
                        AssertEqual((long)Docs(thresholded).Count, thresholded.GetProperty("TotalRecords").GetInt64(), "TotalRecords reflects the threshold");
                    }),

                    Case("SearchCollapseFullTextOnly", "Collapse: full-text-only returns one hit per group, best text match first", async ct =>
                    {
                        JsonElement json = await Search(new { FullText = new { Query = _Query }, Collapse = new { Field = "Tag", TagKey = "parentKey" }, MaxResults = 50 }).ConfigureAwait(false);
                        List<JsonElement> docs = Docs(json);
                        List<string> groups = docs.Select(d => d.GetProperty("GroupKey").GetString()).ToList();
                        AssertEqual(groups.Count, groups.Distinct(StringComparer.Ordinal).Count(), "One hit per group");
                        AssertTrue(groups.Contains("p-old") && groups.Contains("p-new") && groups.Contains("p-loose"), "Every matching group is returned, got " + string.Join(",", groups));
                        for (int i = 1; i < docs.Count; i++)
                            AssertTrue(docs[i - 1].GetProperty("TextScore").GetDouble() >= docs[i].GetProperty("TextScore").GetDouble(), "Best-first order");
                    }),

                    Case("SearchCollapsePoolShortfallNotice", "Collapse: a full pool with too few groups carries a Notice", async ct =>
                    {
                        JsonElement small = await Search(new { Vector = Vec(), Collapse = new { Field = "DocumentId", CandidatePool = 2 }, MaxResults = 10 }).ConfigureAwait(false);
                        AssertTrue(NoticeContains(small, "raise the candidate pool"), "Notice should suggest a larger pool");

                        JsonElement roomy = await Search(new { Vector = Vec(), Collapse = new { Field = "DocumentId" }, MaxResults = 10 }).ConfigureAwait(false);
                        AssertTrue(!NoticeContains(roomy, "raise the candidate pool"), "No notice when the pool held every candidate");
                    }),

                    Case("SearchCollapseWithNeighbors", "Collapse: IncludeNeighbors attaches neighbors to the representative", async ct =>
                    {
                        JsonElement json = await Search(new { Vector = Vec(), Collapse = new { Field = "DocumentId" }, IncludeNeighbors = 1, MaxResults = 1 }).ConfigureAwait(false);
                        JsonElement top = Docs(json)[0];
                        AssertTrue(top.TryGetProperty("Neighbors", out JsonElement neighbors) && neighbors.ValueKind == JsonValueKind.Array && neighbors.GetArrayLength() > 0, "Representative has neighbors");
                    }),

                    Case("SearchCollapseIncludeEmbeddings", "Collapse: IncludeEmbeddings returns the representative's vector", async ct =>
                    {
                        JsonElement on = await Search(new { Vector = Vec(), FullText = new { Query = _Query }, Collapse = new { Field = "DocumentId" }, IncludeEmbeddings = true, MaxResults = 5 }).ConfigureAwait(false);
                        foreach (JsonElement d in Docs(on))
                            AssertTrue(d.TryGetProperty("Embeddings", out JsonElement e) && e.ValueKind == JsonValueKind.Array && e.GetArrayLength() == 3, "Every hit carries a 3-dimension vector");

                        JsonElement off = await Search(new { Vector = Vec(), FullText = new { Query = _Query }, Collapse = new { Field = "DocumentId" }, MaxResults = 5 }).ConfigureAwait(false);
                        foreach (JsonElement d in Docs(off))
                            AssertTrue(!d.TryGetProperty("Embeddings", out JsonElement e) || e.ValueKind == JsonValueKind.Null, "No vectors unless asked");
                    }),

                    // ----- MinimumShouldMatch -----

                    Case("SearchFullTextMinimumShouldMatch", "Full-text: MinimumShouldMatch 2 requires two distinct query terms", async ct =>
                    {
                        JsonElement one = await Search(new { FullText = new { Query = "alpha beta" }, MaxResults = 50 }).ConfigureAwait(false);
                        List<string> oneKeys = Keys(one);
                        AssertTrue(oneKeys.Contains("m-two") && oneKeys.Contains("m-one"), "Default Any matches both");

                        JsonElement two = await Search(new { FullText = new { Query = "alpha beta", MinimumShouldMatch = 2 }, MaxResults = 50 }).ConfigureAwait(false);
                        List<string> twoKeys = Keys(two);
                        AssertTrue(twoKeys.Contains("m-two"), "m = 2 keeps the two-term match");
                        AssertTrue(!twoKeys.Contains("m-one"), "m = 2 excludes the one-term match");

                        JsonElement single = await Search(new { FullText = new { Query = "alpha", MinimumShouldMatch = 2 }, MaxResults = 50 }).ConfigureAwait(false);
                        List<string> singleKeys = Keys(single);
                        AssertTrue(singleKeys.Contains("m-two") && singleKeys.Contains("m-one"), "A one-term query with m = 2 requires that term");

                        JsonElement hybrid = await Search(new { Vector = Vec(), FullText = new { Query = "alpha beta", MinimumShouldMatch = 2 }, MaxResults = 50 }).ConfigureAwait(false);
                        AssertTrue(Docs(hybrid).All(d => d.GetProperty("DocumentKey").GetString() != "m-one" || !GetInt(d, "TextRank").HasValue), "Hybrid text leg honors m = 2");
                    }),

                    // ----- Validation (400) -----

                    Case("SearchValidationRecencyWeightOutOfRange", "Validation: Hybrid.RecencyWeight outside 0-1 is rejected", async ct =>
                    {
                        await AssertBadRequest(new { Vector = Vec(), FullText = new { Query = _Query }, Hybrid = new { RecencyWeight = 1.5 } }).ConfigureAwait(false);
                        await AssertBadRequest(new { Vector = Vec(), FullText = new { Query = _Query }, Hybrid = new { RecencyWeight = -0.1 } }).ConfigureAwait(false);
                    }),

                    Case("SearchValidationCollapse", "Validation: invalid Collapse requests are rejected", async ct =>
                    {
                        await AssertBadRequest(new { Vector = Vec(), Collapse = new { Field = "Tag" } }).ConfigureAwait(false);
                        await AssertBadRequest(new { Vector = Vec(), Collapse = new { Field = "Tag", TagKey = "  " } }).ConfigureAwait(false);
                        await AssertBadRequest(new { Vector = Vec(), Collapse = new { Field = "Chapter" } }).ConfigureAwait(false);
                        await AssertBadRequest(new { Vector = Vec(), Collapse = new { CandidatePool = 0 } }).ConfigureAwait(false);
                        await AssertBadRequest(new { Vector = Vec(), Collapse = new { TagKey = new string('k', 257) } }).ConfigureAwait(false);
                        await AssertBadRequest(new { Vector = Vec(), FullText = new { Query = _Query }, Hybrid = new { Strategy = "Filter" }, Collapse = new { Field = "DocumentId" } }).ConfigureAwait(false);
                        await AssertBadRequest(new { Collapse = new { Field = "DocumentId" } }).ConfigureAwait(false);
                    }),

                    Case("SearchValidationMinimumShouldMatch", "Validation: MinimumShouldMatch outside 1-3, or with a match mode other than Any, is rejected", async ct =>
                    {
                        await AssertBadRequest(new { FullText = new { Query = "alpha beta", MinimumShouldMatch = 0 } }).ConfigureAwait(false);
                        await AssertBadRequest(new { FullText = new { Query = "alpha beta", MinimumShouldMatch = 4 } }).ConfigureAwait(false);
                        await AssertBadRequest(new { FullText = new { Query = "alpha beta", MatchMode = "All", MinimumShouldMatch = 2 } }).ConfigureAwait(false);
                    }),

                    Case("SearchGroupingCleanup", "Grouping: delete collection", async ct =>
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
                suiteId: "RecallDbSearchGrouping",
                caseId: caseId,
                displayName: displayName,
                executeAsync: execute);
        }

        private static object Chunk(string key, string parent, int position, string content, float x, float y, float z, DateTime createdUtc)
        {
            return new
            {
                DocumentKey = key,
                DocumentId = parent,
                Position = position,
                Content = content,
                ContentType = "Text",
                Embeddings = new List<float> { x, y, z },
                CreatedUtc = createdUtc,
                Tags = new Dictionary<string, string> { { "parentKey", parent } }
            };
        }

        private static object Vec()
        {
            return new { SearchType = "CosineSimilarity", Embeddings = _QueryVector };
        }

        private static string GroupingPath(string suffix)
        {
            return "/v1.0/tenants/default/collections/" + _CollectionId + suffix;
        }

        private static async Task<JsonElement> Search(object body)
        {
            AssertNotNullOrEmpty(_CollectionId, "Grouping collection (SearchGroupingSetup must run first)");
            using HttpResponseMessage response = await PostAsync(_Client, GroupingPath("/search"), body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);
            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.GetProperty("Success").GetBoolean(), "Search should succeed");
            return json;
        }

        private static async Task AssertBadRequest(object body)
        {
            AssertNotNullOrEmpty(_CollectionId, "Grouping collection (SearchGroupingSetup must run first)");
            using HttpResponseMessage response = await PostAsync(_Client, GroupingPath("/search"), body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.BadRequest);
        }

        private static List<JsonElement> Docs(JsonElement json)
        {
            return GetDocs(json).EnumerateArray().ToList();
        }

        private static List<string> Keys(JsonElement json)
        {
            return Docs(json).Select(d => d.GetProperty("DocumentKey").GetString()).ToList();
        }

        private static JsonElement FindGroup(JsonElement json, string group)
        {
            foreach (JsonElement doc in Docs(json))
            {
                if (doc.TryGetProperty("GroupKey", out JsonElement g) && g.GetString() == group) return doc;
            }
            AssertTrue(false, "Group " + group + " should be in the results");
            return default;
        }

        private static JsonElement FindDoc(JsonElement json, string key)
        {
            foreach (JsonElement doc in Docs(json))
            {
                if (doc.GetProperty("DocumentKey").GetString() == key) return doc;
            }
            AssertTrue(false, "Document " + key + " should be in the results");
            return default;
        }

        private static string ParentOf(JsonElement doc)
        {
            if (doc.TryGetProperty("Tags", out JsonElement tags) && tags.ValueKind == JsonValueKind.Object
                && tags.TryGetProperty("parentKey", out JsonElement parent) && !string.IsNullOrEmpty(parent.GetString()))
                return parent.GetString();
            return doc.GetProperty("DocumentKey").GetString();
        }

        private static bool NoticeContains(JsonElement json, string text)
        {
            return json.TryGetProperty("Notice", out JsonElement notice)
                && notice.ValueKind == JsonValueKind.String
                && notice.GetString().IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int? GetInt(JsonElement doc, string name)
        {
            if (doc.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number) return value.GetInt32();
            return null;
        }

        private static double? GetDouble(JsonElement doc, string name)
        {
            if (doc.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number) return value.GetDouble();
            return null;
        }

        #endregion
    }
}
