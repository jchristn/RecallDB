namespace RecallDb.Core.Enums
{
    using System.Runtime.Serialization;

    /// <summary>
    /// What a collapsed search groups its candidates by (SearchQuery.Collapse.Field).
    /// </summary>
    public enum CollapseFieldEnum
    {
        /// <summary>
        /// Group by the document_id column (the chunks of one logical document). A document without a
        /// DocumentId is its own group, keyed by its DocumentKey.
        /// </summary>
        [EnumMember(Value = "DocumentId")]
        DocumentId,

        /// <summary>
        /// Group by the value of the tag named Collapse.TagKey. A document without that tag (or with an empty
        /// value) is its own group, keyed by its DocumentKey.
        /// </summary>
        [EnumMember(Value = "Tag")]
        Tag
    }
}
