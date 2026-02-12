namespace RecallDb.Core.Helpers
{
    using System;
    using System.Security.Cryptography;

    /// <summary>
    /// ID generator for creating K-sortable identifiers and bearer tokens.
    /// </summary>
    public static class IdGenerator
    {
        #region Public-Members

        /// <summary>
        /// Tenant ID prefix.
        /// </summary>
        public const string TenantPrefix = "ten_";

        /// <summary>
        /// User ID prefix.
        /// </summary>
        public const string UserPrefix = "usr_";

        /// <summary>
        /// Credential ID prefix.
        /// </summary>
        public const string CredentialPrefix = "cred_";

        /// <summary>
        /// Collection ID prefix.
        /// </summary>
        public const string CollectionPrefix = "col_";

        /// <summary>
        /// Label ID prefix.
        /// </summary>
        public const string LabelPrefix = "lbl_";

        /// <summary>
        /// Tag ID prefix.
        /// </summary>
        public const string TagPrefix = "tag_";

        /// <summary>
        /// Default ID length.
        /// </summary>
        public const int DefaultIdLength = 40;

        /// <summary>
        /// Default bearer token length.
        /// </summary>
        public const int DefaultBearerTokenLength = 64;

        #endregion

        #region Private-Members

        private static readonly PrettyId.IdGenerator _Generator = new PrettyId.IdGenerator();
        private static readonly string _AlphanumericChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Generate a new tenant ID.
        /// </summary>
        /// <returns>K-sortable tenant ID with prefix ten_.</returns>
        public static string NewTenantId()
        {
            return _Generator.GenerateKSortable(TenantPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new user ID.
        /// </summary>
        /// <returns>K-sortable user ID with prefix usr_.</returns>
        public static string NewUserId()
        {
            return _Generator.GenerateKSortable(UserPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new credential ID.
        /// </summary>
        /// <returns>K-sortable credential ID with prefix cred_.</returns>
        public static string NewCredentialId()
        {
            return _Generator.GenerateKSortable(CredentialPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new collection ID.
        /// </summary>
        /// <returns>K-sortable collection ID with prefix col_.</returns>
        public static string NewCollectionId()
        {
            return _Generator.GenerateKSortable(CollectionPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new label ID.
        /// </summary>
        /// <returns>K-sortable label ID with prefix lbl_.</returns>
        public static string NewLabelId()
        {
            return _Generator.GenerateKSortable(LabelPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new tag ID.
        /// </summary>
        /// <returns>K-sortable tag ID with prefix tag_.</returns>
        public static string NewTagId()
        {
            return _Generator.GenerateKSortable(TagPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new bearer token.
        /// </summary>
        /// <returns>64-character cryptographically random alphanumeric string.</returns>
        public static string NewBearerToken()
        {
            char[] token = new char[DefaultBearerTokenLength];
            byte[] randomBytes = RandomNumberGenerator.GetBytes(DefaultBearerTokenLength);
            for (int i = 0; i < DefaultBearerTokenLength; i++)
            {
                token[i] = _AlphanumericChars[randomBytes[i] % _AlphanumericChars.Length];
            }
            return new string(token);
        }

        #endregion
    }
}
