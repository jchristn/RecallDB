namespace RecallDb.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using RecallDb.Core.Enums;

    /// <summary>
    /// SQL fragments shared by search and enumeration filters.
    /// </summary>
    public static class FilterQueries
    {
        /// <summary>
        /// Build the condition for LabelFilter.Required.
        /// Any: the document carries at least one of the labels. All: it carries every one of them, counted as distinct
        /// labels so a label stored twice on a document does not stand in for a missing one. With a single distinct
        /// label both modes produce the same (simpler) condition.
        /// </summary>
        /// <param name="labelsTableName">The collection's labels table.</param>
        /// <param name="quotedLabels">Labels already sanitized and wrapped in single quotes.</param>
        /// <param name="mode">How the labels combine.</param>
        /// <returns>SQL condition, or null when there are no labels.</returns>
        public static string BuildLabelRequiredCondition(string labelsTableName, IEnumerable<string> quotedLabels, LabelMatchModeEnum mode)
        {
            List<string> labels = quotedLabels == null ? new List<string>() : quotedLabels.Distinct().ToList();
            if (labels.Count == 0) return null;

            string inList = string.Join(",", labels);
            if (mode == LabelMatchModeEnum.Any || labels.Count == 1)
            {
                return "document_key IN (" +
                    "SELECT document_key FROM " + labelsTableName + " " +
                    "WHERE label IN (" + inList + ")" +
                    ")";
            }

            return "document_key IN (" +
                "SELECT document_key FROM " + labelsTableName + " " +
                "WHERE label IN (" + inList + ") " +
                "GROUP BY document_key HAVING COUNT(DISTINCT label) = " + labels.Count.ToString(CultureInfo.InvariantCulture) +
                ")";
        }
    }
}
