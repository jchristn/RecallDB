namespace RecallDb.Core.Models
{
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

        #endregion

        #region Private-Members

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
