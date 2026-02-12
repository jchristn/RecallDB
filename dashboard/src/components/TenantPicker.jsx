import React, { useState, useEffect } from 'react'
import { useAuth } from '../context/AuthContext.jsx'
import api from '../api/api.js'

export default function TenantPicker({ value, onChange }) {
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

  return (
    <div className="form-group">
      <label>Tenant</label>
      {isAdmin && tenants.length > 5 && (
        <input
          type="text"
          placeholder="Filter tenants..."
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          style={{ marginBottom: 4, padding: '6px 10px', fontSize: 13 }}
        />
      )}
      <select
        value={value}
        onChange={(e) => onChange(e.target.value)}
        disabled={!isAdmin || loading}
      >
        {filtered.map(t => (
          <option key={t.Id} value={t.Id}>{t.Name || t.Id}</option>
        ))}
      </select>
    </div>
  )
}
