namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Npgsql;

    using RecallDb.Core.Database.Postgresql;
    using RecallDb.Core.Settings;

    using Touchstone.Core;

    using static Test.Shared.TestHelpers;

    /// <summary>
    /// Integration tests for full-text match modes, hybrid search strategies (Rrf, Linear, Filter), search input
    /// validation, and the stored content_tsv schema. The suite creates its own collection in the default tenant
    /// with a seed set designed to make the semantics observable: a document that shares no keyword with a
    /// natural-language question but is its best semantic match, documents that match only some query terms,
    /// a phrase-order pair, and label-filtered documents that must be excluded from both hybrid legs.
    /// The schema cases need direct database access and run only when RECALLDB_TEST_DB holds an Npgsql connection
    /// string for the database the server under test uses; otherwise they return without asserting.
    /// </summary>
    public static class HybridSearchSuites
    {
        #region Shared-State

        private static HttpClient _Client = null;
        private static string _CollectionId = null;
        private static readonly List<float> _QueryVector = new List<float> { 1.0f, 0.0f, 0.0f };

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
        /// The full-text and hybrid search integration test suite.
        /// </summary>
        public static TestSuiteDescriptor Suite { get; } = Build();

        private static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "RecallDbHybridSearch",
                displayName: "RecallDB Full-Text and Hybrid Search Tests",
                beforeSuiteAsync: async ct =>
                {
                    // Own client: the main suite disposes the shared AdminClient when it finishes.
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
                    Case("HybridSearchSetup", "Hybrid search: create collection and seed documents", async ct =>
                    {
                        object colBody = new { Name = "HybridSearchTestCollection", Description = "Full-text and hybrid search tests", Dimensionality = 3 };
                        using HttpResponseMessage colResp = await PutAsync(_Client, "/v1.0/tenants/default/collections", colBody).ConfigureAwait(false);
                        AssertStatusCode(colResp, HttpStatusCode.Created);
                        JsonElement col = await ReadResponse<JsonElement>(colResp).ConfigureAwait(false);
                        _CollectionId = col.GetProperty("Id").GetString();
                        AssertNotNullOrEmpty(_CollectionId, "Hybrid collection Id");

                        List<object> docs = new List<object>
                        {
                            Doc("hy-run", "Run the test suite with dotnet test before every commit", 0.2f, 0.9f, 0.1f),
                            Doc("hy-guide", "Executing the automated checks locally is described in the contributor guide", 0.99f, 0.05f, 0.0f),
                            Doc("hy-harness", "The harness prints a test summary when finished", 0.1f, 0.1f, 0.95f),
                            Doc("hy-chunk", "Chunk key separators are configurable per collection", 0.5f, 0.5f, 0.5f),
                            Doc("hy-keychunk", "Key chunk ordering follows insertion order", 0.4f, 0.6f, 0.2f),
                            Doc("hy-alpha1", "Alpha release notes for the storage engine", 0.3f, 0.3f, 0.9f),
                            Doc("hy-alpha2", "Alpha builds reported faster imports", 0.35f, 0.25f, 0.9f),
                            Doc("hy-cook", "Cooking pasta requires plenty of salted water", 0.0f, 0.2f, 0.98f),
                            Doc("hy-garden", "Gardening tips for a productive spring", 0.05f, 0.95f, 0.3f),
                            Doc("hy-hidden-v", "Confidential benchmark numbers", 0.98f, 0.1f, 0.02f),
                            Doc("hy-hidden-t", "Hidden copy: run the test suite nightly", 0.1f, 0.2f, 0.9f)
                        };

                        using HttpResponseMessage batchResp = await PostAsync(_Client, HybridPath("/documents/batch"), docs).ConfigureAwait(false);
                        AssertStatusCode(batchResp, HttpStatusCode.Created);

                        foreach (string key in new[] { "hy-hidden-v", "hy-hidden-t" })
                        {
                            using HttpResponseMessage labelResp = await PutAsync(_Client, HybridPath("/labels"), new { DocumentKey = key, Label = "hidden" }).ConfigureAwait(false);
                            AssertStatusCode(labelResp, HttpStatusCode.Created);
                        }
                    }),

                    // ----- Full-text match modes -----

                    Case("SearchFullTextAnyMatchesPartialTerms", "Full-text: Any matches documents with only some terms; All does not", async ct =>
                    {
                        JsonElement any = await Search(new { FullText = new { Query = "alpha nonexistentterm", MatchMode = "Any" }, MaxResults = 50 }).ConfigureAwait(false);
                        List<string> anyKeys = Keys(any);
                        AssertTrue(anyKeys.Contains("hy-alpha1") && anyKeys.Contains("hy-alpha2"), "Any should return both alpha documents, got " + string.Join(",", anyKeys));
                        AssertEqual(2L, any.GetProperty("TotalRecords").GetInt64(), "Any TotalRecords");

                        JsonElement all = await Search(new { FullText = new { Query = "alpha nonexistentterm", MatchMode = "All" }, MaxResults = 50 }).ConfigureAwait(false);
                        AssertEqual(0, GetDocs(all).GetArrayLength(), "All should require every term");
                        AssertEqual(0L, all.GetProperty("TotalRecords").GetInt64(), "All TotalRecords");

                        JsonElement dflt = await Search(new { FullText = new { Query = "alpha nonexistentterm" }, MaxResults = 50 }).ConfigureAwait(false);
                        AssertEqual(2L, dflt.GetProperty("TotalRecords").GetInt64(), "MatchMode should default to Any");
                    }),

                    Case("SearchFullTextAnyRanksMoreTermsHigher", "Full-text: Any ranks a 3-of-3 term match above a 1-of-3 match", async ct =>
                    {
                        JsonElement json = await Search(new { FullText = new { Query = "run test suite" }, LabelFilter = new { Excluded = new List<string> { "hidden" } }, MaxResults = 50 }).ConfigureAwait(false);
                        List<string> keys = Keys(json);
                        AssertTrue(keys.Count >= 2, "Expected at least two matches");
                        AssertEqual("hy-run", keys[0], "The document matching all three terms should rank first");
                        AssertTrue(keys.Contains("hy-harness"), "A document matching only 'test' should still be returned under Any");
                        AssertTrue(keys.IndexOf("hy-harness") > keys.IndexOf("hy-run"), "The partial match should rank below the full match");
                    }),

                    Case("SearchFullTextAllPreservesLegacy", "Full-text: All returns exactly the all-terms matches", async ct =>
                    {
                        JsonElement json = await Search(new { FullText = new { Query = "run test suite", MatchMode = "All" }, MaxResults = 50 }).ConfigureAwait(false);
                        List<string> keys = Keys(json).OrderBy(k => k, StringComparer.Ordinal).ToList();
                        AssertEqual("hy-hidden-t,hy-run", string.Join(",", keys), "All should return only documents containing run, test, and suite");

                        JsonElement phraseLike = await Search(new { FullText = new { Query = "chunk key", MatchMode = "All" }, MaxResults = 50 }).ConfigureAwait(false);
                        List<string> chunkKeys = Keys(phraseLike).OrderBy(k => k, StringComparer.Ordinal).ToList();
                        AssertEqual("hy-chunk,hy-keychunk", string.Join(",", chunkKeys), "All ignores term order");
                    }),

                    Case("SearchFullTextPhrase", "Full-text: Phrase matches the in-order document only", async ct =>
                    {
                        JsonElement json = await Search(new { FullText = new { Query = "chunk key", MatchMode = "Phrase" }, MaxResults = 50 }).ConfigureAwait(false);
                        List<string> keys = Keys(json);
                        AssertTrue(keys.Contains("hy-chunk"), "Phrase should match 'Chunk key separators'");
                        AssertTrue(!keys.Contains("hy-keychunk"), "Phrase should not match 'Key chunk ordering'");
                    }),

                    Case("SearchFullTextWebSearchSyntax", "Full-text: WebSearch supports quoted phrases, or, and -exclusion", async ct =>
                    {
                        JsonElement orJson = await Search(new { FullText = new { Query = "\"chunk key\" or gardening", MatchMode = "WebSearch" }, MaxResults = 50 }).ConfigureAwait(false);
                        List<string> orKeys = Keys(orJson).OrderBy(k => k, StringComparer.Ordinal).ToList();
                        AssertEqual("hy-chunk,hy-garden", string.Join(",", orKeys), "Quoted phrase OR term");

                        JsonElement excludeJson = await Search(new { FullText = new { Query = "chunk -separators", MatchMode = "WebSearch" }, MaxResults = 50 }).ConfigureAwait(false);
                        AssertEqual("hy-keychunk", string.Join(",", Keys(excludeJson)), "-exclusion should drop the document containing 'separators'");
                    }),

                    Case("SearchFullTextStopwordsOnly", "Full-text: a stop-words-only query succeeds with 0 results and a Notice", async ct =>
                    {
                        JsonElement json = await Search(new { FullText = new { Query = "the and of" }, MaxResults = 10 }).ConfigureAwait(false);
                        AssertEqual(0, GetDocs(json).GetArrayLength(), "No documents");
                        AssertEqual(0L, json.GetProperty("TotalRecords").GetInt64(), "TotalRecords");
                        AssertTrue(NoticeContains(json, "no searchable terms"), "Notice should explain the empty query");
                    }),

                    Case("SearchFullTextOperatorInjection", "Full-text: tsquery operators in the input are neutralized", async ct =>
                    {
                        string input = "run' | 'cooking & !pasta";
                        JsonElement any = await Search(new { FullText = new { Query = input, MatchMode = "Any" }, LabelFilter = new { Excluded = new List<string> { "hidden" } }, MaxResults = 50 }).ConfigureAwait(false);
                        List<string> keys = Keys(any);
                        AssertTrue(keys.Contains("hy-run"), "'run' is treated as a plain term");
                        AssertTrue(keys.Contains("hy-cook"), "'!pasta' is not a negation under Any; 'cooking' and 'pasta' are plain terms");

                        JsonElement web = await Search(new { FullText = new { Query = input, MatchMode = "WebSearch" }, MaxResults = 50 }).ConfigureAwait(false);
                        AssertTrue(web.GetProperty("Success").GetBoolean(), "WebSearch parses the same input without a syntax error");
                    }),

                    Case("SearchFullTextNonEnglishLanguage", "Full-text: a non-indexed language works through the expression path and sets a Notice", async ct =>
                    {
                        JsonElement simple = await Search(new { FullText = new { Query = "suite", Language = "simple" }, MaxResults = 50 }).ConfigureAwait(false);
                        AssertTrue(Keys(simple).Contains("hy-run"), "The simple configuration should match 'suite'");
                        AssertTrue(NoticeContains(simple, "not served by the full-text index"), "Notice should say the language is unindexed");

                        JsonElement english = await Search(new { FullText = new { Query = "suite", Language = "English" }, MaxResults = 50 }).ConfigureAwait(false);
                        AssertTrue(Keys(english).Contains("hy-run"), "Language is case-insensitive");
                        AssertTrue(!english.TryGetProperty("Notice", out _), "The indexed language should not produce a Notice");
                    }),

                    // ----- Hybrid strategies -----

                    Case("SearchHybridRrfIncludesVectorOnlyHits", "Hybrid Rrf: the best semantic match is returned even with no shared keyword", async ct =>
                    {
                        object text = new { Query = "how do I run the test suite" };
                        JsonElement json = await Search(new { Vector = Vec(), FullText = text, LabelFilter = new { Excluded = new List<string> { "hidden" } }, MaxResults = 10 }).ConfigureAwait(false);
                        List<string> keys = Keys(json);
                        int guideIndex = keys.IndexOf("hy-guide");

                        // Documents found by both legs outrank a single-leg winner under Rrf, so "near the top" means
                        // within the first three here, and ahead of every other vector-only hit.
                        AssertTrue(guideIndex >= 0 && guideIndex <= 2, "hy-guide (vector rank 1, no text match) should be near the top, got index " + guideIndex);
                        foreach (JsonElement other in GetDocs(json).EnumerateArray())
                        {
                            if (other.TryGetProperty("TextRank", out _)) continue;
                            AssertTrue(keys.IndexOf(other.GetProperty("DocumentKey").GetString()) >= guideIndex, "hy-guide should outrank every other vector-only hit");
                        }

                        JsonElement guide = FindDoc(json, "hy-guide");
                        AssertEqual(1, guide.GetProperty("VectorRank").GetInt32(), "hy-guide VectorRank");
                        AssertTrue(!guide.TryGetProperty("TextRank", out _), "hy-guide has no TextRank");
                        AssertTrue(!guide.TryGetProperty("TextScore", out _), "hy-guide has no TextScore");
                        AssertTrue(Math.Abs(guide.GetProperty("Score").GetDouble() - 0.5) < 1e-9, "First in one leg only with TextWeight 0.5 scores 0.5");

                        // The legacy strategy only ranks within text matches, so it cannot return hy-guide.
                        JsonElement legacy = await Search(new { Vector = Vec(), FullText = new { Query = "how do I run the test suite", MatchMode = "All" }, Hybrid = new { Strategy = "Filter" }, LabelFilter = new { Excluded = new List<string> { "hidden" } }, MaxResults = 10 }).ConfigureAwait(false);
                        AssertTrue(!Keys(legacy).Contains("hy-guide"), "Filter + All should not return a document without the keywords");
                    }),

                    Case("SearchHybridRrfIncludesTextOnlyHits", "Hybrid Rrf: a strong text match outside the vector candidate pool is returned", async ct =>
                    {
                        JsonElement json = await Search(new { Vector = Vec(), FullText = new { Query = "suite" }, Hybrid = new { Strategy = "Rrf", CandidatePool = 2 }, LabelFilter = new { Excluded = new List<string> { "hidden" } }, MaxResults = 10 }).ConfigureAwait(false);
                        JsonElement run = FindDoc(json, "hy-run");
                        AssertTrue(run.ValueKind == JsonValueKind.Object, "hy-run should be returned through the text leg");
                        AssertTrue(!run.TryGetProperty("VectorRank", out _), "hy-run is outside the 2-document vector pool");
                        AssertEqual(1, run.GetProperty("TextRank").GetInt32(), "hy-run TextRank");
                        AssertTrue(run.GetProperty("VectorScore").GetDouble() > 0, "VectorScore is still reported for text-only hits");
                    }),

                    Case("SearchHybridRrfScoreNormalized", "Hybrid Rrf: scores are in [0, 1] and first in both legs scores 1.0", async ct =>
                    {
                        JsonElement json = await Search(new { Vector = Vec(), FullText = new { Query = "contributor guide" }, MaxResults = 50 }).ConfigureAwait(false);
                        foreach (JsonElement doc in GetDocs(json).EnumerateArray())
                        {
                            double score = doc.GetProperty("Score").GetDouble();
                            AssertTrue(score >= 0.0 && score <= 1.0 + 1e-9, "Score should be in [0, 1], got " + score);
                        }

                        JsonElement top = GetDocs(json)[0];
                        AssertEqual("hy-guide", top.GetProperty("DocumentKey").GetString(), "hy-guide is first in both legs");
                        AssertTrue(Math.Abs(top.GetProperty("Score").GetDouble() - 1.0) < 1e-9, "First in both legs should score 1.0");

                        // The text leg's winner scores by the same formula whatever its vector rank.
                        JsonElement oneLeg = await Search(new { Vector = Vec(), FullText = new { Query = "gardening" }, LabelFilter = new { Excluded = new List<string> { "hidden" } }, MaxResults = 50 }).ConfigureAwait(false);
                        JsonElement garden = FindDoc(oneLeg, "hy-garden");
                        int vectorRank = garden.TryGetProperty("VectorRank", out JsonElement vr) ? vr.GetInt32() : 0;
                        double expectedGarden = ((vectorRank > 0 ? 0.5 / (60 + vectorRank) : 0) + 0.5 / 61.0) * 61.0;
                        AssertTrue(Math.Abs(garden.GetProperty("Score").GetDouble() - expectedGarden) < 1e-9, "hy-garden fused score should follow the RRF formula");
                    }),

                    Case("SearchHybridRrfRanksPopulated", "Hybrid Rrf: VectorRank and TextRank agree with each leg and the fused score", async ct =>
                    {
                        double w = 0.3;
                        int k = 20;
                        JsonElement json = await Search(new { Vector = Vec(), FullText = new { Query = "run test suite", TextWeight = w }, Hybrid = new { Strategy = "Rrf", RrfK = k }, MaxResults = 50 }).ConfigureAwait(false);
                        List<JsonElement> docs = GetDocs(json).EnumerateArray().ToList();
                        AssertTrue(docs.Count > 0, "Results expected");

                        foreach (JsonElement doc in docs)
                        {
                            int? vRank = GetInt(doc, "VectorRank");
                            int? tRank = GetInt(doc, "TextRank");
                            AssertTrue(vRank.HasValue || tRank.HasValue, "Every fused hit comes from at least one leg");
                            AssertTrue(tRank.HasValue == GetDouble(doc, "TextScore").HasValue, "TextRank is set exactly when TextScore is");

                            double expected = ((vRank.HasValue ? (1 - w) / (k + vRank.Value) : 0) + (tRank.HasValue ? w / (k + tRank.Value) : 0)) * (k + 1);
                            AssertTrue(Math.Abs(doc.GetProperty("Score").GetDouble() - expected) < 1e-9, "Score should equal the weighted RRF formula");
                        }

                        List<JsonElement> byText = docs.Where(d => GetInt(d, "TextRank").HasValue).OrderBy(d => GetInt(d, "TextRank").Value).ToList();
                        for (int i = 1; i < byText.Count; i++)
                            AssertTrue(GetDouble(byText[i], "TextScore").Value <= GetDouble(byText[i - 1], "TextScore").Value + 1e-12, "TextRank order follows TextScore");

                        List<JsonElement> byVector = docs.Where(d => GetInt(d, "VectorRank").HasValue).OrderBy(d => GetInt(d, "VectorRank").Value).ToList();
                        for (int i = 1; i < byVector.Count; i++)
                            AssertTrue(GetDouble(byVector[i], "VectorScore").Value <= GetDouble(byVector[i - 1], "VectorScore").Value + 1e-9, "VectorRank order follows VectorScore");
                    }),

                    Case("SearchHybridNeverFewerThanVector", "Hybrid Rrf: never returns fewer results than vector-only", async ct =>
                    {
                        string[] queries = new[] { "how do I run the test suite", "xyzzy plugh", "alpha", "the and of", "chunk key separators" };
                        foreach (string q in queries)
                        {
                            JsonElement vector = await Search(new { Vector = Vec(), MaxResults = 50 }).ConfigureAwait(false);
                            JsonElement hybrid = await Search(new { Vector = Vec(), FullText = new { Query = q }, MaxResults = 50 }).ConfigureAwait(false);
                            AssertTrue(GetDocs(hybrid).GetArrayLength() >= GetDocs(vector).GetArrayLength(),
                                "Hybrid (" + GetDocs(hybrid).GetArrayLength() + ") should return at least as many results as vector-only (" + GetDocs(vector).GetArrayLength() + ") for '" + q + "'");
                        }
                    }),

                    Case("SearchHybridLinearNormalized", "Hybrid Linear: normalized blend in [0, 1] including non-text matches", async ct =>
                    {
                        double w = 0.4;
                        JsonElement json = await Search(new { Vector = Vec(), FullText = new { Query = "run test suite", TextWeight = w }, Hybrid = new { Strategy = "Linear" }, MaxResults = 100 }).ConfigureAwait(false);
                        List<JsonElement> docs = GetDocs(json).EnumerateArray().ToList();
                        AssertEqual((long)docs.Count, json.GetProperty("TotalRecords").GetInt64(), "The whole candidate set fits on one page");
                        AssertTrue(docs.Any(d => !GetDouble(d, "TextScore").HasValue), "Documents that are not text matches are included");

                        double maxText = docs.Select(d => GetDouble(d, "TextScore") ?? 0).Max();
                        foreach (JsonElement doc in docs)
                        {
                            double score = doc.GetProperty("Score").GetDouble();
                            AssertTrue(score >= 0.0 && score <= 1.0 + 1e-9, "Linear score in [0, 1], got " + score);
                            double vectorNorm = Math.Max(0.0, Math.Min(1.0, doc.GetProperty("VectorScore").GetDouble()));
                            double textNorm = maxText > 0 ? (GetDouble(doc, "TextScore") ?? 0) / maxText : 0;
                            double expected = (1 - w) * vectorNorm + w * textNorm;
                            AssertTrue(Math.Abs(score - expected) < 1e-6, "Linear score should equal (1-w)*vectorNorm + w*textNorm");
                        }
                    }),

                    Case("SearchHybridFilterLegacy", "Hybrid Filter + All: text match required, raw blended score", async ct =>
                    {
                        double w = 0.25;
                        JsonElement json = await Search(new { Vector = Vec(), FullText = new { Query = "run test suite", MatchMode = "All", TextWeight = w }, Hybrid = new { Strategy = "Filter" }, MaxResults = 50 }).ConfigureAwait(false);
                        List<string> keys = Keys(json).OrderBy(k => k, StringComparer.Ordinal).ToList();
                        AssertEqual("hy-hidden-t,hy-run", string.Join(",", keys), "Filter + All returns only all-terms text matches");
                        foreach (JsonElement doc in GetDocs(json).EnumerateArray())
                        {
                            double textScore = doc.GetProperty("TextScore").GetDouble();
                            AssertTrue(textScore > 0, "Every legacy hybrid hit has a TextScore");
                            double vectorScore = 1.0 - doc.GetProperty("Distance").GetDouble();
                            double expected = (1 - w) * vectorScore + w * textScore;
                            AssertTrue(Math.Abs(doc.GetProperty("Score").GetDouble() - expected) < 1e-6, "Legacy blended score formula");
                            AssertTrue(!doc.TryGetProperty("VectorRank", out _), "Filter does not report ranks");
                        }
                    }),

                    Case("SearchHybridPaginationWithinPool", "Hybrid Rrf: continuation pages do not overlap and stay within 2 x CandidatePool", async ct =>
                    {
                        int pool = 3;
                        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                        string token = null;
                        long total = -1;
                        int pages = 0;
                        while (true)
                        {
                            object body = token == null
                                ? (object)new { Vector = Vec(), FullText = new { Query = "run test suite" }, Hybrid = new { CandidatePool = pool }, MaxResults = 2 }
                                : new { Vector = Vec(), FullText = new { Query = "run test suite" }, Hybrid = new { CandidatePool = pool }, MaxResults = 2, ContinuationToken = token };
                            JsonElement page = await Search(body).ConfigureAwait(false);
                            total = page.GetProperty("TotalRecords").GetInt64();
                            foreach (string key in Keys(page))
                                AssertTrue(seen.Add(key), "Pages should not overlap; saw " + key + " twice");
                            pages++;
                            if (page.GetProperty("EndOfResults").GetBoolean()) break;
                            token = page.GetProperty("ContinuationToken").GetString();
                            AssertTrue(pages < 20, "Pagination should terminate");
                        }
                        AssertTrue(total <= 2 * pool, "TotalRecords (" + total + ") should be at most 2 x CandidatePool");
                        AssertEqual(total, (long)seen.Count, "Paging should visit exactly TotalRecords documents");
                    }),

                    Case("SearchHybridWithFiltersAppliedToBothLegs", "Hybrid Rrf: a label filter excludes documents from both legs", async ct =>
                    {
                        JsonElement unfiltered = await Search(new { Vector = Vec(), FullText = new { Query = "run test suite" }, MaxResults = 50 }).ConfigureAwait(false);
                        List<string> all = Keys(unfiltered);
                        AssertTrue(all.Contains("hy-hidden-v") && all.Contains("hy-hidden-t"), "Both hidden documents appear without the filter");

                        JsonElement filtered = await Search(new { Vector = Vec(), FullText = new { Query = "run test suite" }, LabelFilter = new { Excluded = new List<string> { "hidden" } }, MaxResults = 50 }).ConfigureAwait(false);
                        List<string> keys = Keys(filtered);
                        AssertTrue(!keys.Contains("hy-hidden-v"), "The vector leg honors the label filter");
                        AssertTrue(!keys.Contains("hy-hidden-t"), "The text leg honors the label filter");
                    }),

                    Case("SearchHybridScoreThresholdInSql", "Hybrid Rrf: MinimumScore applies to the fused score and TotalRecords", async ct =>
                    {
                        JsonElement json = await Search(new { Vector = Vec(), FullText = new { Query = "run test suite" }, MinimumScore = 0.45, MaxResults = 50 }).ConfigureAwait(false);
                        foreach (JsonElement doc in GetDocs(json).EnumerateArray())
                            AssertTrue(doc.GetProperty("Score").GetDouble() >= 0.45, "Every hit meets the fused threshold");
                        AssertEqual((long)GetDocs(json).GetArrayLength(), json.GetProperty("TotalRecords").GetInt64(), "TotalRecords reflects the threshold");
                    }),

                    Case("SearchHybridEmptyTextQuery", "Hybrid Rrf: a stop-words-only text query falls back to the vector ranking", async ct =>
                    {
                        JsonElement hybrid = await Search(new { Vector = Vec(), FullText = new { Query = "the and of" }, MaxResults = 5 }).ConfigureAwait(false);
                        JsonElement vector = await Search(new { Vector = Vec(), MaxResults = 5 }).ConfigureAwait(false);
                        AssertEqual(string.Join(",", Keys(vector)), string.Join(",", Keys(hybrid)), "Order should equal the vector ranking");
                        AssertTrue(NoticeContains(hybrid, "vector leg only"), "Notice should explain the fallback");
                    }),

                    Case("SearchHybridOptionsIgnoredNotice", "Hybrid options without both legs are ignored with a Notice", async ct =>
                    {
                        JsonElement json = await Search(new { Vector = Vec(), Hybrid = new { Strategy = "Linear" }, MaxResults = 5 }).ConfigureAwait(false);
                        AssertTrue(GetDocs(json).GetArrayLength() > 0, "The vector search still runs");
                        AssertTrue(NoticeContains(json, "Hybrid options were ignored"), "Notice should say Hybrid was ignored");
                        AssertTrue(GetDouble(GetDocs(json)[0], "VectorScore").HasValue, "Vector-only hits report VectorScore");
                    }),

                    // ----- Validation (400) -----

                    Case("SearchValidationLanguageNotAllowed", "Validation: an unknown FullText.Language is rejected", async ct =>
                    {
                        await AssertBadRequest(new { FullText = new { Query = "suite", Language = "english'); drop table x;--" } }).ConfigureAwait(false);
                        await AssertBadRequest(new { FullText = new { Query = "suite", Language = "klingon" } }).ConfigureAwait(false);
                    }),

                    Case("SearchValidationNormalizationOutOfRange", "Validation: FullText.Normalization 64 is rejected", async ct =>
                    {
                        await AssertBadRequest(new { FullText = new { Query = "suite", Normalization = 64 } }).ConfigureAwait(false);
                        await AssertBadRequest(new { FullText = new { Query = "suite", Normalization = -1 } }).ConfigureAwait(false);
                    }),

                    Case("SearchValidationTextWeightOutOfRange", "Validation: FullText.TextWeight 1.5 is rejected, not clamped", async ct =>
                    {
                        await AssertBadRequest(new { Vector = Vec(), FullText = new { Query = "suite", TextWeight = 1.5 } }).ConfigureAwait(false);
                        await AssertBadRequest(new { Vector = Vec(), FullText = new { Query = "suite", TextWeight = -0.1 } }).ConfigureAwait(false);
                    }),

                    Case("SearchValidationRrfKZero", "Validation: Hybrid.RrfK 0 is rejected", async ct =>
                    {
                        await AssertBadRequest(new { Vector = Vec(), FullText = new { Query = "suite" }, Hybrid = new { RrfK = 0 } }).ConfigureAwait(false);
                    }),

                    Case("SearchValidationCandidatePoolTooLarge", "Validation: Hybrid.CandidatePool 10001 is rejected", async ct =>
                    {
                        await AssertBadRequest(new { Vector = Vec(), FullText = new { Query = "suite" }, Hybrid = new { CandidatePool = 10001 } }).ConfigureAwait(false);
                        await AssertBadRequest(new { Vector = Vec(), FullText = new { Query = "suite" }, Hybrid = new { CandidatePool = 0 } }).ConfigureAwait(false);
                    }),

                    Case("SearchValidationBlankFullTextQueryFullTextOnly", "Validation: a blank FullText.Query without a vector is rejected", async ct =>
                    {
                        await AssertBadRequest(new { FullText = new { Query = "   " } }).ConfigureAwait(false);

                        // With a vector the blank text query is ignored (vector-only) and explained in a Notice.
                        JsonElement json = await Search(new { Vector = Vec(), FullText = new { Query = "  " }, MaxResults = 3 }).ConfigureAwait(false);
                        AssertTrue(GetDocs(json).GetArrayLength() > 0, "Vector search still runs");
                        AssertTrue(NoticeContains(json, "vector-only"), "Notice should say the blank text query was ignored");
                    }),

                    Case("SearchValidationMatchModeInvalid", "Validation: an unknown FullText.MatchMode is rejected", async ct =>
                    {
                        await AssertBadRequest(new { FullText = new { Query = "suite", MatchMode = "Sometimes" } }).ConfigureAwait(false);
                        await AssertBadRequest(new { Vector = Vec(), FullText = new { Query = "suite" }, Hybrid = new { Strategy = "Magic" } }).ConfigureAwait(false);
                    }),

                    // ----- Schema (direct database access; opt-in via RECALLDB_TEST_DB) -----

                    Case("CollectionHasStoredTsVector", "Schema: a new collection has content_tsv and its GIN index, and no legacy _fts index", async ct =>
                    {
                        if (TestDbConnectionString == null || string.IsNullOrEmpty(_CollectionId)) return;
                        string table = TableName(_CollectionId);

                        AssertTrue(await ColumnExistsAsync(table, ct).ConfigureAwait(false), "content_tsv column should exist on " + table);
                        List<string> indexes = await ListIndexesAsync(table, ct).ConfigureAwait(false);
                        AssertTrue(indexes.Any(i => i.EndsWith("_tsv", StringComparison.Ordinal)), "A _tsv index should exist: " + string.Join(",", indexes));
                        AssertTrue(!indexes.Any(i => i.EndsWith("_fts", StringComparison.Ordinal)), "No legacy _fts index should exist: " + string.Join(",", indexes));
                    }),

                    Case("SchemaBackfillIdempotent", "Schema: the startup pass migrates an old-DDL collection and is a no-op the second time", async ct =>
                    {
                        if (TestDbConnectionString == null || string.IsNullOrEmpty(_CollectionId)) return;
                        string table = TableName(_CollectionId);
                        string ixId = IndexId(_CollectionId);

                        // Recreate the pre-migration shape: no content_tsv column, legacy expression index.
                        await ExecuteSqlAsync(
                            "ALTER TABLE " + table + " DROP COLUMN IF EXISTS content_tsv; " +
                            "CREATE INDEX IF NOT EXISTS idx_col_" + ixId + "_fts ON " + table + " USING gin (to_tsvector('english', COALESCE(content, '')));",
                            ct).ConfigureAwait(false);
                        AssertTrue(!await ColumnExistsAsync(table, ct).ConfigureAwait(false), "Precondition: column dropped");

                        using (PostgresqlDatabaseDriver driver = new PostgresqlDatabaseDriver(SettingsFromConnectionString(TestDbConnectionString), null))
                        {
                            await driver.EnsureAllCollectionSchemasAsync(ct).ConfigureAwait(false);

                            AssertTrue(await ColumnExistsAsync(table, ct).ConfigureAwait(false), "The pass should add content_tsv");
                            List<string> indexes = await ListIndexesAsync(table, ct).ConfigureAwait(false);
                            AssertTrue(indexes.Contains("idx_col_" + ixId + "_tsv"), "The pass should build the _tsv index");
                            AssertTrue(!indexes.Contains("idx_col_" + ixId + "_fts"), "The pass should drop the legacy _fts index");

                            long fileNodeBefore = await RelFileNodeAsync(table, ct).ConfigureAwait(false);
                            await driver.EnsureAllCollectionSchemasAsync(ct).ConfigureAwait(false);
                            long fileNodeAfter = await RelFileNodeAsync(table, ct).ConfigureAwait(false);
                            AssertEqual(fileNodeBefore, fileNodeAfter, "A second pass must not rewrite the table");
                            AssertEqual(string.Join(",", indexes), string.Join(",", await ListIndexesAsync(table, ct).ConfigureAwait(false)), "A second pass must not change the indexes");
                        }

                        // Search keeps working after the migration.
                        JsonElement json = await Search(new { FullText = new { Query = "suite" }, MaxResults = 10 }).ConfigureAwait(false);
                        AssertTrue(Keys(json).Contains("hy-run"), "Full-text search should work after the backfill");
                    }),

                    Case("HybridSearchCleanup", "Hybrid search: delete collection", async ct =>
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
                suiteId: "RecallDbHybridSearch",
                caseId: caseId,
                displayName: displayName,
                executeAsync: execute);
        }

        private static object Doc(string key, string content, float x, float y, float z)
        {
            return new { DocumentKey = key, DocumentId = key, Content = content, ContentType = "Text", Embeddings = new List<float> { x, y, z } };
        }

        private static object Vec()
        {
            return new { SearchType = "CosineSimilarity", Embeddings = _QueryVector };
        }

        private static string HybridPath(string suffix)
        {
            return "/v1.0/tenants/default/collections/" + _CollectionId + suffix;
        }

        private static async Task<JsonElement> Search(object body)
        {
            AssertNotNullOrEmpty(_CollectionId, "Hybrid collection (HybridSearchSetup must run first)");
            using HttpResponseMessage response = await PostAsync(_Client, HybridPath("/search"), body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.OK);
            JsonElement json = await ReadResponse<JsonElement>(response).ConfigureAwait(false);
            AssertTrue(json.GetProperty("Success").GetBoolean(), "Search should succeed");
            return json;
        }

        private static async Task AssertBadRequest(object body)
        {
            AssertNotNullOrEmpty(_CollectionId, "Hybrid collection (HybridSearchSetup must run first)");
            using HttpResponseMessage response = await PostAsync(_Client, HybridPath("/search"), body).ConfigureAwait(false);
            AssertStatusCode(response, HttpStatusCode.BadRequest);
        }

        private static List<string> Keys(JsonElement json)
        {
            return GetDocs(json).EnumerateArray().Select(d => d.GetProperty("DocumentKey").GetString()).ToList();
        }

        private static JsonElement FindDoc(JsonElement json, string key)
        {
            foreach (JsonElement doc in GetDocs(json).EnumerateArray())
            {
                if (doc.GetProperty("DocumentKey").GetString() == key) return doc;
            }
            AssertTrue(false, "Document " + key + " should be in the results");
            return default;
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

        private static string TableName(string collectionId)
        {
            return "collection_" + collectionId.Replace("-", "_").Replace(".", "_").ToLowerInvariant();
        }

        private static string IndexId(string collectionId)
        {
            // Mirrors DynamicTableQueries.GetIndexIdentifier.
            string sanitized = collectionId.Replace("-", "_").Replace(".", "_");
            if (sanitized.StartsWith("col_", StringComparison.Ordinal) && sanitized.Length > 4)
            {
                int nextUnderscore = sanitized.IndexOf('_', 4);
                if (nextUnderscore > 4) return sanitized.Substring(4, nextUnderscore - 4).ToLowerInvariant();
            }
            if (sanitized.Length <= 8) return sanitized.ToLowerInvariant();
            return sanitized.Substring(sanitized.Length - 8).ToLowerInvariant();
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

        private static async Task ExecuteSqlAsync(string sql, CancellationToken token)
        {
            using NpgsqlConnection connection = new NpgsqlConnection(TestDbConnectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);
            using NpgsqlCommand command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }

        private static async Task<DataTable> QueryAsync(string sql, CancellationToken token)
        {
            using NpgsqlConnection connection = new NpgsqlConnection(TestDbConnectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);
            using NpgsqlCommand command = new NpgsqlCommand(sql, connection);
            using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            DataTable table = new DataTable();
            table.Load(reader);
            return table;
        }

        private static async Task<bool> ColumnExistsAsync(string table, CancellationToken token)
        {
            DataTable result = await QueryAsync(
                "SELECT 1 FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = '" + table + "' AND column_name = 'content_tsv'",
                token).ConfigureAwait(false);
            return result.Rows.Count > 0;
        }

        private static async Task<List<string>> ListIndexesAsync(string table, CancellationToken token)
        {
            DataTable result = await QueryAsync(
                "SELECT indexname FROM pg_indexes WHERE schemaname = current_schema() AND tablename = '" + table + "' ORDER BY indexname",
                token).ConfigureAwait(false);
            return result.Rows.Cast<DataRow>().Select(r => r["indexname"].ToString()).ToList();
        }

        private static async Task<long> RelFileNodeAsync(string table, CancellationToken token)
        {
            DataTable result = await QueryAsync("SELECT relfilenode FROM pg_class WHERE oid = to_regclass('" + table + "')", token).ConfigureAwait(false);
            return result.Rows.Count > 0 ? Convert.ToInt64(result.Rows[0]["relfilenode"]) : -1;
        }

        #endregion
    }
}
