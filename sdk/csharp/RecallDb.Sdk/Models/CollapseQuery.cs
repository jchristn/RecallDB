namespace RecallDb.Sdk.Models
{
    /// <summary>
    /// Collapse options for a search: return one hit per group (its best-scoring chunk) instead of one per chunk.
    /// Supported with vector-only, full-text-only, and hybrid Rrf and Linear searches.
    /// </summary>
    public class CollapseQuery
    {
        /// <summary>
        /// What to group by. Values: DocumentId (the chunks of one logical document) or Tag (the value of the tag
        /// named TagKey). A document with no group value is its own group, keyed by its DocumentKey.
        /// See <see cref="RecallDb.Sdk.Constants.CollapseFields"/>.
        /// Default: DocumentId.
        /// </summary>
        public string Field { get; set; }

        /// <summary>
        /// Name of the tag whose value is the group key. Required when Field is Tag; ignored otherwise.
        /// Maximum length: 256 characters.
        /// </summary>
        public string? TagKey { get; set; }

        /// <summary>
        /// Number of candidates retrieved before collapsing, for vector-only and full-text-only searches; hybrid
        /// searches use Hybrid.CandidatePool when it is set and this value otherwise. Groups are formed within the
        /// pool, so a pool with fewer groups than MaxResults returns fewer hits and a Notice.
        /// Default: null (the server uses the larger of MaxResults * 4 and 100, capped at 1000). Minimum: 1. Maximum: 10000.
        /// </summary>
        public int? CandidatePool { get; set; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public CollapseQuery()
        {
            Field = "DocumentId";
        }
    }
}
