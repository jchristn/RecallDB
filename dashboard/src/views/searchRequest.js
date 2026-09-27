// Search request building and validation for the Search view, kept free of React so it can be unit tested
// with `node --test` (see dashboard/test). Validation mirrors the server's rules (REST_API.md, Search Validation
// Errors) so problems show before submit; the server remains the authority.

export const TAG_CONDITIONS = [
  'Equals', 'NotEquals', 'GreaterThan', 'LessThan',
  'Contains', 'ContainsNot', 'StartsWith', 'EndsWith',
  'IsNull', 'IsNotNull'
]

export const SEARCH_TYPES = [
  { value: 'CosineSimilarity', label: 'Cosine Similarity' },
  { value: 'CosineDistance', label: 'Cosine Distance' },
  { value: 'EuclideanSimilarity', label: 'Euclidean Similarity' },
  { value: 'EuclideanDistance', label: 'Euclidean Distance' },
  { value: 'InnerProduct', label: 'Inner Product' }
]

export const SORT_ORDERS = [
  { value: 'ScoreDescending', label: 'Score Descending' },
  { value: 'ScoreAscending', label: 'Score Ascending' },
  { value: 'DistanceDescending', label: 'Distance Descending' },
  { value: 'DistanceAscending', label: 'Distance Ascending' },
  { value: 'TextScoreDescending', label: 'Text Score Descending' },
  { value: 'TextScoreAscending', label: 'Text Score Ascending' },
  { value: 'CreatedDescending', label: 'Created Descending' },
  { value: 'CreatedAscending', label: 'Created Ascending' }
]

export const TEXT_SEARCH_TYPES = [
  { value: 'TsRank', label: 'TsRank (Term Frequency)' },
  { value: 'TsRankCd', label: 'TsRankCd (Cover Density)' }
]

export const MATCH_MODES = [
  { value: 'Any', label: 'Any', help: 'Matches documents containing any of the query terms (stemmed, stop words removed).' },
  { value: 'All', label: 'All', help: 'Every query term must appear in the document.' },
  { value: 'Phrase', label: 'Phrase', help: 'Terms must appear adjacent and in the order given.' },
  { value: 'WebSearch', label: 'WebSearch', help: 'Web-style syntax: "quoted phrase", or, and -exclude.' }
]

export const HYBRID_STRATEGIES = [
  { value: 'Rrf', label: 'RRF', help: 'Reciprocal rank fusion of the vector and text result lists. A text match is not required.' },
  { value: 'Linear', label: 'Linear', help: 'Weighted sum of normalized vector and text scores. A text match is not required.' },
  { value: 'Filter', label: 'Filter (legacy)', help: 'Text query is a required filter; vector and raw text scores are blended.' }
]

// '' means no collapse; the others are CollapseFieldEnum values.
export const COLLAPSE_FIELDS = [
  { value: '', label: 'None', help: 'One hit per matching document (chunk).' },
  { value: 'DocumentId', label: 'Document ID', help: 'One hit per DocumentId, its best-scoring chunk. Documents without a DocumentId are their own group.' },
  { value: 'Tag', label: 'Tag', help: 'One hit per value of the named tag, its best-scoring chunk. Documents without the tag are their own group.' }
]

// How LabelFilter.Required combines its labels; All is the server default.
export const LABEL_MATCH_MODES = [
  { value: 'All', label: 'All', help: 'A document must carry every required label.' },
  { value: 'Any', label: 'Any', help: 'A document must carry at least one required label.' }
]

export const MINIMUM_SHOULD_MATCH_OPTIONS = [
  { value: 1, label: '1 (any term)' },
  { value: 2, label: '2 distinct terms' },
  { value: 3, label: '3 distinct terms' }
]

