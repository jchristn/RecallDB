namespace RecallDb.Server.Services
{
    using System;

    /// <summary>
    /// An inclusive integer position range used when merging neighbor windows during search enrichment.
    /// </summary>
    internal sealed class PositionRange
    {
        #region Internal-Members

        /// <summary>
        /// Minimum (inclusive) position.
        /// </summary>
        internal int Min { get; }

        /// <summary>
        /// Maximum (inclusive) position.
        /// </summary>
        internal int Max { get; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate with a minimum and maximum position.
        /// </summary>
        /// <param name="min">Minimum position.</param>
        /// <param name="max">Maximum position.</param>
        internal PositionRange(int min, int max)
        {
            if (max < min) throw new ArgumentOutOfRangeException(nameof(max), "Max must be greater than or equal to Min.");
            Min = min;
            Max = max;
        }

        #endregion
    }
}
