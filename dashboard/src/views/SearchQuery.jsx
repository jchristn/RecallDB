import React, { useState, useEffect } from 'react'
import { useAuth } from '../context/AuthContext.jsx'
import api from '../api/api.js'
import DataTable from '../components/DataTable.jsx'
import CopyId from '../components/CopyId.jsx'
import ActionMenu from '../components/ActionMenu.jsx'
import JsonModal from '../components/JsonModal.jsx'
import ErrorModal from '../components/ErrorModal.jsx'

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

const TAG_CONDITIONS = [
  'Equals', 'NotEquals', 'GreaterThan', 'LessThan',
  'Contains', 'ContainsNot', 'StartsWith', 'EndsWith',
  'IsNull', 'IsNotNull'
]

const SEARCH_TYPES = [
  { value: 'CosineSimilarity', label: 'Cosine Similarity' },
  { value: 'CosineDistance', label: 'Cosine Distance' },
  { value: 'EuclideanSimilarity', label: 'Euclidean Similarity' },
  { value: 'EuclideanDistance', label: 'Euclidean Distance' },
  { value: 'InnerProduct', label: 'Inner Product' }
]

const SORT_ORDERS = [
  { value: 'ScoreDescending', label: 'Score Descending' },
  { value: 'ScoreAscending', label: 'Score Ascending' },
  { value: 'DistanceDescending', label: 'Distance Descending' },
  { value: 'DistanceAscending', label: 'Distance Ascending' },
  { value: 'CreatedDescending', label: 'Created Descending' },
  { value: 'CreatedAscending', label: 'Created Ascending' }
]

function parseCommaSep(str) {
  if (!str.trim()) return []
  return str.split(',').map(s => s.trim()).filter(Boolean)
}

