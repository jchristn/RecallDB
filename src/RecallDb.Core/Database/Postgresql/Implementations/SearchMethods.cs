namespace RecallDb.Core.Database.Postgresql.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using RecallDb.Core.Database.Interfaces;
    using RecallDb.Core.Database.Postgresql.Queries;
    using RecallDb.Core.Enums;
    using RecallDb.Core.Models;

    /// <summary>
    /// PostgreSQL implementation of vector, full-text, and hybrid search operations for dynamic collection tables.
    /// </summary>
    public class SearchMethods : ISearchMethods
    {
        #region Public-Members

        /// <summary>
        /// Notice returned when the text query normalizes to no searchable terms in a full-text-only search.
        /// </summary>
        public const string NoticeNoSearchableTerms = "The text query contained no searchable terms.";

        /// <summary>
        /// Notice returned when the text query normalizes to no searchable terms in a hybrid search.
        /// </summary>
        public const string NoticeNoSearchableTermsHybrid = "The text query contained no searchable terms; results are ranked by the vector leg only.";

        /// <summary>
        /// Format of the notice returned when a collapsed search's candidate pool held fewer groups than the page
        /// asked for while at least one leg filled the pool: {0} is the group count, {1} the pool size.
        /// </summary>
        public const string NoticeCollapsePoolShortfallFormat = "Collapse found {0} groups in a candidate pool of {1}; raise the candidate pool for more.";

        /// <summary>
        /// Text search configuration served by the stored content_tsv column and its GIN index.
        /// </summary>
        public const string IndexedLanguage = "english";

        #endregion

        #region Private-Members

        private readonly PostgresqlDatabaseDriver _Driver;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[SearchMethods] ";

        // Columns always projected by a search. The stored embedding vector is intentionally NOT
        // here: it is large and rarely needed on a search hit (the caller supplies the query
        // vector), so it is appended only when SearchQuery.IncludeEmbeddings is set. When omitted,
        // DocumentRecord.FromDataTable leaves Embeddings null (GetFloatArrayValue tolerates the
        // absent column).
        private const string _SelectColumns =
            "id, document_key, document_id, content_length, etag, sha256, position, content_type, content, binary_data, created_utc";

        private const string _EmbeddingsColumn = "embeddings::text as embeddings";

        // pgvector (before 0.8) returns at most hnsw.ef_search rows from an HNSW index scan, and rejects values
        // above 1000. The hybrid vector leg raises ef_search to its candidate pool so the pool can actually fill.
        private const int _DefaultEfSearch = 40;
        private const int _MaxEfSearch = 1000;

        // FullText.MinimumShouldMatch above 1 builds an OR of every m-term conjunction; only the first terms of the
        // query (in query order) are considered, which bounds the tsquery at C(16, 3) = 560 conjunctions.
        private const int _MinimumShouldMatchMaxTerms = 16;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL database driver.</param>
        /// <param name="logging">Logging module.</param>
        public SearchMethods(PostgresqlDatabaseDriver driver, LoggingModule logging)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _Logging = logging;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Perform a vector, full-text, or hybrid search within a collection.
        /// Vector-only when only Vector (with embeddings) is supplied; full-text when only FullText (with a
        /// non-blank Query) is supplied; hybrid when both are supplied, combined per SearchQuery.Hybrid.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="dimensionality">Vector dimensionality of the collection.</param>
        /// <param name="query">Search query parameters including embeddings and filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Search result containing matching documents with scores.</returns>
        /// <exception cref="ArgumentNullException">Thrown when collectionId or query is null.</exception>
        /// <exception cref="ArgumentException">Thrown when FullText.Language is not a plain configuration name.</exception>
        public async Task<SearchResult> SearchAsync(string collectionId, int dimensionality, SearchQuery query, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (query == null) throw new ArgumentNullException(nameof(query));
            token.ThrowIfCancellationRequested();

            string tableName = "collection_" + SanitizeTableName(collectionId);

            // Determine search modes
            bool hasVector = query.Vector != null && query.Vector.Embeddings != null && query.Vector.Embeddings.Count > 0;
            bool hasFullText = query.FullText != null && !string.IsNullOrWhiteSpace(query.FullText.Query);
            int offset = ParseOffset(query.ContinuationToken);

            SearchResult result;

            if (hasFullText)
            {
                TextSearchContext text = await BuildTextSearchContextAsync(collectionId, query.FullText, token).ConfigureAwait(false);

                if (hasVector)
                {
                    HybridStrategyEnum strategy = query.Hybrid != null ? query.Hybrid.Strategy : HybridStrategyEnum.Rrf;
                    if (strategy == HybridStrategyEnum.Filter)
                        result = await SearchHybridFilterAsync(tableName, dimensionality, query, text, offset, token).ConfigureAwait(false);
                    else
                        result = await SearchHybridFusedAsync(tableName, dimensionality, query, text, strategy, offset, token).ConfigureAwait(false);
                }
                else if (query.Collapse != null)
                {
                    result = await SearchFullTextCollapsedAsync(tableName, query, text, offset, token).ConfigureAwait(false);
                }
                else
                {
                    result = await SearchFullTextAsync(tableName, query, text, offset, token).ConfigureAwait(false);
                }

                if (!text.UsesIndex && !string.Equals(text.Language, IndexedLanguage, StringComparison.Ordinal))
                {
                    result.AddNotice("Full-text language '" + text.Language + "' is not served by the full-text index; the text match was evaluated without an index.");
                }
            }
            else if (hasVector && query.Collapse != null)
            {
                result = await SearchVectorCollapsedAsync(tableName, dimensionality, query, offset, token).ConfigureAwait(false);
            }
            else
            {
                result = await SearchVectorOnlyAsync(tableName, dimensionality, query, hasVector, offset, token).ConfigureAwait(false);
            }

            if (_Logging != null) _Logging.Debug(_Header + "search returned " + result.Documents.Count + " results from " + collectionId);
            return result;
        }

        #endregion

        #region Private-Methods

        private async Task<SearchResult> SearchVectorOnlyAsync(string tableName, int dimensionality, SearchQuery query, bool hasVector, int offset, CancellationToken token)
        {
            // The SQL built here is intentionally identical to the pre-hybrid implementation so the
            // HNSW-friendly ORDER BY (see below) keeps working exactly as before.
            string vectorOperator = GetVectorOperator(query.Vector);
            string vectorLiteral = FormatVectorLiteral(query.Vector, dimensionality);
            string scoreExpression = GetScoreExpression(query.Vector, vectorOperator, vectorLiteral);
            string distanceExpression = GetDistanceExpression(vectorOperator, vectorLiteral);

            StringBuilder sb = new StringBuilder();
            sb.Append("SELECT " + GetSelectColumns(query, null) + ", ");
            sb.Append(distanceExpression + " AS distance, ");
            sb.Append(scoreExpression + " AS score ");
            sb.Append("FROM " + tableName);

            List<string> conditions = BuildFilterConditions(tableName, query);
            if (conditions.Count > 0)
            {
                sb.Append(" WHERE " + string.Join(" AND ", conditions));
            }

            // For a pure vector search sorted best-first, order by the raw pgvector distance
            // operator ascending (e.g. "embeddings <=> '[...]'::vector ASC"). This is the ONLY
            // form the HNSW index can satisfy: wrapping the operator in score arithmetic such as
            // "(1.0 - (embeddings <=> q)) DESC" is opaque to the planner and forces a sequential
            // scan that computes the distance for every row (seconds on a large collection).
            // Smallest distance == best match for every supported operator (<=> cosine, <-> L2,
            // <#> negative inner product), so ascending distance is best-first in all cases.
            string orderBy;
            if (hasVector
                && (query.SortOrder == SortOrderEnum.ScoreDescending || query.SortOrder == SortOrderEnum.DistanceAscending))
            {
                orderBy = distanceExpression + " ASC";
            }
            else
            {
                orderBy = GetOrderByClause(query.SortOrder);
            }
            sb.Append(" ORDER BY " + orderBy);
            sb.Append(" LIMIT " + query.MaxResults.ToString(CultureInfo.InvariantCulture));
            sb.Append(" OFFSET " + offset.ToString(CultureInfo.InvariantCulture));

            StringBuilder countSb = new StringBuilder();
            countSb.Append("SELECT COUNT(*) AS count FROM " + tableName);
            if (conditions.Count > 0)
            {
                countSb.Append(" WHERE " + string.Join(" AND ", conditions));
            }

            DataTable selectResult = await _Driver.ExecuteQueryAsync(sb.ToString(), false, token).ConfigureAwait(false);
            DataTable countResult = await _Driver.ExecuteQueryAsync(countSb.ToString(), false, token).ConfigureAwait(false);

            List<DocumentRecord> documents = DocumentRecord.FromDataTable(selectResult);
            long totalCount = ReadLong(countResult, "count");

            // Vector-only thresholds are applied to the returned page (pre-existing behavior).
            double? minScore = query.MinimumScore ?? (query.Vector != null ? query.Vector.MinimumScore : null);
            double? maxScore = query.MaximumScore ?? (query.Vector != null ? query.Vector.MaximumScore : null);
            double? minDist = query.MinimumDistance ?? (query.Vector != null ? query.Vector.MinimumDistance : null);
            double? maxDist = query.MaximumDistance ?? (query.Vector != null ? query.Vector.MaximumDistance : null);

            if (minScore.HasValue)
            {
                documents = documents.Where(d => d.Score >= minScore.Value).ToList();
            }

            if (maxScore.HasValue)
            {
                documents = documents.Where(d => d.Score <= maxScore.Value).ToList();
            }

            if (minDist.HasValue && query.Vector != null)
            {
                documents = documents.Where(d => GetDistanceFromScore(query.Vector, d.Score) >= minDist.Value).ToList();
            }

            if (maxDist.HasValue && query.Vector != null)
            {
                documents = documents.Where(d => GetDistanceFromScore(query.Vector, d.Score) <= maxDist.Value).ToList();
            }

            if (hasVector)
            {
                foreach (DocumentRecord doc in documents)
                {
                    doc.VectorScore = doc.Score;
                }
            }

            return BuildResult(query, documents, totalCount, offset);
        }

        private async Task<SearchResult> SearchFullTextAsync(string tableName, SearchQuery query, TextSearchContext text, int offset, CancellationToken token)
        {
            List<string> conditions = BuildFilterConditions(tableName, query);
            conditions.Add(text.MatchCondition);

            StringBuilder inner = new StringBuilder();
            inner.Append("SELECT " + GetSelectColumns(query, null) + ", ");
            inner.Append("0.0 AS distance, ");
            inner.Append(text.RankExpression + " AS text_score ");
            inner.Append("FROM " + tableName);
            inner.Append(" WHERE " + string.Join(" AND ", conditions));

            // Thresholds are applied in SQL so TotalRecords, EndOfResults and ContinuationToken agree with the pages.
            List<string> thresholds = new List<string>();
            AddThreshold(thresholds, "r.text_score", ">=", query.FullText.MinimumScore);
            AddThreshold(thresholds, "r.text_score", ">=", query.MinimumScore ?? (query.Vector != null ? query.Vector.MinimumScore : null));
            AddThreshold(thresholds, "r.text_score", "<=", query.MaximumScore ?? (query.Vector != null ? query.Vector.MaximumScore : null));
            string whereThresholds = thresholds.Count > 0 ? " WHERE " + string.Join(" AND ", thresholds) : "";

            string selectQuery =
                "SELECT r.*, r.text_score AS score, COUNT(*) OVER () AS total_count, numnode(" + text.TsQueryExpression + ") AS term_count " +
                "FROM (" + inner.ToString() + ") r" + whereThresholds +
                " ORDER BY " + GetOrderByClause(query.SortOrder) + ", id ASC" +
                " LIMIT " + query.MaxResults.ToString(CultureInfo.InvariantCulture) +
                " OFFSET " + offset.ToString(CultureInfo.InvariantCulture);

            string countQuery =
                "SELECT (SELECT COUNT(*) FROM (" + inner.ToString() + ") r" + whereThresholds + ") AS total_count, " +
                "numnode(" + text.TsQueryExpression + ") AS term_count";

            DataTable selectResult = await _Driver.ExecuteQueryAsync(selectQuery, false, token).ConfigureAwait(false);
            List<DocumentRecord> documents = DocumentRecord.FromDataTable(selectResult);

            long totalCount;
            long termCount;
            if (selectResult != null && selectResult.Rows.Count > 0)
            {
                totalCount = ReadLong(selectResult, "total_count");
                termCount = ReadLong(selectResult, "term_count");
            }
            else
            {
                DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
                totalCount = ReadLong(countResult, "total_count");
                termCount = ReadLong(countResult, "term_count");
            }

            SearchResult result = BuildResult(query, documents, totalCount, offset);
            if (termCount == 0) result.AddNotice(NoticeNoSearchableTerms);
            return result;
        }

        private async Task<SearchResult> SearchHybridFusedAsync(
            string tableName,
            int dimensionality,
            SearchQuery query,
            TextSearchContext text,
            HybridStrategyEnum strategy,
            int offset,
            CancellationToken token)
        {
            HybridQuery hybrid = query.Hybrid ?? new HybridQuery();
            CollapseQuery collapse = query.Collapse;
            int pool = hybrid.CandidatePool ?? (collapse != null ? collapse.CandidatePool : null) ?? GetDefaultPool(query);
            int k = hybrid.RrfK;
            double textWeight = query.FullText.TextWeight;
            double vectorWeight = 1.0 - textWeight;

            // Recency is an Rrf-only signal; the other strategies ignore it (SearchService adds the notice).
            double recencyWeight = strategy == HybridStrategyEnum.Rrf ? hybrid.RecencyWeight : 0.0;
            bool recency = recencyWeight > 0.0;
            bool collapsing = collapse != null;
            bool grouping = recency || collapsing;

            string vectorOperator = GetVectorOperator(query.Vector);
            string vectorLiteral = FormatVectorLiteral(query.Vector, dimensionality);
            string distanceExpression = GetDistanceExpression(vectorOperator, vectorLiteral);
            string scoreExpression = GetScoreExpression(query.Vector, vectorOperator, vectorLiteral);
            string poolText = pool.ToString(CultureInfo.InvariantCulture);

            // Vector leg: the same filters plus any vector-specific thresholds, ordered by the raw distance
            // operator so the HNSW index can satisfy ORDER BY + LIMIT.
            List<string> vectorConditions = BuildFilterConditions(tableName, query);
            AddThreshold(vectorConditions, scoreExpression, ">=", query.Vector.MinimumScore);
            AddThreshold(vectorConditions, scoreExpression, "<=", query.Vector.MaximumScore);
            AddThreshold(vectorConditions, distanceExpression, ">=", query.MinimumDistance ?? query.Vector.MinimumDistance);
            AddThreshold(vectorConditions, distanceExpression, "<=", query.MaximumDistance ?? query.Vector.MaximumDistance);

            // Text leg: the same filters plus the text match, ordered by relevance; GIN-satisfiable.
            List<string> textConditions = BuildFilterConditions(tableName, query);
            textConditions.Add(text.MatchCondition);
            AddThreshold(textConditions, text.RankExpression, ">=", query.FullText.MinimumScore);

            StringBuilder cte = new StringBuilder();
            cte.Append("WITH vec AS (");
            cte.Append("SELECT id, " + distanceExpression + " AS distance FROM " + tableName);
            if (vectorConditions.Count > 0) cte.Append(" WHERE " + string.Join(" AND ", vectorConditions));
            cte.Append(" ORDER BY " + distanceExpression + " LIMIT " + poolText + "), ");
            cte.Append("vr AS (SELECT id, ROW_NUMBER() OVER (ORDER BY distance, id) AS rnk FROM vec), ");
            cte.Append("txt AS (");
            cte.Append("SELECT id, " + text.RankExpression + " AS text_score FROM " + tableName);
            cte.Append(" WHERE " + string.Join(" AND ", textConditions));
            cte.Append(" ORDER BY text_score DESC, id LIMIT " + poolText + "), ");
            cte.Append("tr AS (SELECT id, text_score, ROW_NUMBER() OVER (ORDER BY text_score DESC, id) AS rnk FROM txt), ");
            cte.Append("cand AS (");
            cte.Append("SELECT COALESCE(vr.id, tr.id) AS id, tr.text_score, vr.rnk AS vector_rank, tr.rnk AS text_rank ");
            cte.Append("FROM vr FULL OUTER JOIN tr ON vr.id = tr.id), ");
            cte.Append("scored AS (");
            cte.Append("SELECT c.id, c.text_score, c.vector_rank, c.text_rank, (d.embeddings " + vectorOperator + " " + vectorLiteral + ") AS distance");
            if (grouping)
            {
                // The recency key is the collapse group when collapsing, otherwise the document itself.
                string groupKey = collapsing ? BuildGroupKeySql(tableName, collapse, "d") : "CAST(d.id AS text)";
                cte.Append(", d.created_utc AS candidate_created_utc, " + groupKey + " AS group_key");
            }
            cte.Append(" ");
            cte.Append("FROM cand c JOIN " + tableName + " d ON d.id = c.id), ");
            if (grouping)
            {
                cte.Append("grp AS (SELECT group_key, MAX(candidate_created_utc) AS newest, COUNT(*) AS group_hits FROM scored GROUP BY group_key), ");
                if (recency)
                {
                    // Newest first; ties broken by the key in byte order so the ranking is deterministic.
                    cte.Append("rec AS (SELECT group_key, group_hits, ROW_NUMBER() OVER (ORDER BY newest DESC, group_key COLLATE \"C\") AS recency_rank FROM grp), ");
                }
            }
            cte.Append("fused AS (");
            cte.Append("SELECT s.*, ");
            if (grouping) cte.Append("g.group_hits, ");
            if (recency) cte.Append("g.recency_rank, ");
            cte.Append(GetScoreFromDistanceSql(query.Vector, "s.distance") + " AS vector_score, ");
            if (strategy == HybridStrategyEnum.Linear)
                cte.Append(GetLinearFusionSql(query.Vector, vectorWeight, textWeight) + " AS score ");
            else if (recency)
                cte.Append(GetRrfFusionSql(vectorWeight, textWeight, recencyWeight, k, "g.recency_rank") + " AS score ");
            else
                cte.Append(GetRrfFusionSql(vectorWeight, textWeight, k) + " AS score ");
            cte.Append("FROM scored s");
            if (grouping) cte.Append(" JOIN " + (recency ? "rec" : "grp") + " g ON g.group_key = s.group_key");
            cte.Append(")");

            // Collapsing keeps each group's best-scoring candidate (ties by id); thresholds apply to that representative.
            string source = "fused";
            if (collapsing)
            {
                cte.Append(", best AS (SELECT f.*, ROW_NUMBER() OVER (PARTITION BY f.group_key ORDER BY f.score DESC, f.id) AS group_pos FROM fused f)");
                source = "best";
            }

            List<string> thresholds = new List<string>();
            if (collapsing) thresholds.Add("f.group_pos = 1");
            AddThreshold(thresholds, "f.score", ">=", query.MinimumScore);
            AddThreshold(thresholds, "f.score", "<=", query.MaximumScore);
            string whereThresholds = thresholds.Count > 0 ? " WHERE " + string.Join(" AND ", thresholds) : "";

            string extraColumns = "";
            if (recency) extraColumns += "f.recency_rank, ";
            if (collapsing) extraColumns += "f.group_key, f.group_hits, ";

            string poolColumns = collapsing
                ? ", (SELECT COUNT(*) FROM grp) AS group_count, "
                    + "((SELECT COUNT(*) FROM vec) >= " + poolText + " OR (SELECT COUNT(*) FROM txt) >= " + poolText + ") AS pool_full"
                : "";

            string selectQuery =
                cte.ToString() + " " +
                "SELECT " + GetSelectColumns(query, "d") + ", f.distance, f.text_score, f.vector_score, f.vector_rank, f.text_rank, " + extraColumns + "f.score, " +
                "COUNT(*) OVER () AS total_count, numnode(" + text.TsQueryExpression + ") AS term_count" + poolColumns + " " +
                "FROM " + source + " f JOIN " + tableName + " d ON d.id = f.id" + whereThresholds +
                " ORDER BY " + GetHybridOrderByClause(query.SortOrder) +
                " LIMIT " + query.MaxResults.ToString(CultureInfo.InvariantCulture) +
                " OFFSET " + offset.ToString(CultureInfo.InvariantCulture);

            string countQuery =
                cte.ToString() + " " +
                "SELECT (SELECT COUNT(*) FROM " + source + " f" + whereThresholds + ") AS total_count, " +
                "numnode(" + text.TsQueryExpression + ") AS term_count" + poolColumns;

            List<string> setup = GetHnswSetupStatements(pool);

            DataTable selectResult = await _Driver.ExecuteQueryAsync(selectQuery, setup, false, token).ConfigureAwait(false);
            List<DocumentRecord> documents = DocumentRecord.FromDataTable(selectResult);

            long totalCount;
            long termCount;
            if (selectResult != null && selectResult.Rows.Count > 0)
            {
                totalCount = ReadLong(selectResult, "total_count");
                termCount = ReadLong(selectResult, "term_count");
            }
            else
            {
                DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, setup, false, token).ConfigureAwait(false);
                totalCount = ReadLong(countResult, "total_count");
                termCount = ReadLong(countResult, "term_count");
                selectResult = countResult;
            }

            SearchResult result = BuildResult(query, documents, totalCount, offset);
            if (termCount == 0) result.AddNotice(NoticeNoSearchableTermsHybrid);
            if (collapsing) AddPoolShortfallNotice(result, query, selectResult, pool, offset);
            return result;
        }

        private async Task<SearchResult> SearchVectorCollapsedAsync(string tableName, int dimensionality, SearchQuery query, int offset, CancellationToken token)
        {
            // Pool-then-collapse: a page of chunks is not a page of groups, so the vector leg retrieves a candidate
            // pool (HNSW-satisfiable ORDER BY distance LIMIT pool), groups it, and keeps each group's nearest chunk.
            // The uncollapsed path (SearchVectorOnlyAsync) is untouched.
            CollapseQuery collapse = query.Collapse;
            int pool = collapse.CandidatePool ?? GetDefaultPool(query);
            string poolText = pool.ToString(CultureInfo.InvariantCulture);

            string vectorOperator = GetVectorOperator(query.Vector);
            string vectorLiteral = FormatVectorLiteral(query.Vector, dimensionality);
            string distanceExpression = GetDistanceExpression(vectorOperator, vectorLiteral);
            string scoreExpression = GetScoreFromDistanceSql(query.Vector, "b.distance");

            List<string> conditions = BuildFilterConditions(tableName, query);

            StringBuilder cte = new StringBuilder();
            cte.Append("WITH vec AS (");
            cte.Append("SELECT id, " + distanceExpression + " AS distance FROM " + tableName);
            if (conditions.Count > 0) cte.Append(" WHERE " + string.Join(" AND ", conditions));
            cte.Append(" ORDER BY " + distanceExpression + " LIMIT " + poolText + "), ");
            cte.Append("scored AS (SELECT v.id, v.distance, " + BuildGroupKeySql(tableName, collapse, "d") + " AS group_key ");
            cte.Append("FROM vec v JOIN " + tableName + " d ON d.id = v.id), ");
            cte.Append("best AS (SELECT s.*, COUNT(*) OVER (PARTITION BY s.group_key) AS group_hits, ");
            cte.Append("ROW_NUMBER() OVER (PARTITION BY s.group_key ORDER BY s.distance, s.id) AS group_pos FROM scored s)");

            // Thresholds apply to each group's representative, in SQL, so pages are never short.
            List<string> thresholds = new List<string> { "b.group_pos = 1" };
            AddThreshold(thresholds, scoreExpression, ">=", query.MinimumScore ?? query.Vector.MinimumScore);
            AddThreshold(thresholds, scoreExpression, "<=", query.MaximumScore ?? query.Vector.MaximumScore);
            AddThreshold(thresholds, "b.distance", ">=", query.MinimumDistance ?? query.Vector.MinimumDistance);
            AddThreshold(thresholds, "b.distance", "<=", query.MaximumDistance ?? query.Vector.MaximumDistance);
            string whereThresholds = " WHERE " + string.Join(" AND ", thresholds);

            string poolColumns = ", (SELECT COUNT(DISTINCT group_key) FROM scored) AS group_count, "
                + "((SELECT COUNT(*) FROM vec) >= " + poolText + ") AS pool_full";

            string selectQuery =
                cte.ToString() + " " +
                "SELECT " + GetSelectColumns(query, "d") + ", b.distance, " + scoreExpression + " AS score, " +
                "CAST(NULL AS double precision) AS text_score, b.group_key, b.group_hits, " +
                "COUNT(*) OVER () AS total_count" + poolColumns + " " +
                "FROM best b JOIN " + tableName + " d ON d.id = b.id" + whereThresholds +
                " ORDER BY " + GetOrderByClause(query.SortOrder) + ", id ASC" +
                " LIMIT " + query.MaxResults.ToString(CultureInfo.InvariantCulture) +
                " OFFSET " + offset.ToString(CultureInfo.InvariantCulture);

            string countQuery =
                cte.ToString() + " " +
                "SELECT (SELECT COUNT(*) FROM best b" + whereThresholds + ") AS total_count" + poolColumns;

            List<string> setup = GetHnswSetupStatements(pool);

            DataTable selectResult = await _Driver.ExecuteQueryAsync(selectQuery, setup, false, token).ConfigureAwait(false);
            List<DocumentRecord> documents = DocumentRecord.FromDataTable(selectResult);

            long totalCount;
            if (selectResult != null && selectResult.Rows.Count > 0)
            {
                totalCount = ReadLong(selectResult, "total_count");
            }
            else
            {
                selectResult = await _Driver.ExecuteQueryAsync(countQuery, setup, false, token).ConfigureAwait(false);
                totalCount = ReadLong(selectResult, "total_count");
            }

            foreach (DocumentRecord doc in documents)
            {
                doc.VectorScore = doc.Score;
            }

            SearchResult result = BuildResult(query, documents, totalCount, offset);
            AddPoolShortfallNotice(result, query, selectResult, pool, offset);
            return result;
        }

        private async Task<SearchResult> SearchFullTextCollapsedAsync(string tableName, SearchQuery query, TextSearchContext text, int offset, CancellationToken token)
        {
            // Same shape as SearchVectorCollapsedAsync over the text leg: rank a candidate pool (GIN-satisfiable match),
            // group it, and keep each group's highest-ranked chunk. The uncollapsed path (SearchFullTextAsync) is untouched.
            CollapseQuery collapse = query.Collapse;
            int pool = collapse.CandidatePool ?? GetDefaultPool(query);
            string poolText = pool.ToString(CultureInfo.InvariantCulture);

            List<string> conditions = BuildFilterConditions(tableName, query);
            conditions.Add(text.MatchCondition);
            AddThreshold(conditions, text.RankExpression, ">=", query.FullText.MinimumScore);

            StringBuilder cte = new StringBuilder();
            cte.Append("WITH txt AS (");
            cte.Append("SELECT id, " + text.RankExpression + " AS text_score FROM " + tableName);
            cte.Append(" WHERE " + string.Join(" AND ", conditions));
            cte.Append(" ORDER BY text_score DESC, id LIMIT " + poolText + "), ");
            cte.Append("scored AS (SELECT x.id, x.text_score, " + BuildGroupKeySql(tableName, collapse, "d") + " AS group_key ");
            cte.Append("FROM txt x JOIN " + tableName + " d ON d.id = x.id), ");
            cte.Append("best AS (SELECT s.*, COUNT(*) OVER (PARTITION BY s.group_key) AS group_hits, ");
            cte.Append("ROW_NUMBER() OVER (PARTITION BY s.group_key ORDER BY s.text_score DESC, s.id) AS group_pos FROM scored s)");

            List<string> thresholds = new List<string> { "b.group_pos = 1" };
            AddThreshold(thresholds, "b.text_score", ">=", query.MinimumScore ?? (query.Vector != null ? query.Vector.MinimumScore : null));
            AddThreshold(thresholds, "b.text_score", "<=", query.MaximumScore ?? (query.Vector != null ? query.Vector.MaximumScore : null));
            string whereThresholds = " WHERE " + string.Join(" AND ", thresholds);

            string poolColumns = ", (SELECT COUNT(DISTINCT group_key) FROM scored) AS group_count, "
                + "((SELECT COUNT(*) FROM txt) >= " + poolText + ") AS pool_full";

            string selectQuery =
                cte.ToString() + " " +
                "SELECT " + GetSelectColumns(query, "d") + ", 0.0 AS distance, b.text_score, b.text_score AS score, b.group_key, b.group_hits, " +
                "COUNT(*) OVER () AS total_count, numnode(" + text.TsQueryExpression + ") AS term_count" + poolColumns + " " +
                "FROM best b JOIN " + tableName + " d ON d.id = b.id" + whereThresholds +
                " ORDER BY " + GetOrderByClause(query.SortOrder) + ", id ASC" +
                " LIMIT " + query.MaxResults.ToString(CultureInfo.InvariantCulture) +
                " OFFSET " + offset.ToString(CultureInfo.InvariantCulture);

            string countQuery =
                cte.ToString() + " " +
                "SELECT (SELECT COUNT(*) FROM best b" + whereThresholds + ") AS total_count, " +
                "numnode(" + text.TsQueryExpression + ") AS term_count" + poolColumns;

            DataTable selectResult = await _Driver.ExecuteQueryAsync(selectQuery, false, token).ConfigureAwait(false);
            List<DocumentRecord> documents = DocumentRecord.FromDataTable(selectResult);

            long totalCount;
            long termCount;
            if (selectResult != null && selectResult.Rows.Count > 0)
            {
                totalCount = ReadLong(selectResult, "total_count");
                termCount = ReadLong(selectResult, "term_count");
            }
            else
            {
                selectResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
                totalCount = ReadLong(selectResult, "total_count");
                termCount = ReadLong(selectResult, "term_count");
            }

            SearchResult result = BuildResult(query, documents, totalCount, offset);
            if (termCount == 0) result.AddNotice(NoticeNoSearchableTerms);
            AddPoolShortfallNotice(result, query, selectResult, pool, offset);
            return result;
        }

        private async Task<SearchResult> SearchHybridFilterAsync(string tableName, int dimensionality, SearchQuery query, TextSearchContext text, int offset, CancellationToken token)
        {
            // Legacy semantics: the text query is a required filter and documents are ranked by the raw blend
            // (1 - w) * vectorScore + w * textScore.
            string vectorOperator = GetVectorOperator(query.Vector);
            string vectorLiteral = FormatVectorLiteral(query.Vector, dimensionality);
            string scoreExpression = GetScoreExpression(query.Vector, vectorOperator, vectorLiteral);
            string distanceExpression = GetDistanceExpression(vectorOperator, vectorLiteral);
            double textWeight = query.FullText.TextWeight;
            double vectorWeight = 1.0 - textWeight;

            List<string> conditions = BuildFilterConditions(tableName, query);
            conditions.Add(text.MatchCondition);

            StringBuilder inner = new StringBuilder();
            inner.Append("SELECT " + GetSelectColumns(query, null) + ", ");
            inner.Append(distanceExpression + " AS distance, ");
            inner.Append(text.RankExpression + " AS text_score, ");
            inner.Append(scoreExpression + " AS vector_score, ");
            inner.Append("(" + FormatDouble(vectorWeight) + " * " + scoreExpression
                + " + " + FormatDouble(textWeight) + " * " + text.RankExpression + ") AS score ");
            inner.Append("FROM " + tableName);
            inner.Append(" WHERE " + string.Join(" AND ", conditions));

            List<string> thresholds = new List<string>();
            AddThreshold(thresholds, "r.score", ">=", query.MinimumScore ?? query.Vector.MinimumScore);
            AddThreshold(thresholds, "r.score", "<=", query.MaximumScore ?? query.Vector.MaximumScore);
            AddThreshold(thresholds, "r.distance", ">=", query.MinimumDistance ?? query.Vector.MinimumDistance);
            AddThreshold(thresholds, "r.distance", "<=", query.MaximumDistance ?? query.Vector.MaximumDistance);
            AddThreshold(thresholds, "r.text_score", ">=", query.FullText.MinimumScore);
            string whereThresholds = thresholds.Count > 0 ? " WHERE " + string.Join(" AND ", thresholds) : "";

            string selectQuery =
                "SELECT r.*, COUNT(*) OVER () AS total_count, numnode(" + text.TsQueryExpression + ") AS term_count " +
                "FROM (" + inner.ToString() + ") r" + whereThresholds +
                " ORDER BY " + GetOrderByClause(query.SortOrder) + ", id ASC" +
                " LIMIT " + query.MaxResults.ToString(CultureInfo.InvariantCulture) +
                " OFFSET " + offset.ToString(CultureInfo.InvariantCulture);

            string countQuery =
                "SELECT (SELECT COUNT(*) FROM (" + inner.ToString() + ") r" + whereThresholds + ") AS total_count, " +
                "numnode(" + text.TsQueryExpression + ") AS term_count";

            DataTable selectResult = await _Driver.ExecuteQueryAsync(selectQuery, false, token).ConfigureAwait(false);
            List<DocumentRecord> documents = DocumentRecord.FromDataTable(selectResult);

            long totalCount;
            long termCount;
            if (selectResult != null && selectResult.Rows.Count > 0)
            {
                totalCount = ReadLong(selectResult, "total_count");
                termCount = ReadLong(selectResult, "term_count");
            }
            else
            {
                DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
                totalCount = ReadLong(countResult, "total_count");
                termCount = ReadLong(countResult, "term_count");
            }

            SearchResult result = BuildResult(query, documents, totalCount, offset);
            if (termCount == 0) result.AddNotice(NoticeNoSearchableTerms);
            return result;
        }

        private async Task<TextSearchContext> BuildTextSearchContextAsync(string collectionId, FullTextQuery fullText, CancellationToken token)
        {
            string language = NormalizeLanguage(fullText.Language);
            if (!IsPlainIdentifier(language))
                throw new ArgumentException("FullText.Language '" + fullText.Language + "' is not a valid text search configuration name.", nameof(fullText));

            bool useStoredColumn = string.Equals(language, IndexedLanguage, StringComparison.Ordinal)
                && await _Driver.HasStoredTsVectorAsync(collectionId, token).ConfigureAwait(false);

            // content_tsv is to_tsvector('english', COALESCE(content, '')), so for english the stored column and the
            // expression are identical; the expression is also exactly what the legacy _fts index covers.
            string tsvector = useStoredColumn
                ? "content_tsv"
                : "to_tsvector('" + language + "', COALESCE(content, ''))";

            string tsquery = BuildTsQueryExpression(fullText.MatchMode, language, fullText.Query, fullText.MinimumShouldMatch);
            string rankFunction = fullText.SearchType == TextSearchTypeEnum.TsRankCd ? "ts_rank_cd" : "ts_rank";
            string normalization = fullText.Normalization.ToString(CultureInfo.InvariantCulture);

            TextSearchContext context = new TextSearchContext();
            context.Language = language;
            context.UsesIndex = useStoredColumn || string.Equals(language, IndexedLanguage, StringComparison.Ordinal);
            context.TsQueryExpression = tsquery;
            context.MatchCondition = tsvector + " @@ " + tsquery;
            context.RankExpression = rankFunction + "(" + tsvector + ", " + tsquery + ", " + normalization + ")";
            return context;
        }

        private string BuildTsQueryExpression(TextMatchModeEnum matchMode, string language, string queryText, int minimumShouldMatch)
        {
            string sanitizedQuery = _Driver.Sanitize(queryText);

            if (matchMode == TextMatchModeEnum.Any && minimumShouldMatch > 1)
                return BuildMinimumShouldMatchTsQuery(language, sanitizedQuery, minimumShouldMatch);

            switch (matchMode)
            {
                case TextMatchModeEnum.All:
                    return "plainto_tsquery('" + language + "', '" + sanitizedQuery + "')";

                case TextMatchModeEnum.Phrase:
                    return "phraseto_tsquery('" + language + "', '" + sanitizedQuery + "')";

                case TextMatchModeEnum.WebSearch:
                    return "websearch_to_tsquery('" + language + "', '" + sanitizedQuery + "')";

                case TextMatchModeEnum.Any:
                default:
                    // OR together the lexemes to_tsvector produces, so the query and the index agree exactly on
                    // stemming and stop words, and any tsquery operators in the input are neutralized (the input only
                    // ever passes through to_tsvector). Each lexeme is emitted as a quoted tsquery literal with
                    // backslashes and single quotes escaped. A query with no lexemes yields an empty tsquery, which
                    // matches nothing without raising an error. Do NOT OR per-word plainto_tsquery calls instead:
                    // that still ANDs the lexemes within one word ("run-tests" becomes 'run' & 'test').
                    return "(SELECT COALESCE(string_agg('''' || replace(replace(lexeme, '\\', '\\\\'), '''', '''''') || '''', ' | '), '') "
                        + "FROM unnest(to_tsvector('" + language + "', '" + sanitizedQuery + "')))::tsquery";
            }
        }

        private string BuildMinimumShouldMatchTsQuery(string language, string sanitizedQuery, int m)
        {
            // Same lexemes and escaping as the Any expression, but instead of OR-ing the lexemes, OR every m-lexeme
            // conjunction, so the GIN index only returns documents holding at least m distinct query terms. Only the
            // first terms in query order are considered (_MinimumShouldMatchMaxTerms). A query with fewer than m
            // lexemes requires all of them; one with none yields the empty tsquery, which matches nothing.
            string lexemes =
                "SELECT '''' || replace(replace(lexeme, '\\', '\\\\'), '''', '''''') || '''' AS q "
                + "FROM unnest(to_tsvector('" + language + "', '" + sanitizedQuery + "')) "
                + "ORDER BY positions[1] NULLS LAST, lexeme "
                + "LIMIT " + _MinimumShouldMatchMaxTerms.ToString(CultureInfo.InvariantCulture);

            string conjunctions = m >= 3
                ? "SELECT string_agg('(' || a.q || ' & ' || b.q || ' & ' || c.q || ')', ' | ') FROM lex a JOIN lex b ON a.q < b.q JOIN lex c ON b.q < c.q"
                : "SELECT string_agg('(' || a.q || ' & ' || b.q || ')', ' | ') FROM lex a JOIN lex b ON a.q < b.q";

            return "(WITH lex AS (" + lexemes + ") "
                + "SELECT CASE WHEN (SELECT COUNT(*) FROM lex) < " + m.ToString(CultureInfo.InvariantCulture) + " "
                + "THEN (SELECT COALESCE(string_agg(q, ' & '), '') FROM lex) "
                + "ELSE (" + conjunctions + ") END)::tsquery";
        }

        private string BuildGroupKeySql(string tableName, CollapseQuery collapse, string alias)
        {
            // A document with no group value is its own group, keyed by its document_key. The tag value is read with
            // a scalar subquery (served by the _t_dkey index) because _tags allows repeated (document_key, key) rows.
            if (collapse.Field == CollapseFieldEnum.Tag)
            {
                return "COALESCE(NULLIF((SELECT MIN(g.value) FROM " + tableName + "_tags g "
                    + "WHERE g.document_key = " + alias + ".document_key AND g.key = '" + _Driver.Sanitize(collapse.TagKey) + "'), ''), "
                    + alias + ".document_key)";
            }

            return "COALESCE(NULLIF(" + alias + ".document_id, ''), " + alias + ".document_key)";
        }

        private void AddPoolShortfallNotice(SearchResult result, SearchQuery query, DataTable table, int pool, int offset)
        {
            // Only worth saying when a leg filled the pool: otherwise every matching candidate was already considered
            // and a larger pool would not find more groups.
            if (table == null || table.Rows.Count < 1 || !table.Columns.Contains("pool_full")) return;
            object full = table.Rows[0]["pool_full"];
            if (full == null || full == DBNull.Value || !Convert.ToBoolean(full, CultureInfo.InvariantCulture)) return;

            long groups = ReadLong(table, "group_count");
            if (groups >= (long)offset + query.MaxResults) return;

            result.AddNotice(string.Format(CultureInfo.InvariantCulture, NoticeCollapsePoolShortfallFormat, groups, pool));
        }

        private static int GetDefaultPool(SearchQuery query)
        {
            return Math.Min(Math.Max(query.MaxResults * 4, 100), 1000);
        }

        private List<string> BuildFilterConditions(string tableName, SearchQuery query)
        {
            string labelsTableName = tableName + "_labels";
            string tagsTableName = tableName + "_tags";
            List<string> conditions = new List<string>();

            if (query.CreatedBefore.HasValue)
            {
                conditions.Add("created_utc < '" + _Driver.FormatDateTime(query.CreatedBefore.Value) + "'");
            }

            if (query.CreatedAfter.HasValue)
            {
                conditions.Add("created_utc > '" + _Driver.FormatDateTime(query.CreatedAfter.Value) + "'");
            }

            if (query.DocumentIds != null && query.DocumentIds.Count > 0)
            {
                List<string> sanitizedIds = new List<string>();
                foreach (string docId in query.DocumentIds)
                {
                    sanitizedIds.Add("'" + _Driver.Sanitize(docId) + "'");
                }
                conditions.Add("document_id IN (" + string.Join(",", sanitizedIds) + ")");
            }

            if (query.LabelFilter != null)
            {
                if (query.LabelFilter.Required != null && query.LabelFilter.Required.Count > 0)
                {
                    List<string> sanitizedLabels = new List<string>();
                    foreach (string label in query.LabelFilter.Required)
                    {
                        sanitizedLabels.Add("'" + _Driver.Sanitize(label) + "'");
                    }
                    conditions.Add(
                        "document_key IN (" +
                        "SELECT document_key FROM " + labelsTableName + " " +
                        "WHERE label IN (" + string.Join(",", sanitizedLabels) + ")" +
                        ")");
                }

                if (query.LabelFilter.Excluded != null && query.LabelFilter.Excluded.Count > 0)
                {
                    List<string> sanitizedLabels = new List<string>();
                    foreach (string label in query.LabelFilter.Excluded)
                    {
                        sanitizedLabels.Add("'" + _Driver.Sanitize(label) + "'");
                    }
                    conditions.Add(
                        "document_key NOT IN (" +
                        "SELECT document_key FROM " + labelsTableName + " " +
                        "WHERE label IN (" + string.Join(",", sanitizedLabels) + ")" +
                        ")");
                }
            }

            if (query.TagFilter != null)
            {
                if (query.TagFilter.Required != null && query.TagFilter.Required.Count > 0)
                {
                    foreach (TagCondition tagCondition in query.TagFilter.Required)
                    {
                        string tagClause = BuildTagConditionClause(tagsTableName, tagCondition, false);
                        if (!string.IsNullOrEmpty(tagClause))
                        {
                            conditions.Add(tagClause);
                        }
                    }
                }

                if (query.TagFilter.Excluded != null && query.TagFilter.Excluded.Count > 0)
                {
                    foreach (TagCondition tagCondition in query.TagFilter.Excluded)
                    {
                        string tagClause = BuildTagConditionClause(tagsTableName, tagCondition, true);
                        if (!string.IsNullOrEmpty(tagClause))
                        {
                            conditions.Add(tagClause);
                        }
                    }
                }
            }

            if (query.Terms != null)
            {
                if (query.Terms.Required != null && query.Terms.Required.Count > 0)
                {
                    foreach (string term in query.Terms.Required)
                    {
                        string sanitized = _Driver.Sanitize(term).Replace("%", "\\%").Replace("_", "\\_");
                        conditions.Add("content ILIKE '%" + sanitized + "%'");
                    }
                }

                if (query.Terms.Excluded != null && query.Terms.Excluded.Count > 0)
                {
                    foreach (string term in query.Terms.Excluded)
                    {
                        string sanitized = _Driver.Sanitize(term).Replace("%", "\\%").Replace("_", "\\_");
                        conditions.Add("content NOT ILIKE '%" + sanitized + "%'");
                    }
                }
            }

            return conditions;
        }

        private SearchResult BuildResult(SearchQuery query, List<DocumentRecord> documents, long totalCount, int offset)
        {
            SearchResult result = new SearchResult();
            result.Success = true;
            result.MaxResults = query.MaxResults;
            result.TotalRecords = totalCount;
            result.Documents = documents;

            long nextOffset = offset + documents.Count;

            if (nextOffset >= totalCount || documents.Count < query.MaxResults)
            {
                result.EndOfResults = true;
                result.ContinuationToken = null;
                result.RecordsRemaining = 0;
            }
            else
            {
                result.EndOfResults = false;
                result.ContinuationToken = nextOffset.ToString(CultureInfo.InvariantCulture);
                result.RecordsRemaining = totalCount - nextOffset;
            }

            return result;
        }

        private List<string> GetHnswSetupStatements(int pool)
        {
            // Only raise ef_search when the pool exceeds pgvector's default; SET LOCAL scopes it to this query.
            if (pool <= _DefaultEfSearch) return null;
            int efSearch = Math.Min(pool, _MaxEfSearch);
            return new List<string> { "SET LOCAL hnsw.ef_search = " + efSearch.ToString(CultureInfo.InvariantCulture) };
        }

        private string GetRrfFusionSql(double vectorWeight, double textWeight, int k)
        {
            // raw = (1 - w) / (k + vectorRank) + w / (k + textRank), a missing rank contributes 0;
            // multiplying by (k + 1) normalizes to [0, 1] (first in both legs scores 1.0).
            string kText = k.ToString(CultureInfo.InvariantCulture);
            return "((COALESCE(CAST(" + FormatDouble(vectorWeight) + " AS double precision) / (" + kText + " + s.vector_rank), 0) + "
                + "COALESCE(CAST(" + FormatDouble(textWeight) + " AS double precision) / (" + kText + " + s.text_rank), 0)) * (" + kText + " + 1))";
        }

        private string GetRrfFusionSql(double vectorWeight, double textWeight, double recencyWeight, int k, string recencyRankColumn)
        {
            // raw = (1 - w) / (k + vectorRank) + w / (k + textRank) + r / (k + recencyRank), a missing rank contributes 0;
            // multiplying by (k + 1) / ((1 - w) + w + r) normalizes to [0, 1] (first in every signal scores 1.0).
            // Only used when r > 0; with r = 0 the two-signal overload keeps the SQL text unchanged.
            string kText = k.ToString(CultureInfo.InvariantCulture);
            return "((COALESCE(CAST(" + FormatDouble(vectorWeight) + " AS double precision) / (" + kText + " + s.vector_rank), 0) + "
                + "COALESCE(CAST(" + FormatDouble(textWeight) + " AS double precision) / (" + kText + " + s.text_rank), 0) + "
                + "COALESCE(CAST(" + FormatDouble(recencyWeight) + " AS double precision) / (" + kText + " + " + recencyRankColumn + "), 0)) * (" + kText + " + 1) / "
                + "CAST(" + FormatDouble(vectorWeight + textWeight + recencyWeight) + " AS double precision))";
        }

        private string GetLinearFusionSql(VectorQuery vectorQuery, double vectorWeight, double textWeight)
        {
            string vectorNorm;
            if (vectorQuery == null || vectorQuery.SearchType == SearchTypeEnum.CosineSimilarity)
            {
                vectorNorm = "GREATEST(0.0, LEAST(1.0, 1.0 - s.distance))";
            }
            else
            {
                // Min-max within the candidate set; smaller distance is better for every operator.
                vectorNorm = "COALESCE((MAX(s.distance) OVER () - s.distance) / NULLIF(MAX(s.distance) OVER () - MIN(s.distance) OVER (), 0), 1.0)";
            }

            string textNorm = "COALESCE(s.text_score / NULLIF(MAX(s.text_score) OVER (), 0), 0)";

            return "(CAST(" + FormatDouble(vectorWeight) + " AS double precision) * " + vectorNorm + " + "
                + "CAST(" + FormatDouble(textWeight) + " AS double precision) * " + textNorm + ")";
        }

        private string GetScoreFromDistanceSql(VectorQuery vectorQuery, string distanceColumn)
        {
            // Mirrors GetScoreExpression, expressed over an already-computed distance.
            if (vectorQuery != null)
            {
                switch (vectorQuery.SearchType)
                {
                    case SearchTypeEnum.InnerProduct:
                        return "(-" + distanceColumn + ")";
                    case SearchTypeEnum.CosineDistance:
                    case SearchTypeEnum.EuclideanDistance:
                        return "(" + distanceColumn + ")";
                }
            }

            return "(1.0 - " + distanceColumn + ")";
        }

        private string GetHybridOrderByClause(SortOrderEnum sortOrder)
        {
            // Documents that are not text matches have a NULL text_score; keep them after the matches.
            string orderBy = GetOrderByClause(sortOrder);
            if (sortOrder == SortOrderEnum.TextScoreAscending || sortOrder == SortOrderEnum.TextScoreDescending)
                orderBy += " NULLS LAST";
            return orderBy + ", id ASC";
        }

        private string GetSelectColumns(SearchQuery query, string alias)
        {
            // The embedding vector is appended only when the caller explicitly opts in (SearchQuery.IncludeEmbeddings).
            string columns = query.IncludeEmbeddings ? _SelectColumns + ", " + _EmbeddingsColumn : _SelectColumns;
            if (string.IsNullOrEmpty(alias)) return columns;

            List<string> qualified = new List<string>();
            foreach (string column in columns.Split(','))
            {
                qualified.Add(alias + "." + column.Trim());
            }
            return string.Join(", ", qualified);
        }

        private void AddThreshold(List<string> conditions, string expression, string op, double? value)
        {
            if (!value.HasValue) return;
            if (double.IsNaN(value.Value) || double.IsInfinity(value.Value)) return;
            conditions.Add(expression + " " + op + " " + FormatDouble(value.Value));
        }

        private static string FormatDouble(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static long ReadLong(DataTable table, string column)
        {
            if (table == null || table.Rows.Count < 1) return 0;
            if (!table.Columns.Contains(column)) return 0;
            object value = table.Rows[0][column];
            if (value == null || value == DBNull.Value) return 0;
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        private static int ParseOffset(string continuationToken)
        {
            if (!string.IsNullOrEmpty(continuationToken)
                && int.TryParse(continuationToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedOffset)
                && parsedOffset > 0)
            {
                return parsedOffset;
            }

            return 0;
        }

        private static string NormalizeLanguage(string language)
        {
            if (string.IsNullOrWhiteSpace(language)) return IndexedLanguage;
            return language.Trim().ToLowerInvariant();
        }

        private static bool IsPlainIdentifier(string value)
        {
            // Defense in depth: SearchService validates the language against pg_ts_config, and this guard keeps
            // the value a bare identifier before it is interpolated into SQL.
            if (string.IsNullOrEmpty(value) || value.Length > 63) return false;
            foreach (char c in value)
            {
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')) return false;
            }
            return true;
        }

        private string SanitizeTableName(string collectionId)
        {
            return DynamicTableQueries.SanitizeTableName(collectionId);
        }

        private string GetVectorOperator(VectorQuery vectorQuery)
        {
            if (vectorQuery == null) return "<=>";

            switch (vectorQuery.SearchType)
            {
                case SearchTypeEnum.CosineSimilarity:
                case SearchTypeEnum.CosineDistance:
                    return "<=>";
                case SearchTypeEnum.EuclideanSimilarity:
                case SearchTypeEnum.EuclideanDistance:
                    return "<->";
                case SearchTypeEnum.InnerProduct:
                    return "<#>";
                default:
                    return "<=>";
            }
        }

        private string FormatVectorLiteral(VectorQuery vectorQuery, int dimensionality)
        {
            if (vectorQuery == null || vectorQuery.Embeddings == null || vectorQuery.Embeddings.Count == 0)
                return "'[" + string.Join(",", Enumerable.Repeat("0", dimensionality)) + "]'::vector";

            StringBuilder sb = new StringBuilder();
            sb.Append("'[");

            for (int i = 0; i < vectorQuery.Embeddings.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(vectorQuery.Embeddings[i].ToString(CultureInfo.InvariantCulture));
            }

            sb.Append("]'::vector");
            return sb.ToString();
        }

        private string GetDistanceExpression(string vectorOperator, string vectorLiteral)
        {
            return "(embeddings " + vectorOperator + " " + vectorLiteral + ")";
        }

        private string GetScoreExpression(VectorQuery vectorQuery, string vectorOperator, string vectorLiteral)
        {
            if (vectorQuery != null)
            {
                switch (vectorQuery.SearchType)
                {
                    case SearchTypeEnum.InnerProduct:
                        return "(-(embeddings " + vectorOperator + " " + vectorLiteral + "))";
                    case SearchTypeEnum.CosineDistance:
                    case SearchTypeEnum.EuclideanDistance:
                        return "(embeddings " + vectorOperator + " " + vectorLiteral + ")";
                }
            }

            return "(1.0 - (embeddings " + vectorOperator + " " + vectorLiteral + "))";
        }

        private double GetDistanceFromScore(VectorQuery vectorQuery, double score)
        {
            switch (vectorQuery.SearchType)
            {
                case SearchTypeEnum.InnerProduct:
                    return -score;
                case SearchTypeEnum.CosineDistance:
                case SearchTypeEnum.EuclideanDistance:
                    return score;
                default:
                    return 1.0 - score;
            }
        }

        private string GetOrderByClause(SortOrderEnum sortOrder)
        {
            switch (sortOrder)
            {
                case SortOrderEnum.ScoreAscending:
                    return "score ASC";
                case SortOrderEnum.ScoreDescending:
                    return "score DESC";
                case SortOrderEnum.DistanceAscending:
                    return "distance ASC";
                case SortOrderEnum.DistanceDescending:
                    return "distance DESC";
                case SortOrderEnum.CreatedAscending:
                    return "created_utc ASC";
                case SortOrderEnum.CreatedDescending:
                    return "created_utc DESC";
                case SortOrderEnum.TextScoreAscending:
                    return "text_score ASC";
                case SortOrderEnum.TextScoreDescending:
                    return "text_score DESC";
                default:
                    return "score DESC";
            }
        }

        private string BuildTagConditionClause(string tagsTableName, TagCondition tagCondition, bool excluded)
        {
            if (tagCondition == null) return null;
            if (string.IsNullOrEmpty(tagCondition.Key)) return null;

            string sanitizedKey = _Driver.Sanitize(tagCondition.Key);
            string sanitizedValue = tagCondition.Value != null ? _Driver.Sanitize(tagCondition.Value) : "";
            string inOrNotIn = excluded ? "NOT IN" : "IN";

            switch (tagCondition.Condition)
            {
                case TagConditionEnum.Equals:
                    return "document_key " + inOrNotIn + " (" +
                        "SELECT document_key FROM " + tagsTableName + " " +
                        "WHERE key = '" + sanitizedKey + "' AND value = '" + sanitizedValue + "'" +
                        ")";

                case TagConditionEnum.NotEquals:
                    return "document_key " + (excluded ? "IN" : "NOT IN") + " (" +
                        "SELECT document_key FROM " + tagsTableName + " " +
                        "WHERE key = '" + sanitizedKey + "' AND value = '" + sanitizedValue + "'" +
                        ")";

                case TagConditionEnum.GreaterThan:
                    return "document_key " + inOrNotIn + " (" +
                        "SELECT document_key FROM " + tagsTableName + " " +
                        "WHERE key = '" + sanitizedKey + "' AND value > '" + sanitizedValue + "'" +
                        ")";

                case TagConditionEnum.LessThan:
                    return "document_key " + inOrNotIn + " (" +
                        "SELECT document_key FROM " + tagsTableName + " " +
                        "WHERE key = '" + sanitizedKey + "' AND value < '" + sanitizedValue + "'" +
                        ")";

                case TagConditionEnum.Contains:
                    return "document_key " + inOrNotIn + " (" +
                        "SELECT document_key FROM " + tagsTableName + " " +
                        "WHERE key = '" + sanitizedKey + "' AND value LIKE '%" + sanitizedValue + "%'" +
                        ")";

                case TagConditionEnum.ContainsNot:
                    return "document_key " + inOrNotIn + " (" +
                        "SELECT document_key FROM " + tagsTableName + " " +
                        "WHERE key = '" + sanitizedKey + "' AND value NOT LIKE '%" + sanitizedValue + "%'" +
                        ")";

                case TagConditionEnum.StartsWith:
                    return "document_key " + inOrNotIn + " (" +
                        "SELECT document_key FROM " + tagsTableName + " " +
                        "WHERE key = '" + sanitizedKey + "' AND value LIKE '" + sanitizedValue + "%'" +
                        ")";

                case TagConditionEnum.EndsWith:
                    return "document_key " + inOrNotIn + " (" +
                        "SELECT document_key FROM " + tagsTableName + " " +
                        "WHERE key = '" + sanitizedKey + "' AND value LIKE '%" + sanitizedValue + "'" +
                        ")";

                case TagConditionEnum.IsNull:
                    return "document_key " + (excluded ? "IN" : "NOT IN") + " (" +
                        "SELECT document_key FROM " + tagsTableName + " " +
                        "WHERE key = '" + sanitizedKey + "' AND value IS NOT NULL AND value != ''" +
                        ")";

                case TagConditionEnum.IsNotNull:
                    return "document_key " + inOrNotIn + " (" +
                        "SELECT document_key FROM " + tagsTableName + " " +
                        "WHERE key = '" + sanitizedKey + "' AND value IS NOT NULL AND value != ''" +
                        ")";

                default:
                    return null;
            }
        }

        #endregion
    }
}
