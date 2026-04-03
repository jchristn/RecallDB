namespace RecallDb.Server.Classes
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using WatsonWebserver.Core;

    /// <summary>
    /// Custom serialization helper that uses JsonStringEnumConverter for both
    /// serialization and deserialization. The Watson default helper only applies
    /// converters during serialization, causing enum string values to fail
    /// during deserialization.
    /// </summary>
    public class RecallDbSerializationHelper : ISerializationHelper
    {
        private static readonly JsonSerializerOptions _Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// Deserialize JSON to an instance.
        /// </summary>
        public T DeserializeJson<T>(string json)
        {
            if (string.IsNullOrEmpty(json)) return default;
            return JsonSerializer.Deserialize<T>(json, _Options);
        }

        /// <summary>
        /// Serialize object to JSON.
        /// </summary>
        public string SerializeJson(object obj, bool pretty = true)
        {
            if (obj == null) return null;
            return JsonSerializer.Serialize(obj, _Options);
        }
    }
}
