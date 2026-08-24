namespace RecallDb.Core.Observability
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;

    /// <summary>
    /// Core telemetry contract: the <see cref="System.Diagnostics.Metrics.Meter"/> and
    /// <see cref="System.Diagnostics.ActivitySource"/> that the data-access layer emits into. The names
    /// ("RecallDb.Core") are a stable contract that the observability host subscribes to by name. Instrumented
    /// code depends only on the .NET base class library; all OpenTelemetry wiring is owned by the host, so these
    /// instruments are cheap no-ops until something subscribes.
    /// </summary>
    public static class RecallDbTelemetry
    {
        #region Public-Members

        /// <summary>
        /// Meter name for the core (storage) layer. Subscribe to this name to collect its metrics.
        /// </summary>
        public const string MeterName = "RecallDb.Core";

        /// <summary>
        /// Activity source name for the core (storage) layer. Subscribe to this name to collect its traces.
        /// </summary>
        public const string ActivitySourceName = "RecallDb.Core";

        /// <summary>
        /// Core layer meter.
        /// </summary>
        public static readonly Meter Meter = new Meter(MeterName);

        /// <summary>
        /// Core layer activity source.
        /// </summary>
        public static readonly ActivitySource ActivitySource = new ActivitySource(ActivitySourceName);

        /// <summary>
        /// Duration of a single database query, in seconds.
        /// </summary>
        public static readonly Histogram<double> DbQueryDuration = Meter.CreateHistogram<double>(
            "recalldb.db.query.duration", "s", "Duration of a database query.");

        /// <summary>
        /// Count of database queries executed.
        /// </summary>
        public static readonly Counter<long> DbQueries = Meter.CreateCounter<long>(
            "recalldb.db.queries", "{query}", "Count of database queries executed.");

        /// <summary>
        /// Database queries currently executing.
        /// </summary>
        public static readonly UpDownCounter<long> DbActiveQueries = Meter.CreateUpDownCounter<long>(
            "recalldb.db.active_queries", "{query}", "Database queries currently executing.");

        /// <summary>
        /// Rows returned by a database query that produced a result set.
        /// </summary>
        public static readonly Histogram<long> DbRowsReturned = Meter.CreateHistogram<long>(
            "recalldb.db.rows_returned", "{row}", "Rows returned by a database query.");

        /// <summary>
        /// Metric/span tag key for the SQL operation verb (select, insert, update, delete, ddl, other).
        /// </summary>
        public const string TagDbOperation = "db.operation";

        /// <summary>
        /// Metric/span tag key for the outcome (success or error).
        /// </summary>
        public const string TagOutcome = "recalldb.outcome";

        /// <summary>
        /// Metric/span tag key indicating whether the query ran inside an explicit transaction.
        /// </summary>
        public const string TagTransaction = "recalldb.transaction";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Derive a low-cardinality SQL operation label from a query string by inspecting its leading verb.
        /// </summary>
        /// <param name="sql">SQL query text.</param>
        /// <returns>One of select, insert, update, delete, ddl, or other.</returns>
        public static string DeriveSqlOperation(string sql)
        {
            if (string.IsNullOrEmpty(sql)) return "other";

            int i = 0;
            while (i < sql.Length && char.IsWhiteSpace(sql[i])) i++;

            int start = i;
            while (i < sql.Length && (char.IsLetter(sql[i]))) i++;
            if (i == start) return "other";

            string verb = sql.Substring(start, i - start).ToLowerInvariant();
            switch (verb)
            {
                case "select":
                case "with":
                    return "select";
                case "insert":
                    return "insert";
                case "update":
                    return "update";
                case "delete":
                    return "delete";
                case "create":
                case "drop":
                case "alter":
                case "truncate":
                    return "ddl";
                default:
                    return "other";
            }
        }

        /// <summary>
        /// Record an exception on an activity as a standard OpenTelemetry exception event and set the error status.
        /// Null-safe: does nothing when the activity is null (for example when nothing is sampling the trace).
        /// </summary>
        /// <param name="activity">Activity, may be null.</param>
        /// <param name="e">Exception.</param>
        public static void RecordException(Activity activity, Exception e)
        {
            if (activity == null || e == null) return;

            ActivityTagsCollection tags = new ActivityTagsCollection();
            tags["exception.type"] = e.GetType().FullName;
            tags["exception.message"] = e.Message;
            if (!string.IsNullOrEmpty(e.StackTrace)) tags["exception.stacktrace"] = e.StackTrace;

            activity.AddEvent(new ActivityEvent("exception", default, tags));
            activity.SetStatus(ActivityStatusCode.Error, e.Message);
        }

        #endregion
    }
}
