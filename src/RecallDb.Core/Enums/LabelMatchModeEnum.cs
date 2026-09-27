namespace RecallDb.Core.Enums
{
    using System.Runtime.Serialization;

    /// <summary>
    /// How LabelFilter.Required combines its labels.
    /// </summary>
    public enum LabelMatchModeEnum
    {
        /// <summary>
        /// The document must carry every required label.
        /// </summary>
        [EnumMember(Value = "All")]
        All,

        /// <summary>
        /// The document must carry at least one of the required labels.
        /// </summary>
        [EnumMember(Value = "Any")]
        Any
    }
}
