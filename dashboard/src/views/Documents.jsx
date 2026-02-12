import React, { useState, useEffect } from 'react'
import { useParams, Link } from 'react-router-dom'
import api from '../api/api.js'
import DataTable from '../components/DataTable.jsx'
import CopyId from '../components/CopyId.jsx'
import ActionMenu from '../components/ActionMenu.jsx'
import ConfirmModal from '../components/ConfirmModal.jsx'
import JsonModal from '../components/JsonModal.jsx'
import ErrorModal from '../components/ErrorModal.jsx'

function formatBytes(bytes) {
  if (bytes === 0) return '0 B'
  const units = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(1024))
  return (bytes / Math.pow(1024, i)).toFixed(i > 0 ? 1 : 0) + ' ' + units[i]
}

export default function Documents() {
  const { tenantId, collectionId } = useParams()
  const [documents, setDocuments] = useState([])
  const [showCreate, setShowCreate] = useState(false)
  const [form, setForm] = useState({ Content: '', ContentType: 'Text', Embeddings: '' })
  const [error, setError] = useState(null)
  const [deleteTarget, setDeleteTarget] = useState(null)
  const [jsonModal, setJsonModal] = useState(null)
  const [statsModal, setStatsModal] = useState(null)
  const [statsLoading, setStatsLoading] = useState(false)

  useEffect(() => { loadDocuments() }, [tenantId, collectionId])

  const loadDocuments = async () => {
    try {
      const result = await api.listDocuments(tenantId, collectionId)
      setDocuments(result?.Objects || [])
    } catch (err) { setError(err) }
  }

  const handleCreate = async (e) => {
    e.preventDefault()
    try {
      let embeddings = null
      if (form.Embeddings.trim()) {
        embeddings = form.Embeddings.split(',').map(v => parseFloat(v.trim()))
      }
      await api.createDocument(tenantId, collectionId, {
        Content: form.Content,
        ContentType: form.ContentType,
        Embeddings: embeddings
      })
      setForm({ Content: '', ContentType: 'Text', Embeddings: '' })
      setShowCreate(false)
      loadDocuments()
    } catch (err) { setError(err) }
  }

  const handleDelete = async () => {
    if (!deleteTarget) return
    try { await api.deleteDocument(tenantId, collectionId, deleteTarget); loadDocuments() } catch (err) { setError(err) }
    setDeleteTarget(null)
  }

  const openStats = async (d) => {
    setStatsLoading(true)
    setStatsModal({ document: d, stats: null })
    try {
      const stats = await api.documentStats(tenantId, collectionId, d.DocumentKey)
      setStatsModal({ document: d, stats })
    } catch (err) {
      setError(err)
      setStatsModal(null)
    } finally {
      setStatsLoading(false)
    }
  }

  const columns = [
    {
      key: 'DocumentKey',
      label: 'Key',
      width: '180px',
      render: (d) => <CopyId value={d.DocumentKey} truncate={16} />,
      filterValue: (d) => d.DocumentKey
    },
    { key: 'DocumentId', label: 'Doc ID', render: (d) => d.DocumentId || '-' },
    { key: 'Position', label: 'Position', width: '80px' },
    { key: 'ContentType', label: 'Type', width: '80px' },
    { key: 'ContentLength', label: 'Length', width: '80px' },
    {
      key: 'CreatedUtc',
      label: 'Created',
      render: (d) => new Date(d.CreatedUtc).toLocaleDateString(),
      sortValue: (d) => d.CreatedUtc
    },
    {
      key: 'actions',
      label: 'Actions',
      isAction: true,
      width: '50px',
      render: (d) => (
        <ActionMenu actions={[
          { label: 'Stats', onClick: () => openStats(d) },
          { label: 'View JSON', onClick: () => setJsonModal(d) },
          { divider: true },
          { label: 'Delete', danger: true, onClick: () => setDeleteTarget(d.DocumentKey) }
        ]} />
      )
    }
  ]

  return (
    <div>
      <div className="breadcrumb">
        <a href="/tenants">Tenants</a> / <a href={`/tenants/${tenantId}/collections`}>{tenantId}</a> / Collections / {collectionId} / Documents
      </div>
      <div className="page-header">
        <h1>Documents</h1>
        <div>
          <Link to={`/tenants/${tenantId}/collections/${collectionId}/search`} className="btn btn-secondary" style={{marginRight:8}}>Search</Link>
          <Link to={`/tenants/${tenantId}/collections/${collectionId}/query`} className="btn btn-secondary" style={{marginRight:8}}>Query</Link>
          <button className="btn btn-primary" onClick={() => setShowCreate(true)}>Add Document</button>
        </div>
      </div>
      <ErrorModal error={error} onClose={() => setError(null)} />
      <div className="card">
        <DataTable data={documents} columns={columns} />
      </div>

      {showCreate && (
        <div className="modal-overlay" onClick={() => setShowCreate(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2>Add Document</h2>
            <form onSubmit={handleCreate}>
              <div className="form-group"><label>Content</label><textarea value={form.Content} onChange={(e) => setForm({...form, Content: e.target.value})} rows={4} /></div>
              <div className="form-group">
                <label>Content Type</label>
                <select value={form.ContentType} onChange={(e) => setForm({...form, ContentType: e.target.value})}>
                  <option>Text</option><option>Code</option><option>List</option><option>Table</option><option>Binary</option><option>Image</option><option>Hyperlink</option><option>Meta</option>
                </select>
              </div>
              <div className="form-group"><label>Embeddings (comma-separated floats)</label><input type="text" value={form.Embeddings} onChange={(e) => setForm({...form, Embeddings: e.target.value})} placeholder="0.1, 0.2, 0.3" /></div>
              <div className="modal-actions">
                <button type="button" className="btn btn-secondary" onClick={() => setShowCreate(false)}>Cancel</button>
                <button type="submit" className="btn btn-primary">Create</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {deleteTarget && (
        <ConfirmModal
          title="Delete Document"
          message="Are you sure you want to delete this document? This action cannot be undone."
          danger
          onConfirm={handleDelete}
          onCancel={() => setDeleteTarget(null)}
        />
      )}

      {jsonModal && (
        <JsonModal title="Document JSON" data={jsonModal} onClose={() => setJsonModal(null)} />
      )}

      {statsModal && (
        <div className="modal-overlay" onClick={() => setStatsModal(null)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2>Document Stats</h2>
            <p style={{ fontSize: 13, color: 'var(--text-secondary)', marginBottom: 16 }}>
              {statsModal.document.DocumentId || statsModal.document.DocumentKey}
            </p>
            {statsLoading ? (
              <div className="data-table-loading"><div className="spinner" />Loading stats...</div>
            ) : statsModal.stats && (
              <div className="stats-grid" style={{ marginBottom: 0 }}>
                <div className="stat-card">
                  <h3>Chunks</h3>
                  <div className="value">{statsModal.stats.ChunkCount?.toLocaleString()}</div>
                </div>
                <div className="stat-card">
                  <h3>Content Size</h3>
                  <div className="value" style={{ fontSize: 22 }}>{formatBytes(statsModal.stats.TotalContentLength || 0)}</div>
                </div>
                <div className="stat-card">
                  <h3>Labels</h3>
                  <div className="value">{statsModal.stats.LabelCount?.toLocaleString()}</div>
                </div>
                <div className="stat-card">
                  <h3>Tags</h3>
                  <div className="value">{statsModal.stats.TagCount?.toLocaleString()}</div>
                </div>
              </div>
            )}
            <div className="modal-actions">
              <button type="button" className="btn btn-secondary" onClick={() => setStatsModal(null)}>Close</button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
