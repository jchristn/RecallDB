namespace RecallDb.Core.Enums
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Sort order enumeration for search results.
    /// </summary>
    public enum SortOrderEnum
    {
        /// <summary>
        /// Sort by score ascending.
        /// </summary>
        [EnumMember(Value = "ScoreAscending")]
        ScoreAscending,

        /// <summary>
        /// Sort by score descending.
        /// </summary>
        [EnumMember(Value = "ScoreDescending")]
        ScoreDescending,

        /// <summary>
        /// Sort by distance ascending.
        /// </summary>
        [EnumMember(Value = "DistanceAscending")]
        DistanceAscending,

        /// <summary>
        /// Sort by distance descending.
        /// </summary>
        [EnumMember(Value = "DistanceDescending")]
        DistanceDescending,

        /// <summary>
        /// Sort by creation date ascending.
        /// </summary>
        [EnumMember(Value = "CreatedAscending")]
        CreatedAscending,

        /// <summary>
        /// Sort by creation date descending.
        /// </summary>
        [EnumMember(Value = "CreatedDescending")]
        CreatedDescending
    }
}
