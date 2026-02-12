import React, { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../context/AuthContext.jsx'
import api from '../api/api.js'
import DataTable from '../components/DataTable.jsx'
import CopyId from '../components/CopyId.jsx'
import ActionMenu from '../components/ActionMenu.jsx'
import ConfirmModal from '../components/ConfirmModal.jsx'
import JsonModal from '../components/JsonModal.jsx'
import ErrorModal from '../components/ErrorModal.jsx'

export default function Tenants() {
  const { isAdmin } = useAuth()
  const navigate = useNavigate()
  const [tenants, setTenants] = useState([])
  const [showCreate, setShowCreate] = useState(false)
  const [createForm, setCreateForm] = useState({ Name: '', Labels: [], Tags: [{ key: '', value: '' }] })
  const [error, setError] = useState(null)
  const [deleteTarget, setDeleteTarget] = useState(null)
  const [jsonModal, setJsonModal] = useState(null)
  const [editTarget, setEditTarget] = useState(null)
  const [editForm, setEditForm] = useState({ Name: '', Active: true, Labels: [], Tags: [{ key: '', value: '' }] })
  const [loading, setLoading] = useState(false)

  useEffect(() => { loadTenants() }, [])

  const loadTenants = async () => {
    try {
      setLoading(true)
      const result = await api.listTenants()
      setTenants(result?.Objects || [])
    } catch (err) { setError(err) }
    finally { setLoading(false) }
  }

  const handleCreate = async (e) => {
    e.preventDefault()
    try {
      const labels = createForm.Labels.filter(l => l.trim())
      const tags = {}
      createForm.Tags.forEach(t => { if (t.key.trim()) tags[t.key.trim()] = t.value })
      await api.createTenant({
        Name: createForm.Name,
        Labels: labels.length > 0 ? labels : null,
        Tags: Object.keys(tags).length > 0 ? tags : null
      })
      setCreateForm({ Name: '', Labels: [], Tags: [{ key: '', value: '' }] })
      setShowCreate(false)
      loadTenants()
    } catch (err) { setError(err) }
  }

  const handleDelete = async () => {
    if (!deleteTarget) return
    try {
      await api.deleteTenant(deleteTarget)
      loadTenants()
    } catch (err) { setError(err) }
    setDeleteTarget(null)
  }

  const openEdit = (t) => {
    const labels = Array.isArray(t.Labels) ? [...t.Labels] : []
    const tags = t.Tags && typeof t.Tags === 'object' && Object.keys(t.Tags).length > 0
      ? Object.entries(t.Tags).map(([key, value]) => ({ key, value: value || '' }))
      : [{ key: '', value: '' }]
    setEditForm({ Name: t.Name || '', Active: t.Active, Labels: labels, Tags: tags })
    setEditTarget(t)
  }

  const handleEdit = async (e) => {
    e.preventDefault()
    try {
      const labels = editForm.Labels.filter(l => l.trim())
      const tags = {}
      editForm.Tags.forEach(t => { if (t.key.trim()) tags[t.key.trim()] = t.value })
      await api.updateTenant(editTarget.Id, {
        ...editTarget,
        Name: editForm.Name,
        Active: editForm.Active,
        Labels: labels.length > 0 ? labels : null,
        Tags: Object.keys(tags).length > 0 ? tags : null
      })
      setEditTarget(null)
      loadTenants()
    } catch (err) { setError(err) }
  }

  const columns = [
    {
      key: 'Id',
      label: 'ID',
      width: '280px',
      render: (t) => <CopyId value={t.Id} />
    },
    { key: 'Name', label: 'Name' },
    {
      key: 'Active',
      label: 'Status',
      render: (t) => <span className={t.Active ? 'status-active' : 'status-inactive'}>{t.Active ? 'Active' : 'Inactive'}</span>,
      filterValue: (t) => t.Active ? 'Active' : 'Inactive',
      sortValue: (t) => t.Active ? 'Active' : 'Inactive'
    },
    {
      key: 'CreatedUtc',
      label: 'Created',
      render: (t) => new Date(t.CreatedUtc).toLocaleDateString(),
      sortValue: (t) => t.CreatedUtc
    },
    {
      key: 'actions',
      label: 'Actions',
      isAction: true,
      width: '50px',
      render: (t) => {
        const actions = [
          { label: 'Users', onClick: () => navigate(`/tenants/${t.Id}/users`) },
          { label: 'Collections', onClick: () => navigate(`/tenants/${t.Id}/collections`) },
          { label: 'Credentials', onClick: () => navigate(`/tenants/${t.Id}/credentials`) },
          { label: 'Edit', onClick: () => openEdit(t) },
          { label: 'View JSON', onClick: () => setJsonModal(t) }
        ]
        if (isAdmin) {
          actions.push({ divider: true })
          actions.push({ label: 'Delete', danger: true, onClick: () => setDeleteTarget(t.Id) })
        }
        return <ActionMenu actions={actions} />
      }
    }
  ]

  return (
    <div>
      <div className="page-header">
        <h1>Tenants</h1>
        {isAdmin && <button className="btn btn-primary" onClick={() => setShowCreate(true)}>Create Tenant</button>}
      </div>

      <ErrorModal error={error} onClose={() => setError(null)} />

      <div className="card">
        <DataTable data={tenants} columns={columns} onRefresh={loadTenants} refreshing={loading} />
      </div>

      {showCreate && (
        <div className="modal-overlay" onClick={() => setShowCreate(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2>Create Tenant</h2>
            <form onSubmit={handleCreate}>
              <div className="form-group">
                <label>Name</label>
                <input type="text" value={createForm.Name} onChange={(e) => setCreateForm({...createForm, Name: e.target.value})} required />
              </div>
              <div className="form-group">
                <label>Labels</label>
                {createForm.Labels.map((label, i) => (
                  <div key={i} style={{ display: 'flex', gap: 8, marginBottom: 4, alignItems: 'center' }}>
                    <input type="text" value={label} onChange={(e) => { const labels = [...createForm.Labels]; labels[i] = e.target.value; setCreateForm({...createForm, Labels: labels}) }} placeholder="Label" style={{ flex: 1 }} />
                    <button type="button" className="btn btn-secondary" style={{ padding: '4px 8px' }} onClick={() => { const labels = createForm.Labels.filter((_, j) => j !== i); setCreateForm({...createForm, Labels: labels}) }}>×</button>
                  </div>
                ))}
                <button type="button" className="btn btn-secondary" style={{ marginTop: 4, fontSize: 12 }} onClick={() => setCreateForm({...createForm, Labels: [...createForm.Labels, '']})}>+ Add Label</button>
              </div>
              <div className="form-group">
                <label>Tags</label>
                {createForm.Tags.map((tag, i) => (
                  <div key={i} style={{ display: 'flex', gap: 8, marginBottom: 4 }}>
                    <input type="text" placeholder="Key" value={tag.key} onChange={(e) => { const tags = [...createForm.Tags]; tags[i] = { ...tags[i], key: e.target.value }; setCreateForm({...createForm, Tags: tags}) }} style={{ flex: 1 }} />
                    <input type="text" placeholder="Value" value={tag.value} onChange={(e) => { const tags = [...createForm.Tags]; tags[i] = { ...tags[i], value: e.target.value }; setCreateForm({...createForm, Tags: tags}) }} style={{ flex: 1 }} />
                    <button type="button" className="btn btn-secondary" style={{ padding: '4px 8px' }} onClick={() => { const tags = createForm.Tags.filter((_, j) => j !== i); setCreateForm({...createForm, Tags: tags.length ? tags : [{ key: '', value: '' }]}) }}>×</button>
                  </div>
                ))}
                <button type="button" className="btn btn-secondary" style={{ marginTop: 4, fontSize: 12 }} onClick={() => setCreateForm({...createForm, Tags: [...createForm.Tags, { key: '', value: '' }]})}>+ Add Tag</button>
              </div>
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
          title="Delete Tenant"
          message="Are you sure you want to delete this tenant? This action cannot be undone."
          danger
          onConfirm={handleDelete}
          onCancel={() => setDeleteTarget(null)}
        />
      )}

      {editTarget && (
        <div className="modal-overlay" onClick={() => setEditTarget(null)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2>Edit Tenant</h2>
            <form onSubmit={handleEdit}>
              <div className="form-group"><label>Tenant ID</label><input type="text" value={editTarget.Id} readOnly /></div>
              <div className="form-group"><label>Name</label><input type="text" value={editForm.Name} onChange={(e) => setEditForm({...editForm, Name: e.target.value})} required /></div>
              <div className="form-group">
                <label style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <input type="checkbox" checked={editForm.Active} onChange={(e) => setEditForm({...editForm, Active: e.target.checked})} style={{ width: 'auto' }} />
                  Active
                </label>
              </div>
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

      {jsonModal && (
        <JsonModal title="Tenant JSON" data={jsonModal} onClose={() => setJsonModal(null)} />
      )}
    </div>
  )
}
