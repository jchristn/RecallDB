namespace RecallDb.Core.Enums
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Tag condition enumeration for filtering.
    /// </summary>
    public enum TagConditionEnum
    {
        /// <summary>
        /// Value equals.
        /// </summary>
        [EnumMember(Value = "Equals")]
        Equals,

        /// <summary>
        /// Value not equals.
        /// </summary>
        [EnumMember(Value = "NotEquals")]
        NotEquals,

        /// <summary>
        /// Value greater than.
        /// </summary>
        [EnumMember(Value = "GreaterThan")]
        GreaterThan,

        /// <summary>
        /// Value less than.
        /// </summary>
        [EnumMember(Value = "LessThan")]
        LessThan,

        /// <summary>
        /// Value contains.
        /// </summary>
        [EnumMember(Value = "Contains")]
        Contains,

        /// <summary>
        /// Value does not contain.
        /// </summary>
        [EnumMember(Value = "ContainsNot")]
        ContainsNot,

        /// <summary>
        /// Value starts with.
        /// </summary>
        [EnumMember(Value = "StartsWith")]
        StartsWith,

        /// <summary>
        /// Value ends with.
        /// </summary>
        [EnumMember(Value = "EndsWith")]
        EndsWith,

        /// <summary>
        /// Value is null.
        /// </summary>
        [EnumMember(Value = "IsNull")]
        IsNull,

        /// <summary>
        /// Value is not null.
        /// </summary>
        [EnumMember(Value = "IsNotNull")]
        IsNotNull
    }
}
