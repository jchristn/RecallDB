import React, { useState, useEffect } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../context/AuthContext.jsx'
import api from '../api/api.js'
import DataTable from '../components/DataTable.jsx'
import CopyId from '../components/CopyId.jsx'
import ActionMenu from '../components/ActionMenu.jsx'
import ConfirmModal from '../components/ConfirmModal.jsx'
import JsonModal from '../components/JsonModal.jsx'
import ErrorModal from '../components/ErrorModal.jsx'

export default function DocumentsBrowser() {
  const { tenant, isAdmin } = useAuth()
  const [tenants, setTenants] = useState([])
  const [collections, setCollections] = useState([])
  const [documents, setDocuments] = useState([])
  const [selectedTenant, setSelectedTenant] = useState('')
  const [selectedCollection, setSelectedCollection] = useState('')
  const [showCreate, setShowCreate] = useState(false)
  const [form, setForm] = useState({ Content: '', ContentType: 'Text', Embeddings: '' })
  const [error, setError] = useState(null)
  const [deleteTarget, setDeleteTarget] = useState(null)
  const [jsonModal, setJsonModal] = useState(null)
  const [loadingTenants, setLoadingTenants] = useState(true)
  const [loadingCollections, setLoadingCollections] = useState(false)
  const [loadingDocuments, setLoadingDocuments] = useState(false)
  const [tenantFilter, setTenantFilter] = useState('')
  const [collectionFilter, setCollectionFilter] = useState('')

  useEffect(() => {
    loadTenants()
  }, [])

  useEffect(() => {
    if (selectedTenant) {
      loadCollections(selectedTenant)
    } else {
      setCollections([])
      setSelectedCollection('')
      setDocuments([])
    }
  }, [selectedTenant])

  useEffect(() => {
    if (selectedTenant && selectedCollection) {
      loadDocuments()
    } else {
      setDocuments([])
    }
  }, [selectedTenant, selectedCollection])

  const loadTenants = async () => {
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
    } catch (err) { setError(err) }
    finally { setLoadingTenants(false) }
  }

  const loadCollections = async (tid) => {
    try {
      setLoadingCollections(true)
      setSelectedCollection('')
      setDocuments([])
      const result = await api.listCollections(tid)
      const list = result?.Objects || []
      setCollections(list)
      if (list.length === 1) setSelectedCollection(list[0].Id)
    } catch (err) { setError(err) }
    finally { setLoadingCollections(false) }
  }

  const loadDocuments = async () => {
    try {
      setLoadingDocuments(true)
      const result = await api.listDocuments(selectedTenant, selectedCollection)
      setDocuments(result?.Objects || [])
    } catch (err) { setError(err) }
    finally { setLoadingDocuments(false) }
  }

  const handleCreate = async (e) => {
    e.preventDefault()
    try {
      let embeddings = null
      if (form.Embeddings.trim()) {
        embeddings = form.Embeddings.split(',').map(v => parseFloat(v.trim()))
      }
      await api.createDocument(selectedTenant, selectedCollection, {
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
    try {
      await api.deleteDocument(selectedTenant, selectedCollection, deleteTarget)
      loadDocuments()
    } catch (err) { setError(err) }
    setDeleteTarget(null)
  }

  const filteredTenants = tenants.filter(t =>
    !tenantFilter || (t.Name || t.Id || '').toLowerCase().includes(tenantFilter.toLowerCase())
  )

  const filteredCollections = collections.filter(c =>
    !collectionFilter || (c.Name || c.Id || '').toLowerCase().includes(collectionFilter.toLowerCase())
  )

  const selectedTenantName = tenants.find(t => t.Id === selectedTenant)?.Name || selectedTenant
  const selectedCollectionName = collections.find(c => c.Id === selectedCollection)?.Name || selectedCollection

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
          { label: 'View JSON', onClick: () => setJsonModal(d) },
          { divider: true },
          { label: 'Delete', danger: true, onClick: () => setDeleteTarget(d.DocumentKey) }
        ]} />
      )
    }
  ]

  return (
    <div>
      <div className="page-header">
        <h1>Documents</h1>
        <div>
          {selectedTenant && selectedCollection && (
            <>
              <Link to={`/tenants/${selectedTenant}/collections/${selectedCollection}/search`} className="btn btn-secondary" style={{ marginRight: 8 }}>Search</Link>
              <Link to={`/tenants/${selectedTenant}/collections/${selectedCollection}/query`} className="btn btn-secondary" style={{ marginRight: 8 }}>Query</Link>
              <button className="btn btn-primary" onClick={() => setShowCreate(true)}>Add Document</button>
            </>
          )}
        </div>
      </div>
      <ErrorModal error={error} onClose={() => setError(null)} />

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
                disabled={loadingTenants || tenants.length === 0}
              />
              <select
                value={selectedTenant}
                onChange={(e) => setSelectedTenant(e.target.value)}
                disabled={loadingTenants || tenants.length === 0}
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
                disabled={!selectedTenant || collections.length === 0}
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

      {selectedTenant && selectedCollection && (
        <div className="card">
          {loadingDocuments ? (
            <div className="data-table-loading"><div className="spinner" />Loading documents...</div>
          ) : (
            <DataTable data={documents} columns={columns} />
          )}
        </div>
      )}

      {!selectedTenant && !loadingTenants && (
        <div className="card" style={{ textAlign: 'center', padding: 40, color: 'var(--text-secondary)' }}>
          Select a tenant to browse documents.
        </div>
      )}

      {selectedTenant && !selectedCollection && !loadingCollections && (
        <div className="card" style={{ textAlign: 'center', padding: 40, color: 'var(--text-secondary)' }}>
          {collections.length === 0 ? 'No collections found in this tenant.' : 'Select a collection to browse documents.'}
        </div>
      )}

      {showCreate && (
        <div className="modal-overlay" onClick={() => setShowCreate(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2>Add Document</h2>
            <form onSubmit={handleCreate}>
              <div className="form-group"><label>Content</label><textarea value={form.Content} onChange={(e) => setForm({ ...form, Content: e.target.value })} rows={4} /></div>
              <div className="form-group">
                <label>Content Type</label>
                <select value={form.ContentType} onChange={(e) => setForm({ ...form, ContentType: e.target.value })}>
                  <option>Text</option><option>Code</option><option>List</option><option>Table</option><option>Binary</option><option>Image</option><option>Hyperlink</option><option>Meta</option>
                </select>
              </div>
              <div className="form-group"><label>Embeddings (comma-separated floats)</label><input type="text" value={form.Embeddings} onChange={(e) => setForm({ ...form, Embeddings: e.target.value })} placeholder="0.1, 0.2, 0.3" /></div>
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
    </div>
  )
}
