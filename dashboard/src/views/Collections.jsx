import React, { useState, useEffect } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import api from '../api/api.js'
import DataTable from '../components/DataTable.jsx'
import CopyId from '../components/CopyId.jsx'
import ActionMenu from '../components/ActionMenu.jsx'
import ConfirmModal from '../components/ConfirmModal.jsx'
import JsonModal from '../components/JsonModal.jsx'
import ErrorModal from '../components/ErrorModal.jsx'
import TenantPicker from '../components/TenantPicker.jsx'
import TenantSelector from '../components/TenantSelector.jsx'

function formatBytes(bytes) {
  if (bytes === 0) return '0 B'
  const units = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(1024))
  return (bytes / Math.pow(1024, i)).toFixed(i > 0 ? 1 : 0) + ' ' + units[i]
}

export default function Collections() {
  const { tenantId } = useParams()
  const navigate = useNavigate()
  const [collections, setCollections] = useState([])
  const [showCreate, setShowCreate] = useState(false)
  const [form, setForm] = useState({ Name: '', Description: '', Dimensionality: 384 })
  const [createTenantId, setCreateTenantId] = useState(tenantId)
  const [error, setError] = useState(null)
  const [deleteTarget, setDeleteTarget] = useState(null)
  const [jsonModal, setJsonModal] = useState(null)
  const [editTarget, setEditTarget] = useState(null)
  const [editForm, setEditForm] = useState({ Name: '', Description: '', Active: true })
  const [statsModal, setStatsModal] = useState(null)
  const [statsLoading, setStatsLoading] = useState(false)
  const [loading, setLoading] = useState(false)
  const [distinctLabelsModal, setDistinctLabelsModal] = useState(null)
  const [distinctTagKeysModal, setDistinctTagKeysModal] = useState(null)
  const [copiedDistinct, setCopiedDistinct] = useState(false)

  useEffect(() => { loadCollections() }, [tenantId])

  const loadCollections = async () => {
    try {
      setLoading(true)
      const result = await api.listCollections(tenantId)
      setCollections(result?.Objects || [])
    } catch (err) { setError(err) }
    finally { setLoading(false) }
  }

  const handleCreate = async (e) => {
    e.preventDefault()
    try {
      await api.createCollection(createTenantId, { ...form, TenantId: createTenantId, Dimensionality: parseInt(form.Dimensionality) })
      setForm({ Name: '', Description: '', Dimensionality: 384 })
      setShowCreate(false)
      loadCollections()
    } catch (err) { setError(err) }
  }

  const handleDelete = async () => {
    if (!deleteTarget) return
    try { await api.deleteCollection(tenantId, deleteTarget); loadCollections() } catch (err) { setError(err) }
    setDeleteTarget(null)
  }

  const openEdit = (c) => {
    setEditForm({ Name: c.Name || '', Description: c.Description || '', Active: c.Active })
    setEditTarget(c)
  }

  const handleEdit = async (e) => {
    e.preventDefault()
    try {
      await api.updateCollection(tenantId, editTarget.Id, { ...editTarget, ...editForm })
      setEditTarget(null)
      loadCollections()
    } catch (err) { setError(err) }
  }

  const openStats = async (c) => {
    setStatsLoading(true)
    setStatsModal({ collection: c, stats: null })
    try {
      const stats = await api.collectionStats(tenantId, c.Id)
      setStatsModal({ collection: c, stats })
    } catch (err) {
      setError(err)
      setStatsModal(null)
    } finally {
      setStatsLoading(false)
    }
  }

  const openDistinctLabels = async (c) => {
    try {
      const data = await api.distinctLabels(tenantId, c.Id)
      setDistinctLabelsModal({ collection: c, data })
    } catch (err) { setError(err) }
  }

  const openDistinctTagKeys = async (c) => {
    try {
      const data = await api.distinctTagKeys(tenantId, c.Id)
      setDistinctTagKeysModal({ collection: c, data })
    } catch (err) { setError(err) }
  }

  const columns = [
    {
      key: 'Id',
      label: 'ID',
      width: '280px',
      render: (c) => <CopyId value={c.Id} />
    },
    { key: 'Name', label: 'Name' },
    { key: 'Dimensionality', label: 'Dimensions', width: '100px' },
    {
      key: 'Active',
      label: 'Status',
      render: (c) => <span className={c.Active ? 'status-active' : 'status-inactive'}>{c.Active ? 'Active' : 'Inactive'}</span>,
      filterValue: (c) => c.Active ? 'Active' : 'Inactive',
      sortValue: (c) => c.Active ? 'Active' : 'Inactive'
    },
    {
      key: 'CreatedUtc',
      label: 'Created',
      render: (c) => new Date(c.CreatedUtc).toLocaleDateString(),
      sortValue: (c) => c.CreatedUtc
    },
    {
      key: 'actions',
      label: 'Actions',
      isAction: true,
      width: '50px',
      render: (c) => (
        <ActionMenu actions={[
          { label: 'Documents', onClick: () => navigate(`/tenants/${tenantId}/collections/${c.Id}/documents`) },
          { label: 'Search', onClick: () => navigate(`/tenants/${tenantId}/collections/${c.Id}/search`) },
          { label: 'Query', onClick: () => navigate(`/tenants/${tenantId}/collections/${c.Id}/query`) },
          { label: 'Stats', onClick: () => openStats(c) },
          { label: 'Distinct Labels', onClick: () => openDistinctLabels(c) },
          { label: 'Distinct Tag Keys', onClick: () => openDistinctTagKeys(c) },
          { label: 'Edit', onClick: () => openEdit(c) },
          { label: 'View JSON', onClick: () => setJsonModal(c) },
          { divider: true },
          { label: 'Delete', danger: true, onClick: () => setDeleteTarget(c.Id) }
        ]} />
      )
    }
  ]

  return (
    <div>
      <div className="breadcrumb"><a href="/tenants">Tenants</a> / Collections</div>
      <TenantSelector tenantId={tenantId} basePath="collections" />
      <div className="page-header">
        <h1>Collections</h1>
        <button className="btn btn-primary" onClick={() => setShowCreate(true)}>Create Collection</button>
      </div>
      <ErrorModal error={error} onClose={() => setError(null)} />
      <div className="card">
        <DataTable data={collections} columns={columns} onRefresh={loadCollections} refreshing={loading} />
      </div>

      {showCreate && (
        <div className="modal-overlay" onClick={() => setShowCreate(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2>Create Collection</h2>
            <form onSubmit={handleCreate}>
              <TenantPicker value={createTenantId} onChange={setCreateTenantId} />
              <div className="form-group"><label>Name</label><input type="text" value={form.Name} onChange={(e) => setForm({...form, Name: e.target.value})} required /></div>
              <div className="form-group"><label>Description</label><textarea value={form.Description} onChange={(e) => setForm({...form, Description: e.target.value})} rows={3} /></div>
              <div className="form-group"><label>Dimensionality</label><input type="number" value={form.Dimensionality} onChange={(e) => setForm({...form, Dimensionality: e.target.value})} min={1} required /></div>
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
          title="Delete Collection"
          message="Are you sure you want to delete this collection and all its data? This action cannot be undone."
          danger
          onConfirm={handleDelete}
          onCancel={() => setDeleteTarget(null)}
        />
      )}

      {editTarget && (
        <div className="modal-overlay" onClick={() => setEditTarget(null)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2>Edit Collection</h2>
            <form onSubmit={handleEdit}>
              <div className="form-group"><label>Collection ID</label><input type="text" value={editTarget.Id} readOnly /></div>
              <div className="form-group"><label>Tenant ID</label><input type="text" value={editTarget.TenantId} readOnly /></div>
              <div className="form-group"><label>Name</label><input type="text" value={editForm.Name} onChange={(e) => setEditForm({...editForm, Name: e.target.value})} required /></div>
              <div className="form-group"><label>Description</label><textarea value={editForm.Description} onChange={(e) => setEditForm({...editForm, Description: e.target.value})} rows={3} /></div>
              <div className="form-group">
                <label style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <input type="checkbox" checked={editForm.Active} onChange={(e) => setEditForm({...editForm, Active: e.target.checked})} style={{ width: 'auto' }} />
                  Active
                </label>
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
            <h2>Collection Stats</h2>
            <p style={{ fontSize: 13, color: 'var(--text-secondary)', marginBottom: 16 }}>
              {statsModal.collection.Name} ({statsModal.collection.Id})
            </p>
            {statsLoading ? (
              <div className="data-table-loading"><div className="spinner" />Loading stats...</div>
            ) : statsModal.stats && (
              <div className="stats-grid" style={{ marginBottom: 0 }}>
                <div className="stat-card">
                  <h3>Chunks</h3>
                  <div className="value">{statsModal.stats.DocumentCount?.toLocaleString()}</div>
                </div>
                <div className="stat-card">
                  <h3>Documents</h3>
                  <div className="value">{statsModal.stats.UniqueDocumentCount?.toLocaleString()}</div>
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
                <div className="stat-card">
                  <h3>Dimensions</h3>
                  <div className="value">{statsModal.collection.Dimensionality}</div>
                </div>
              </div>
            )}
            <div className="modal-actions">
              <button type="button" className="btn btn-secondary" onClick={() => setStatsModal(null)}>Close</button>
            </div>
          </div>
        </div>
      )}

      {jsonModal && (
        <JsonModal title="Collection JSON" data={jsonModal} onClose={() => setJsonModal(null)} />
      )}

      {distinctLabelsModal && (
        <div className="modal-overlay" onClick={() => { setDistinctLabelsModal(null); setCopiedDistinct(false) }}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div>
                <h2 style={{ marginBottom: 2 }}>Distinct Labels</h2>
                <p style={{ fontSize: 13, color: 'var(--text-secondary)', margin: 0 }}>{distinctLabelsModal.collection.Name}</p>
              </div>
              <button type="button" onClick={() => { navigator.clipboard.writeText(JSON.stringify(distinctLabelsModal.data, null, 2)); setCopiedDistinct(true); setTimeout(() => setCopiedDistinct(false), 1500) }} title="Copy JSON" style={{ background: 'none', border: 'none', cursor: 'pointer', padding: 6, borderRadius: 4, display: 'flex', alignItems: 'center', justifyContent: 'center', color: copiedDistinct ? 'var(--success)' : 'var(--text-secondary)', transition: 'color 0.2s' }}>
                {copiedDistinct ? (
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><polyline points="20 6 9 17 4 12" /></svg>
                ) : (
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="9" y="9" width="13" height="13" rx="2" ry="2" /><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1" /></svg>
                )}
              </button>
            </div>
            <div style={{ maxHeight: '50vh', overflow: 'auto', marginTop: 12 }}>
              {distinctLabelsModal.data.length === 0 ? (
                <p style={{ color: 'var(--text-secondary)', fontStyle: 'italic' }}>No labels found.</p>
              ) : (
                <ul style={{ listStyle: 'none', margin: 0, padding: 0 }}>
                  {distinctLabelsModal.data.map((label, i) => (
                    <li key={i} style={{ padding: '6px 10px', fontSize: 14 }}>{label}</li>
                  ))}
                </ul>
              )}
            </div>
            <div className="modal-actions">
              <button type="button" className="btn btn-secondary" onClick={() => { setDistinctLabelsModal(null); setCopiedDistinct(false) }}>Close</button>
            </div>
          </div>
        </div>
      )}

      {distinctTagKeysModal && (
        <div className="modal-overlay" onClick={() => { setDistinctTagKeysModal(null); setCopiedDistinct(false) }}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div>
                <h2 style={{ marginBottom: 2 }}>Distinct Tag Keys</h2>
                <p style={{ fontSize: 13, color: 'var(--text-secondary)', margin: 0 }}>{distinctTagKeysModal.collection.Name}</p>
              </div>
              <button type="button" onClick={() => { navigator.clipboard.writeText(JSON.stringify(distinctTagKeysModal.data, null, 2)); setCopiedDistinct(true); setTimeout(() => setCopiedDistinct(false), 1500) }} title="Copy JSON" style={{ background: 'none', border: 'none', cursor: 'pointer', padding: 6, borderRadius: 4, display: 'flex', alignItems: 'center', justifyContent: 'center', color: copiedDistinct ? 'var(--success)' : 'var(--text-secondary)', transition: 'color 0.2s' }}>
                {copiedDistinct ? (
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><polyline points="20 6 9 17 4 12" /></svg>
                ) : (
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="9" y="9" width="13" height="13" rx="2" ry="2" /><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1" /></svg>
                )}
              </button>
            </div>
            <div style={{ maxHeight: '50vh', overflow: 'auto', marginTop: 12 }}>
              {distinctTagKeysModal.data.length === 0 ? (
                <p style={{ color: 'var(--text-secondary)', fontStyle: 'italic' }}>No tag keys found.</p>
              ) : (
                <ul style={{ listStyle: 'none', margin: 0, padding: 0 }}>
                  {distinctTagKeysModal.data.map((key, i) => (
                    <li key={i} style={{ padding: '6px 10px', fontSize: 14 }}>{key}</li>
                  ))}
                </ul>
              )}
            </div>
            <div className="modal-actions">
              <button type="button" className="btn btn-secondary" onClick={() => { setDistinctTagKeysModal(null); setCopiedDistinct(false) }}>Close</button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
