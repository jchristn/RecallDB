namespace RecallDb.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    using SyslogLogging;

    using RecallDb.Core.Database;
    using RecallDb.Core.Models;
    using RecallDb.Server.Classes;

    /// <summary>
    /// Search operations shared by REST and MCP, including optional neighbor-chunk enrichment.
    /// </summary>
    public class SearchService : ServiceBase
    {
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

            CollectionMetadata col = await _Database.Collections.ReadAsync(ctx.TenantId, ctx.CollectionId).ConfigureAwait(false);
            if (col == null)
                return ServiceResult.Fail(404, "Not found", "Collection not found.");

            string cid = ctx.CollectionId;
            string origin = ctx.Origin == RecallDb.Core.Enums.RequestOriginEnum.Mcp ? "mcp" : "rest";
            string mode = DeriveSearchMode(query);

            Stopwatch sw = Stopwatch.StartNew();
            SearchResult result;
            using (Activity searchActivity = RecallDb.Server.Observability.ServerTelemetry.ActivitySource.StartActivity("search " + mode, ActivityKind.Internal))
            {
                searchActivity?.SetTag(RecallDb.Server.Observability.ServerTelemetry.TagOrigin, origin);
                searchActivity?.SetTag(RecallDb.Server.Observability.ServerTelemetry.TagSearchMode, mode);
                try
                {
                    result = await _Database.Search.SearchAsync(cid, col.Dimensionality, query).ConfigureAwait(false);
                    await _Documents.AttachLabelsAndTagsAsync(cid, result.Documents).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    RecallDb.Core.Observability.RecallDbTelemetry.RecordException(searchActivity, e);
                    RecallDb.Server.Observability.ServerTelemetry.RecordSearch(origin, mode, false, 500, sw.Elapsed.TotalSeconds, -1);
                    throw;
                }
            }

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
            RecallDb.Server.Observability.ServerTelemetry.RecordSearch(origin, mode, true, 200, sw.Elapsed.TotalSeconds, resultCount);

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
                if (doc.Neighbors != null) SanitizeScores(doc.Neighbors);
            }
        }

        private static string DeriveSearchMode(SearchQuery query)
        {
            if (query == null) return "unknown";
            bool hasVector = query.Vector != null;
            bool hasFullText = query.FullText != null;
            if (hasVector && hasFullText) return "hybrid";
            if (hasVector) return "vector";
            if (hasFullText) return "fulltext";
            return "filter";
        }

        #endregion
    }
}