// Server-side validation ranges.
export const TEXT_WEIGHT_RANGE = { min: 0, max: 1 }
export const NORMALIZATION_RANGE = { min: 0, max: 63 }
export const RRF_K_RANGE = { min: 1, max: 100000 }
export const CANDIDATE_POOL_RANGE = { min: 1, max: 10000 }
export const RECENCY_WEIGHT_RANGE = { min: 0, max: 1 }
export const COLLAPSE_POOL_RANGE = { min: 1, max: 10000 }
export const MINIMUM_SHOULD_MATCH_RANGE = { min: 1, max: 3 }
export const EF_SEARCH_RANGE = { min: 1, max: 1000 }
export const TAG_KEY_MAX_LENGTH = 256

export const DEFAULT_TEXT_WEIGHT = 0.5
export const DEFAULT_NORMALIZATION = 32
export const DEFAULT_RECENCY_WEIGHT = 0

export function isBlank(value) {
  return value === null || value === undefined || String(value).trim() === ''
}

// Returns the parsed number, or the fallback when the input is blank or not a finite number.
// Unlike `parseFloat(x) || fallback`, this keeps an explicit 0.
export function numberOrDefault(value, fallback) {
  if (isBlank(value)) return fallback
  const n = Number(value)
  return Number.isFinite(n) ? n : fallback
}

// Returns an error message when a non-blank input is outside [min, max] (or not an integer when required).
export function rangeError(value, { min, max }, integer) {
  if (isBlank(value)) return null
  const n = Number(value)
  if (!Number.isFinite(n)) return 'Must be a number.'
  if (integer && !Number.isInteger(n)) return 'Must be a whole number.'
  if (n < min || n > max) return `Must be between ${min} and ${max}.`
  return null
}

export function parseCommaSep(str) {
  if (!str || !str.trim()) return []
  return str.split(',').map(s => s.trim()).filter(Boolean)
}

// Parsed embedding values as strings, ignoring blanks and non-numbers.
export function parseEmbeddings(embeddings) {
  if (!embeddings || !embeddings.trim()) return []
  return embeddings.split(',').map(v => v.trim()).filter(v => v && !isNaN(parseFloat(v)))
}

// Which parts of the form are in play: the same rules decide what is shown, validated, and sent.
export function searchShape(form) {
  const hasVector = parseEmbeddings(form.embeddings).length > 0
  const hasFullText = !isBlank(form.fullTextQuery)
  const isHybrid = hasVector && hasFullText
  const strategy = form.hybridStrategy || 'Rrf'
  const matchMode = form.fullTextMatchMode || 'Any'
  const collapseField = form.collapseField || ''
  return {
    hasVector,
    hasFullText,
    isHybrid,
    strategy,
    showRrfK: isHybrid && strategy === 'Rrf',
    showRecencyWeight: isHybrid && strategy === 'Rrf',
    showCandidatePool: isHybrid && strategy !== 'Filter',
    showMinimumShouldMatch: hasFullText && matchMode === 'Any',
    collapseField,
    isCollapsed: collapseField !== '',
    showCollapseTagKey: collapseField === 'Tag',
    // Hybrid searches size their pool with Hybrid.CandidatePool, so the collapse pool is for single-leg searches.
    showCollapsePool: collapseField !== '' && !isHybrid
  }
}

export const FIELD_LABELS = {
  vectorEfSearch: 'EF Search',
  labelRequiredMode: 'Required Labels Match',
  fullTextWeight: 'Text Weight',
  fullTextNormalization: 'Normalization',
  fullTextMinimumShouldMatch: 'Min Terms to Match',
  hybridRrfK: 'RRF k',
  hybridCandidatePool: 'Candidate Pool',
  hybridRecencyWeight: 'Recency Weight',
  collapseField: 'Collapse',
  collapseTagKey: 'Tag Key',
  collapseCandidatePool: 'Collapse Candidate Pool'
}

