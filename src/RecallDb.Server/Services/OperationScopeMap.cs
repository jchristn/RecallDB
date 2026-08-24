namespace RecallDb.Server.Services
{
    using System.Collections.Generic;

    using RecallDb.Core.Enums;

    /// <summary>
    /// Single source of truth mapping every request type (shared by REST and MCP, keyed as "family/operation")
    /// to the (ResourceType, Operation) pair it requires. Adding a new route or tool forces the author to add an
    /// entry here, which decides the permission gate the request costs.
    /// </summary>
    public static class OperationScopeMap
    {
        #region Public-Members

        /// <summary>
        /// Server health / info.
        /// </summary>
        public const string ServerInfo = "server/info";

        /// <summary>
        /// Authenticate a credential.
        /// </summary>
        public const string AuthAuthenticate = "auth/authenticate";

        #endregion

        #region Private-Members

        private static readonly Dictionary<string, OperationScope> _Map = Build();

        #endregion

        #region Public-Methods

        /// <summary>
        /// Resolve the operation scope for a request type. Unknown request types default to (Server, Read) for
        /// read-shaped health calls; callers that require an entry should verify with <see cref="Contains"/>.
        /// Per the authentication requirements, an unclassifiable mutation must never be treated as a read, so
        /// callers building a context for a body-bearing request should supply a known request type.
        /// </summary>
        /// <param name="requestType">Request type key (e.g. "tenant/create").</param>
        /// <returns>OperationScope; never null.</returns>
        public static OperationScope Resolve(string requestType)
        {
            if (!string.IsNullOrEmpty(requestType) && _Map.TryGetValue(requestType, out OperationScope scope))
                return scope;

            return new OperationScope(ResourceTypeEnum.Server, OperationTypeEnum.Read);
        }

        /// <summary>
        /// Whether a request type is present in the map.
        /// </summary>
        /// <param name="requestType">Request type key.</param>
        /// <returns>True if mapped.</returns>
        public static bool Contains(string requestType)
        {
            return !string.IsNullOrEmpty(requestType) && _Map.ContainsKey(requestType);
        }

        /// <summary>
        /// All mapped request-type keys.
        /// </summary>
        /// <returns>Read-only collection of keys.</returns>
        public static IReadOnlyCollection<string> Keys()
        {
            return _Map.Keys;
        }

        #endregion

        #region Private-Methods

        private static Dictionary<string, OperationScope> Build()
        {
            Dictionary<string, OperationScope> map = new Dictionary<string, OperationScope>();

            map["server/info"] = new OperationScope(ResourceTypeEnum.Server, OperationTypeEnum.Read);
            map["auth/authenticate"] = new OperationScope(ResourceTypeEnum.Authentication, OperationTypeEnum.Execute);

            AddCrud(map, "tenant", ResourceTypeEnum.Tenant);
            AddCrud(map, "user", ResourceTypeEnum.User);
            AddCrud(map, "credential", ResourceTypeEnum.Credential);
            AddCrud(map, "collection", ResourceTypeEnum.Collection);
            map["collection/stats"] = new OperationScope(ResourceTypeEnum.Collection, OperationTypeEnum.Read);

            AddCrud(map, "document", ResourceTypeEnum.Document);
            map["document/readByPosition"] = new OperationScope(ResourceTypeEnum.Document, OperationTypeEnum.Read);
            map["document/stats"] = new OperationScope(ResourceTypeEnum.Document, OperationTypeEnum.Read);
            map["document/batchCreate"] = new OperationScope(ResourceTypeEnum.Document, OperationTypeEnum.Create);
            map["document/batchDelete"] = new OperationScope(ResourceTypeEnum.Document, OperationTypeEnum.Delete);
            map["document/deleteByFilter"] = new OperationScope(ResourceTypeEnum.Document, OperationTypeEnum.Delete);

            AddCrud(map, "label", ResourceTypeEnum.Label);
            AddCrud(map, "tag", ResourceTypeEnum.Tag);

            map["search/query"] = new OperationScope(ResourceTypeEnum.Search, OperationTypeEnum.Search);

            map["requestHistory/enumerate"] = new OperationScope(ResourceTypeEnum.RequestHistory, OperationTypeEnum.Enumerate);
            map["requestHistory/read"] = new OperationScope(ResourceTypeEnum.RequestHistory, OperationTypeEnum.Read);
            map["requestHistory/summary"] = new OperationScope(ResourceTypeEnum.RequestHistory, OperationTypeEnum.Read);
            map["requestHistory/delete"] = new OperationScope(ResourceTypeEnum.RequestHistory, OperationTypeEnum.Delete);

            return map;
        }

        private static void AddCrud(Dictionary<string, OperationScope> map, string family, ResourceTypeEnum resourceType)
        {
            map[family + "/read"] = new OperationScope(resourceType, OperationTypeEnum.Read);
            map[family + "/exists"] = new OperationScope(resourceType, OperationTypeEnum.Read);
            map[family + "/enumerate"] = new OperationScope(resourceType, OperationTypeEnum.Enumerate);
            map[family + "/create"] = new OperationScope(resourceType, OperationTypeEnum.Create);
            map[family + "/update"] = new OperationScope(resourceType, OperationTypeEnum.Update);
            map[family + "/delete"] = new OperationScope(resourceType, OperationTypeEnum.Delete);
        }

        #endregion
    }
}
