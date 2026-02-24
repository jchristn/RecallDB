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
  const [totalRecords, setTotalRecords] = useState(0)
  const [pageIndex, setPageIndex] = useState(0)
  const [pageSize, setPageSize] = useState(10)
  const [showCreate, setShowCreate] = useState(false)
  const [form, setForm] = useState({ Content: '', ContentType: 'Text', Embeddings: '', DocumentId: '', Position: 0, Labels: [], Tags: [{ key: '', value: '' }] })
  const [error, setError] = useState(null)
  const [deleteTarget, setDeleteTarget] = useState(null)
  const [jsonModal, setJsonModal] = useState(null)
  const [statsModal, setStatsModal] = useState(null)
  const [statsLoading, setStatsLoading] = useState(false)
  const [editTarget, setEditTarget] = useState(null)
  const [editForm, setEditForm] = useState({ Content: '', ContentType: 'Text', DocumentId: '', Position: 0, Labels: [], Tags: [{ key: '', value: '' }] })
  const [loading, setLoading] = useState(false)

  useEffect(() => { loadDocuments(0, pageSize) }, [tenantId, collectionId])

  const loadDocuments = async (page = pageIndex, size = pageSize) => {
    try {
      setLoading(true)
      const query = {
        MaxResults: size,
        ContinuationToken: page > 0 ? String(page * size) : null
      }
      const result = await api.enumerateDocuments(tenantId, collectionId, query)
      setDocuments(result?.Objects || [])
      setTotalRecords(result?.TotalRecords || 0)
      setPageIndex(page)
      setPageSize(size)
    } catch (err) { setError(err) }
    finally { setLoading(false) }
  }

  const handlePageChange = (page, size) => {
    loadDocuments(page, size)
  }

  const handleCreate = async (e) => {
    e.preventDefault()
    try {
      let embeddings = null
      if (form.Embeddings.trim()) {
        embeddings = form.Embeddings.split(',').map(v => parseFloat(v.trim()))
      }
      const labels = form.Labels.filter(l => l.trim())
      const tags = {}
      form.Tags.forEach(t => { if (t.key.trim()) tags[t.key.trim()] = t.value })
      await api.createDocument(tenantId, collectionId, {
        Content: form.Content,
        ContentType: form.ContentType,
        Embeddings: embeddings,
        DocumentId: form.DocumentId || null,
        Position: form.Position || 0,
        Labels: labels.length > 0 ? labels : null,
        Tags: Object.keys(tags).length > 0 ? tags : null
      })
      setForm({ Content: '', ContentType: 'Text', Embeddings: '', DocumentId: '', Position: 0, Labels: [], Tags: [{ key: '', value: '' }] })
      setShowCreate(false)
      loadDocuments(pageIndex, pageSize)
    } catch (err) { setError(err) }
  }

  const handleDelete = async () => {
    if (!deleteTarget) return
    try { await api.deleteDocument(tenantId, collectionId, deleteTarget); loadDocuments(pageIndex, pageSize) } catch (err) { setError(err) }
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

  const openEdit = (d) => {
    const labels = Array.isArray(d.Labels) ? [...d.Labels] : []
    const tags = d.Tags && typeof d.Tags === 'object' && Object.keys(d.Tags).length > 0
      ? Object.entries(d.Tags).map(([key, value]) => ({ key, value: value || '' }))
      : [{ key: '', value: '' }]
    setEditForm({
      Content: d.Content || '',
      ContentType: d.ContentType || 'Text',
      DocumentId: d.DocumentId || '',
      Position: d.Position || 0,
      Labels: labels,
      Tags: tags
    })
    setEditTarget(d)
  }

  const handleEdit = async (e) => {
    e.preventDefault()
    try {
      const labels = editForm.Labels.filter(l => l.trim())
      const tags = {}
      editForm.Tags.forEach(t => { if (t.key.trim()) tags[t.key.trim()] = t.value })
      await api.updateDocument(tenantId, collectionId, editTarget.DocumentKey, {
        Content: editForm.Content,
        ContentType: editForm.ContentType,
        DocumentId: editForm.DocumentId || null,
        Position: editForm.Position || 0,
        Labels: labels.length > 0 ? labels : null,
        Tags: Object.keys(tags).length > 0 ? tags : null
      })
      setEditTarget(null)
      loadDocuments(pageIndex, pageSize)
    } catch (err) { setError(err) }
  }

  const columns = [
    {
      key: 'DocumentKey',
      label: 'Key',
      width: '220px',
      render: (d) => <CopyId value={d.DocumentKey} />,
      filterValue: (d) => d.DocumentKey
    },
    { key: 'DocumentId', label: 'Doc ID', render: (d) => d.DocumentId ? <CopyId value={d.DocumentId} /> : '-' },
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
          { label: 'Edit', onClick: () => openEdit(d) },
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
        <DataTable data={documents} columns={columns} onRefresh={() => loadDocuments(pageIndex, pageSize)} refreshing={loading} totalRecords={totalRecords} onPageChange={handlePageChange} />
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
              <div className="form-group"><label>Document ID</label><input type="text" value={form.DocumentId} onChange={(e) => setForm({...form, DocumentId: e.target.value})} placeholder="Optional — groups related chunks" /></div>
              <div className="form-group"><label>Position</label><input type="number" value={form.Position} onChange={(e) => setForm({...form, Position: parseInt(e.target.value) || 0})} /></div>
              <div className="form-group">
                <label>Labels</label>
                {form.Labels.map((label, i) => (
                  <div key={i} style={{ display: 'flex', gap: 8, marginBottom: 4, alignItems: 'center' }}>
                    <input type="text" value={label} onChange={(e) => { const labels = [...form.Labels]; labels[i] = e.target.value; setForm({...form, Labels: labels}) }} placeholder="Label" style={{ flex: 1 }} />
                    <button type="button" className="btn btn-secondary" style={{ padding: '4px 8px' }} onClick={() => { const labels = form.Labels.filter((_, j) => j !== i); setForm({...form, Labels: labels}) }}>×</button>
                  </div>
                ))}
                <button type="button" className="btn btn-secondary" style={{ marginTop: 4, fontSize: 12 }} onClick={() => setForm({...form, Labels: [...form.Labels, '']})}>+ Add Label</button>
              </div>
              <div className="form-group">
                <label>Tags</label>
                {form.Tags.map((tag, i) => (
                  <div key={i} style={{ display: 'flex', gap: 8, marginBottom: 4 }}>
                    <input type="text" placeholder="Key" value={tag.key} onChange={(e) => { const tags = [...form.Tags]; tags[i] = { ...tags[i], key: e.target.value }; setForm({...form, Tags: tags}) }} style={{ flex: 1 }} />
                    <input type="text" placeholder="Value" value={tag.value} onChange={(e) => { const tags = [...form.Tags]; tags[i] = { ...tags[i], value: e.target.value }; setForm({...form, Tags: tags}) }} style={{ flex: 1 }} />
                    <button type="button" className="btn btn-secondary" style={{ padding: '4px 8px' }} onClick={() => { const tags = form.Tags.filter((_, j) => j !== i); setForm({...form, Tags: tags.length ? tags : [{ key: '', value: '' }]}) }}>×</button>
                  </div>
                ))}
                <button type="button" className="btn btn-secondary" style={{ marginTop: 4, fontSize: 12 }} onClick={() => setForm({...form, Tags: [...form.Tags, { key: '', value: '' }]})}>+ Add Tag</button>
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

      {editTarget && (
        <div className="modal-overlay" onClick={() => setEditTarget(null)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2>Edit Document</h2>
            <form onSubmit={handleEdit}>
              <div className="form-group"><label>Document Key</label><input type="text" value={editTarget.DocumentKey} readOnly /></div>
              <div className="form-group"><label>Content</label><textarea value={editForm.Content} onChange={(e) => setEditForm({...editForm, Content: e.target.value})} rows={4} /></div>
              <div className="form-group">
                <label>Content Type</label>
                <select value={editForm.ContentType} onChange={(e) => setEditForm({...editForm, ContentType: e.target.value})}>
                  <option>Text</option><option>Code</option><option>List</option><option>Table</option><option>Binary</option><option>Image</option><option>Hyperlink</option><option>Meta</option>
                </select>
              </div>
              <div className="form-group"><label>Document ID</label><input type="text" value={editForm.DocumentId} onChange={(e) => setEditForm({...editForm, DocumentId: e.target.value})} placeholder="Optional — groups related chunks" /></div>
              <div className="form-group"><label>Position</label><input type="number" value={editForm.Position} onChange={(e) => setEditForm({...editForm, Position: parseInt(e.target.value) || 0})} /></div>
              <div className="form-group">
                <label>Labels</label>
                {editForm.Labels.map((label, i) => (
                  <div key={i} style={{ display: 'flex', gap: 8, marginBottom: 4, alignItems: 'center' }}>
                    <input type="text" value={label} onChange={(e) => { const labels = [...editForm.Labels]; labels[i] = e.target.value; setEditForm({...editForm, Labels: labels}) }} placeholder="Label" style={{ flex: 1 }} />
                    <button type="button" className="btn btn-secondary" style={{ padding: '4px 8px' }} onClick={() => { const labels = editForm.Labels.filter((_, j) => j !== i); setEditForm({...editForm, Labels: labels}) }}>×</button>
                  </div>
                ))}
                <button type="button" className="btn btn-secondary" style={{ marginTop: 4, fontSize: 12 }} onClick={() => setEditForm({...editForm, Labels: [...editForm.Labels, '']})}>+ Add Label</button>
              </div>
              <div className="form-group">
                <label>Tags</label>
                {editForm.Tags.map((tag, i) => (
                  <div key={i} style={{ display: 'flex', gap: 8, marginBottom: 4 }}>
                    <input type="text" placeholder="Key" value={tag.key} onChange={(e) => { const tags = [...editForm.Tags]; tags[i] = { ...tags[i], key: e.target.value }; setEditForm({...editForm, Tags: tags}) }} style={{ flex: 1 }} />
                    <input type="text" placeholder="Value" value={tag.value} onChange={(e) => { const tags = [...editForm.Tags]; tags[i] = { ...tags[i], value: e.target.value }; setEditForm({...editForm, Tags: tags}) }} style={{ flex: 1 }} />
                    <button type="button" className="btn btn-secondary" style={{ padding: '4px 8px' }} onClick={() => { const tags = editForm.Tags.filter((_, j) => j !== i); setEditForm({...editForm, Tags: tags.length ? tags : [{ key: '', value: '' }]}) }}>×</button>
                  </div>
                ))}
                <button type="button" className="btn btn-secondary" style={{ marginTop: 4, fontSize: 12 }} onClick={() => setEditForm({...editForm, Tags: [...editForm.Tags, { key: '', value: '' }]})}>+ Add Tag</button>
              </div>
              <div className="modal-actions">
                <button type="button" className="btn btn-secondary" onClick={() => setEditTarget(null)}>Cancel</button>
                <button type="submit" className="btn btn-primary">Save</button>
              </div>
            </form>
          </div>
        </div>
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