// Per-field error messages (null when valid). Hidden fields are not validated, because they are not sent.
export function validateFields(form) {
  const shape = searchShape(form)
  const tagKey = form.collapseTagKey == null ? '' : String(form.collapseTagKey)
  let collapseTagKey = null
  if (shape.showCollapseTagKey) {
    if (isBlank(tagKey)) collapseTagKey = 'Required when collapsing by tag.'
    else if (tagKey.trim().length > TAG_KEY_MAX_LENGTH) collapseTagKey = `Must be at most ${TAG_KEY_MAX_LENGTH} characters.`
  }
  let collapseField = null
  if (shape.isCollapsed) {
    if (!COLLAPSE_FIELDS.some(f => f.value === shape.collapseField)) collapseField = 'Must be None, Document ID, or Tag.'
    else if (shape.isHybrid && shape.strategy === 'Filter') collapseField = 'Collapse is not supported with the Filter strategy.'
  }
  const labelMode = form.labelRequiredMode || 'All'
  return {
    vectorEfSearch: shape.hasVector ? rangeError(form.vectorEfSearch, EF_SEARCH_RANGE, true) : null,
    labelRequiredMode: LABEL_MATCH_MODES.some(m => m.value === labelMode) ? null : 'Must be All or Any.',
    fullTextWeight: rangeError(form.fullTextWeight, TEXT_WEIGHT_RANGE, false),
    fullTextNormalization: rangeError(form.fullTextNormalization, NORMALIZATION_RANGE, true),
    fullTextMinimumShouldMatch: shape.showMinimumShouldMatch ? rangeError(form.fullTextMinimumShouldMatch, MINIMUM_SHOULD_MATCH_RANGE, true) : null,
    hybridRrfK: shape.showRrfK ? rangeError(form.hybridRrfK, RRF_K_RANGE, true) : null,
    hybridCandidatePool: shape.showCandidatePool ? rangeError(form.hybridCandidatePool, CANDIDATE_POOL_RANGE, true) : null,
    hybridRecencyWeight: shape.showRecencyWeight ? rangeError(form.hybridRecencyWeight, RECENCY_WEIGHT_RANGE, false) : null,
    collapseField,
    collapseTagKey,
    collapseCandidatePool: shape.showCollapsePool ? rangeError(form.collapseCandidatePool, COLLAPSE_POOL_RANGE, true) : null
  }
}

// Validates the whole form. Returns { fieldErrors, error }, where error is the message to show on submit, or null.
export function validateSearch(form, dimensionality) {
  const fieldErrors = validateFields(form)
  const shape = searchShape(form)
  const embeddingCount = parseEmbeddings(form.embeddings).length
  let error = null
  if (!shape.hasVector && !shape.hasFullText) {
    error = 'Embeddings or a full-text query are required. Provide embeddings for vector search, a full-text query for text search, or both for hybrid search.'
  } else if (dimensionality && embeddingCount > 0 && embeddingCount !== dimensionality) {
    error = `Embeddings count (${embeddingCount}) does not match the collection dimensionality (${dimensionality}).`
  } else {
    const invalid = Object.keys(fieldErrors).filter(k => fieldErrors[k])
    if (invalid.length > 0) {
      error = 'Fix the highlighted fields before searching. ' + invalid.map(k => `${FIELD_LABELS[k]}: ${fieldErrors[k]}`).join(' ')
    }
  }
  return { fieldErrors, error }
}

