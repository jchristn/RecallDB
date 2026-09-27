import React, { useState, useEffect } from 'react'
import { useAuth } from '../context/AuthContext.jsx'
import api from '../api/api.js'
import DataTable from '../components/DataTable.jsx'
import CopyId from '../components/CopyId.jsx'
import ActionMenu from '../components/ActionMenu.jsx'
import JsonModal from '../components/JsonModal.jsx'
import ViewDocumentModal from '../components/ViewDocumentModal.jsx'
import ErrorModal from '../components/ErrorModal.jsx'
import {
  TAG_CONDITIONS, SEARCH_TYPES, SORT_ORDERS, TEXT_SEARCH_TYPES, MATCH_MODES, HYBRID_STRATEGIES, COLLAPSE_FIELDS,
  MINIMUM_SHOULD_MATCH_OPTIONS, TEXT_WEIGHT_RANGE, NORMALIZATION_RANGE, RRF_K_RANGE, CANDIDATE_POOL_RANGE,
  RECENCY_WEIGHT_RANGE, COLLAPSE_POOL_RANGE, TAG_KEY_MAX_LENGTH, DEFAULT_TEXT_WEIGHT, DEFAULT_NORMALIZATION,
  DEFAULT_RECENCY_WEIGHT, parseCommaSep, searchShape, validateSearch, buildQuery, resultColumnFlags
} from './searchRequest.js'

function CollapsibleSection({ title, children, defaultOpen = false }) {
  const [open, setOpen] = useState(defaultOpen)
  return (
    <div className="collapsible-section">
      <div className="collapsible-header" onClick={() => setOpen(!open)}>
        <span>{title}</span>
        <span>{open ? '\u25B2' : '\u25BC'}</span>
      </div>
      {open && <div className="collapsible-body">{children}</div>}
    </div>
  )
}

const HINT_STYLE = { display: 'block', fontSize: 12, color: 'var(--text-secondary)', marginTop: 4 }
const INVALID_STYLE = { borderColor: 'var(--danger)' }

function FieldError({ message }) {
  if (!message) return null
  return <span role="alert" style={{ display: 'block', fontSize: 12, color: 'var(--danger)', marginTop: 4 }}>{message}</span>
}

function TenantCollectionPicker({ selectedTenant, setSelectedTenant, selectedCollection, setSelectedCollection }) {
  const { tenant, isAdmin } = useAuth()
  const [tenants, setTenants] = useState([])
  const [collections, setCollections] = useState([])
  const [loadingTenants, setLoadingTenants] = useState(true)
  const [loadingCollections, setLoadingCollections] = useState(false)
  const [tenantFilter, setTenantFilter] = useState('')
  const [collectionFilter, setCollectionFilter] = useState('')

  useEffect(() => {
    (async () => {
      try {
        setLoadingTenants(true)
        if (isAdmin) {
          const result = await api.listTenants()
          const list = result?.Objects || []
          setTenants(list)
          if (list.length === 1) setSelectedTenant(list[0].Id)
          else if (tenant?.Id) setSelectedTenant(tenant.Id)
        } else {
          setTenants(tenant ? [tenant] : [])
          if (tenant?.Id) setSelectedTenant(tenant.Id)
        }
      } catch {}
      finally { setLoadingTenants(false) }
    })()
  }, [])

  useEffect(() => {
    if (!selectedTenant) {
      setCollections([])
      setSelectedCollection('')
      return
    }
    (async () => {
      try {
        setLoadingCollections(true)
        setSelectedCollection('')
        const result = await api.listCollections(selectedTenant)
        const list = result?.Objects || []
        setCollections(list)
        if (list.length === 1) setSelectedCollection(list[0].Id)
      } catch {}
      finally { setLoadingCollections(false) }
    })()
  }, [selectedTenant])

  const filteredTenants = tenants.filter(t =>
    !tenantFilter || (t.Name || t.Id || '').toLowerCase().includes(tenantFilter.toLowerCase())
  )

  const filteredCollections = collections.filter(c =>
    !collectionFilter || (c.Name || c.Id || '').toLowerCase().includes(collectionFilter.toLowerCase())
  )

  return (
    <div className="card" style={{ marginBottom: 20 }}>
      <div className="selector-row">
        <div className="selector-group">
          <label>Tenant</label>
          <div className="searchable-select">
            <input
              type="text"
              placeholder={loadingTenants ? 'Loading...' : 'Filter tenants...'}
              value={tenantFilter}
              onChange={(e) => setTenantFilter(e.target.value)}
              className="selector-filter"
              disabled={tenants.length <= 1}
            />
            <select
              value={selectedTenant}
              onChange={(e) => setSelectedTenant(e.target.value)}
              disabled={tenants.length === 0}
            >
              <option value="">Select a tenant...</option>
              {filteredTenants.map(t => (
                <option key={t.Id} value={t.Id}>{t.Name || t.Id}</option>
              ))}
            </select>
          </div>
        </div>
        <div className="selector-group">
          <label>Collection</label>
          <div className="searchable-select">
            <input
              type="text"
              placeholder={loadingCollections ? 'Loading...' : 'Filter collections...'}
              value={collectionFilter}
              onChange={(e) => setCollectionFilter(e.target.value)}
              className="selector-filter"
              disabled={collections.length <= 1}
            />
            <select
              value={selectedCollection}
              onChange={(e) => setSelectedCollection(e.target.value)}
              disabled={!selectedTenant || collections.length === 0}
            >
              <option value="">Select a collection...</option>
              {filteredCollections.map(c => (
                <option key={c.Id} value={c.Id}>{c.Name || c.Id}</option>
              ))}
            </select>
          </div>
        </div>
      </div>
    </div>
  )
}

