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

export default function Users() {
  const { tenantId } = useParams()
  const [users, setUsers] = useState([])
  const [showCreate, setShowCreate] = useState(false)
  const [form, setForm] = useState({ Email: '', FirstName: '', LastName: '', Password: '' })
  const [createTenantId, setCreateTenantId] = useState(tenantId)
  const [error, setError] = useState(null)
  const [deleteTarget, setDeleteTarget] = useState(null)
  const [jsonModal, setJsonModal] = useState(null)
  const [editTarget, setEditTarget] = useState(null)
  const [editForm, setEditForm] = useState({ Email: '', FirstName: '', LastName: '', IsAdmin: false, IsTenantAdmin: false, Active: true })

  useEffect(() => { loadUsers() }, [tenantId])

  const loadUsers = async () => {
    try {
      const result = await api.listUsers(tenantId)
      setUsers(result?.Objects || [])
    } catch (err) { setError(err) }
  }

  const handleCreate = async (e) => {
    e.preventDefault()
    try {
      await api.createUser(createTenantId, { ...form, TenantId: createTenantId })
      setForm({ Email: '', FirstName: '', LastName: '', Password: '' })
      setShowCreate(false)
      loadUsers()
    } catch (err) { setError(err) }
  }

  const handleDelete = async () => {
    if (!deleteTarget) return
    try { await api.deleteUser(tenantId, deleteTarget); loadUsers() } catch (err) { setError(err) }
    setDeleteTarget(null)
  }

  const openEdit = (u) => {
    setEditForm({ Email: u.Email || '', FirstName: u.FirstName || '', LastName: u.LastName || '', IsAdmin: u.IsAdmin || false, IsTenantAdmin: u.IsTenantAdmin || false, Active: u.Active })
    setEditTarget(u)
  }

  const handleEdit = async (e) => {
    e.preventDefault()
    try {
      await api.updateUser(tenantId, editTarget.Id, { ...editTarget, ...editForm })
      setEditTarget(null)
      loadUsers()
    } catch (err) { setError(err) }
  }

  const columns = [
    {
      key: 'Id',
      label: 'ID',
      width: '280px',
      render: (u) => <CopyId value={u.Id} />
    },
    { key: 'Email', label: 'Email' },
    {
      key: 'Name',
      label: 'Name',
      render: (u) => [u.FirstName, u.LastName].filter(Boolean).join(' ') || '-',
      filterValue: (u) => [u.FirstName, u.LastName].filter(Boolean).join(' '),
      sortValue: (u) => [u.FirstName, u.LastName].filter(Boolean).join(' ')
    },
    {
      key: 'Admin',
      label: 'Admin',
      render: (u) => u.IsAdmin ? 'Global' : u.IsTenantAdmin ? 'Tenant' : 'No',
      filterValue: (u) => u.IsAdmin ? 'Global' : u.IsTenantAdmin ? 'Tenant' : 'No',
      sortValue: (u) => u.IsAdmin ? 'Global' : u.IsTenantAdmin ? 'Tenant' : 'No'
    },
    {
      key: 'Active',
      label: 'Status',
      render: (u) => <span className={u.Active ? 'status-active' : 'status-inactive'}>{u.Active ? 'Active' : 'Inactive'}</span>,
      filterValue: (u) => u.Active ? 'Active' : 'Inactive',
      sortValue: (u) => u.Active ? 'Active' : 'Inactive'
    },
    {
      key: 'actions',
      label: 'Actions',
      isAction: true,
      width: '50px',
      render: (u) => (
        <ActionMenu actions={[
          { label: 'Edit', onClick: () => openEdit(u) },
          { label: 'View JSON', onClick: () => setJsonModal(u) },
          { divider: true },
          { label: 'Delete', danger: true, onClick: () => setDeleteTarget(u.Id) }
        ]} />
      )
    }
  ]

  return (
    <div>
      <div className="breadcrumb"><a href="/tenants">Tenants</a> / Users</div>
      <TenantSelector tenantId={tenantId} basePath="users" />
      <div className="page-header">
        <h1>Users</h1>
        <button className="btn btn-primary" onClick={() => setShowCreate(true)}>Create User</button>
      </div>
      <ErrorModal error={error} onClose={() => setError(null)} />
      <div className="card">
        <DataTable data={users} columns={columns} />
      </div>

      {showCreate && (
        <div className="modal-overlay" onClick={() => setShowCreate(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2>Create User</h2>
            <form onSubmit={handleCreate}>
              <TenantPicker value={createTenantId} onChange={setCreateTenantId} />
              <div className="form-group"><label>Email</label><input type="email" value={form.Email} onChange={(e) => setForm({...form, Email: e.target.value})} required /></div>
              <div className="form-group"><label>First Name</label><input type="text" value={form.FirstName} onChange={(e) => setForm({...form, FirstName: e.target.value})} /></div>
              <div className="form-group"><label>Last Name</label><input type="text" value={form.LastName} onChange={(e) => setForm({...form, LastName: e.target.value})} /></div>
              <div className="form-group"><label>Password</label><input type="password" value={form.Password} onChange={(e) => setForm({...form, Password: e.target.value})} required /></div>
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
          title="Delete User"
          message="Are you sure you want to delete this user? This action cannot be undone."
          danger
          onConfirm={handleDelete}
          onCancel={() => setDeleteTarget(null)}
        />
      )}

      {editTarget && (
        <div className="modal-overlay" onClick={() => setEditTarget(null)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2>Edit User</h2>
            <form onSubmit={handleEdit}>
              <div className="form-group"><label>User ID</label><input type="text" value={editTarget.Id} readOnly /></div>
              <div className="form-group"><label>Tenant ID</label><input type="text" value={editTarget.TenantId} readOnly /></div>
              <div className="form-group"><label>Email</label><input type="email" value={editForm.Email} onChange={(e) => setEditForm({...editForm, Email: e.target.value})} required /></div>
              <div className="form-group"><label>First Name</label><input type="text" value={editForm.FirstName} onChange={(e) => setEditForm({...editForm, FirstName: e.target.value})} /></div>
              <div className="form-group"><label>Last Name</label><input type="text" value={editForm.LastName} onChange={(e) => setEditForm({...editForm, LastName: e.target.value})} /></div>
              <div className="form-group">
                <label style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <input type="checkbox" checked={editForm.IsAdmin} onChange={(e) => setEditForm({...editForm, IsAdmin: e.target.checked})} style={{ width: 'auto' }} />
                  Global Admin
                </label>
              </div>
              <div className="form-group">
                <label style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <input type="checkbox" checked={editForm.IsTenantAdmin} onChange={(e) => setEditForm({...editForm, IsTenantAdmin: e.target.checked})} style={{ width: 'auto' }} />
                  Tenant Admin
                </label>
              </div>
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
        <JsonModal title="User JSON" data={jsonModal} onClose={() => setJsonModal(null)} />
      )}
    </div>
  )
}
