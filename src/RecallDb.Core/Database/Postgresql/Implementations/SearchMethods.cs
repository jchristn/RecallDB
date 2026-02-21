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
    using RecallDb.Core.Enums;
    using RecallDb.Core.Models;

    /// <summary>
    /// PostgreSQL implementation of vector similarity search operations for dynamic collection tables.
    /// </summary>
    public class SearchMethods : ISearchMethods
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private readonly PostgresqlDatabaseDriver _Driver;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[SearchMethods] ";

        private const string _SelectColumns =
            "id, document_key, document_id, content_length, etag, sha256, position, content_type, content, binary_data, embeddings::text as embeddings, created_utc";

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
        /// Perform a vector similarity search within a collection.
        /// </summary>
        /// <param name="collectionId">Collection ID.</param>
        /// <param name="dimensionality">Vector dimensionality of the collection.</param>
        /// <param name="query">Search query parameters including embeddings and filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Search result containing matching documents with scores.</returns>
        public async Task<SearchResult> SearchAsync(string collectionId, int dimensionality, SearchQuery query, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentNullException(nameof(collectionId));
            if (query == null) throw new ArgumentNullException(nameof(query));

            string tableName = "collection_" + SanitizeTableName(collectionId);
            string labelsTableName = tableName + "_labels";
            string tagsTableName = tableName + "_tags";

            // Determine search modes
            bool hasVector = query.Vector != null && query.Vector.Embeddings != null && query.Vector.Embeddings.Count > 0;
            bool hasFullText = query.FullText != null && !string.IsNullOrWhiteSpace(query.FullText.Query);

            string vectorOperator = GetVectorOperator(query.Vector);
            string vectorLiteral = FormatVectorLiteral(query.Vector, dimensionality);
            string scoreExpression = GetScoreExpression(query.Vector, vectorOperator, vectorLiteral);
            string distanceExpression = GetDistanceExpression(vectorOperator, vectorLiteral);

            // Full-text search setup
            string ftsScoreExpression = null;
            string ftsMatchCondition = null;

            if (hasFullText)
            {
                string sanitizedQuery = _Driver.Sanitize(query.FullText.Query);
                string language = _Driver.Sanitize(query.FullText.Language ?? "english");
                string normalization = query.FullText.Normalization.ToString(CultureInfo.InvariantCulture);
                string rankFunction = query.FullText.SearchType == TextSearchTypeEnum.TsRankCd
                    ? "ts_rank_cd" : "ts_rank";

                string tsvector = "to_tsvector('" + language + "', COALESCE(content, ''))";
                string tsquery = "plainto_tsquery('" + language + "', '" + sanitizedQuery + "')";

                ftsScoreExpression = rankFunction + "(" + tsvector + ", " + tsquery + ", " + normalization + ")";
                ftsMatchCondition = tsvector + " @@ " + tsquery;
            }

            // Build SELECT clause based on search mode
            StringBuilder sb = new StringBuilder();

            if (hasVector && hasFullText)
            {
                // Hybrid mode: weighted blend of vector and text scores
                double textWeight = Math.Clamp(query.FullText.TextWeight, 0.0, 1.0);
                double vectorWeight = 1.0 - textWeight;
                string vectorScoreExpr = GetScoreExpression(query.Vector, vectorOperator, vectorLiteral);

                sb.Append("SELECT " + _SelectColumns + ", ");
                sb.Append(distanceExpression + " AS distance, ");
                sb.Append(ftsScoreExpression + " AS text_score, ");
                sb.Append("(" + vectorWeight.ToString(CultureInfo.InvariantCulture) + " * " + vectorScoreExpr
                    + " + " + textWeight.ToString(CultureInfo.InvariantCulture) + " * " + ftsScoreExpression
                    + ") AS score ");
            }
            else if (hasFullText)
            {
                // Full-text-only mode
                sb.Append("SELECT " + _SelectColumns + ", ");
                sb.Append("0.0 AS distance, ");
                sb.Append(ftsScoreExpression + " AS text_score, ");
                sb.Append(ftsScoreExpression + " AS score ");
            }
            else
            {
                // Vector-only mode (existing behavior, unchanged)
                sb.Append("SELECT " + _SelectColumns + ", ");
                sb.Append(distanceExpression + " AS distance, ");
                sb.Append(scoreExpression + " AS score ");
            }

            sb.Append("FROM " + tableName);

            // Build WHERE clause
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

            // Full-text search match condition
            if (hasFullText)
            {
                conditions.Add(ftsMatchCondition);
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

            if (conditions.Count > 0)
            {
                sb.Append(" WHERE " + string.Join(" AND ", conditions));
            }

            // Build ORDER BY clause
            string orderBy = GetOrderByClause(query.SortOrder);
            sb.Append(" ORDER BY " + orderBy);

            // Build LIMIT/OFFSET
            int offset = 0;

            if (!string.IsNullOrEmpty(query.ContinuationToken))
            {
                if (int.TryParse(query.ContinuationToken, out int parsedOffset))
                {
                    offset = parsedOffset;
                }
            }

            sb.Append(" LIMIT " + query.MaxResults.ToString(CultureInfo.InvariantCulture));
            sb.Append(" OFFSET " + offset.ToString(CultureInfo.InvariantCulture));

            string selectQuery = sb.ToString();

            // Build count query with same filters
            StringBuilder countSb = new StringBuilder();
            countSb.Append("SELECT COUNT(*) AS count FROM " + tableName);

            if (conditions.Count > 0)
            {
                countSb.Append(" WHERE " + string.Join(" AND ", conditions));
            }

            string countQuery = countSb.ToString();

            // Execute queries
            DataTable selectResult = await _Driver.ExecuteQueryAsync(selectQuery, false, token).ConfigureAwait(false);
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);

            List<DocumentRecord> documents = DocumentRecord.FromDataTable(selectResult);
            long totalCount = 0;

            if (countResult != null && countResult.Rows.Count > 0)
            {
                totalCount = Convert.ToInt64(countResult.Rows[0]["count"]);
            }

            // Apply score/distance filtering from query-level thresholds
            {
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
            }

            // Apply full-text minimum score threshold
            if (hasFullText && query.FullText.MinimumScore.HasValue)
            {
                documents = documents
                    .Where(d => d.TextScore.HasValue && d.TextScore.Value >= query.FullText.MinimumScore.Value)
                    .ToList();
            }

            // Build result
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

            if (_Logging != null) _Logging.Debug(_Header + "search returned " + documents.Count + " results from " + collectionId);
            return result;
        }

        #endregion

        #region Private-Methods

        private string SanitizeTableName(string collectionId)
        {
            if (string.IsNullOrEmpty(collectionId)) return "unknown";
            return collectionId.Replace("-", "_").Replace(".", "_");
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