export function buildQuery(form) {
  const {
    embeddings, searchType, minScore, maxScore, minDistance, maxDistance, vectorEfSearch,
    requiredLabels = [], excludedLabels = [], labelRequiredMode, requiredTags = [], excludedTags = [], requiredTerms = '', excludedTerms = '',
    sortOrder, maxResults, includeNeighbors, includeEmbeddings, createdBefore, createdAfter, documentIds = '',
    fullTextQuery, fullTextSearchType, fullTextMatchMode, fullTextLanguage, fullTextNormalization, fullTextMinScore, fullTextWeight,
    fullTextMinimumShouldMatch,
    hybridStrategy, hybridRrfK, hybridCandidatePool, hybridRecencyWeight,
    collapseTagKey, collapseCandidatePool
  } = form
  const shape = searchShape(form)

  const query = {
    SortOrder: sortOrder || 'ScoreDescending',
    MaxResults: parseInt(maxResults) || 10
  }

  if (createdBefore) query.CreatedBefore = new Date(createdBefore).toISOString()
  if (createdAfter) query.CreatedAfter = new Date(createdAfter).toISOString()

  const docIds = parseCommaSep(documentIds)
  if (docIds.length > 0) query.DocumentIds = docIds

  if (embeddings && embeddings.trim()) {
    const embList = embeddings.split(',').map(v => parseFloat(v.trim())).filter(v => !isNaN(v))
    if (embList.length > 0) {
      query.Vector = { SearchType: searchType || 'CosineSimilarity', Embeddings: embList }
      if (minScore) query.Vector.MinimumScore = parseFloat(minScore)
      if (maxScore) query.Vector.MaximumScore = parseFloat(maxScore)
      if (minDistance) query.Vector.MinimumDistance = parseFloat(minDistance)
      if (maxDistance) query.Vector.MaximumDistance = parseFloat(maxDistance)
      if (!isBlank(vectorEfSearch)) query.Vector.EfSearch = Number(vectorEfSearch)
    }
  }

  const reqLabels = Array.isArray(requiredLabels) ? requiredLabels.filter(Boolean) : parseCommaSep(requiredLabels)
  const excLabels = Array.isArray(excludedLabels) ? excludedLabels.filter(Boolean) : parseCommaSep(excludedLabels)
  if (reqLabels.length > 0 || excLabels.length > 0) {
    query.LabelFilter = {}
    if (reqLabels.length > 0) {
      query.LabelFilter.Required = reqLabels
      query.LabelFilter.RequiredMode = labelRequiredMode || 'All'
    }
    if (excLabels.length > 0) query.LabelFilter.Excluded = excLabels
  }

  const validReqTags = requiredTags.filter(t => t.Key.trim())
  const validExcTags = excludedTags.filter(t => t.Key.trim())
  if (validReqTags.length > 0 || validExcTags.length > 0) {
    query.TagFilter = {}
    if (validReqTags.length > 0) query.TagFilter.Required = validReqTags
    if (validExcTags.length > 0) query.TagFilter.Excluded = validExcTags
  }

  const reqTerms = parseCommaSep(requiredTerms)
  const excTerms = parseCommaSep(excludedTerms)
  if (reqTerms.length > 0 || excTerms.length > 0) {
    query.Terms = {}
    if (reqTerms.length > 0) query.Terms.Required = reqTerms
    if (excTerms.length > 0) query.Terms.Excluded = excTerms
  }

  const parsedNeighbors = parseInt(includeNeighbors)
  if (parsedNeighbors > 0) query.IncludeNeighbors = parsedNeighbors
  if (includeEmbeddings) query.IncludeEmbeddings = true

  if (fullTextQuery && fullTextQuery.trim()) {
    query.FullText = {
      Query: fullTextQuery.trim(),
      SearchType: fullTextSearchType || 'TsRank',
      MatchMode: fullTextMatchMode || 'Any',
      Language: fullTextLanguage || 'english',
      Normalization: numberOrDefault(fullTextNormalization, DEFAULT_NORMALIZATION),
      TextWeight: numberOrDefault(fullTextWeight, DEFAULT_TEXT_WEIGHT)
    }
    const ftMinScore = parseFloat(fullTextMinScore)
    if (!isNaN(ftMinScore)) query.FullText.MinimumScore = ftMinScore
    // 1 is the server default (today's Any), so it is only sent when it narrows the match.
    const msm = numberOrDefault(fullTextMinimumShouldMatch, 1)
    if (shape.showMinimumShouldMatch && msm > 1) query.FullText.MinimumShouldMatch = msm
  }

  // Hybrid options only apply when both a vector and a text query are present.
  if (query.Vector && query.FullText) {
    query.Hybrid = { Strategy: shape.strategy }
    if (shape.showRrfK && !isBlank(hybridRrfK)) query.Hybrid.RrfK = Number(hybridRrfK)
    if (shape.showCandidatePool && !isBlank(hybridCandidatePool)) query.Hybrid.CandidatePool = Number(hybridCandidatePool)
    if (shape.showRecencyWeight && !isBlank(hybridRecencyWeight)) query.Hybrid.RecencyWeight = Number(hybridRecencyWeight)
  }

  if (shape.isCollapsed) {
    query.Collapse = { Field: shape.collapseField }
    if (shape.showCollapseTagKey) query.Collapse.TagKey = String(collapseTagKey || '').trim()
    if (shape.showCollapsePool && !isBlank(collapseCandidatePool)) query.Collapse.CandidatePool = Number(collapseCandidatePool)
  }

  return query
}

