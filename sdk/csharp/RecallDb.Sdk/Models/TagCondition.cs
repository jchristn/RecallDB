namespace RecallDb.Sdk.Models
{
    /// <summary>
    /// A single tag condition used for filtering.
    /// </summary>
    public class TagCondition
    {
        /// <summary>
        /// Tag key.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Condition to evaluate against the tag value.
        /// Values: Equals, NotEquals, GreaterThan, LessThan, Contains, ContainsNot, StartsWith, EndsWith, IsNull, IsNotNull.
        /// Default: Equals.
        /// </summary>
        public string Condition { get; set; }

        /// <summary>
        /// Value to compare against.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TagCondition()
        {
            Condition = "Equals";
        }
    }
}
