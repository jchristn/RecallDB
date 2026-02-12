namespace RecallDb.Sdk.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Set of tag conditions used for filtering documents.
    /// </summary>
    public class TagFilterSet
    {
        /// <summary>
        /// Tag conditions that must all be satisfied.
        /// </summary>
        public List<TagCondition> Required { get; set; }

        /// <summary>
        /// Tag conditions that must not be satisfied.
        /// </summary>
        public List<TagCondition> Excluded { get; set; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TagFilterSet()
        {
            Required = new List<TagCondition>();
            Excluded = new List<TagCondition>();
        }
    }
}
