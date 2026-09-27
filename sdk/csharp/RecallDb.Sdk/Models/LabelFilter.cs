namespace RecallDb.Sdk.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Filter for document labels.
    /// </summary>
    public class LabelFilter
    {
        /// <summary>
        /// Labels the document must carry: every one of them when <see cref="RequiredMode"/> is All (the server
        /// default), or at least one when it is Any. An empty list does not filter.
        /// </summary>
        public List<string> Required { get; set; }

        /// <summary>
        /// How <see cref="Required"/> combines its labels: <see cref="Constants.LabelMatchModes.All"/> (every label,
        /// the server default) or <see cref="Constants.LabelMatchModes.Any"/> (at least one). Null is omitted on the
        /// wire. Capability: <see cref="Constants.Capabilities.LabelFilterRequiredMode"/>; a server without it treats
        /// Required as Any.
        /// </summary>
        public string? RequiredMode { get; set; }

        /// <summary>
        /// Labels of which the document must carry none. An empty list does not filter.
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
