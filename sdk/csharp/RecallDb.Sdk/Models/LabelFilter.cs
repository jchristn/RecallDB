namespace RecallDb.Sdk.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Filter for document labels.
    /// </summary>
    public class LabelFilter
    {
        /// <summary>
        /// Labels that must be present on the document.
        /// </summary>
        public List<string> Required { get; set; }

        /// <summary>
        /// Labels that must not be present on the document.
        /// </summary>
        public List<string> Excluded { get; set; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public LabelFilter()
        {
            Required = new List<string>();
            Excluded = new List<string>();
        }
    }
}
