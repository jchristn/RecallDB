namespace RecallDb.Core.Helpers
{
    using System;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// JSON serialization helper using System.Text.Json.
    /// Default PascalCase, WriteIndented, ignores null values.
    /// </summary>
    public static class Serializer
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private static readonly JsonSerializerOptions _Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        #endregion

        #region Public-Methods

        /// <summary>
        /// Serialize an object to a JSON string.
        /// </summary>
        /// <param name="obj">Object to serialize.</param>
        /// <returns>JSON string.</returns>
        public static string SerializeJson(object obj)
        {
            if (obj == null) return null;
            return JsonSerializer.Serialize(obj, _Options);
        }

        /// <summary>
        /// Deserialize a JSON string to an object.
        /// </summary>
        /// <typeparam name="T">Target type.</typeparam>
        /// <param name="json">JSON string.</param>
        /// <returns>Deserialized object.</returns>
        public static T DeserializeJson<T>(string json)
        {
            if (string.IsNullOrEmpty(json)) return default;
            return JsonSerializer.Deserialize<T>(json, _Options);
        }

        /// <summary>
        /// Serialize an object to a JSON byte array.
        /// </summary>
        /// <param name="obj">Object to serialize.</param>
        /// <returns>UTF-8 encoded JSON bytes.</returns>
        public static byte[] SerializeJsonBytes(object obj)
        {
            if (obj == null) return null;
            string json = JsonSerializer.Serialize(obj, _Options);
            return Encoding.UTF8.GetBytes(json);
        }

        /// <summary>
        /// Deserialize a JSON byte array to an object.
        /// </summary>
        /// <typeparam name="T">Target type.</typeparam>
        /// <param name="bytes">UTF-8 encoded JSON bytes.</param>
        /// <returns>Deserialized object.</returns>
        public static T DeserializeJson<T>(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return default;
            string json = Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<T>(json, _Options);
        }

        #endregion
    }
}
