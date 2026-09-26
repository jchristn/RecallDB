namespace RecallDb.Sdk
{
    using System;
    using System.Net;
    using System.Text.Json;

    /// <summary>
    /// Exception thrown by the RecallDB SDK when an API call fails.
    /// </summary>
    public class RecallDbException : Exception
    {
        #region Public-Members

        /// <summary>
        /// HTTP status code returned by the server.
        /// </summary>
        public HttpStatusCode StatusCode
        {
            get
            {
                return _StatusCode;
            }
        }

        /// <summary>
        /// Raw response body from the server. Empty for responses without a body, such as HEAD.
        /// </summary>
        public string ResponseBody
        {
            get
            {
                return _ResponseBody;
            }
        }

        /// <summary>
        /// The Error field of the server's error object (for example "BadRequest" or "NotAuthorized").
        /// Null when the response body is not a JSON error object.
        /// </summary>
        public string? ErrorCode
        {
            get
            {
                return _ErrorCode;
            }
        }

        /// <summary>
        /// The most specific message in the server's error object: its Context, Message, or Description field,
        /// in that order (for example "RrfK must be between 1 and 100000."). Null when the response body is not a
        /// JSON error object.
        /// </summary>
        public string? ErrorMessage
        {
            get
            {
                return _ErrorMessage;
            }
        }

        #endregion

        #region Private-Members

        private readonly HttpStatusCode _StatusCode;
        private readonly string _ResponseBody;
        private readonly string? _ErrorCode;
        private readonly string? _ErrorMessage;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="statusCode">HTTP status code.</param>
        /// <param name="responseBody">Raw response body.</param>
        public RecallDbException(HttpStatusCode statusCode, string? responseBody)
            : this(statusCode, responseBody, null)
        {
        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="statusCode">HTTP status code.</param>
        /// <param name="responseBody">Raw response body.</param>
        /// <param name="innerException">Inner exception.</param>
        public RecallDbException(HttpStatusCode statusCode, string? responseBody, Exception? innerException)
            : base(BuildMessage(statusCode, responseBody ?? "", out string? errorCode, out string? errorMessage), innerException)
        {
            _StatusCode = statusCode;
            _ResponseBody = responseBody ?? "";
            _ErrorCode = errorCode;
            _ErrorMessage = errorMessage;
        }

        #endregion

        #region Private-Methods

        private static string BuildMessage(HttpStatusCode statusCode, string responseBody, out string? errorCode, out string? errorMessage)
        {
            ParseErrorBody(responseBody, out errorCode, out errorMessage);
            if (errorCode != null || errorMessage != null)
            {
                return "RecallDB API returned " + (int)statusCode + " (" + (errorCode ?? statusCode.ToString()) + ")"
                    + (errorMessage != null ? ": " + errorMessage : "");
            }

            return "RecallDB API returned " + (int)statusCode + ": " + responseBody;
        }

        private static void ParseErrorBody(string responseBody, out string? errorCode, out string? errorMessage)
        {
            errorCode = null;
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(responseBody)) return;

            try
            {
                using JsonDocument doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return;

                errorCode = GetString(doc.RootElement, "Error");
                errorMessage = GetString(doc.RootElement, "Context")
                    ?? GetString(doc.RootElement, "Message")
                    ?? GetString(doc.RootElement, "Description");
            }
            catch (JsonException)
            {
                // Not a JSON error object; the raw body is still available through ResponseBody.
            }
        }

        private static string? GetString(JsonElement element, string name)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.String)
                {
                    string? value = property.Value.GetString();
                    return string.IsNullOrWhiteSpace(value) ? null : value;
                }
            }

            return null;
        }

        #endregion
    }
}
