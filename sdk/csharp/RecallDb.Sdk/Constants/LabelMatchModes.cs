namespace RecallDb.Sdk.Constants
{
    /// <summary>
    /// Values for LabelFilter.RequiredMode.
    /// </summary>
    public static class LabelMatchModes
    {
        /// <summary>
        /// The document must carry every required label (default).
        /// </summary>
        public const string All = "All";

        /// <summary>
        /// The document must carry at least one of the required labels.
        /// </summary>
        public const string Any = "Any";
    }
}
