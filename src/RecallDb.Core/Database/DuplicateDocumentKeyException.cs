namespace RecallDb.Core.Database
{
    using System;

    /// <summary>
    /// Thrown when a document cannot be created because a document with the same DocumentKey already exists in the
    /// collection (the collection's unique document_key index). Nothing is written. Callers translate this into a
    /// 409 Conflict.
    /// </summary>
    public class DuplicateDocumentKeyException : Exception
    {
        /// <summary>
        /// Collection id the conflict occurred in.
        /// </summary>
        public string CollectionId { get; }

        /// <summary>
        /// The conflicting key when a single document was written, or null for a batch (the database does not say
        /// which row conflicted).
        /// </summary>
        public string DocumentKey { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="collectionId">Collection id.</param>
        /// <param name="documentKey">Conflicting key, or null when not known.</param>
        /// <param name="inner">Underlying database exception, if any.</param>
        public DuplicateDocumentKeyException(string collectionId, string documentKey, Exception inner = null)
            : base(documentKey != null
                ? "A document with DocumentKey '" + documentKey + "' already exists in collection '" + collectionId + "'."
                : "One or more DocumentKeys already exist in collection '" + collectionId + "'.", inner)
        {
            CollectionId = collectionId;
            DocumentKey = documentKey;
        }
    }
}
