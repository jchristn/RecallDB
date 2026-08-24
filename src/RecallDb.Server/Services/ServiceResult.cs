namespace RecallDb.Server.Services
{
    using System;

    /// <summary>
    /// Transport-agnostic result returned by the service layer. REST adapters map this onto an HTTP response;
    /// MCP adapters return <see cref="Data"/> on success or raise a JSON-RPC error on failure. This keeps REST
    /// and MCP behavior identical because both consume the same result.
    /// </summary>
    public class ServiceResult
    {
        #region Public-Members

        /// <summary>
        /// Whether the operation succeeded. Default: true.
        /// </summary>
        public bool Success
        {
            get
            {
                return _Success;
            }
            set
            {
                _Success = value;
            }
        }

        /// <summary>
        /// HTTP-equivalent status code (200, 201, 204, 400, 401, 403, 404). Default: 200.
        /// </summary>
        public int StatusCode
        {
            get
            {
                return _StatusCode;
            }
            set
            {
                _StatusCode = value;
            }
        }

        /// <summary>
        /// Short error label when <see cref="Success"/> is false. Null on success.
        /// </summary>
        public string Error
        {
            get
            {
                return _Error;
            }
            set
            {
                _Error = value;
            }
        }

        /// <summary>
        /// Human-readable error context when <see cref="Success"/> is false. Null on success.
        /// </summary>
        public string Context
        {
            get
            {
                return _Context;
            }
            set
            {
                _Context = value;
            }
        }

        /// <summary>
        /// Response payload on success. May be null for status 204 (no content) or existence checks.
        /// </summary>
        public object Data
        {
            get
            {
                return _Data;
            }
            set
            {
                _Data = value;
            }
        }

        #endregion

        #region Private-Members

        private bool _Success = true;
        private int _StatusCode = 200;
        private string _Error = null;
        private string _Context = null;
        private object _Data = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ServiceResult()
        {
        }

        /// <summary>
        /// Create a successful result.
        /// </summary>
        /// <param name="data">Response payload.</param>
        /// <param name="statusCode">HTTP-equivalent status code. Default: 200.</param>
        /// <returns>ServiceResult.</returns>
        public static ServiceResult Ok(object data, int statusCode = 200)
        {
            ServiceResult result = new ServiceResult();
            result.Success = true;
            result.StatusCode = statusCode;
            result.Data = data;
            return result;
        }

        /// <summary>
        /// Create a "no content" (204) successful result.
        /// </summary>
        /// <returns>ServiceResult.</returns>
        public static ServiceResult NoContent()
        {
            ServiceResult result = new ServiceResult();
            result.Success = true;
            result.StatusCode = 204;
            result.Data = null;
            return result;
        }

        /// <summary>
        /// Create a failed result.
        /// </summary>
        /// <param name="statusCode">HTTP-equivalent status code.</param>
        /// <param name="error">Short error label.</param>
        /// <param name="context">Human-readable error context.</param>
        /// <returns>ServiceResult.</returns>
        public static ServiceResult Fail(int statusCode, string error, string context = null)
        {
            ServiceResult result = new ServiceResult();
            result.Success = false;
            result.StatusCode = statusCode;
            result.Error = error;
            result.Context = context;
            return result;
        }

        #endregion
    }
}
