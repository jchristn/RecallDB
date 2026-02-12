import React, { useState } from 'react'
import { useParams, Link } from 'react-router-dom'
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

export default function QueryBuilder() {
  const { tenantId, collectionId } = useParams()

  const [sortOrder, setSortOrder] = useState('ScoreDescending')
  const [maxResults, setMaxResults] = useState(10)
  const [createdBefore, setCreatedBefore] = useState('')
  const [createdAfter, setCreatedAfter] = useState('')
  const [documentIds, setDocumentIds] = useState('')

  const [searchType, setSearchType] = useState('CosineSimilarity')
  const [embeddings, setEmbeddings] = useState('')
  const [minScore, setMinScore] = useState('')
  const [maxScore, setMaxScore] = useState('')
  const [minDistance, setMinDistance] = useState('')
  const [maxDistance, setMaxDistance] = useState('')

  const [requiredLabels, setRequiredLabels] = useState('')
  const [excludedLabels, setExcludedLabels] = useState('')

  const [requiredTags, setRequiredTags] = useState([])
  const [excludedTags, setExcludedTags] = useState([])

  const [requiredTerms, setRequiredTerms] = useState('')
  const [excludedTerms, setExcludedTerms] = useState('')

  const [results, setResults] = useState(null)
  const [error, setError] = useState(null)
  const [loading, setLoading] = useState(false)
  const [jsonModal, setJsonModal] = useState(null)

  const tagConditions = [
    'Equals', 'NotEquals', 'GreaterThan', 'LessThan',
    'Contains', 'ContainsNot', 'StartsWith', 'EndsWith',
    'IsNull', 'IsNotNull'
  ]

  const addTagRow = (setter) => {
    setter(prev => [...prev, { Key: '', Condition: 'Equals', Value: '' }])
  }

  const updateTagRow = (setter, index, field, value) => {
    setter(prev => prev.map((row, i) => i === index ? { ...row, [field]: value } : row))
  }

  const removeTagRow = (setter, index) => {
    setter(prev => prev.filter((_, i) => i !== index))
  }

  const parseCommaSep = (str) => {
    if (!str.trim()) return []
    return str.split(',').map(s => s.trim()).filter(Boolean)
  }

  const handleSearch = async (e) => {
    e.preventDefault()
    setError(null)
    setLoading(true)
    try {
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

      const reqLabels = parseCommaSep(requiredLabels)
      const excLabels = parseCommaSep(excludedLabels)
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
      key: 'DocumentKey',
      label: 'Key',
      width: '180px',
      render: (d) => <CopyId value={d.DocumentKey} truncate={16} />,
      filterValue: (d) => d.DocumentKey
    },
    { key: 'DocumentId', label: 'Doc ID', render: (d) => d.DocumentId || '-' },
    { key: 'Position', label: 'Pos', width: '60px' },
    { key: 'ContentType', label: 'Type', width: '70px' },
    {
      key: 'Score',
      label: 'Score',
      width: '90px',
      render: (d) => d.Score != null ? <span className="score-badge">{d.Score.toFixed(4)}</span> : '-',
      sortValue: (d) => d.Score
    },
    {
      key: 'Content',
      label: 'Content',
      render: (d) => {
        const text = d.Content || '(no content)'
        return text.length > 120 ? text.substring(0, 120) + '...' : text
      },
      filterValue: (d) => d.Content || ''
    },
    {
      key: 'actions',
      label: 'Actions',
      isAction: true,
      width: '50px',
      render: (d) => (
        <ActionMenu actions={[
          { label: 'View JSON', onClick: () => setJsonModal(d) }
        ]} />
      )
    }
  ]

  return (
    <div>
      <div className="breadcrumb">
        <a href="/tenants">Tenants</a> / <a href={`/tenants/${tenantId}/collections`}>{tenantId}</a> / Collections / <Link to={`/tenants/${tenantId}/collections/${collectionId}/documents`}>{collectionId}</Link> / Query
      </div>
      <div className="page-header">
        <h1>Query Builder</h1>
      </div>
      <ErrorModal error={error} onClose={() => setError(null)} />

      <div className="card">
        <form onSubmit={handleSearch}>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16, marginBottom: 16 }}>
            <div className="form-group">
              <label>Sort Order</label>
              <select value={sortOrder} onChange={(e) => setSortOrder(e.target.value)}>
                <option value="ScoreDescending">Score Descending</option>
                <option value="ScoreAscending">Score Ascending</option>
                <option value="DistanceDescending">Distance Descending</option>
                <option value="DistanceAscending">Distance Ascending</option>
                <option value="CreatedDescending">Created Descending</option>
                <option value="CreatedAscending">Created Ascending</option>
              </select>
            </div>
            <div className="form-group">
              <label>Max Results</label>
              <input type="number" value={maxResults} onChange={(e) => setMaxResults(e.target.value)} min={1} max={1000} />
            </div>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16, marginBottom: 16 }}>
            <div className="form-group">
              <label>Created Before</label>
              <input type="datetime-local" value={createdBefore} onChange={(e) => setCreatedBefore(e.target.value)} />
            </div>
            <div className="form-group">
              <label>Created After</label>
              <input type="datetime-local" value={createdAfter} onChange={(e) => setCreatedAfter(e.target.value)} />
            </div>
          </div>

          <div className="form-group" style={{ marginBottom: 16 }}>
            <label>Document IDs (comma-separated)</label>
            <textarea value={documentIds} onChange={(e) => setDocumentIds(e.target.value)} rows={2} placeholder="doc-id-1, doc-id-2" />
          </div>

          <CollapsibleSection title="Vector Search">
            <div className="form-group">
              <label>Embeddings (comma-separated floats)</label>
              <textarea value={embeddings} onChange={(e) => setEmbeddings(e.target.value)} rows={3} placeholder="0.1, 0.2, 0.3, ..." />
            </div>
            <div className="form-group">
              <label>Search Type</label>
              <select value={searchType} onChange={(e) => setSearchType(e.target.value)}>
                <option value="CosineSimilarity">Cosine Similarity</option>
                <option value="CosineDistance">Cosine Distance</option>
                <option value="EuclideanSimilarity">Euclidean Similarity</option>
                <option value="EuclideanDistance">Euclidean Distance</option>
                <option value="InnerProduct">Inner Product</option>
              </select>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
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
          </CollapsibleSection>

          <CollapsibleSection title="Label Filter">
            <div className="form-group">
              <label>Required Labels (comma-separated)</label>
              <textarea value={requiredLabels} onChange={(e) => setRequiredLabels(e.target.value)} rows={2} placeholder="label1, label2" />
            </div>
            <div className="form-group">
              <label>Excluded Labels (comma-separated)</label>
              <textarea value={excludedLabels} onChange={(e) => setExcludedLabels(e.target.value)} rows={2} placeholder="label3, label4" />
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
                    {tagConditions.map(c => <option key={c} value={c}>{c}</option>)}
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
                    {tagConditions.map(c => <option key={c} value={c}>{c}</option>)}
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

          <button type="submit" className="btn btn-primary" disabled={loading} style={{ marginTop: 8 }}>
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

      {jsonModal && (
        <JsonModal title="Document JSON" data={jsonModal} onClose={() => setJsonModal(null)} />
      )}
    </div>
  )
}
