namespace RecallDb.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using RecallDb.Core.Enums;

    /// <summary>
    /// Vector search query parameters.
    /// </summary>
    public class VectorQuery
    {
        #region Public-Members

        /// <summary>
        /// Type of vector search to perform.
        /// Default: CosineSimilarity.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SearchTypeEnum SearchType
        {
            get
            {
                return _SearchType;
            }
            set
            {
                _SearchType = value;
            }
        }

        /// <summary>
        /// Vector embeddings to search with.
        /// </summary>
        public List<float> Embeddings
        {
            get
            {
                return _Embeddings;
            }
            set
            {
                _Embeddings = value;
            }
        }

        /// <summary>
        /// Minimum score threshold for results.
        /// </summary>
        public double? MinimumScore
        {
            get
            {
                return _MinimumScore;
            }
            set
            {
                _MinimumScore = value;
            }
        }

        /// <summary>
        /// Maximum score threshold for results.
        /// </summary>
        public double? MaximumScore
        {
            get
            {
                return _MaximumScore;
            }
            set
            {
                _MaximumScore = value;
            }
        }

        /// <summary>
        /// Minimum distance threshold for results.
        /// </summary>
        public double? MinimumDistance
        {
            get
            {
                return _MinimumDistance;
            }
            set
            {
                _MinimumDistance = value;
            }
        }

        /// <summary>
        /// Maximum distance threshold for results.
        /// </summary>
        public double? MaximumDistance
        {
            get
            {
                return _MaximumDistance;
            }
            set
            {
                _MaximumDistance = value;
            }
        }

        /// <summary>
        /// Size of the HNSW candidate list (pgvector's hnsw.ef_search) for this search, which is also the most rows the
        /// vector index can return. Larger values raise recall and allow deeper pages, especially with filters, at some
        /// cost in latency. Only cosine searches use the index; other search types ignore it.
        /// Default: null, which means min(max((offset + MaxResults) x 4, 100), 1000) for a vector-only search, and the
        /// candidate pool (at most 1000) for hybrid and collapsed searches.
        /// Minimum: 1. Maximum: 1000. Values outside the range are clamped.
        /// </summary>
        public int? EfSearch
        {
            get
            {
                return _EfSearch;
            }
            set
            {
                if (value.HasValue) value = Math.Min(Math.Max(value.Value, MinEfSearch), MaxEfSearch);
                _EfSearch = value;
            }
        }

        /// <summary>
        /// Smallest accepted EfSearch.
        /// </summary>
        public const int MinEfSearch = 1;

        /// <summary>
        /// Largest accepted EfSearch (pgvector 0.5.1 rejects hnsw.ef_search above 1000).
        /// </summary>
        public const int MaxEfSearch = 1000;

        #endregion

        #region Private-Members

        private int? _EfSearch = null;
        private SearchTypeEnum _SearchType = SearchTypeEnum.CosineSimilarity;
        private List<float> _Embeddings = null;
        private double? _MinimumScore = null;
        private double? _MaximumScore = null;
        private double? _MinimumDistance = null;
        private double? _MaximumDistance = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public VectorQuery()
        {
        }

        #endregion
    }
}