function buildQuery({ embeddings, searchType, minScore, maxScore, minDistance, maxDistance,
  requiredLabels, excludedLabels, requiredTags, excludedTags, requiredTerms, excludedTerms,
  sortOrder, maxResults, createdBefore, createdAfter, documentIds }) {
  const query = {
    SortOrder: sortOrder,
    MaxResults: parseInt(maxResults) || 10
  }

  if (createdBefore) query.CreatedBefore = new Date(createdBefore).toISOString()
  if (createdAfter) query.CreatedAfter = new Date(createdAfter).toISOString()

  const docIds = parseCommaSep(documentIds)
  if (docIds.length > 0) query.DocumentIds = docIds

  if (embeddings.trim()) {
    const embList = embeddings.split(',').map(v => parseFloat(v.trim())).filter(v => !isNaN(v))
    if (embList.length > 0) {
      query.Vector = { SearchType: searchType, Embeddings: embList }
      if (minScore) query.Vector.MinimumScore = parseFloat(minScore)
      if (maxScore) query.Vector.MaximumScore = parseFloat(maxScore)
      if (minDistance) query.Vector.MinimumDistance = parseFloat(minDistance)
      if (maxDistance) query.Vector.MaximumDistance = parseFloat(maxDistance)
    }
  }

  const reqLabels = Array.isArray(requiredLabels) ? requiredLabels.filter(Boolean) : parseCommaSep(requiredLabels)
  const excLabels = Array.isArray(excludedLabels) ? excludedLabels.filter(Boolean) : parseCommaSep(excludedLabels)
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
  const [sortOrder, setSortOrder] = useState('ScoreDescending')
  const [maxResults, setMaxResults] = useState(10)
  const [createdBefore, setCreatedBefore] = useState('')
  const [createdAfter, setCreatedAfter] = useState('')
  const [documentIds, setDocumentIds] = useState('')
  const [results, setResults] = useState(null)
  const [error, setError] = useState(null)
  const [loading, setLoading] = useState(false)
  const [jsonModal, setJsonModal] = useState(null)
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

  const parsedEmbeddings = embeddings.trim()
    ? embeddings.split(',').map(v => v.trim()).filter(v => v && !isNaN(parseFloat(v)))
    : []
  const embeddingsEmpty = parsedEmbeddings.length === 0
  const embeddingsMismatch = dimensionality && parsedEmbeddings.length > 0 && parsedEmbeddings.length !== dimensionality

  const handleSearch = async (e) => {
    e.preventDefault()
    setError(null)
    if (embeddingsEmpty) {
      setError(new Error('Embeddings are required. Please provide a comma-separated list of floats matching the collection dimensionality' + (dimensionality ? ` (${dimensionality})` : '') + '.'))
      return
    }
    if (embeddingsMismatch) {
      setError(new Error(`Embeddings count (${parsedEmbeddings.length}) does not match the collection dimensionality (${dimensionality}).`))
      return
    }
    setLoading(true)
    try {
      const query = buildQuery({
        embeddings, searchType, minScore, maxScore, minDistance, maxDistance,
        requiredLabels, excludedLabels, requiredTags, excludedTags, requiredTerms, excludedTerms,
        sortOrder, maxResults, createdBefore, createdAfter, documentIds
      })
      const result = await api.search(tenantId, collectionId, query)
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
      key: 'Score', label: 'Score', width: '90px',
      render: (d) => d.Score != null ? <span className="score-badge">{d.Score.toFixed(4)}</span> : '-',
      sortValue: (d) => d.Score
    },
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
          <div className="form-group">
            <label>Embeddings (comma-separated floats){dimensionality ? ` — collection dimensionality: ${dimensionality}` : ''}</label>
            <textarea value={embeddings} onChange={(e) => setEmbeddings(e.target.value)} rows={3} placeholder="0.1, 0.2, 0.3, ..." />
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 16, marginBottom: 16 }}>
            <div className="form-group">
              <label>Search Type</label>
              <select value={searchType} onChange={(e) => setSearchType(e.target.value)}>
                {SEARCH_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
              </select>
            </div>
            <div className="form-group">
              <label>Sort Order</label>
              <select value={sortOrder} onChange={(e) => setSortOrder(e.target.value)}>
                {SORT_ORDERS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
              </select>
            </div>
            <div className="form-group">
              <label>Max Results</label>
              <input type="number" value={maxResults} onChange={(e) => setMaxResults(e.target.value)} min={1} max={1000} />
            </div>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1fr', gap: 16, marginBottom: 16 }}>
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
            <div className="form-group">
              <label>Max Distance</label>
              <input type="number" step="any" value={maxDistance} onChange={(e) => setMaxDistance(e.target.value)} placeholder="1.0" />
            </div>
          </div>

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

          <div className="form-group" style={{ marginBottom: 16 }}>
            <label>Document IDs (comma-separated)</label>
            <input type="text" value={documentIds} onChange={(e) => setDocumentIds(e.target.value)} placeholder="doc-id-1, doc-id-2" />
          </div>

          <button type="submit" className="btn btn-primary" disabled={loading || !tenantId || !collectionId}>
            {loading ? 'Searching...' : 'Search'}
          </button>
        </form>
      </div>

      {results && (
        <div style={{ marginTop: 24 }}>
          <p style={{ marginBottom: 12, color: 'var(--text-secondary)' }}>
            {results.TotalRecords || 0} total results {results.EndOfResults ? '' : `(showing first ${results.Documents?.length || 0})`}
          </p>
          <div className="card">
            <DataTable data={results.Documents || []} columns={resultColumns} />
          </div>
        </div>
      )}

      {jsonModal && <JsonModal title="Document JSON" data={jsonModal} onClose={() => setJsonModal(null)} />}
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
              {results.EndOfResults ? ' — end of results' : ''}
            </p>
            {!results.EndOfResults && results.ContinuationToken && (
              <button type="button" className="btn btn-secondary" onClick={handleNextPage} disabled={loading}>
                {loading ? 'Loading...' : 'Next Page'}
              </button>
            )}
          </div>
          <div className="card">
            <DataTable data={results.Objects || []} columns={resultColumns} />
          </div>
        </div>
      )}

      {jsonModal && <JsonModal title="Document JSON" data={jsonModal} onClose={() => setJsonModal(null)} />}
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
