import React, { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../context/AuthContext.jsx'
import api from '../api/api.js'

export default function TenantSelector({ tenantId, basePath }) {
  const navigate = useNavigate()
  const { tenant, isAdmin } = useAuth()
  const [tenants, setTenants] = useState([])
  const [filter, setFilter] = useState('')
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    if (isAdmin) {
      setLoading(true)
      api.listTenants()
        .then(result => setTenants(result?.Objects || []))
        .catch(() => {})
        .finally(() => setLoading(false))
    } else {
      setTenants(tenant ? [tenant] : [])
    }
  }, [isAdmin, tenant])

  const filtered = tenants.filter(t =>
    !filter || (t.Name || t.Id || '').toLowerCase().includes(filter.toLowerCase())
  )

  const handleChange = (newTenantId) => {
    if (newTenantId && newTenantId !== tenantId) {
      navigate(`/tenants/${newTenantId}/${basePath}`)
    }
  }

  return (
    <div className="tenant-selector">
      <label>Tenant</label>
      <div className="searchable-select">
        <input
          type="text"
          className="selector-filter"
          placeholder="Filter tenants..."
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          disabled={!isAdmin || loading}
        />
        <select
          value={tenantId}
          onChange={(e) => handleChange(e.target.value)}
          disabled={!isAdmin || loading}
        >
          {filtered.map(t => (
            <option key={t.Id} value={t.Id}>{t.Name || t.Id}</option>
          ))}
        </select>
      </div>
    </div>
  )
}