function SearchTab({ tenantId, collectionId }) {
  const [embeddings, setEmbeddings] = useState('')
  const [searchType, setSearchType] = useState('CosineSimilarity')
  const [minScore, setMinScore] = useState('')
  const [maxScore, setMaxScore] = useState('')
  const [minDistance, setMinDistance] = useState('')
  const [maxDistance, setMaxDistance] = useState('')
  const [requiredLabels, setRequiredLabels] = useState([])
  const [excludedLabels, setExcludedLabels] = useState([])
  const [requiredTags, setRequiredTags] = useState([])
  const [excludedTags, setExcludedTags] = useState([])

  const addLabel = (setter) => setter(prev => [...prev, ''])
  const updateLabel = (setter, index, value) => setter(prev => prev.map((v, i) => i === index ? value : v))
  const removeLabel = (setter, index) => setter(prev => prev.filter((_, i) => i !== index))
  const [requiredTerms, setRequiredTerms] = useState('')
  const [excludedTerms, setExcludedTerms] = useState('')
  const [fullTextQuery, setFullTextQuery] = useState('')
  const [fullTextSearchType, setFullTextSearchType] = useState('TsRank')
  const [fullTextMatchMode, setFullTextMatchMode] = useState('Any')
  const [fullTextLanguage, setFullTextLanguage] = useState('english')
  const [fullTextNormalization, setFullTextNormalization] = useState(DEFAULT_NORMALIZATION)
  const [fullTextMinScore, setFullTextMinScore] = useState('')
  const [fullTextWeight, setFullTextWeight] = useState(DEFAULT_TEXT_WEIGHT)
  const [hybridStrategy, setHybridStrategy] = useState('Rrf')
  const [hybridRrfK, setHybridRrfK] = useState(60)
  const [hybridCandidatePool, setHybridCandidatePool] = useState('')
  const [hybridRecencyWeight, setHybridRecencyWeight] = useState(DEFAULT_RECENCY_WEIGHT)
  const [fullTextMinimumShouldMatch, setFullTextMinimumShouldMatch] = useState(1)
  const [collapseField, setCollapseField] = useState('')
  const [collapseTagKey, setCollapseTagKey] = useState('')
  const [collapseCandidatePool, setCollapseCandidatePool] = useState('')
  const [includeEmbeddings, setIncludeEmbeddings] = useState(false)
  const [lastSearchWasHybrid, setLastSearchWasHybrid] = useState(false)
  const [lastSearchWasCollapsed, setLastSearchWasCollapsed] = useState(false)
  const [sortOrder, setSortOrder] = useState('ScoreDescending')
  const [maxResults, setMaxResults] = useState(10)
  const [includeNeighbors, setIncludeNeighbors] = useState('')
  const [createdBefore, setCreatedBefore] = useState('')
  const [createdAfter, setCreatedAfter] = useState('')
  const [documentIds, setDocumentIds] = useState('')
  const [results, setResults] = useState(null)
  const [error, setError] = useState(null)
  const [loading, setLoading] = useState(false)
  const [jsonModal, setJsonModal] = useState(null)
  const [viewModal, setViewModal] = useState(null)
  const [dimensionality, setDimensionality] = useState(null)

  useEffect(() => {
    if (tenantId && collectionId) {
      api.getCollection(tenantId, collectionId)
        .then(col => setDimensionality(col?.Dimensionality || null))
        .catch(() => setDimensionality(null))
    }
  }, [tenantId, collectionId])

  const addTagRow = (setter) => setter(prev => [...prev, { Key: '', Condition: 'Equals', Value: '' }])
  const updateTagRow = (setter, index, field, value) => setter(prev => prev.map((row, i) => i === index ? { ...row, [field]: value } : row))
  const removeTagRow = (setter, index) => setter(prev => prev.filter((_, i) => i !== index))

  const form = {
    embeddings, searchType, minScore, maxScore, minDistance, maxDistance,
    requiredLabels, excludedLabels, requiredTags, excludedTags, requiredTerms, excludedTerms,
    sortOrder, maxResults, includeNeighbors, includeEmbeddings, createdBefore, createdAfter, documentIds,
    fullTextQuery, fullTextSearchType, fullTextMatchMode, fullTextLanguage, fullTextNormalization, fullTextMinScore, fullTextWeight,
    fullTextMinimumShouldMatch,
    hybridStrategy, hybridRrfK, hybridCandidatePool, hybridRecencyWeight,
    collapseField, collapseTagKey, collapseCandidatePool
  }
  const shape = searchShape(form)
  const { isHybrid, showRrfK, showCandidatePool, showRecencyWeight, showCollapseTagKey, showCollapsePool } = shape
  const { fieldErrors } = validateSearch(form, dimensionality)

  const selectedMatchMode = MATCH_MODES.find(m => m.value === fullTextMatchMode) || MATCH_MODES[0]
  const selectedStrategy = HYBRID_STRATEGIES.find(s => s.value === hybridStrategy) || HYBRID_STRATEGIES[0]
  const selectedCollapse = COLLAPSE_FIELDS.find(f => f.value === collapseField) || COLLAPSE_FIELDS[0]

  const handleSearch = async (e) => {
    e.preventDefault()
    setError(null)
    const validation = validateSearch(form, dimensionality)
    if (validation.error) {
      setError(new Error(validation.error))
      return
    }
    setLoading(true)
    try {
      const query = buildQuery(form)
      const result = await api.search(tenantId, collectionId, query)
      setLastSearchWasHybrid(!!query.Hybrid)
      setLastSearchWasCollapsed(!!query.Collapse)
      setResults(result)
    } catch (err) {
      setError(err)
    } finally {
      setLoading(false)
    }
  }

  const renderScore = (value) => value != null ? <span className="score-badge">{value.toFixed(4)}</span> : '-'
  const renderRank = (value) => value != null ? value : '-'
  const { showVectorScore, showRanks, showGroup, showRecencyRank } = resultColumnFlags(results?.Documents, lastSearchWasHybrid)

  const resultColumns = [
    {
      key: 'DocumentKey', label: 'Key',
      render: (d) => <CopyId value={d.DocumentKey} />,
      filterValue: (d) => d.DocumentKey
    },
    { key: 'DocumentId', label: 'Doc ID', render: (d) => d.DocumentId ? <CopyId value={d.DocumentId} truncate={16} /> : '-' },
    { key: 'Position', label: 'Pos', width: '60px' },
    { key: 'ContentType', label: 'Type', width: '70px' },
    {
      key: 'Score', label: lastSearchWasHybrid ? 'Fused Score' : 'Score', width: '100px',
      render: (d) => renderScore(d.Score),
      sortValue: (d) => d.Score
    },
    ...(showVectorScore ? [{
      key: 'VectorScore', label: 'Vector Score', width: '110px',
      render: (d) => renderScore(d.VectorScore),
      sortValue: (d) => d.VectorScore
    }] : []),
    {
      key: 'TextScore', label: 'Text Score', width: '100px',
      render: (d) => renderScore(d.TextScore),
      sortValue: (d) => d.TextScore
    },
    ...(showRanks ? [
      {
        key: 'VectorRank', label: 'Vector Rank', width: '100px',
        render: (d) => renderRank(d.VectorRank),
        sortValue: (d) => d.VectorRank
      },
      {
        key: 'TextRank', label: 'Text Rank', width: '90px',
        render: (d) => renderRank(d.TextRank),
        sortValue: (d) => d.TextRank
      }
    ] : []),
    ...(showRecencyRank ? [{
      key: 'RecencyRank', label: 'Recency Rank', width: '110px',
      render: (d) => renderRank(d.RecencyRank),
      sortValue: (d) => d.RecencyRank
    }] : []),
    ...(showGroup ? [
      {
        key: 'GroupKey', label: 'Group',
        render: (d) => d.GroupKey ? <CopyId value={d.GroupKey} truncate={16} /> : '-',
        filterValue: (d) => d.GroupKey || ''
      },
      {
        key: 'GroupHits', label: 'Group Hits', width: '90px',
        render: (d) => renderRank(d.GroupHits),
        sortValue: (d) => d.GroupHits
      }
    ] : []),
    {
      key: 'Content', label: 'Content',
      render: (d) => {
        const text = d.Content || '(no content)'
        return text.length > 120 ? text.substring(0, 120) + '...' : text
      },
      filterValue: (d) => d.Content || ''
    },
    {
      key: 'actions', label: 'Actions', isAction: true, width: '50px',
      render: (d) => <ActionMenu actions={[{ label: 'View JSON', onClick: () => setJsonModal(d) }]} />
    }
  ]

  return (
    <div>
      <ErrorModal error={error} onClose={() => setError(null)} />
      <div className="card">
        <form onSubmit={handleSearch}>
          {/* Vector Search */}
          <CollapsibleSection title="Vector Search" defaultOpen={true}>
            <div className="form-group">
              <label>Embeddings (comma-separated floats){dimensionality ? `, dimensionality: ${dimensionality}` : ''}</label>
              <textarea value={embeddings} onChange={(e) => setEmbeddings(e.target.value)} rows={3} placeholder="0.1, 0.2, 0.3, ..." />
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1fr', gap: 16, marginBottom: 16 }}>
              <div className="form-group">
                <label>Search Type</label>
                <select value={searchType} onChange={(e) => setSearchType(e.target.value)}>
                  {SEARCH_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
                </select>
              </div>
              <div className="form-group">
                <label>Min Score</label>
                <input type="number" step="any" value={minScore} onChange={(e) => setMinScore(e.target.value)} placeholder="0.0" />
              </div>
              <div className="form-group">
                <label>Max Score</label>
                <input type="number" step="any" value={maxScore} onChange={(e) => setMaxScore(e.target.value)} placeholder="1.0" />
              </div>
              <div className="form-group">
                <label>Min Distance</label>
                <input type="number" step="any" value={minDistance} onChange={(e) => setMinDistance(e.target.value)} placeholder="0.0" />
              </div>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: 16 }}>
              <div className="form-group">
                <label>Max Distance</label>
                <input type="number" step="any" value={maxDistance} onChange={(e) => setMaxDistance(e.target.value)} placeholder="1.0" style={{ maxWidth: 200 }} />
              </div>
            </div>
          </CollapsibleSection>

          {/* Full-Text Search */}
          <CollapsibleSection title="Full-Text Search">
            <div className="form-group">
              <label>Query</label>
              <textarea value={fullTextQuery} onChange={(e) => setFullTextQuery(e.target.value)} rows={2} placeholder="Enter search terms (e.g. OAuth2 PKCE configuration)" />
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1fr', gap: 16, marginBottom: 16 }}>
              <div className="form-group">
                <label htmlFor="ft-rank-function">Ranking Function</label>
                <select id="ft-rank-function" value={fullTextSearchType} onChange={(e) => setFullTextSearchType(e.target.value)}>
                  {TEXT_SEARCH_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
                </select>
              </div>
              <div className="form-group">
                <label htmlFor="ft-match-mode">Match Mode</label>
                <select id="ft-match-mode" value={fullTextMatchMode} onChange={(e) => setFullTextMatchMode(e.target.value)} aria-describedby="ft-match-mode-help">
                  {MATCH_MODES.map(m => <option key={m.value} value={m.value} title={m.help}>{m.label}</option>)}
                </select>
                <span id="ft-match-mode-help" style={HINT_STYLE}>{selectedMatchMode.help}</span>
              </div>
              <div className="form-group">
                <label htmlFor="ft-language">Language</label>
                <input id="ft-language" type="text" value={fullTextLanguage} onChange={(e) => setFullTextLanguage(e.target.value)} placeholder="english" />
              </div>
              <div className="form-group">
                <label htmlFor="ft-normalization">Normalization (0-63)</label>
                <input
                  id="ft-normalization" type="number" step="1"
                  min={NORMALIZATION_RANGE.min} max={NORMALIZATION_RANGE.max}
                  value={fullTextNormalization}
                  onChange={(e) => setFullTextNormalization(e.target.value)}
                  placeholder={String(DEFAULT_NORMALIZATION)}
                  aria-invalid={!!fieldErrors.fullTextNormalization}
                  style={fieldErrors.fullTextNormalization ? INVALID_STYLE : undefined}
                />
                <FieldError message={fieldErrors.fullTextNormalization} />
              </div>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 16 }}>
              <div className="form-group">
                <label htmlFor="ft-min-score">Min Text Score</label>
                <input id="ft-min-score" type="number" step="any" value={fullTextMinScore} onChange={(e) => setFullTextMinScore(e.target.value)} placeholder="0.0" />
              </div>
              <div className="form-group">
                <label htmlFor="ft-text-weight">Text Weight (hybrid blend, 0.0-1.0)</label>
                <input
                  id="ft-text-weight" type="number" step="0.1"
                  min={TEXT_WEIGHT_RANGE.min} max={TEXT_WEIGHT_RANGE.max}
                  value={fullTextWeight}
                  onChange={(e) => setFullTextWeight(e.target.value)}
                  placeholder={String(DEFAULT_TEXT_WEIGHT)}
                  aria-invalid={!!fieldErrors.fullTextWeight}
                  style={fieldErrors.fullTextWeight ? INVALID_STYLE : undefined}
                />
                <span style={HINT_STYLE}>Share given to the text leg in hybrid search; the vector leg gets the rest. 0 ranks by vector only.</span>
                <FieldError message={fieldErrors.fullTextWeight} />
              </div>
              {fullTextMatchMode === 'Any' && (
                <div className="form-group">
                  <label htmlFor="ft-min-should-match">Min Terms to Match</label>
                  <select
                    id="ft-min-should-match"
                    value={fullTextMinimumShouldMatch}
                    onChange={(e) => setFullTextMinimumShouldMatch(Number(e.target.value))}
                    aria-invalid={!!fieldErrors.fullTextMinimumShouldMatch}
                    style={fieldErrors.fullTextMinimumShouldMatch ? INVALID_STYLE : undefined}
                  >
                    {MINIMUM_SHOULD_MATCH_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
                  </select>
                  <span style={HINT_STYLE}>Match mode Any only. 2 or 3 requires that many distinct query terms. Fewer rows are ranked, but long queries can get slower. A query with fewer terms requires all of them.</span>
                  <FieldError message={fieldErrors.fullTextMinimumShouldMatch} />
                </div>
              )}
            </div>
          </CollapsibleSection>

          {/* Hybrid ranking: only meaningful when both a vector and a text query are supplied */}
          {isHybrid && (
            <CollapsibleSection title="Hybrid Ranking" defaultOpen={true}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1fr', gap: 16 }}>
                <div className="form-group">
                  <label htmlFor="hy-strategy">Hybrid Strategy</label>
                  <select id="hy-strategy" value={hybridStrategy} onChange={(e) => setHybridStrategy(e.target.value)} aria-describedby="hy-strategy-help">
                    {HYBRID_STRATEGIES.map(s => <option key={s.value} value={s.value} title={s.help}>{s.label}</option>)}
                  </select>
                  <span id="hy-strategy-help" style={HINT_STYLE}>{selectedStrategy.help}</span>
                </div>
                {showRrfK && (
                  <div className="form-group">
                    <label htmlFor="hy-rrf-k">RRF k</label>
                    <input
                      id="hy-rrf-k" type="number" step="1"
                      min={RRF_K_RANGE.min} max={RRF_K_RANGE.max}
                      value={hybridRrfK}
                      onChange={(e) => setHybridRrfK(e.target.value)}
                      placeholder="60"
                      aria-invalid={!!fieldErrors.hybridRrfK}
                      style={fieldErrors.hybridRrfK ? INVALID_STYLE : undefined}
                    />
                    <span style={HINT_STYLE}>Rank smoothing constant, 1-100000 (default 60). Larger values flatten rank differences.</span>
                    <FieldError message={fieldErrors.hybridRrfK} />
                  </div>
                )}
                {showCandidatePool && (
                  <div className="form-group">
                    <label htmlFor="hy-candidate-pool">Candidate Pool</label>
                    <input
                      id="hy-candidate-pool" type="number" step="1"
                      min={CANDIDATE_POOL_RANGE.min} max={CANDIDATE_POOL_RANGE.max}
                      value={hybridCandidatePool}
                      onChange={(e) => setHybridCandidatePool(e.target.value)}
                      placeholder="Auto"
                      aria-invalid={!!fieldErrors.hybridCandidatePool}
                      style={fieldErrors.hybridCandidatePool ? INVALID_STYLE : undefined}
                    />
                    <span style={HINT_STYLE}>Candidates each leg retrieves before fusion, 1-10000. Leave blank for automatic (max of 4x Max Results and 100, capped at 1000).</span>
                    <FieldError message={fieldErrors.hybridCandidatePool} />
                  </div>
                )}
                {showRecencyWeight && (
                  <div className="form-group">
                    <label htmlFor="hy-recency-weight">Recency Weight (0.0-1.0)</label>
                    <input
                      id="hy-recency-weight" type="number" step="0.05"
                      min={RECENCY_WEIGHT_RANGE.min} max={RECENCY_WEIGHT_RANGE.max}
                      value={hybridRecencyWeight}
                      onChange={(e) => setHybridRecencyWeight(e.target.value)}
                      placeholder={String(DEFAULT_RECENCY_WEIGHT)}
                      aria-invalid={!!fieldErrors.hybridRecencyWeight}
                      style={fieldErrors.hybridRecencyWeight ? INVALID_STYLE : undefined}
                    />
                    <span style={HINT_STYLE}>Weight of a third RRF signal that ranks candidates newest first (per collapse group when collapsing). 0 turns it off.</span>
                    <FieldError message={fieldErrors.hybridRecencyWeight} />
                  </div>
                )}
              </div>
            </CollapsibleSection>
          )}

          {/* Result grouping: one hit per group instead of one per chunk */}
          <CollapsibleSection title="Result Grouping (Collapse)" defaultOpen={collapseField !== ''}>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 16 }}>
              <div className="form-group">
                <label htmlFor="co-field">Collapse By</label>
                <select
                  id="co-field" value={collapseField}
                  onChange={(e) => setCollapseField(e.target.value)}
                  aria-describedby="co-field-help"
                  aria-invalid={!!fieldErrors.collapseField}
                  style={fieldErrors.collapseField ? INVALID_STYLE : undefined}
                >
                  {COLLAPSE_FIELDS.map(f => <option key={f.value} value={f.value} title={f.help}>{f.label}</option>)}
                </select>
                <span id="co-field-help" style={HINT_STYLE}>{selectedCollapse.help}{collapseField ? ' Max Results and paging count groups.' : ''}</span>
                <FieldError message={fieldErrors.collapseField} />
              </div>
              {showCollapseTagKey && (
                <div className="form-group">
                  <label htmlFor="co-tag-key">Tag Key</label>
                  <input
                    id="co-tag-key" type="text" maxLength={TAG_KEY_MAX_LENGTH}
                    value={collapseTagKey}
                    onChange={(e) => setCollapseTagKey(e.target.value)}
                    placeholder="parentKey"
                    aria-invalid={!!fieldErrors.collapseTagKey}
                    style={fieldErrors.collapseTagKey ? INVALID_STYLE : undefined}
                  />
                  <span style={HINT_STYLE}>Name of the tag whose value identifies the group, at most {TAG_KEY_MAX_LENGTH} characters.</span>
                  <FieldError message={fieldErrors.collapseTagKey} />
                </div>
              )}
              {showCollapsePool && (
                <div className="form-group">
                  <label htmlFor="co-candidate-pool">Collapse Candidate Pool</label>
                  <input
                    id="co-candidate-pool" type="number" step="1"
                    min={COLLAPSE_POOL_RANGE.min} max={COLLAPSE_POOL_RANGE.max}
                    value={collapseCandidatePool}
                    onChange={(e) => setCollapseCandidatePool(e.target.value)}
                    placeholder="Auto"
                    aria-invalid={!!fieldErrors.collapseCandidatePool}
                    style={fieldErrors.collapseCandidatePool ? INVALID_STYLE : undefined}
                  />
                  <span style={HINT_STYLE}>Candidates retrieved before grouping, 1-10000. Leave blank for automatic (max of 4x Max Results and 100, capped at 1000). Hybrid searches use the hybrid Candidate Pool.</span>
                  <FieldError message={fieldErrors.collapseCandidatePool} />
                </div>
              )}
            </div>
          </CollapsibleSection>

          {/* Filters */}
          <CollapsibleSection title="Filters">
            {/* Labels */}
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16, marginBottom: 16 }}>
              <div>
                <label style={{ display: 'block', fontSize: 13, fontWeight: 600, marginBottom: 8 }}>Required Labels</label>
                {requiredLabels.map((label, i) => (
                  <div key={i} style={{ display: 'flex', gap: 8, marginBottom: 6, alignItems: 'center' }}>
                    <div className="form-group" style={{ flex: 1, marginBottom: 0 }}>
                      <input type="text" value={label} onChange={(e) => updateLabel(setRequiredLabels, i, e.target.value)} placeholder="Label" />
                    </div>
                    <button type="button" className="btn btn-sm btn-danger" onClick={() => removeLabel(setRequiredLabels, i)}>X</button>
                  </div>
                ))}
                <button type="button" className="add-row-btn" onClick={() => addLabel(setRequiredLabels)}>+ Add Label</button>
              </div>
              <div>
                <label style={{ display: 'block', fontSize: 13, fontWeight: 600, marginBottom: 8 }}>Excluded Labels</label>
                {excludedLabels.map((label, i) => (
                  <div key={i} style={{ display: 'flex', gap: 8, marginBottom: 6, alignItems: 'center' }}>
                    <div className="form-group" style={{ flex: 1, marginBottom: 0 }}>
                      <input type="text" value={label} onChange={(e) => updateLabel(setExcludedLabels, i, e.target.value)} placeholder="Label" />
                    </div>
                    <button type="button" className="btn btn-sm btn-danger" onClick={() => removeLabel(setExcludedLabels, i)}>X</button>
                  </div>
                ))}
                <button type="button" className="add-row-btn" onClick={() => addLabel(setExcludedLabels)}>+ Add Label</button>
              </div>
            </div>

            {/* Tags */}
            <div style={{ marginBottom: 16 }}>
              <label style={{ display: 'block', fontSize: 13, fontWeight: 600, marginBottom: 8 }}>Required Tags</label>
              {requiredTags.map((tag, i) => (
                <div key={i} className="tag-row">
                  <div className="form-group">
                    <input type="text" value={tag.Key} onChange={(e) => updateTagRow(setRequiredTags, i, 'Key', e.target.value)} placeholder="Key" />
                  </div>
                  <div className="form-group">
                    <select value={tag.Condition} onChange={(e) => updateTagRow(setRequiredTags, i, 'Condition', e.target.value)}>
                      {TAG_CONDITIONS.map(c => <option key={c} value={c}>{c}</option>)}
                    </select>
                  </div>
                  <div className="form-group">
                    <input type="text" value={tag.Value} onChange={(e) => updateTagRow(setRequiredTags, i, 'Value', e.target.value)} placeholder="Value" />
                  </div>
                  <button type="button" className="btn btn-sm btn-danger" onClick={() => removeTagRow(setRequiredTags, i)} style={{ marginBottom: 0, alignSelf: 'end' }}>X</button>
                </div>
              ))}
              <button type="button" className="add-row-btn" onClick={() => addTagRow(setRequiredTags)}>+ Add Required Tag</button>
            </div>

            <div style={{ marginBottom: 16 }}>
              <label style={{ display: 'block', fontSize: 13, fontWeight: 600, marginBottom: 8 }}>Excluded Tags</label>
              {excludedTags.map((tag, i) => (
                <div key={i} className="tag-row">
                  <div className="form-group">
                    <input type="text" value={tag.Key} onChange={(e) => updateTagRow(setExcludedTags, i, 'Key', e.target.value)} placeholder="Key" />
                  </div>
                  <div className="form-group">
                    <select value={tag.Condition} onChange={(e) => updateTagRow(setExcludedTags, i, 'Condition', e.target.value)}>
                      {TAG_CONDITIONS.map(c => <option key={c} value={c}>{c}</option>)}
                    </select>
                  </div>
                  <div className="form-group">
                    <input type="text" value={tag.Value} onChange={(e) => updateTagRow(setExcludedTags, i, 'Value', e.target.value)} placeholder="Value" />
                  </div>
                  <button type="button" className="btn btn-sm btn-danger" onClick={() => removeTagRow(setExcludedTags, i)} style={{ marginBottom: 0, alignSelf: 'end' }}>X</button>
                </div>
              ))}
              <button type="button" className="add-row-btn" onClick={() => addTagRow(setExcludedTags)}>+ Add Excluded Tag</button>
            </div>

            {/* Terms */}
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16, marginBottom: 16 }}>
              <div className="form-group">
                <label>Required Terms (comma-separated)</label>
                <input type="text" value={requiredTerms} onChange={(e) => setRequiredTerms(e.target.value)} placeholder="machine learning, neural network" />
              </div>
              <div className="form-group">
                <label>Excluded Terms (comma-separated)</label>
                <input type="text" value={excludedTerms} onChange={(e) => setExcludedTerms(e.target.value)} placeholder="deprecated, draft" />
              </div>
            </div>

            {/* Date range & Doc IDs */}
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16, marginBottom: 16 }}>
              <div className="form-group">
                <label>Created After</label>
                <input type="datetime-local" value={createdAfter} onChange={(e) => setCreatedAfter(e.target.value)} />
              </div>
              <div className="form-group">
                <label>Created Before</label>
                <input type="datetime-local" value={createdBefore} onChange={(e) => setCreatedBefore(e.target.value)} />
              </div>
            </div>

            <div className="form-group">
              <label>Document IDs (comma-separated)</label>
              <input type="text" value={documentIds} onChange={(e) => setDocumentIds(e.target.value)} placeholder="doc-id-1, doc-id-2" />
            </div>
          </CollapsibleSection>

          {/* Results options & submit */}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr auto auto', gap: 16, alignItems: 'end', marginTop: 8 }}>
            <div className="form-group" style={{ marginBottom: 0 }}>
              <label>Sort Order</label>
              <select value={sortOrder} onChange={(e) => setSortOrder(e.target.value)}>
                {SORT_ORDERS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
              </select>
            </div>
            <div className="form-group" style={{ marginBottom: 0 }}>
              <label>Max Results</label>
              <input type="number" value={maxResults} onChange={(e) => setMaxResults(e.target.value)} min={1} max={1000} />
            </div>
            <div className="form-group" style={{ marginBottom: 0 }}>
              <label>Include Neighbors</label>
              <input type="number" value={includeNeighbors} onChange={(e) => setIncludeNeighbors(e.target.value)} min={0} max={10} placeholder="0" />
            </div>
            <div className="form-group" style={{ marginBottom: 0 }}>
              <label htmlFor="sr-include-embeddings" style={{ display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer', whiteSpace: 'nowrap' }}>
                <input
                  id="sr-include-embeddings" type="checkbox"
                  checked={includeEmbeddings}
                  onChange={(e) => setIncludeEmbeddings(e.target.checked)}
                  style={{ width: 'auto' }}
                />
                Include Embeddings
              </label>
            </div>
            <button type="submit" className="btn btn-primary" disabled={loading || !tenantId || !collectionId} style={{ marginBottom: 0 }}>
              {loading ? 'Searching...' : 'Search'}
            </button>
          </div>
        </form>
      </div>

      {results && (
        <div style={{ marginTop: 24 }}>
          <p style={{ marginBottom: 12, color: 'var(--text-secondary)' }}>
            {results.TotalRecords || 0} total {lastSearchWasCollapsed ? 'groups' : 'results'} {results.EndOfResults ? '' : `(showing first ${results.Documents?.length || 0})`}
          </p>
          {results.Notice && (
            <div
              role="status"
              style={{
                marginBottom: 12, padding: '10px 16px', fontSize: 14, borderRadius: 6,
                background: 'var(--card-bg)', color: 'var(--text)',
                border: '1px solid var(--border)', borderLeft: '4px solid var(--warning)'
              }}
            >
              <strong>Notice:</strong> {results.Notice}
            </div>
          )}
          <div className="card">
            <DataTable
              data={results.Documents || []}
              columns={resultColumns}
              onRowClick={(d) => setViewModal(d)}
              expandable={results.Documents?.some(d => d.Neighbors && d.Neighbors.length > 0)}
              renderExpanded={(doc) => doc.Neighbors && doc.Neighbors.length > 0 ? (
                <div style={{ padding: '8px 16px', background: 'var(--bg-tertiary, #f5f5f5)', borderTop: '1px solid var(--border-color, #eee)' }}>
                  <p style={{ fontSize: 12, fontWeight: 600, color: 'var(--text-secondary)', marginBottom: 8 }}>
                    Neighbors ({doc.Neighbors.length} chunk{doc.Neighbors.length !== 1 ? 's' : ''})
                  </p>
                  <table style={{ width: '100%', fontSize: 13, borderCollapse: 'collapse' }}>
                    <thead>
                      <tr style={{ borderBottom: '1px solid var(--border-color, #ddd)' }}>
                        <th style={{ textAlign: 'left', padding: '4px 8px', width: 60 }}>Pos</th>
                        <th style={{ textAlign: 'left', padding: '4px 8px', width: 70 }}>Type</th>
                        <th style={{ textAlign: 'left', padding: '4px 8px' }}>Content</th>
                        <th style={{ width: 50 }}></th>
                      </tr>
                    </thead>
                    <tbody>
                      {doc.Neighbors.map((nb, i) => (
                        <tr key={i} style={{ borderBottom: '1px solid var(--border-color, #eee)' }}>
                          <td style={{ padding: '4px 8px' }}>{nb.Position}</td>
                          <td style={{ padding: '4px 8px' }}>{nb.ContentType}</td>
                          <td style={{ padding: '4px 8px' }}>{(nb.Content || '').length > 120 ? nb.Content.substring(0, 120) + '...' : (nb.Content || '(no content)')}</td>
                          <td style={{ padding: '4px 8px' }}>
                            <ActionMenu actions={[{ label: 'View JSON', onClick: () => setJsonModal(nb) }]} />
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : null}
            />
          </div>
        </div>
      )}

      {jsonModal && <JsonModal title="Document JSON" data={jsonModal} onClose={() => setJsonModal(null)} />}
      {viewModal && <ViewDocumentModal document={viewModal} tenantId={tenantId} collectionId={collectionId} onClose={() => setViewModal(null)} />}
    </div>
  )
}

const ENUMERATION_ORDERS = [
  { value: 'CreatedDescending', label: 'Created Descending' },
  { value: 'CreatedAscending', label: 'Created Ascending' }
]

function QueryTab({ tenantId, collectionId }) {
  const [ordering, setOrdering] = useState('CreatedDescending')
  const [maxResults, setMaxResults] = useState(100)
  const [createdBefore, setCreatedBefore] = useState('')
  const [createdAfter, setCreatedAfter] = useState('')
  const [documentIds, setDocumentIds] = useState('')
  const [requiredLabels, setRequiredLabels] = useState([])
  const [excludedLabels, setExcludedLabels] = useState([])
  const [requiredTags, setRequiredTags] = useState([])
  const [excludedTags, setExcludedTags] = useState([])
  const [requiredTerms, setRequiredTerms] = useState('')
  const [excludedTerms, setExcludedTerms] = useState('')
  const [results, setResults] = useState(null)
  const [error, setError] = useState(null)
  const [loading, setLoading] = useState(false)
  const [jsonModal, setJsonModal] = useState(null)
  const [viewModal, setViewModal] = useState(null)

  const addLabel = (setter) => setter(prev => [...prev, ''])
  const updateLabel = (setter, index, value) => setter(prev => prev.map((v, i) => i === index ? value : v))
  const removeLabel = (setter, index) => setter(prev => prev.filter((_, i) => i !== index))
  const addTagRow = (setter) => setter(prev => [...prev, { Key: '', Condition: 'Equals', Value: '' }])
  const updateTagRow = (setter, index, field, value) => setter(prev => prev.map((row, i) => i === index ? { ...row, [field]: value } : row))
  const removeTagRow = (setter, index) => setter(prev => prev.filter((_, i) => i !== index))

  const buildEnumerationQuery = (continuationToken) => {
    const query = {
      MaxResults: parseInt(maxResults) || 100,
      Ordering: ordering
    }

    if (continuationToken) query.ContinuationToken = continuationToken

    if (createdBefore) query.CreatedBefore = new Date(createdBefore).toISOString()
    if (createdAfter) query.CreatedAfter = new Date(createdAfter).toISOString()

    const docIds = parseCommaSep(documentIds)
    if (docIds.length > 0) query.DocumentIds = docIds

    const reqLabels = requiredLabels.filter(l => l.trim())
    const excLabels = excludedLabels.filter(l => l.trim())
    if (reqLabels.length > 0 || excLabels.length > 0) {
      query.LabelFilter = {}
      if (reqLabels.length > 0) query.LabelFilter.Required = reqLabels
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

    return query
  }

  const handleEnumerate = async (e) => {
    e.preventDefault()
    setError(null)
    setLoading(true)
    try {
      const query = buildEnumerationQuery(null)
      const result = await api.enumerateDocuments(tenantId, collectionId, query)
      setResults(result)
    } catch (err) {
      setError(err)
    } finally {
      setLoading(false)
    }
  }

  const handleNextPage = async () => {
    if (!results?.ContinuationToken) return
    setError(null)
    setLoading(true)
    try {
      const query = buildEnumerationQuery(results.ContinuationToken)
      const result = await api.enumerateDocuments(tenantId, collectionId, query)
      setResults(result)
    } catch (err) {
      setError(err)
    } finally {
      setLoading(false)
    }
  }

  const resultColumns = [
    {
      key: 'DocumentKey', label: 'Key',
      render: (d) => <CopyId value={d.DocumentKey} />,
      filterValue: (d) => d.DocumentKey
    },
    { key: 'DocumentId', label: 'Doc ID', render: (d) => d.DocumentId ? <CopyId value={d.DocumentId} truncate={16} /> : '-' },
    { key: 'Position', label: 'Pos', width: '60px' },
    { key: 'ContentType', label: 'Type', width: '70px' },
    {
      key: 'Content', label: 'Content',
      render: (d) => {
        const text = d.Content || '(no content)'
        return text.length > 120 ? text.substring(0, 120) + '...' : text
      },
      filterValue: (d) => d.Content || ''
    },
    {
      key: 'actions', label: 'Actions', isAction: true, width: '50px',
      render: (d) => <ActionMenu actions={[{ label: 'View JSON', onClick: () => setJsonModal(d) }]} />
    }
  ]

  return (
    <div>
      <ErrorModal error={error} onClose={() => setError(null)} />
      <div className="card">
        <form onSubmit={handleEnumerate}>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16, marginBottom: 16 }}>
            <div className="form-group">
              <label>Ordering</label>
              <select value={ordering} onChange={(e) => setOrdering(e.target.value)}>
                {ENUMERATION_ORDERS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
              </select>
            </div>
            <div className="form-group">
              <label>Max Results</label>
              <input type="number" value={maxResults} onChange={(e) => setMaxResults(e.target.value)} min={1} max={1000} />
            </div>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16, marginBottom: 16 }}>
            <div className="form-group">
              <label>Created After</label>
              <input type="datetime-local" value={createdAfter} onChange={(e) => setCreatedAfter(e.target.value)} />
            </div>
            <div className="form-group">
              <label>Created Before</label>
              <input type="datetime-local" value={createdBefore} onChange={(e) => setCreatedBefore(e.target.value)} />
            </div>
          </div>

          <div className="form-group" style={{ marginBottom: 16 }}>
            <label>Document IDs (comma-separated)</label>
            <textarea value={documentIds} onChange={(e) => setDocumentIds(e.target.value)} rows={2} placeholder="doc-id-1, doc-id-2" />
          </div>

          <CollapsibleSection title="Label Filter">
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16, marginBottom: 16 }}>
              <div>
                <label style={{ display: 'block', fontSize: 13, fontWeight: 600, marginBottom: 8 }}>Required Labels</label>
                {requiredLabels.map((label, i) => (
                  <div key={i} style={{ display: 'flex', gap: 8, marginBottom: 6, alignItems: 'center' }}>
                    <div className="form-group" style={{ flex: 1, marginBottom: 0 }}>
                      <input type="text" value={label} onChange={(e) => updateLabel(setRequiredLabels, i, e.target.value)} placeholder="Label" />
                    </div>
                    <button type="button" className="btn btn-sm btn-danger" onClick={() => removeLabel(setRequiredLabels, i)}>X</button>
                  </div>
                ))}
                <button type="button" className="add-row-btn" onClick={() => addLabel(setRequiredLabels)}>+ Add Label</button>
              </div>
              <div>
                <label style={{ display: 'block', fontSize: 13, fontWeight: 600, marginBottom: 8 }}>Excluded Labels</label>
                {excludedLabels.map((label, i) => (
                  <div key={i} style={{ display: 'flex', gap: 8, marginBottom: 6, alignItems: 'center' }}>
                    <div className="form-group" style={{ flex: 1, marginBottom: 0 }}>
                      <input type="text" value={label} onChange={(e) => updateLabel(setExcludedLabels, i, e.target.value)} placeholder="Label" />
                    </div>
                    <button type="button" className="btn btn-sm btn-danger" onClick={() => removeLabel(setExcludedLabels, i)}>X</button>
                  </div>
                ))}
                <button type="button" className="add-row-btn" onClick={() => addLabel(setExcludedLabels)}>+ Add Label</button>
              </div>
            </div>
          </CollapsibleSection>

          <CollapsibleSection title="Tag Filter">
            <p style={{ fontSize: 13, color: 'var(--text-secondary)', marginBottom: 12 }}>Required tag conditions (all must match):</p>
            {requiredTags.map((tag, i) => (
              <div key={i} className="tag-row">
                <div className="form-group">
                  <label>Key</label>
                  <input type="text" value={tag.Key} onChange={(e) => updateTagRow(setRequiredTags, i, 'Key', e.target.value)} placeholder="tag key" />
                </div>
                <div className="form-group">
                  <label>Condition</label>
                  <select value={tag.Condition} onChange={(e) => updateTagRow(setRequiredTags, i, 'Condition', e.target.value)}>
                    {TAG_CONDITIONS.map(c => <option key={c} value={c}>{c}</option>)}
                  </select>
                </div>
                <div className="form-group">
                  <label>Value</label>
                  <input type="text" value={tag.Value} onChange={(e) => updateTagRow(setRequiredTags, i, 'Value', e.target.value)} placeholder="value" />
                </div>
                <button type="button" className="btn btn-sm btn-danger" onClick={() => removeTagRow(setRequiredTags, i)} style={{ marginBottom: 0, alignSelf: 'end' }}>X</button>
              </div>
            ))}
            <button type="button" className="add-row-btn" onClick={() => addTagRow(setRequiredTags)}>+ Add Required Tag</button>

            <p style={{ fontSize: 13, color: 'var(--text-secondary)', marginBottom: 12, marginTop: 16 }}>Excluded tag conditions (none may match):</p>
            {excludedTags.map((tag, i) => (
              <div key={i} className="tag-row">
                <div className="form-group">
                  <label>Key</label>
                  <input type="text" value={tag.Key} onChange={(e) => updateTagRow(setExcludedTags, i, 'Key', e.target.value)} placeholder="tag key" />
                </div>
                <div className="form-group">
                  <label>Condition</label>
                  <select value={tag.Condition} onChange={(e) => updateTagRow(setExcludedTags, i, 'Condition', e.target.value)}>
                    {TAG_CONDITIONS.map(c => <option key={c} value={c}>{c}</option>)}
                  </select>
                </div>
                <div className="form-group">
                  <label>Value</label>
                  <input type="text" value={tag.Value} onChange={(e) => updateTagRow(setExcludedTags, i, 'Value', e.target.value)} placeholder="value" />
                </div>
                <button type="button" className="btn btn-sm btn-danger" onClick={() => removeTagRow(setExcludedTags, i)} style={{ marginBottom: 0, alignSelf: 'end' }}>X</button>
              </div>
            ))}
            <button type="button" className="add-row-btn" onClick={() => addTagRow(setExcludedTags)}>+ Add Excluded Tag</button>
          </CollapsibleSection>

          <CollapsibleSection title="Terms Filter">
            <div className="form-group">
              <label>Required Terms (comma-separated, all must match)</label>
              <textarea value={requiredTerms} onChange={(e) => setRequiredTerms(e.target.value)} rows={2} placeholder="machine learning, neural network" />
            </div>
            <div className="form-group">
              <label>Excluded Terms (comma-separated, none may match)</label>
              <textarea value={excludedTerms} onChange={(e) => setExcludedTerms(e.target.value)} rows={2} placeholder="deprecated, draft" />
            </div>
          </CollapsibleSection>

          <button type="submit" className="btn btn-primary" disabled={loading || !tenantId || !collectionId} style={{ marginTop: 8 }}>
            {loading ? 'Enumerating...' : 'Enumerate'}
          </button>
        </form>
      </div>

      {results && (
        <div style={{ marginTop: 24 }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 12 }}>
            <p style={{ color: 'var(--text-secondary)', margin: 0 }}>
              {results.TotalRecords || 0} total records
              {results.RecordsRemaining > 0 ? ` (${results.RecordsRemaining} remaining)` : ''}
              {results.EndOfResults ? ' (end of results)' : ''}
            </p>
            {!results.EndOfResults && results.ContinuationToken && (
              <button type="button" className="btn btn-secondary" onClick={handleNextPage} disabled={loading}>
                {loading ? 'Loading...' : 'Next Page'}
              </button>
            )}
          </div>
          <div className="card">
            <DataTable data={results.Objects || []} columns={resultColumns} onRowClick={(d) => setViewModal(d)} />
          </div>
        </div>
      )}

      {jsonModal && <JsonModal title="Document JSON" data={jsonModal} onClose={() => setJsonModal(null)} />}
      {viewModal && <ViewDocumentModal document={viewModal} tenantId={tenantId} collectionId={collectionId} onClose={() => setViewModal(null)} />}
    </div>
  )
}

export default function SearchQuery({ mode = 'search' }) {
  const [selectedTenant, setSelectedTenant] = useState('')
  const [selectedCollection, setSelectedCollection] = useState('')

  return (
    <div>
      <div className="page-header">
        <h1>{mode === 'search' ? 'Search' : 'Enumerate Documents'}</h1>
      </div>

      <TenantCollectionPicker
        selectedTenant={selectedTenant}
        setSelectedTenant={setSelectedTenant}
        selectedCollection={selectedCollection}
        setSelectedCollection={setSelectedCollection}
      />

      {!selectedTenant || !selectedCollection ? (
        <div className="card" style={{ textAlign: 'center', padding: 40, color: 'var(--text-secondary)' }}>
          Select a tenant and collection to begin {mode === 'search' ? 'searching' : 'enumerating documents'}.
        </div>
      ) : mode === 'search' ? (
        <SearchTab tenantId={selectedTenant} collectionId={selectedCollection} />
      ) : (
        <QueryTab tenantId={selectedTenant} collectionId={selectedCollection} />
      )}
    </div>
  )
}
