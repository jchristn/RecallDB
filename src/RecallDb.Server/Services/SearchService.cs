namespace RecallDb.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using SyslogLogging;

    using RecallDb.Core.Database;
    using RecallDb.Core.Enums;
    using RecallDb.Core.Models;
    using RecallDb.Server.Classes;

    /// <summary>
    /// Search operations shared by REST and MCP, including optional neighbor-chunk enrichment.
    /// </summary>
    public class SearchService : ServiceBase
    {
        #region Public-Members

        /// <summary>
        /// Notice returned when Hybrid options are supplied without both a vector and a text query.
        /// </summary>
        public const string NoticeHybridIgnored = "Hybrid options were ignored because the search does not include both a vector query and a text query.";

        /// <summary>
        /// Notice returned when a vector search carries a FullText object whose Query is blank.
        /// </summary>
        public const string NoticeBlankFullTextIgnored = "FullText.Query was blank, so the search ran as vector-only.";

        #endregion

        #region Private-Members

        private readonly DocumentService _Documents;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="documents">Document service (used for label/tag enrichment of matches and neighbors).</param>
        public SearchService(DatabaseDriverBase database, LoggingModule logging, DocumentService documents)
            : base(database, logging)
        {
            if (documents == null) throw new ArgumentNullException(nameof(documents));
            _Documents = documents;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Execute a vector, full-text, or hybrid search within a collection, attaching labels and tags to matches
        /// and optionally enriching each match with neighboring chunks.
        /// </summary>
        /// <param name="ctx">Request context. Search carries the query.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ServiceResult wrapping a SearchResult.</returns>
        public async Task<ServiceResult> SearchAsync(RequestContext ctx, CancellationToken token = default)
        {
            if (!ValidateTenantAccess(ctx.Auth, ctx.TenantId))
                return ServiceResult.Fail(403, "Forbidden", "Access denied.");

            SearchQuery query = ctx.Search;
            if (query == null)
                return ServiceResult.Fail(400, "Bad request", "Request body is required.");

            if (query.Vector != null && query.Vector.Embeddings != null)
            {
                foreach (float value in query.Vector.Embeddings)
                {
                    if (float.IsNaN(value) || float.IsInfinity(value))
                        return ServiceResult.Fail(400, "Bad request", "Search embeddings must contain only finite numeric values.");
                }
            }

            bool hasVector = HasVector(query);
            bool hasFullText = HasFullText(query);

            if (query.FullText != null && !hasFullText && !hasVector)
                return ServiceResult.Fail(400, "Bad request", "FullText.Query is required for a full-text search.");

            if (hasFullText)
            {
                string languageError = await ValidateLanguageAsync(query.FullText.Language, token).ConfigureAwait(false);
                if (languageError != null)
                    return ServiceResult.Fail(400, "Bad request", languageError);
            }

            CollectionMetadata col = await _Database.Collections.ReadAsync(ctx.TenantId, ctx.CollectionId).ConfigureAwait(false);
            if (col == null)
                return ServiceResult.Fail(404, "Not found", "Collection not found.");

            string cid = ctx.CollectionId;
            string origin = ctx.Origin == RecallDb.Core.Enums.RequestOriginEnum.Mcp ? "mcp" : "rest";
            string mode = DeriveSearchMode(query);
            string matchMode = DeriveMatchMode(query);
            string hybridStrategy = DeriveHybridStrategy(query);

            Stopwatch sw = Stopwatch.StartNew();
            SearchResult result;
            using (Activity searchActivity = RecallDb.Server.Observability.ServerTelemetry.ActivitySource.StartActivity("search " + mode, ActivityKind.Internal))
            {
                searchActivity?.SetTag(RecallDb.Server.Observability.ServerTelemetry.TagOrigin, origin);
                searchActivity?.SetTag(RecallDb.Server.Observability.ServerTelemetry.TagSearchMode, mode);
                searchActivity?.SetTag(RecallDb.Server.Observability.ServerTelemetry.TagSearchMatchMode, matchMode);
                searchActivity?.SetTag(RecallDb.Server.Observability.ServerTelemetry.TagSearchHybridStrategy, hybridStrategy);
                try
                {
                    result = await _Database.Search.SearchAsync(cid, col.Dimensionality, query, token).ConfigureAwait(false);
                    await _Documents.AttachLabelsAndTagsAsync(cid, result.Documents).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    RecallDb.Core.Observability.RecallDbTelemetry.RecordException(searchActivity, e);
                    RecallDb.Server.Observability.ServerTelemetry.RecordSearch(origin, mode, false, 500, sw.Elapsed.TotalSeconds, -1, matchMode, hybridStrategy);
                    throw;
                }
            }

            if (query.Hybrid != null && !(hasVector && hasFullText))
                result.AddNotice(NoticeHybridIgnored);

            if (hasVector && query.FullText != null && !hasFullText)
                result.AddNotice(NoticeBlankFullTextIgnored);

            if (query.IncludeNeighbors.HasValue && query.IncludeNeighbors.Value > 0 && result.Documents != null && result.Documents.Count > 0)
            {
                int n = query.IncludeNeighbors.Value;

                Dictionary<string, List<DocumentRecord>> groupedByDocId = new Dictionary<string, List<DocumentRecord>>();
                foreach (DocumentRecord doc in result.Documents)
                {
                    if (string.IsNullOrEmpty(doc.DocumentId)) continue;
                    if (!groupedByDocId.ContainsKey(doc.DocumentId))
                        groupedByDocId[doc.DocumentId] = new List<DocumentRecord>();
                    groupedByDocId[doc.DocumentId].Add(doc);
                }

                List<DocumentRecord> allNeighborDocs = new List<DocumentRecord>();

                foreach (KeyValuePair<string, List<DocumentRecord>> kvp in groupedByDocId)
                {
                    string documentId = kvp.Key;
                    List<DocumentRecord> matchedDocs = kvp.Value;

                    List<PositionRange> ranges = new List<PositionRange>();
                    foreach (DocumentRecord doc in matchedDocs)
                    {
                        int minPos = Math.Max(0, doc.Position - n);
                        int maxPos = doc.Position + n;
                        ranges.Add(new PositionRange(minPos, maxPos));
                    }

                    ranges.Sort((a, b) => a.Min.CompareTo(b.Min));
                    List<PositionRange> merged = new List<PositionRange>();
                    merged.Add(ranges[0]);
                    for (int i = 1; i < ranges.Count; i++)
                    {
                        PositionRange last = merged[merged.Count - 1];
                        if (ranges[i].Min <= last.Max + 1)
                        {
                            merged[merged.Count - 1] = new PositionRange(last.Min, Math.Max(last.Max, ranges[i].Max));
                        }
                        else
                        {
                            merged.Add(ranges[i]);
                        }
                    }

                    List<DocumentRecord> fetchedChunks = new List<DocumentRecord>();
                    foreach (PositionRange range in merged)
                    {
                        List<DocumentRecord> chunks = await _Database.Documents.ReadByDocumentIdAndPositionRangeAsync(
                            cid, documentId, range.Min, range.Max).ConfigureAwait(false);
                        fetchedChunks.AddRange(chunks);
                    }

                    foreach (DocumentRecord doc in matchedDocs)
                    {
                        int minPos = Math.Max(0, doc.Position - n);
                        int maxPos = doc.Position + n;
                        doc.Neighbors = new List<DocumentRecord>();
                        foreach (DocumentRecord chunk in fetchedChunks)
                        {
                            if (chunk.Position >= minPos && chunk.Position <= maxPos && chunk.Position != doc.Position)
                            {
                                doc.Neighbors.Add(chunk);
                            }
                        }
                    }

                    allNeighborDocs.AddRange(fetchedChunks);
                }

                if (allNeighborDocs.Count > 0)
                {
                    await _Documents.AttachLabelsAndTagsAsync(cid, allNeighborDocs).ConfigureAwait(false);
                }
            }

            result.TotalMs = sw.Elapsed.TotalMilliseconds;

            // Defensive: a degenerate query vector can yield non-finite distances/scores that
            // System.Text.Json cannot serialize (NaN/Infinity). Coerce them to 0 so a bad
            // query never produces a 500.
            SanitizeScores(result.Documents);

            int resultCount = result.Documents != null ? result.Documents.Count : 0;
            RecallDb.Server.Observability.ServerTelemetry.RecordSearch(origin, mode, true, 200, sw.Elapsed.TotalSeconds, resultCount, matchMode, hybridStrategy);

            return ServiceResult.Ok(result);
        }

        #endregion

        #region Private-Methods

        private static void SanitizeScores(List<DocumentRecord> docs)
        {
            if (docs == null) return;
            foreach (DocumentRecord doc in docs)
            {
                if (doc == null) continue;
                if (double.IsNaN(doc.Distance) || double.IsInfinity(doc.Distance)) doc.Distance = 0;
                if (double.IsNaN(doc.Score) || double.IsInfinity(doc.Score)) doc.Score = 0;
                if (doc.TextScore.HasValue && (double.IsNaN(doc.TextScore.Value) || double.IsInfinity(doc.TextScore.Value)))
                    doc.TextScore = 0;
                if (doc.VectorScore.HasValue && (double.IsNaN(doc.VectorScore.Value) || double.IsInfinity(doc.VectorScore.Value)))
                    doc.VectorScore = 0;
                if (doc.Neighbors != null) SanitizeScores(doc.Neighbors);
            }
        }

        // Same predicates as SearchMethods.SearchAsync, so search.mode tags agree with what actually ran.
        private static bool HasVector(SearchQuery query)
        {
            return query != null && query.Vector != null && query.Vector.Embeddings != null && query.Vector.Embeddings.Count > 0;
        }

        private static bool HasFullText(SearchQuery query)
        {
            return query != null && query.FullText != null && !string.IsNullOrWhiteSpace(query.FullText.Query);
        }

        private static string DeriveSearchMode(SearchQuery query)
        {
            if (query == null) return "unknown";
            bool hasVector = HasVector(query);
            bool hasFullText = HasFullText(query);
            if (hasVector && hasFullText) return "hybrid";
            if (hasVector) return "vector";
            if (hasFullText) return "fulltext";
            return "filter";
        }

        private static string DeriveMatchMode(SearchQuery query)
        {
            if (!HasFullText(query)) return "none";
            return query.FullText.MatchMode.ToString().ToLowerInvariant();
        }

        private static string DeriveHybridStrategy(SearchQuery query)
        {
            if (!HasVector(query) || !HasFullText(query)) return "none";
            HybridStrategyEnum strategy = query.Hybrid != null ? query.Hybrid.Strategy : HybridStrategyEnum.Rrf;
            return strategy.ToString().ToLowerInvariant();
        }

        private async Task<string> ValidateLanguageAsync(string language, CancellationToken token)
        {
            // A null or blank language means the default (english).
            if (string.IsNullOrWhiteSpace(language)) return null;

            string normalized = language.Trim().ToLowerInvariant();
            List<string> allowed = await _Database.ListTextSearchConfigurationsAsync(token).ConfigureAwait(false);

            // No allowlist (driver without text search catalogs): fall back to a strict identifier check.
            if (allowed == null || allowed.Count < 1)
            {
                bool plain = normalized.All(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_');
                return plain ? null : "FullText.Language '" + language + "' is not a valid text search configuration name.";
            }

            if (allowed.Contains(normalized)) return null;

            return "FullText.Language '" + language + "' is not a text search configuration installed in the database. "
                + "Valid values: " + string.Join(", ", allowed) + ".";
        }

        #endregion
    }
}
