namespace RecallDb.Server.Mcp
{
    using System;
    using System.Threading.Tasks;

    using Voltaic.Core;

    using RecallDb.Core.Enums;
    using RecallDb.Core.Helpers;
    using RecallDb.Server.Classes;
    using RecallDb.Server.Services;

    /// <summary>
    /// Helpers for MCP tool handlers: argument extraction, request-context construction (including per-caller
    /// bearer authentication), and mapping a <see cref="ServiceResult"/> onto an MCP tool result or JSON-RPC error.
    /// </summary>
    public static class McpHelpers
    {
        #region Public-Members

        /// <summary>
        /// Argument name carrying the caller's bearer token on every authenticated tool.
        /// </summary>
        public const string BearerTokenArg = "bearerToken";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build a request context for an authenticated MCP tool: classifies the operation from the tool name and
        /// resolves the caller's identity from the bearer-token argument via the shared authentication service.
        /// The resolved <see cref="AuthenticationResult"/> is always non-null (unauthenticated when no valid token
        /// is supplied), so downstream services enforce access consistently with REST.
        /// </summary>
        /// <param name="toolName">Tool name (e.g. "tenant/create").</param>
        /// <param name="args">Tool arguments.</param>
        /// <param name="authentication">Authentication service.</param>
        /// <returns>RequestContext.</returns>
        public static async Task<RequestContext> BuildAuthenticatedContextAsync(string toolName, RpcParameters args, AuthenticationService authentication)
        {
            if (authentication == null) throw new ArgumentNullException(nameof(authentication));

            RequestContext ctx = BuildBaseContext(toolName);
            string bearerToken = GetStringOptional(args, BearerTokenArg);
            ctx.Auth = await authentication.AuthenticateBearerAsync(bearerToken ?? string.Empty).ConfigureAwait(false);
            return ctx;
        }

        /// <summary>
        /// Build a request context for an unauthenticated MCP tool (server/info, auth/authenticate).
        /// </summary>
        /// <param name="toolName">Tool name.</param>
        /// <returns>RequestContext.</returns>
        public static RequestContext BuildAnonymousContext(string toolName)
        {
            return BuildBaseContext(toolName);
        }

        /// <summary>
        /// Map a service result onto an MCP tool return value. On success returns the payload (or a small
        /// success object for no-content results). On failure throws so Voltaic surfaces a JSON-RPC error.
        /// </summary>
        /// <param name="result">Service result.</param>
        /// <returns>Tool return value.</returns>
        public static object MapResult(ServiceResult result)
        {
            if (result == null) throw new InvalidOperationException("500 Internal error: null service result.");

            if (!result.Success)
                throw new McpToolException(result.StatusCode, result.Error, result.Context);

            if (result.StatusCode == 204)
                return new McpDeleteResult();

            return result.Data;
        }

        /// <summary>
        /// Map an existence-check service result onto a boolean, throwing on authorization failure.
        /// </summary>
        /// <param name="result">Service result.</param>
        /// <returns>True if the resource exists.</returns>
        public static object MapExists(ServiceResult result)
        {
            if (result == null) throw new InvalidOperationException("500 Internal error: null service result.");

            if (!result.Success)
                throw new McpToolException(result.StatusCode, result.Error, result.Context);

            return result.Data is bool existsValue && existsValue;
        }

        /// <summary>
        /// Get a required string argument, throwing when missing or empty.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <param name="name">Argument name.</param>
        /// <returns>String value.</returns>
        public static string GetStringRequired(RpcParameters args, string name)
        {
            string value = GetStringOptional(args, name);
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("Argument '" + name + "' is required.");
            return value;
        }

        /// <summary>
        /// Get an optional string argument, returning null when absent.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <param name="name">Argument name.</param>
        /// <returns>String value or null.</returns>
        public static string GetStringOptional(RpcParameters args, string name)
        {
            if (args == null || !args.HasValue) return null;
            if (!args.ContainsProperty(name)) return null;
            return args.GetString(name);
        }

        /// <summary>
        /// Get an optional integer argument.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <param name="name">Argument name.</param>
        /// <returns>Integer value or null.</returns>
        public static int? GetIntOptional(RpcParameters args, string name)
        {
            if (args == null || !args.HasValue) return null;
            if (!args.ContainsProperty(name)) return null;
            long? value = args.GetInt64(name);
            if (!value.HasValue) return null;
            return (int)value.Value;
        }

        /// <summary>
        /// Deserialize a required JSON-string argument into the specified type.
        /// </summary>
        /// <typeparam name="T">Target type.</typeparam>
        /// <param name="args">Arguments.</param>
        /// <param name="name">Argument name.</param>
        /// <returns>Deserialized object.</returns>
        public static T DeserializeArgRequired<T>(RpcParameters args, string name)
        {
            string json = GetStringRequired(args, name);
            T value = Serializer.DeserializeJson<T>(json);
            if (value == null)
                throw new ArgumentException("Argument '" + name + "' could not be parsed.");
            return value;
        }

        /// <summary>
        /// Deserialize an optional JSON-string argument into the specified type, returning default when absent.
        /// </summary>
        /// <typeparam name="T">Target type.</typeparam>
        /// <param name="args">Arguments.</param>
        /// <param name="name">Argument name.</param>
        /// <returns>Deserialized object or default.</returns>
        public static T DeserializeArgOptional<T>(RpcParameters args, string name)
        {
            string json = GetStringOptional(args, name);
            if (string.IsNullOrEmpty(json)) return default;
            return Serializer.DeserializeJson<T>(json);
        }

        #endregion

        #region Private-Methods

        private static RequestContext BuildBaseContext(string toolName)
        {
            RequestContext ctx = new RequestContext();
            ctx.Origin = RequestOriginEnum.Mcp;
            ctx.RequestType = toolName;
            OperationScope scope = OperationScopeMap.Resolve(toolName);
            ctx.ResourceType = scope.ResourceType;
            ctx.Operation = scope.Operation;
            return ctx;
        }

        #endregion
    }
}
