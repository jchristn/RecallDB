"""
RecallDB Python SDK.

Provides a client for interacting with the RecallDB REST API.
"""

import requests


class RecallDbException(Exception):
    """Exception raised when a RecallDB API call fails."""

    def __init__(self, status_code, response_body):
        self.status_code = status_code
        self.response_body = response_body
        super().__init__(f"RecallDB API returned {status_code}: {response_body}")


class RecallDbClient:
    """Client for the RecallDB REST API."""

    def __init__(self, endpoint, bearer_token):
        """
        Initialize the RecallDB client.

        Args:
            endpoint: Base URL of the RecallDB server (e.g. http://localhost:8600).
            bearer_token: Bearer token for authentication.
        """
        if not endpoint:
            raise ValueError("endpoint is required")
        if not bearer_token:
            raise ValueError("bearer_token is required")

        self._endpoint = endpoint.rstrip("/")
        self._session = requests.Session()
        self._session.headers.update({
            "Authorization": f"Bearer {bearer_token}",
            "Content-Type": "application/json"
        })

    # -------------------------------------------------------------------------
    # Health
    # -------------------------------------------------------------------------

    def health(self):
        """
        Retrieve health and version information from the server.

        Returns:
            dict: Health response fields.
        """
        return self._get("/")

    # -------------------------------------------------------------------------
    # Authenticate
    # -------------------------------------------------------------------------

    def authenticate(self, request):
        """
        Authenticate using bearer token or email+password credentials.

        Args:
            request: dict with BearerToken or TenantId+Email+Password.

        Returns:
            dict: Authentication response.
        """
        return self._post("/v1.0/authenticate", request)

    # -------------------------------------------------------------------------
    # Tenants
    # -------------------------------------------------------------------------

    def create_tenant(self, tenant):
        """
        Create a tenant.

        Args:
            tenant: dict with tenant metadata.

        Returns:
            dict: Created tenant metadata.
        """
        return self._put("/v1.0/tenants", tenant)

    def get_tenant(self, tenant_id):
        """
        Retrieve a tenant by ID.

        Args:
            tenant_id: Tenant ID.

        Returns:
            dict: Tenant metadata.
        """
        return self._get(f"/v1.0/tenants/{tenant_id}")

    def update_tenant(self, tenant_id, tenant):
        """
        Update a tenant.

        Args:
            tenant_id: Tenant ID.
            tenant: dict with updated tenant metadata.

        Returns:
            dict: Updated tenant metadata.
        """
        return self._put(f"/v1.0/tenants/{tenant_id}", tenant)

    def delete_tenant(self, tenant_id):
        """
        Delete a tenant.

        Args:
            tenant_id: Tenant ID.
        """
        self._delete(f"/v1.0/tenants/{tenant_id}")

    def tenant_exists(self, tenant_id):
        """
        Check if a tenant exists.

        Args:
            tenant_id: Tenant ID.

        Returns:
            bool: True if the tenant exists.
        """
        return self._head(f"/v1.0/tenants/{tenant_id}")

    def enumerate_tenants(self, query=None):
        """
        Enumerate tenants with a query.

        Args:
            query: dict with enumeration query parameters.

        Returns:
            dict: Enumeration result containing tenant metadata.
        """
        if query is None:
            query = {}
        return self._post("/v1.0/tenants/enumerate", query)

    # -------------------------------------------------------------------------
    # Users
    # -------------------------------------------------------------------------

    def create_user(self, tenant_id, user):
        """
        Create a user.

        Args:
            tenant_id: Tenant ID.
            user: dict with user master record.

        Returns:
            dict: Created user master record.
        """
        return self._put(f"/v1.0/tenants/{tenant_id}/users", user)

    def get_user(self, tenant_id, user_id):
        """
        Retrieve a user by ID.

        Args:
            tenant_id: Tenant ID.
            user_id: User ID.

        Returns:
            dict: User master record.
        """
        return self._get(f"/v1.0/tenants/{tenant_id}/users/{user_id}")

    def update_user(self, tenant_id, user_id, user):
        """
        Update a user.

        Args:
            tenant_id: Tenant ID.
            user_id: User ID.
            user: dict with updated user master record.

        Returns:
            dict: Updated user master record.
        """
        return self._put(f"/v1.0/tenants/{tenant_id}/users/{user_id}", user)

    def delete_user(self, tenant_id, user_id):
        """
        Delete a user.

        Args:
            tenant_id: Tenant ID.
            user_id: User ID.
        """
        self._delete(f"/v1.0/tenants/{tenant_id}/users/{user_id}")

    def user_exists(self, tenant_id, user_id):
        """
        Check if a user exists.

        Args:
            tenant_id: Tenant ID.
            user_id: User ID.

        Returns:
            bool: True if the user exists.
        """
        return self._head(f"/v1.0/tenants/{tenant_id}/users/{user_id}")

    def enumerate_users(self, tenant_id, query=None):
        """
        Enumerate users with a query.

        Args:
            tenant_id: Tenant ID.
            query: dict with enumeration query parameters.

        Returns:
            dict: Enumeration result containing user master records.
        """
        if query is None:
            query = {}
        return self._post(f"/v1.0/tenants/{tenant_id}/users/enumerate", query)

    # -------------------------------------------------------------------------
    # Credentials
    # -------------------------------------------------------------------------

    def create_credential(self, tenant_id, credential):
        """
        Create a credential.

        Args:
            tenant_id: Tenant ID.
            credential: dict with credential data.

        Returns:
            dict: Created credential.
        """
        return self._put(f"/v1.0/tenants/{tenant_id}/credentials", credential)

    def get_credential(self, tenant_id, credential_id):
        """
        Retrieve a credential by ID.

        Args:
            tenant_id: Tenant ID.
            credential_id: Credential ID.

        Returns:
            dict: Credential.
        """
        return self._get(f"/v1.0/tenants/{tenant_id}/credentials/{credential_id}")

    def update_credential(self, tenant_id, credential_id, credential):
        """
        Update a credential.

        Args:
            tenant_id: Tenant ID.
            credential_id: Credential ID.
            credential: dict with updated credential data.

        Returns:
            dict: Updated credential.
        """
        return self._put(f"/v1.0/tenants/{tenant_id}/credentials/{credential_id}", credential)

    def delete_credential(self, tenant_id, credential_id):
        """
        Delete a credential.

        Args:
            tenant_id: Tenant ID.
            credential_id: Credential ID.
        """
        self._delete(f"/v1.0/tenants/{tenant_id}/credentials/{credential_id}")

    def credential_exists(self, tenant_id, credential_id):
        """
        Check if a credential exists.

        Args:
            tenant_id: Tenant ID.
            credential_id: Credential ID.

        Returns:
            bool: True if the credential exists.
        """
        return self._head(f"/v1.0/tenants/{tenant_id}/credentials/{credential_id}")

    def enumerate_credentials(self, tenant_id, query=None):
        """
        Enumerate credentials with a query.

        Args:
            tenant_id: Tenant ID.
            query: dict with enumeration query parameters.

        Returns:
            dict: Enumeration result containing credentials.
        """
        if query is None:
            query = {}
        return self._post(f"/v1.0/tenants/{tenant_id}/credentials/enumerate", query)

    # -------------------------------------------------------------------------
    # Collections
    # -------------------------------------------------------------------------

    def create_collection(self, tenant_id, collection):
        """
        Create a collection.

        Args:
            tenant_id: Tenant ID.
            collection: dict with collection metadata.

        Returns:
            dict: Created collection metadata.
        """
        return self._put(f"/v1.0/tenants/{tenant_id}/collections", collection)

    def get_collection(self, tenant_id, collection_id):
        """
        Retrieve a collection by ID.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.

        Returns:
            dict: Collection metadata.
        """
        return self._get(f"/v1.0/tenants/{tenant_id}/collections/{collection_id}")

    def update_collection(self, tenant_id, collection_id, collection):
        """
        Update a collection.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.
            collection: dict with updated collection metadata.

        Returns:
            dict: Updated collection metadata.
        """
        return self._put(f"/v1.0/tenants/{tenant_id}/collections/{collection_id}", collection)

    def delete_collection(self, tenant_id, collection_id):
        """
        Delete a collection.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.
        """
        self._delete(f"/v1.0/tenants/{tenant_id}/collections/{collection_id}")

    def collection_exists(self, tenant_id, collection_id):
        """
        Check if a collection exists.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.

        Returns:
            bool: True if the collection exists.
        """
        return self._head(f"/v1.0/tenants/{tenant_id}/collections/{collection_id}")

    def enumerate_collections(self, tenant_id, query=None):
        """
        Enumerate collections with a query.

        Args:
            tenant_id: Tenant ID.
            query: dict with enumeration query parameters.

        Returns:
            dict: Enumeration result containing collection metadata.
        """
        if query is None:
            query = {}
        return self._post(f"/v1.0/tenants/{tenant_id}/collections/enumerate", query)

    # -------------------------------------------------------------------------
    # Documents
    # -------------------------------------------------------------------------

    def create_document(self, tenant_id, collection_id, document):
        """
        Create a document.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.
            document: dict with document record.

        Returns:
            dict: Created document record.
        """
        return self._put(
            f"/v1.0/tenants/{tenant_id}/collections/{collection_id}/documents",
            document)

    def get_document(self, tenant_id, collection_id, document_key):
        """
        Retrieve a document by document key.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.
            document_key: Document key.

        Returns:
            dict: Document record.
        """
        return self._get(
            f"/v1.0/tenants/{tenant_id}/collections/{collection_id}/documents/{document_key}")

    def get_document_by_position(self, tenant_id, collection_id, document_id, position):
        """
        Retrieve a document chunk by document ID and position.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.
            document_id: Document ID.
            position: Chunk position.

        Returns:
            dict: Document record.
        """
        return self._get(
            f"/v1.0/tenants/{tenant_id}/collections/{collection_id}/documents/{document_id}/{position}")

    def update_document(self, tenant_id, collection_id, document_key, document):
        """
        Update a document.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.
            document_key: Document key.
            document: dict with updated document record.

        Returns:
            dict: Updated document record.
        """
        return self._put(
            f"/v1.0/tenants/{tenant_id}/collections/{collection_id}/documents/{document_key}",
            document)

    def delete_document(self, tenant_id, collection_id, document_key):
        """
        Delete a document.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.
            document_key: Document key.
        """
        self._delete(
            f"/v1.0/tenants/{tenant_id}/collections/{collection_id}/documents/{document_key}")

    def document_exists(self, tenant_id, collection_id, document_key):
        """
        Check if a document exists.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.
            document_key: Document key.

        Returns:
            bool: True if the document exists.
        """
        return self._head(
            f"/v1.0/tenants/{tenant_id}/collections/{collection_id}/documents/{document_key}")

    def enumerate_documents(self, tenant_id, collection_id, query=None):
        """
        Enumerate documents with a query.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.
            query: dict with enumeration query parameters.

        Returns:
            dict: Enumeration result containing document records.
        """
        if query is None:
            query = {}
        return self._post(
            f"/v1.0/tenants/{tenant_id}/collections/{collection_id}/documents/enumerate",
            query)

    def create_document_batch(self, tenant_id, collection_id, documents):
        """
        Batch create documents.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.
            documents: list of dict with document records.

        Returns:
            list: List of created document records.
        """
        return self._post(
            f"/v1.0/tenants/{tenant_id}/collections/{collection_id}/documents/batch",
            documents)

    def delete_document_batch(self, tenant_id, collection_id, document_keys):
        """
        Batch delete documents by their document keys.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.
            document_keys: list of document key strings to delete.
        """
        self._post(
            f"/v1.0/tenants/{tenant_id}/collections/{collection_id}/documents/batch/delete",
            {"DocumentKeys": document_keys})

    def delete_documents_by_filter(self, tenant_id, collection_id, filter_query=None):
        """
        Delete documents matching filter criteria.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.
            filter_query: dict with enumeration query filter parameters.

        Returns:
            dict: Delete result with DocumentsDeleted count.
        """
        if filter_query is None:
            filter_query = {}
        return self._post(
            f"/v1.0/tenants/{tenant_id}/collections/{collection_id}/documents/delete/filter",
            filter_query)

    # -------------------------------------------------------------------------
    # Labels
    # -------------------------------------------------------------------------

    def create_label(self, tenant_id, collection_id, label):
        """
        Create a label.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.
            label: dict with label record.

        Returns:
            dict: Created label record.
        """
        return self._put(
            f"/v1.0/tenants/{tenant_id}/collections/{collection_id}/labels",
            label)

    def get_label(self, tenant_id, collection_id, label_id):
        """
        Retrieve a label by ID.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.
            label_id: Label ID.

        Returns:
            dict: Label record.
        """
        return self._get(
            f"/v1.0/tenants/{tenant_id}/collections/{collection_id}/labels/{label_id}")

    def delete_label(self, tenant_id, collection_id, label_id):
        """
        Delete a label.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.
            label_id: Label ID.
        """
        self._delete(
            f"/v1.0/tenants/{tenant_id}/collections/{collection_id}/labels/{label_id}")

    def list_labels(self, tenant_id, collection_id):
        """
        List all labels in a collection.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.

        Returns:
            dict: Enumeration result containing label records.
        """
        return self._get(
            f"/v1.0/tenants/{tenant_id}/collections/{collection_id}/labels")

    # -------------------------------------------------------------------------
    # Tags
    # -------------------------------------------------------------------------

    def create_tag(self, tenant_id, collection_id, tag):
        """
        Create a tag.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.
            tag: dict with tag record.

        Returns:
            dict: Created tag record.
        """
        return self._put(
            f"/v1.0/tenants/{tenant_id}/collections/{collection_id}/tags",
            tag)

    def get_tag(self, tenant_id, collection_id, tag_id):
        """
        Retrieve a tag by ID.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.
            tag_id: Tag ID.

        Returns:
            dict: Tag record.
        """
        return self._get(
            f"/v1.0/tenants/{tenant_id}/collections/{collection_id}/tags/{tag_id}")

    def delete_tag(self, tenant_id, collection_id, tag_id):
        """
        Delete a tag.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.
            tag_id: Tag ID.
        """
        self._delete(
            f"/v1.0/tenants/{tenant_id}/collections/{collection_id}/tags/{tag_id}")

    def list_tags(self, tenant_id, collection_id):
        """
        List all tags in a collection.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.

        Returns:
            dict: Enumeration result containing tag records.
        """
        return self._get(
            f"/v1.0/tenants/{tenant_id}/collections/{collection_id}/tags")

    # -------------------------------------------------------------------------
    # Search
    # -------------------------------------------------------------------------

    def search(self, tenant_id, collection_id, query):
        """
        Search documents in a collection.

        Supports three search modes:
        - Vector-only: provide Vector without FullText.
        - Full-text-only: provide FullText without Vector.
        - Hybrid: provide both Vector and FullText for blended scoring.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.
            query: dict with search query parameters. Supported keys:
                SortOrder (str): Sort order, e.g. "ScoreDescending", "TextScoreDescending".
                Vector (dict): Vector query with SearchType, Embeddings, MinimumScore, etc.
                FullText (dict): Full-text search query parameters:
                    Query (str): Search text (required). Processed with stemming and stop word removal.
                    SearchType (str): Ranking function - "TsRank" (default) or "TsRankCd" (cover density).
                    Language (str): Text search configuration, default "english".
                    Normalization (int): ts_rank normalization bitmask, default 32 (0-1 range).
                    MinimumScore (float): Minimum text relevance score threshold.
                    TextWeight (float): Weight for text score in hybrid mode (0.0-1.0, default 0.5).
                LabelFilter (dict): Label filter with Required and Excluded lists.
                TagFilter (dict): Tag filter with Required and Excluded condition lists.
                Terms (dict): Terms filter for content matching,
                    e.g. {"Required": ["term1"], "Excluded": ["term2"]}.
                MaxResults (int): Maximum results (1-1000, default 10).
                IncludeNeighbors (int): Number of neighboring chunks before and after
                    each matched chunk to include (0-10). When set, each document in the
                    response will include a Neighbors list of surrounding chunks ordered
                    by position. Default: null (no neighbors).

        Returns:
            dict: Search result. Documents include Score and, when FullText is used,
                TextScore (float) with the full-text relevance score. When IncludeNeighbors
                is set, each document will also include a Neighbors list of adjacent chunks.
        """
        return self._post(
            f"/v1.0/tenants/{tenant_id}/collections/{collection_id}/search",
            query)

    # -------------------------------------------------------------------------
    # Private HTTP helpers
    # -------------------------------------------------------------------------

    def _get(self, path):
        response = self._session.get(self._endpoint + path)
        return self._handle_response(response)

    def _head(self, path):
        response = self._session.head(self._endpoint + path)
        return response.status_code == 200

    def _post(self, path, body):
        response = self._session.post(self._endpoint + path, json=body)
        return self._handle_response(response)

    def _put(self, path, body):
        response = self._session.put(self._endpoint + path, json=body)
        return self._handle_response(response)

    def _delete(self, path):
        response = self._session.delete(self._endpoint + path)
        if not response.ok and response.status_code != 204:
            raise RecallDbException(response.status_code, response.text)

    def _handle_response(self, response):
        if not response.ok:
            raise RecallDbException(response.status_code, response.text)
        if not response.text:
            return None
        return response.json()
