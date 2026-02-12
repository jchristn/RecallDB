import React, { useState, useEffect } from 'react'
import { useParams } from 'react-router-dom'
import api from '../api/api.js'
import DataTable from '../components/DataTable.jsx'
import CopyId from '../components/CopyId.jsx'
import ActionMenu from '../components/ActionMenu.jsx'
import ConfirmModal from '../components/ConfirmModal.jsx'
import JsonModal from '../components/JsonModal.jsx'
import ErrorModal from '../components/ErrorModal.jsx'
import TenantPicker from '../components/TenantPicker.jsx'
import TenantSelector from '../components/TenantSelector.jsx'

export default function Credentials() {
  const { tenantId } = useParams()
  const [credentials, setCredentials] = useState([])
  const [showCreate, setShowCreate] = useState(false)
  const [form, setForm] = useState({ Name: '', UserId: '' })
  const [createTenantId, setCreateTenantId] = useState(tenantId)
  const [error, setError] = useState(null)
  const [deleteTarget, setDeleteTarget] = useState(null)
  const [jsonModal, setJsonModal] = useState(null)
  const [editTarget, setEditTarget] = useState(null)
  const [editForm, setEditForm] = useState({ Name: '', Active: true })
  const [loading, setLoading] = useState(false)

  useEffect(() => { loadCredentials() }, [tenantId])

  const loadCredentials = async () => {
    try {
      setLoading(true)
      const result = await api.listCredentials(tenantId)
      setCredentials(result?.Objects || [])
    } catch (err) { setError(err) }
    finally { setLoading(false) }
  }

  const handleCreate = async (e) => {
    e.preventDefault()
    try {
      await api.createCredential(createTenantId, { ...form, TenantId: createTenantId })
      setForm({ Name: '', UserId: '' })
      setShowCreate(false)
      loadCredentials()
    } catch (err) { setError(err) }
  }

  const handleDelete = async () => {
    if (!deleteTarget) return
    try { await api.deleteCredential(tenantId, deleteTarget); loadCredentials() } catch (err) { setError(err) }
    setDeleteTarget(null)
  }

  const openEdit = (c) => {
    setEditForm({ Name: c.Name || '', Active: c.Active })
    setEditTarget(c)
  }

  const handleEdit = async (e) => {
    e.preventDefault()
    try {
      await api.updateCredential(tenantId, editTarget.Id, { ...editTarget, ...editForm })
      setEditTarget(null)
      loadCredentials()
    } catch (err) { setError(err) }
  }

  const columns = [
    {
      key: 'Id',
      label: 'ID',
      width: '280px',
      render: (c) => <CopyId value={c.Id} />
    },
    { key: 'Name', label: 'Name', render: (c) => c.Name || '-' },
    {
      key: 'UserId',
      label: 'User ID',
      render: (c) => <CopyId value={c.UserId} />
    },
    {
      key: 'BearerToken',
      label: 'Bearer Token',
      render: (c) => <CopyId value={c.BearerToken} truncate={12} />,
      filterValue: (c) => c.BearerToken
    },
    {
      key: 'Active',
      label: 'Status',
      render: (c) => <span className={c.Active ? 'status-active' : 'status-inactive'}>{c.Active ? 'Active' : 'Inactive'}</span>,
      filterValue: (c) => c.Active ? 'Active' : 'Inactive',
      sortValue: (c) => c.Active ? 'Active' : 'Inactive'
    },
    {
      key: 'actions',
      label: 'Actions',
      isAction: true,
      width: '50px',
      render: (c) => (
        <ActionMenu actions={[
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
      <div className="breadcrumb"><a href="/tenants">Tenants</a> / Credentials</div>
      <TenantSelector tenantId={tenantId} basePath="credentials" />
      <div className="page-header">
        <h1>Credentials</h1>
        <button className="btn btn-primary" onClick={() => setShowCreate(true)}>Create Credential</button>
      </div>
      <ErrorModal error={error} onClose={() => setError(null)} />
      <div className="card">
        <DataTable data={credentials} columns={columns} onRefresh={loadCredentials} refreshing={loading} />
      </div>

      {showCreate && (
        <div className="modal-overlay" onClick={() => setShowCreate(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2>Create Credential</h2>
            <form onSubmit={handleCreate}>
              <TenantPicker value={createTenantId} onChange={setCreateTenantId} />
              <div className="form-group"><label>Name</label><input type="text" value={form.Name} onChange={(e) => setForm({...form, Name: e.target.value})} /></div>
              <div className="form-group"><label>User ID</label><input type="text" value={form.UserId} onChange={(e) => setForm({...form, UserId: e.target.value})} required /></div>
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
          title="Delete Credential"
          message="Are you sure you want to delete this credential? This action cannot be undone."
          danger
          onConfirm={handleDelete}
          onCancel={() => setDeleteTarget(null)}
        />
      )}

      {editTarget && (
        <div className="modal-overlay" onClick={() => setEditTarget(null)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2>Edit Credential</h2>
            <form onSubmit={handleEdit}>
              <div className="form-group"><label>Credential ID</label><input type="text" value={editTarget.Id} readOnly /></div>
              <div className="form-group"><label>Tenant ID</label><input type="text" value={editTarget.TenantId} readOnly /></div>
              <div className="form-group"><label>User ID</label><input type="text" value={editTarget.UserId} readOnly /></div>
              <div className="form-group"><label>Name</label><input type="text" value={editForm.Name} onChange={(e) => setEditForm({...editForm, Name: e.target.value})} /></div>
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

      {jsonModal && (
        <JsonModal title="Credential JSON" data={jsonModal} onClose={() => setJsonModal(null)} />
      )}
    </div>
  )
}
