"""
RecallDB Python SDK.

Provides a client for interacting with the RecallDB REST API.
"""

import json
import numbers
from urllib.parse import quote

import requests


__version__ = "0.2.2"


def _seg(value):
    """URL-encode one caller-supplied path segment, including '/', '#', '?', '%', and spaces."""
    return quote(str(value), safe="")


class RecallDbException(Exception):
    """
    Exception raised when a RecallDB API call fails.

    Attributes:
        status_code (int): HTTP status code returned by the server.
        response_body (str): Raw response body text, unchanged (empty for HEAD requests).
        error_code (str or None): The body's Error field (for example "BadRequest" or
            "NotAuthorized"), or None when the body is not a JSON object.
        error_message (str or None): The first non-empty of the body's Context, Message, and
            Description fields, or None when the body is not a JSON object or has none of them.
    """

    def __init__(self, status_code, response_body):
        self.status_code = status_code
        self.response_body = response_body
        self.error_code = None
        self.error_message = None

        parsed = None
        if response_body:
            try:
                parsed = json.loads(response_body)
            except (ValueError, TypeError):
                parsed = None

        if isinstance(parsed, dict):
            error_code = parsed.get("Error")
            if isinstance(error_code, str) and error_code:
                self.error_code = error_code
            for field in ("Context", "Message", "Description"):
                value = parsed.get(field)
                if isinstance(value, str) and value.strip():
                    self.error_message = value
                    break

        if self.error_code is not None and self.error_message is not None:
            message = f"RecallDB API returned {status_code} ({self.error_code}): {self.error_message}"
        elif self.error_message is not None:
            message = f"RecallDB API returned {status_code}: {self.error_message}"
        else:
            message = f"RecallDB API returned {status_code}: {response_body}"
        super().__init__(message)


# -----------------------------------------------------------------------------
# Named constants for string values accepted and returned by the server
# -----------------------------------------------------------------------------

class Capabilities:
    """Capability names reported by GET / (see RecallDbClient.supports)."""
    HYBRID_RRF = "search.hybrid.rrf"
    HYBRID_RECENCY = "search.hybrid.recency"
    COLLAPSE = "search.collapse"
    INCLUDE_EMBEDDINGS = "search.include-embeddings"
    FULLTEXT_MINIMUM_SHOULD_MATCH = "search.fulltext.minimum-should-match"


class HybridStrategies:
    """Values for Hybrid.Strategy."""
    RRF = "Rrf"
    LINEAR = "Linear"
    FILTER = "Filter"


class FullTextMatchModes:
    """Values for FullText.MatchMode."""
    ANY = "Any"
    ALL = "All"
    PHRASE = "Phrase"
    WEB_SEARCH = "WebSearch"


class FullTextSearchTypes:
    """Values for FullText.SearchType."""
    TS_RANK = "TsRank"
    TS_RANK_CD = "TsRankCd"


class VectorSearchTypes:
    """Values for Vector.SearchType."""
    COSINE_SIMILARITY = "CosineSimilarity"
    COSINE_DISTANCE = "CosineDistance"
    EUCLIDEAN_SIMILARITY = "EuclideanSimilarity"
    EUCLIDEAN_DISTANCE = "EuclideanDistance"
    INNER_PRODUCT = "InnerProduct"


class SortOrders:
    """Values for SortOrder on search requests."""
    SCORE_ASCENDING = "ScoreAscending"
    SCORE_DESCENDING = "ScoreDescending"
    DISTANCE_ASCENDING = "DistanceAscending"
    DISTANCE_DESCENDING = "DistanceDescending"
    CREATED_ASCENDING = "CreatedAscending"
    CREATED_DESCENDING = "CreatedDescending"
    TEXT_SCORE_ASCENDING = "TextScoreAscending"
    TEXT_SCORE_DESCENDING = "TextScoreDescending"


class CollapseFields:
    """Values for Collapse.Field."""
    DOCUMENT_ID = "DocumentId"
    TAG = "Tag"


_DEFAULT_TIMEOUT = 100


