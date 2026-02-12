namespace RecallDb.Sdk
{
    using System;
    using System.Net;

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
        /// Raw response body from the server.
        /// </summary>
        public string ResponseBody
        {
            get
            {
                return _ResponseBody;
            }
        }

        #endregion

        #region Private-Members

        private readonly HttpStatusCode _StatusCode;
        private readonly string _ResponseBody;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="statusCode">HTTP status code.</param>
        /// <param name="responseBody">Raw response body.</param>
        public RecallDbException(HttpStatusCode statusCode, string responseBody)
            : base("RecallDB API returned " + (int)statusCode + ": " + responseBody)
        {
            _StatusCode = statusCode;
            _ResponseBody = responseBody;
        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="statusCode">HTTP status code.</param>
        /// <param name="responseBody">Raw response body.</param>
        /// <param name="innerException">Inner exception.</param>
        public RecallDbException(HttpStatusCode statusCode, string responseBody, Exception innerException)
            : base("RecallDB API returned " + (int)statusCode + ": " + responseBody, innerException)
        {
            _StatusCode = statusCode;
            _ResponseBody = responseBody;
        }

        #endregion
    }
}
