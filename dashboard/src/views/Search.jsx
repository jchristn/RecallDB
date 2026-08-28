import React, { useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import api from '../api/api.js'
import ErrorModal from '../components/ErrorModal.jsx'

export default function Search() {
  const { tenantId, collectionId } = useParams()
  const [embeddings, setEmbeddings] = useState('')
  const [searchType, setSearchType] = useState('CosineSimilarity')
  const [maxResults, setMaxResults] = useState(10)
  const [results, setResults] = useState(null)
  const [error, setError] = useState(null)
  const [loading, setLoading] = useState(false)

  const handleSearch = async (e) => {
    e.preventDefault()
    setError(null)
    setLoading(true)
    try {
      let embeddingsList = null
      if (embeddings.trim()) {
        embeddingsList = embeddings.split(',').map(v => parseFloat(v.trim())).filter(v => !isNaN(v) && isFinite(v))
      }

      const query = {
        MaxResults: parseInt(maxResults),
        SortOrder: 'ScoreDescending'
      }

      if (embeddingsList && embeddingsList.length > 0) {
        query.Vector = {
          SearchType: searchType,
          Embeddings: embeddingsList
        }
      }

      const result = await api.search(tenantId, collectionId, query)
      setResults(result)
    } catch (err) {
      setError(err)
    } finally {
      setLoading(false)
    }
  }

  return (
    <div>
      <div className="breadcrumb">
        <a href="/tenants">Tenants</a> / <a href={`/tenants/${tenantId}/collections`}>{tenantId}</a> / Collections / <Link to={`/tenants/${tenantId}/collections/${collectionId}/documents`}>{collectionId}</Link> / Search
      </div>
      <div className="page-header">
        <h1>Search</h1>
      </div>
      <ErrorModal error={error} onClose={() => setError(null)} />

      <div className="card">
        <form onSubmit={handleSearch} className="search-form">
          <div className="form-group">
            <label>Embeddings (comma-separated floats)</label>
            <input type="text" value={embeddings} onChange={(e) => setEmbeddings(e.target.value)} placeholder="0.1, 0.2, 0.3, ..." />
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
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
            <div className="form-group">
              <label>Max Results</label>
              <input type="number" value={maxResults} onChange={(e) => setMaxResults(e.target.value)} min={1} max={1000} />
            </div>
          </div>
          <button type="submit" className="btn btn-primary" disabled={loading}>
            {loading ? 'Searching...' : 'Search'}
          </button>
        </form>
      </div>

      {results && (
        <div>
          <p style={{ marginBottom: 12, color: '#64748b' }}>
            {results.TotalRecords || 0} results found
          </p>
          <div className="search-results">
            {(results.Documents || []).map((doc, i) => (
              <div key={doc.DocumentKey || i} className="result-item">
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 8 }}>
                  <span className="copyable-id">{doc.DocumentKey}</span>
                  <span className="score-badge">Score: {doc.Score?.toFixed(4)}</span>
                </div>
                <div style={{ fontSize: 13, color: '#475569' }}>
                  {doc.Content ? doc.Content.substring(0, 300) : '(no text content)'}
                  {doc.Content && doc.Content.length > 300 && '...'}
                </div>
                <div style={{ fontSize: 11, color: '#94a3b8', marginTop: 8 }}>
                  Type: {doc.ContentType} | Position: {doc.Position} | Doc ID: {doc.DocumentId || 'N/A'}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
