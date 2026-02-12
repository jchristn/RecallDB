namespace RecallDb.Core.Helpers
{
    using System;
    using System.Data;

    /// <summary>
    /// Helper methods for extracting values from DataRow objects.
    /// </summary>
    public static class DataTableHelper
    {
        #region Public-Methods

        /// <summary>
        /// Get a string value from a DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <param name="columnName">Column name.</param>
        /// <returns>String value or null.</returns>
        public static string GetStringValue(DataRow row, string columnName)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (string.IsNullOrEmpty(columnName)) throw new ArgumentNullException(nameof(columnName));
            if (!row.Table.Columns.Contains(columnName)) return null;
            if (row[columnName] == null || row[columnName] == DBNull.Value) return null;
            return row[columnName].ToString();
        }

        /// <summary>
        /// Get a boolean value from a DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <param name="columnName">Column name.</param>
        /// <returns>Boolean value or false.</returns>
        public static bool GetBooleanValue(DataRow row, string columnName)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (string.IsNullOrEmpty(columnName)) throw new ArgumentNullException(nameof(columnName));
            if (!row.Table.Columns.Contains(columnName)) return false;
            if (row[columnName] == null || row[columnName] == DBNull.Value) return false;
            return Convert.ToBoolean(row[columnName]);
        }

        /// <summary>
        /// Get an integer value from a DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <param name="columnName">Column name.</param>
        /// <returns>Integer value or 0.</returns>
        public static int GetIntValue(DataRow row, string columnName)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (string.IsNullOrEmpty(columnName)) throw new ArgumentNullException(nameof(columnName));
            if (!row.Table.Columns.Contains(columnName)) return 0;
            if (row[columnName] == null || row[columnName] == DBNull.Value) return 0;
            return Convert.ToInt32(row[columnName]);
        }

        /// <summary>
        /// Get a long value from a DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <param name="columnName">Column name.</param>
        /// <returns>Long value or 0.</returns>
        public static long GetLongValue(DataRow row, string columnName)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (string.IsNullOrEmpty(columnName)) throw new ArgumentNullException(nameof(columnName));
            if (!row.Table.Columns.Contains(columnName)) return 0;
            if (row[columnName] == null || row[columnName] == DBNull.Value) return 0;
            return Convert.ToInt64(row[columnName]);
        }

        /// <summary>
        /// Get a DateTime value from a DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <param name="columnName">Column name.</param>
        /// <returns>DateTime value or DateTime.MinValue.</returns>
        public static DateTime GetDateTimeValue(DataRow row, string columnName)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (string.IsNullOrEmpty(columnName)) throw new ArgumentNullException(nameof(columnName));
            if (!row.Table.Columns.Contains(columnName)) return DateTime.MinValue;
            if (row[columnName] == null || row[columnName] == DBNull.Value) return DateTime.MinValue;
            return Convert.ToDateTime(row[columnName]);
        }

        /// <summary>
        /// Get a nullable integer value from a DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <param name="columnName">Column name.</param>
        /// <returns>Nullable integer value.</returns>
        public static int? GetNullableIntValue(DataRow row, string columnName)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (string.IsNullOrEmpty(columnName)) throw new ArgumentNullException(nameof(columnName));
            if (!row.Table.Columns.Contains(columnName)) return null;
            if (row[columnName] == null || row[columnName] == DBNull.Value) return null;
            return Convert.ToInt32(row[columnName]);
        }

        /// <summary>
        /// Get a byte array value from a DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <param name="columnName">Column name.</param>
        /// <returns>Byte array or null.</returns>
        public static byte[] GetByteArrayValue(DataRow row, string columnName)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (string.IsNullOrEmpty(columnName)) throw new ArgumentNullException(nameof(columnName));
            if (!row.Table.Columns.Contains(columnName)) return null;
            if (row[columnName] == null || row[columnName] == DBNull.Value) return null;
            return (byte[])row[columnName];
        }

        /// <summary>
        /// Get a float array from a DataRow (for pgvector embeddings stored as string).
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <param name="columnName">Column name.</param>
        /// <returns>List of float values or null.</returns>
        public static System.Collections.Generic.List<float> GetFloatArrayValue(DataRow row, string columnName)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (string.IsNullOrEmpty(columnName)) throw new ArgumentNullException(nameof(columnName));
            if (!row.Table.Columns.Contains(columnName)) return null;
            if (row[columnName] == null || row[columnName] == DBNull.Value) return null;

            string val = row[columnName].ToString();
            if (string.IsNullOrEmpty(val)) return null;

            // pgvector returns vectors as [0.1,0.2,0.3] format
            val = val.Trim('[', ']');
            if (string.IsNullOrEmpty(val)) return new System.Collections.Generic.List<float>();

            string[] parts = val.Split(',');
            System.Collections.Generic.List<float> result = new System.Collections.Generic.List<float>();
            for (int i = 0; i < parts.Length; i++)
            {
                if (float.TryParse(parts[i].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f))
                {
                    result.Add(f);
                }
            }
            return result;
        }

        /// <summary>
        /// Get a double value from a DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <param name="columnName">Column name.</param>
        /// <returns>Double value or 0.</returns>
        public static double GetDoubleValue(DataRow row, string columnName)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (string.IsNullOrEmpty(columnName)) throw new ArgumentNullException(nameof(columnName));
            if (!row.Table.Columns.Contains(columnName)) return 0;
            if (row[columnName] == null || row[columnName] == DBNull.Value) return 0;
            return Convert.ToDouble(row[columnName]);
        }

        #endregion
    }
}
