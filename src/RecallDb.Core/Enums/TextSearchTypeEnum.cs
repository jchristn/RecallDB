namespace RecallDb.Core.Enums
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Text search ranking function enumeration.
    /// </summary>
    public enum TextSearchTypeEnum
    {
        /// <summary>
        /// Standard ts_rank scoring (term frequency with length normalization).
        /// </summary>
        [EnumMember(Value = "TsRank")]
        TsRank,

        /// <summary>
        /// Cover density ranking (ts_rank_cd) — rewards term proximity.
        /// </summary>
        [EnumMember(Value = "TsRankCd")]
        TsRankCd
    }
}
