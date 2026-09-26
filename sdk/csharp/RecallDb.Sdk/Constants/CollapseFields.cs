namespace RecallDb.Sdk.Constants
{
    /// <summary>
    /// Values for CollapseQuery.Field.
    /// </summary>
    public static class CollapseFields
    {
        /// <summary>
        /// Group by DocumentId (the chunks of one logical document) (default).
        /// </summary>
        public const string DocumentId = "DocumentId";

        /// <summary>
        /// Group by the value of the tag named CollapseQuery.TagKey.
        /// </summary>
        public const string Tag = "Tag";
    }
}
