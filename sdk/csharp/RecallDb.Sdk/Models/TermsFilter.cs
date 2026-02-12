namespace RecallDb.Sdk.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Filter for document content terms.
    /// </summary>
    public class TermsFilter
    {
        /// <summary>
        /// Terms that must be present in the document content (case-insensitive substring match, all must match).
        /// </summary>
        public List<string> Required { get; set; }

        /// <summary>
        /// Terms that must not be present in the document content (case-insensitive substring match, none may match).
        /// </summary>
        public List<string> Excluded { get; set; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TermsFilter()
        {
            Required = new List<string>();
            Excluded = new List<string>();
        }
    }
}
