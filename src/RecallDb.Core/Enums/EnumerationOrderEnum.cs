namespace RecallDb.Core.Enums
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Enumeration order for listing results.
    /// </summary>
    public enum EnumerationOrderEnum
    {
        /// <summary>
        /// Order by creation date ascending.
        /// </summary>
        [EnumMember(Value = "CreatedAscending")]
        CreatedAscending,

        /// <summary>
        /// Order by creation date descending.
        /// </summary>
        [EnumMember(Value = "CreatedDescending")]
        CreatedDescending
    }
}
