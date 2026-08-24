namespace RecallDb.Server.Services
{
    using RecallDb.Core.Enums;

    /// <summary>
    /// The (ResourceType, Operation) pair a request type maps to. Produced by <see cref="OperationScopeMap"/>.
    /// </summary>
    public class OperationScope
    {
        #region Public-Members

        /// <summary>
        /// Resource type the request targets.
        /// </summary>
        public ResourceTypeEnum ResourceType { get; set; }

        /// <summary>
        /// Operation the request performs.
        /// </summary>
        public OperationTypeEnum Operation { get; set; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public OperationScope()
        {
        }

        /// <summary>
        /// Instantiate with a resource type and operation.
        /// </summary>
        /// <param name="resourceType">Resource type.</param>
        /// <param name="operation">Operation.</param>
        public OperationScope(ResourceTypeEnum resourceType, OperationTypeEnum operation)
        {
            ResourceType = resourceType;
            Operation = operation;
        }

        #endregion
    }
}