class RecallDbClient:
    """Client for the RecallDB REST API."""

    def __init__(self, endpoint, bearer_token, timeout=_DEFAULT_TIMEOUT, session=None):
        """
        Initialize the RecallDB client.

        Args:
            endpoint: Base URL of the RecallDB server (e.g. http://127.0.0.1:8600).
            bearer_token: Bearer token for authentication.
            timeout: Seconds to wait on every request (passed to requests as its connect and
                read timeout). Default 100. None disables the timeout. Any other value must be
                a positive number, otherwise ValueError is raised. When the timeout elapses the
                call raises requests.exceptions.Timeout (a subclass of
                requests.exceptions.RequestException) instead of hanging.
            session: Optional caller-supplied requests.Session, for example one with a retry
                adapter mounted. The client never modifies or closes a supplied session: its
                headers are left untouched, and the Authorization and Content-Type headers are
                sent on each request instead, so they apply to this client's calls only. When
                omitted, the client creates and owns its own session (see close()).
        """
        if not endpoint:
            raise ValueError("endpoint is required")
        if not bearer_token:
            raise ValueError("bearer_token is required")
        if timeout is not None:
            if isinstance(timeout, bool) or not isinstance(timeout, numbers.Real) or timeout <= 0:
                raise ValueError("timeout must be a positive number of seconds or None")

        self._endpoint = endpoint.rstrip("/")
        self._timeout = timeout
        self._owns_session = session is None
        self._session = session if session is not None else requests.Session()
        self._headers = {
            "Authorization": f"Bearer {bearer_token}",
            "Content-Type": "application/json"
        }
        self._capabilities = None

    @property
    def timeout(self):
        """The per-request timeout in seconds, or None when disabled."""
        return self._timeout

    def close(self):
        """Close the session the client created. A caller-supplied session is left open."""
        if self._owns_session:
            self._session.close()

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc, tb):
        self.close()

    # -------------------------------------------------------------------------
    # Health and server info
    # -------------------------------------------------------------------------

    def health(self):
        """
        Retrieve health and version information from the server.

        Returns:
            dict: Health response fields.
        """
        return self._get("/")

    def get_server_info(self):
        """
        Retrieve server information from GET /.

        Returns:
            dict: Name (str), Version (str), UptimeMs (int), and Capabilities (list of str).
                Capabilities is [] when the server does not report it (older servers). The
                capability list is also cached on this client for supports().
        """
        info = self._get("/")
        if not isinstance(info, dict):
            info = {}
        capabilities = info.get("Capabilities")
        if not isinstance(capabilities, list):
            capabilities = []
        info["Capabilities"] = capabilities
        self._capabilities = list(capabilities)
        return info

    def supports(self, name, refresh=False):
        """
        Check whether the server reports a capability (see the Capabilities constants).

        The capability list is fetched with get_server_info() on first use and cached on this
        client. Older servers report no capabilities, so this returns False on them.

        Args:
            name: Capability name, e.g. Capabilities.COLLAPSE.
            refresh: True to fetch the list from the server again.

        Returns:
            bool: True if the capability is listed.
        """
        if self._capabilities is None or refresh:
            self.get_server_info()
        return name in self._capabilities

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
        return self._get(f"/v1.0/tenants/{_seg(tenant_id)}")

    def update_tenant(self, tenant_id, tenant):
        """
        Update a tenant.

        Args:
            tenant_id: Tenant ID.
            tenant: dict with updated tenant metadata.

        Returns:
            dict: Updated tenant metadata.
        """
        return self._put(f"/v1.0/tenants/{_seg(tenant_id)}", tenant)

    def delete_tenant(self, tenant_id):
        """
        Delete a tenant.

        Args:
            tenant_id: Tenant ID.
        """
        self._delete(f"/v1.0/tenants/{_seg(tenant_id)}")

    def tenant_exists(self, tenant_id):
        """
        Check if a tenant exists.

        Args:
            tenant_id: Tenant ID.

        Returns:
            bool: True if the tenant exists (200), False if it does not (404).

        Raises:
            RecallDbException: For any other status (for example 401, 403, 429, or 5xx).
        """
        return self._head(f"/v1.0/tenants/{_seg(tenant_id)}")

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
        return self._put(f"/v1.0/tenants/{_seg(tenant_id)}/users", user)

    def get_user(self, tenant_id, user_id):
        """
        Retrieve a user by ID.

        Args:
            tenant_id: Tenant ID.
            user_id: User ID.

        Returns:
            dict: User master record.
        """
        return self._get(f"/v1.0/tenants/{_seg(tenant_id)}/users/{_seg(user_id)}")

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
        return self._put(f"/v1.0/tenants/{_seg(tenant_id)}/users/{_seg(user_id)}", user)

    def delete_user(self, tenant_id, user_id):
        """
        Delete a user.

        Args:
            tenant_id: Tenant ID.
            user_id: User ID.
        """
        self._delete(f"/v1.0/tenants/{_seg(tenant_id)}/users/{_seg(user_id)}")

    def user_exists(self, tenant_id, user_id):
        """
        Check if a user exists.

        Args:
            tenant_id: Tenant ID.
            user_id: User ID.

        Returns:
            bool: True if the user exists (200), False if it does not (404).

        Raises:
            RecallDbException: For any other status (for example 401, 403, 429, or 5xx).
        """
        return self._head(f"/v1.0/tenants/{_seg(tenant_id)}/users/{_seg(user_id)}")

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
        return self._post(f"/v1.0/tenants/{_seg(tenant_id)}/users/enumerate", query)

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
        return self._put(f"/v1.0/tenants/{_seg(tenant_id)}/credentials", credential)

    def get_credential(self, tenant_id, credential_id):
        """
        Retrieve a credential by ID.

        Args:
            tenant_id: Tenant ID.
            credential_id: Credential ID.

        Returns:
            dict: Credential.
        """
        return self._get(f"/v1.0/tenants/{_seg(tenant_id)}/credentials/{_seg(credential_id)}")

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
        return self._put(f"/v1.0/tenants/{_seg(tenant_id)}/credentials/{_seg(credential_id)}", credential)

    def delete_credential(self, tenant_id, credential_id):
        """
        Delete a credential.

        Args:
            tenant_id: Tenant ID.
            credential_id: Credential ID.
        """
        self._delete(f"/v1.0/tenants/{_seg(tenant_id)}/credentials/{_seg(credential_id)}")

    def credential_exists(self, tenant_id, credential_id):
        """
        Check if a credential exists.

        Args:
            tenant_id: Tenant ID.
            credential_id: Credential ID.

        Returns:
            bool: True if the credential exists (200), False if it does not (404).

        Raises:
            RecallDbException: For any other status (for example 401, 403, 429, or 5xx).
        """
        return self._head(f"/v1.0/tenants/{_seg(tenant_id)}/credentials/{_seg(credential_id)}")

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
        return self._post(f"/v1.0/tenants/{_seg(tenant_id)}/credentials/enumerate", query)

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
        return self._put(f"/v1.0/tenants/{_seg(tenant_id)}/collections", collection)

    def get_collection(self, tenant_id, collection_id):
        """
        Retrieve a collection by ID.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.

        Returns:
            dict: Collection metadata.
        """
        return self._get(f"/v1.0/tenants/{_seg(tenant_id)}/collections/{_seg(collection_id)}")

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
        return self._put(f"/v1.0/tenants/{_seg(tenant_id)}/collections/{_seg(collection_id)}", collection)

    def delete_collection(self, tenant_id, collection_id):
        """
        Delete a collection.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.
        """
        self._delete(f"/v1.0/tenants/{_seg(tenant_id)}/collections/{_seg(collection_id)}")

    def collection_exists(self, tenant_id, collection_id):
        """
        Check if a collection exists.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.

        Returns:
            bool: True if the collection exists (200), False if it does not (404).

        Raises:
            RecallDbException: For any other status (for example 401, 403, 429, or 5xx).
        """
        return self._head(f"/v1.0/tenants/{_seg(tenant_id)}/collections/{_seg(collection_id)}")

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
        return self._post(f"/v1.0/tenants/{_seg(tenant_id)}/collections/enumerate", query)

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
            f"/v1.0/tenants/{_seg(tenant_id)}/collections/{_seg(collection_id)}/documents",
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
            f"/v1.0/tenants/{_seg(tenant_id)}/collections/{_seg(collection_id)}/documents/{_seg(document_key)}")

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
            f"/v1.0/tenants/{_seg(tenant_id)}/collections/{_seg(collection_id)}/documents/{_seg(document_id)}/{_seg(position)}")

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
            f"/v1.0/tenants/{_seg(tenant_id)}/collections/{_seg(collection_id)}/documents/{_seg(document_key)}",
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
            f"/v1.0/tenants/{_seg(tenant_id)}/collections/{_seg(collection_id)}/documents/{_seg(document_key)}")

    def document_exists(self, tenant_id, collection_id, document_key):
        """
        Check if a document exists.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.
            document_key: Document key.

        Returns:
            bool: True if the document exists (200), False if it does not (404).

        Raises:
            RecallDbException: For any other status (for example 401, 403, 429, or 5xx).
        """
        return self._head(
            f"/v1.0/tenants/{_seg(tenant_id)}/collections/{_seg(collection_id)}/documents/{_seg(document_key)}")

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
            f"/v1.0/tenants/{_seg(tenant_id)}/collections/{_seg(collection_id)}/documents/enumerate",
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
            f"/v1.0/tenants/{_seg(tenant_id)}/collections/{_seg(collection_id)}/documents/batch",
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
            f"/v1.0/tenants/{_seg(tenant_id)}/collections/{_seg(collection_id)}/documents/batch/delete",
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
            f"/v1.0/tenants/{_seg(tenant_id)}/collections/{_seg(collection_id)}/documents/delete/filter",
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
            f"/v1.0/tenants/{_seg(tenant_id)}/collections/{_seg(collection_id)}/labels",
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
            f"/v1.0/tenants/{_seg(tenant_id)}/collections/{_seg(collection_id)}/labels/{_seg(label_id)}")

    def delete_label(self, tenant_id, collection_id, label_id):
        """
        Delete a label.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.
            label_id: Label ID.
        """
        self._delete(
            f"/v1.0/tenants/{_seg(tenant_id)}/collections/{_seg(collection_id)}/labels/{_seg(label_id)}")

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
            f"/v1.0/tenants/{_seg(tenant_id)}/collections/{_seg(collection_id)}/labels")

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
            f"/v1.0/tenants/{_seg(tenant_id)}/collections/{_seg(collection_id)}/tags",
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
            f"/v1.0/tenants/{_seg(tenant_id)}/collections/{_seg(collection_id)}/tags/{_seg(tag_id)}")

    def delete_tag(self, tenant_id, collection_id, tag_id):
        """
        Delete a tag.

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.
            tag_id: Tag ID.
        """
        self._delete(
            f"/v1.0/tenants/{_seg(tenant_id)}/collections/{_seg(collection_id)}/tags/{_seg(tag_id)}")

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
            f"/v1.0/tenants/{_seg(tenant_id)}/collections/{_seg(collection_id)}/tags")

    # -------------------------------------------------------------------------
    # Search
    # -------------------------------------------------------------------------

    def search(self, tenant_id, collection_id, query):
        """
        Search documents in a collection.

        Supports three search modes:
        - Vector-only: provide Vector without FullText.
        - Full-text-only: provide FullText without Vector.
        - Hybrid: provide both Vector and FullText in one call. By default the two legs
          are combined with reciprocal rank fusion (Hybrid.Strategy "Rrf"), so a text match
          is not required.

        Full-text queries match documents containing any of the query's terms by default
        (FullText.MatchMode "Any"). Use MatchMode "All" to require every term (the previous
        behavior), and Hybrid.Strategy "Filter" to restore the previous hybrid behavior
        (text match required, raw scores blended).

        Compatibility: the request is sent as given. Servers that predate a field silently
        ignore it (no error, and the result is computed without it), so check supports()
        before relying on a newer field: Capabilities.HYBRID_RRF (Hybrid.Strategy Rrf and the
        rank fields), Capabilities.HYBRID_RECENCY (Hybrid.RecencyWeight),
        Capabilities.COLLAPSE (Collapse), Capabilities.INCLUDE_EMBEDDINGS (IncludeEmbeddings),
        and Capabilities.FULLTEXT_MINIMUM_SHOULD_MATCH (FullText.MinimumShouldMatch).

        Args:
            tenant_id: Tenant ID.
            collection_id: Collection ID.
            query: dict with search query parameters. Supported keys:
                SortOrder (str): Sort order, see SortOrders, e.g. "ScoreDescending",
                    "TextScoreDescending".
                Vector (dict): Vector query with SearchType (see VectorSearchTypes),
                    Embeddings (list of float), MinimumScore, MaximumScore, MinimumDistance,
                    MaximumDistance.
                FullText (dict): Full-text search query parameters:
                    Query (str): Search text (required). Processed with stemming and stop word removal.
                    SearchType (str): Ranking function, "TsRank" (default) or "TsRankCd"
                        (cover density). See FullTextSearchTypes.
                    MatchMode (str): How the query text is matched: "Any" (default, any term
                        matches), "All" (every term required), "Phrase" (terms adjacent and in
                        order), or "WebSearch" (web-search syntax: "quoted phrase", or, -exclude).
                        See FullTextMatchModes.
                    MinimumShouldMatch (int): Minimum number of distinct query terms a document
                        must contain, MatchMode "Any" only (1-3, default 1, which is plain Any
                        matching). A query with fewer distinct terms than the value requires all
                        of them. A value above 1 with another MatchMode, or a value outside 1-3,
                        is rejected with 400.
                    Language (str): Text search configuration installed on the server, default
                        "english". Unknown configurations are rejected with 400.
                    Normalization (int): ts_rank normalization bitmask (0-63), default 32 (0-1 range).
                    MinimumScore (float): Minimum text relevance score threshold.
                    TextWeight (float): Share of the text leg in hybrid mode (0.0-1.0, default 0.5);
                        the vector leg gets 1 - TextWeight. Values outside 0.0-1.0 are rejected with 400.
                Hybrid (dict): Hybrid options, used only when both Vector (with Embeddings) and
                    FullText (with a non-blank Query) are provided; otherwise they are ignored and
                    the result carries a Notice. Default None (reciprocal rank fusion with default
                    settings):
                    Strategy (str): "Rrf" (default, reciprocal rank fusion, scores 0-1), "Linear"
                        (weighted sum of normalized scores, 0-1), or "Filter" (legacy: text match
                        required, raw scores blended). See HybridStrategies.
                    RrfK (int): Reciprocal rank fusion constant k (1-100000, default 60).
                    CandidatePool (int): Candidates each leg retrieves before fusion (1-10000).
                        Default None (max(MaxResults * 4, 100), capped at 1000).
                    RecencyWeight (float): Weight of a third, recency signal in Rrf fusion
                        (0.0-1.0, default 0.0, which is off). Candidates are ranked by the newest
                        CreatedUtc of their recency key (the collapse group when Collapse is set,
                        otherwise the document) and each adds RecencyWeight / (k + RecencyRank).
                        The fused score stays in 0-1. Rrf only: Linear and Filter ignore it and the
                        result carries a Notice. Values outside 0.0-1.0 are rejected with 400.
                Collapse (dict): Return one hit per group (its best-scoring candidate) instead of
                    one hit per chunk. When set, MaxResults, TotalRecords, RecordsRemaining, and
                    continuation tokens count groups. Works with vector-only, full-text-only, and
                    hybrid Rrf and Linear searches. Rejected with 400 with Hybrid.Strategy
                    "Filter", and when the request has neither a vector nor a text query.
                    Default None (no collapse):
                    Field (str): "DocumentId" (default) or "Tag". See CollapseFields. A document
                        with no group value is its own group, keyed by its DocumentKey.
                    TagKey (str): Name of the tag whose value is the group key. Required when
                        Field is "Tag" (400 otherwise); ignored for "DocumentId".
                    CandidatePool (int): Candidates retrieved before collapsing, for vector-only
                        and full-text-only searches (1-10000). Hybrid searches use
                        Hybrid.CandidatePool when set and this value otherwise. Default None
                        (max(MaxResults * 4, 100), capped at 1000). A pool with fewer distinct
                        groups than MaxResults returns fewer hits and a Notice.
                IncludeEmbeddings (bool): Return each hit's stored vector in Embeddings. Default
                    False. Vectors are sent as JSON numbers, which costs about 4 KB per hit at
                    384 dimensions and about 8 KB per hit at 768 dimensions, so request them only
                    when needed (for example for client-side reranking or deduplication).
                LabelFilter (dict): Label filter with Required and Excluded lists.
                TagFilter (dict): Tag filter with Required and Excluded condition lists.
                Terms (dict): Terms filter for content matching,
                    e.g. {"Required": ["term1"], "Excluded": ["term2"]}.
                MaxResults (int): Maximum results (1-1000, default 10). Counts groups when
                    Collapse is set.
                IncludeNeighbors (int): Number of neighboring chunks before and after
                    each matched chunk to include (0-10). When set, each document in the
                    response will include a Neighbors list of surrounding chunks ordered
                    by position. Default: null (no neighbors).

        Returns:
            dict: Search result with Success, MaxResults, TotalRecords, RecordsRemaining,
                EndOfResults, ContinuationToken, Documents, and optionally Notice (str), a
                description of how the search was evaluated (for example when the text query had
                no searchable terms, hybrid options or RecencyWeight were ignored, or a collapse
                pool held fewer groups than MaxResults). Each document (hit) carries:
                Score (float): The ranking score. In hybrid Rrf and Linear searches this is the
                    fused score (0-1).
                VectorScore (float): Raw similarity in the vector metric's units, in vector-only
                    and hybrid searches.
                TextScore (float): Full-text relevance score, when FullText is used and the
                    document matched the text leg; absent otherwise.
                VectorRank, TextRank (int): 1-based ranks in the hybrid vector and text legs
                    (Rrf and Linear only); absent (None) when the document was not in that leg,
                    so a text-only hit has no VectorRank.
                RecencyRank (int): 1-based rank of the hit's recency key, 1 = newest (hybrid Rrf
                    with RecencyWeight above 0 only). Every candidate of one collapse group
                    shares a rank.
                GroupKey (str): The DocumentId or tag value the hit was grouped by, or its
                    DocumentKey when it has none (Collapse only).
                GroupHits (int): Number of candidates in the hit's group, including the hit
                    (Collapse only).
                Embeddings (list of float): The stored vector (IncludeEmbeddings only).
                Neighbors (list): Adjacent chunks ordered by position (IncludeNeighbors only).

        Raises:
            RecallDbException: When the server rejects the request, for example with 400 for
                an out-of-range value; error_message carries the server's explanation.
            requests.exceptions.Timeout: When the server does not respond within the client's
                timeout.
        """
        return self._post(
            f"/v1.0/tenants/{_seg(tenant_id)}/collections/{_seg(collection_id)}/search",
            query)

    # -------------------------------------------------------------------------
    # Private HTTP helpers
    # -------------------------------------------------------------------------

    def _request(self, method, path, **kwargs):
        return self._session.request(
            method,
            self._endpoint + path,
            headers=self._headers,
            timeout=self._timeout,
            **kwargs)

    def _get(self, path):
        response = self._request("GET", path)
        return self._handle_response(response)

    def _head(self, path):
        response = self._request("HEAD", path)
        if response.status_code == 200:
            return True
        if response.status_code == 404:
            return False
        raise RecallDbException(response.status_code, response.text)

    def _post(self, path, body):
        response = self._request("POST", path, json=body)
        return self._handle_response(response)

    def _put(self, path, body):
        response = self._request("PUT", path, json=body)
        return self._handle_response(response)

    def _delete(self, path):
        response = self._request("DELETE", path)
        if not response.ok and response.status_code != 204:
            raise RecallDbException(response.status_code, response.text)

    def _handle_response(self, response):
        if not response.ok:
            raise RecallDbException(response.status_code, response.text)
        if not response.text:
            return None
        return response.json()
