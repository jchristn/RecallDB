namespace RecallDb.Core.Enums
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Content type enumeration.
    /// </summary>
    public enum ContentTypeEnum
    {
        /// <summary>
        /// Text content.
        /// </summary>
        [EnumMember(Value = "Text")]
        Text,

        /// <summary>
        /// List content.
        /// </summary>
        [EnumMember(Value = "List")]
        List,

        /// <summary>
        /// Table content.
        /// </summary>
        [EnumMember(Value = "Table")]
        Table,

        /// <summary>
        /// Binary content.
        /// </summary>
        [EnumMember(Value = "Binary")]
        Binary,

        /// <summary>
        /// Image content.
        /// </summary>
        [EnumMember(Value = "Image")]
        Image,

        /// <summary>
        /// Code content.
        /// </summary>
        [EnumMember(Value = "Code")]
        Code,

        /// <summary>
        /// Hyperlink content.
        /// </summary>
        [EnumMember(Value = "Hyperlink")]
        Hyperlink,

        /// <summary>
        /// Metadata content.
        /// </summary>
        [EnumMember(Value = "Meta")]
        Meta,

        /// <summary>
        /// Unknown content type.
        /// </summary>
        [EnumMember(Value = "Unknown")]
        Unknown
    }
}