// The LabelFilter part of an enumeration or delete-by-filter body, or null when no label is given.
export function buildLabelFilter(requiredLabels, excludedLabels, requiredMode) {
  const req = (requiredLabels || []).filter(l => l && l.trim())
  const exc = (excludedLabels || []).filter(l => l && l.trim())
  if (req.length === 0 && exc.length === 0) return null
  const filter = {}
  if (req.length > 0) {
    filter.Required = req
    filter.RequiredMode = requiredMode || 'All'
  }
  if (exc.length > 0) filter.Excluded = exc
  return filter
}

// Result-table columns that only appear when some hit carries the field.
export function resultColumnFlags(documents, lastSearchWasHybrid) {
  const docs = documents || []
  return {
    showVectorScore: !!lastSearchWasHybrid || docs.some(d => d.VectorScore != null),
    showRanks: !!lastSearchWasHybrid || docs.some(d => d.VectorRank != null || d.TextRank != null),
    showGroup: docs.some(d => d.GroupKey != null || d.GroupHits != null),
    showRecencyRank: docs.some(d => d.RecencyRank != null)
  }
}

// Extra request bodies for the API Explorer's search operation, beyond the one the server's OpenAPI document
// carries. The first mirrors the single-call hybrid request in REST_API.md (collapse by tag with recency).
export const SEARCH_EXAMPLES = [
  {
    label: 'Hybrid: collapse by tag with recency',
    body: {
      Vector: { SearchType: 'CosineSimilarity', Embeddings: [0.1, 0.2, 0.3] },
      FullText: { Query: 'how do I rotate the signing key', MatchMode: 'Any', TextWeight: 0.5 },
      Hybrid: { Strategy: 'Rrf', RrfK: 60, CandidatePool: 40, RecencyWeight: 0.1 },
      Collapse: { Field: 'Tag', TagKey: 'parentKey' },
      IncludeEmbeddings: false,
      MaxResults: 10
    }
  },
  {
    label: 'Vector: collapse by document ID',
    body: {
      Vector: { SearchType: 'CosineSimilarity', Embeddings: [0.1, 0.2, 0.3] },
      Collapse: { Field: 'DocumentId', CandidatePool: 100 },
      MaxResults: 10
    }
  },
  {
    label: 'Full-text: minimum should match',
    body: {
      FullText: { Query: 'signing key rotation', MatchMode: 'Any', MinimumShouldMatch: 2 },
      MaxResults: 10
    }
  }
]

const SEARCH_PATH = /\/collections\/\{[^}]+\}\/search$/

// The extra examples for an operation, or an empty list when it is not the search operation.
export function examplesForOperation(method, path) {
  if (String(method || '').toUpperCase() !== 'POST' || !SEARCH_PATH.test(String(path || ''))) return []
  return SEARCH_EXAMPLES
}
