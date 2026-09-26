namespace RecallDb.Core.Models
{
    using System;
    using System.Text.Json.Serialization;
    using RecallDb.Core.Enums;

    /// <summary>
    /// Collapse options: return one hit per group (its best-scoring candidate) instead of one hit per chunk.
    /// When set, MaxResults, TotalRecords, RecordsRemaining, and continuation tokens all count groups, and each
    /// hit carries GroupKey and GroupHits. Supported with vector-only, full-text-only, and hybrid Rrf and Linear
    /// searches; rejected with hybrid Filter.
    /// </summary>
    public class CollapseQuery
    {
        #region Public-Members

        /// <summary>
        /// What to group by.
        /// DocumentId (default): the document_id column. Tag: the value of the tag named TagKey.
        /// A document with no group value is its own group, keyed by its DocumentKey.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not a defined CollapseFieldEnum.</exception>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public CollapseFieldEnum Field
        {
            get
            {
                return _Field;
            }
            set
            {
                if (!Enum.IsDefined(typeof(CollapseFieldEnum), value))
                    throw new ArgumentOutOfRangeException(nameof(Field), "Collapse.Field must be one of DocumentId or Tag.");
                _Field = value;
            }
        }

        /// <summary>
        /// Name of the tag whose value is the group key. Required when Field is Tag; ignored otherwise.
        /// Default: null. Maximum length: 256 characters.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is longer than 256 characters.</exception>
        public string TagKey
        {
            get
            {
                return _TagKey;
            }
            set
            {
                if (value != null && value.Length > 256)
                    throw new ArgumentOutOfRangeException(nameof(TagKey), "Collapse.TagKey must be at most 256 characters.");
                _TagKey = value;
            }
        }

        /// <summary>
        /// Number of candidates retrieved before collapsing, for vector-only and full-text-only searches.
        /// Hybrid searches use Hybrid.CandidatePool when it is set, and this value otherwise.
        /// Default: null, which means max(MaxResults x 4, 100) capped at 1000. Minimum: 1. Maximum: 10000.
        /// Groups are formed within the pool, so a pool with fewer distinct groups than MaxResults returns fewer
        /// hits and a Notice.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is outside 1-10000.</exception>
        public int? CandidatePool
        {
            get
            {
                return _CandidatePool;
            }
            set
            {
                if (value.HasValue && (value.Value < 1 || value.Value > 10000))
                    throw new ArgumentOutOfRangeException(nameof(CandidatePool), "Collapse.CandidatePool must be between 1 and 10000.");
                _CandidatePool = value;
            }
        }

        #endregion

        #region Private-Members

        private CollapseFieldEnum _Field = CollapseFieldEnum.DocumentId;
        private string _TagKey = null;
        private int? _CandidatePool = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public CollapseQuery()
        {
        }

        #endregion
    }
}
