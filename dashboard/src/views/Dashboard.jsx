import React, { useState, useEffect } from 'react'
import { useAuth } from '../context/AuthContext.jsx'
import api from '../api/api.js'

function formatUptime(ms) {
  const totalSeconds = Math.floor(ms / 1000)
  const days = Math.floor(totalSeconds / 86400)
  const hours = Math.floor((totalSeconds % 86400) / 3600)
  const minutes = Math.floor((totalSeconds % 3600) / 60)
  const seconds = totalSeconds % 60
  const parts = []
  if (days > 0) parts.push(`${days}d`)
  if (hours > 0) parts.push(`${hours}h`)
  if (minutes > 0) parts.push(`${minutes}m`)
  parts.push(`${seconds}s`)
  return parts.join(' ')
}

export default function Dashboard() {
  const { tenant, isAdmin } = useAuth()
  const [stats, setStats] = useState({ tenants: 0, collections: 0 })
  const [health, setHealth] = useState(null)
  const [currentTenant, setCurrentTenant] = useState(null)

  useEffect(() => {
    loadData()
  }, [tenant])

  const loadData = async () => {
    try {
      const h = await api.health()
      setHealth(h)

      const tenants = await api.listTenants()
      const tenantList = tenants?.Objects || []
      let collectionCount = 0

      // Resolve current tenant
      if (tenant) {
        setCurrentTenant(tenant)
      } else if (isAdmin && tenantList.length > 0) {
        // Admin API key login: default to first tenant (usually "default")
        const defaultTenant = tenantList.find(t => t.Name === 'default' || t.Id === 'default') || tenantList[0]
        setCurrentTenant(defaultTenant)
      }

      if (isAdmin && tenantList.length > 0) {
        for (const t of tenantList) {
          try {
            const cols = await api.listCollections(t.Id)
            collectionCount += cols?.Objects?.length || 0
          } catch (e) { /* skip tenants we can't access */ }
        }
      } else if (tenant) {
        try {
          const cols = await api.listCollections(tenant.Id)
          collectionCount = cols?.Objects?.length || 0
        } catch (e) { /* ignore */ }
      }

      setStats({
        tenants: tenantList.length,
        collections: collectionCount
      })
    } catch (err) {
      console.error('Failed to load dashboard data:', err)
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>Dashboard</h1>
      </div>

      <div className="stats-grid">
        <div className="stat-card">
          <h3>Server</h3>
          <div className="value">{health ? 'Online' : 'Loading...'}</div>
          {health && <p style={{ fontSize: 12, color: 'var(--text-secondary)' }}>v{health.Version}</p>}
        </div>
        <div className="stat-card">
          <h3>Uptime</h3>
          <div className="value" style={{ fontSize: 22 }}>{health ? formatUptime(health.UptimeMs) : '-'}</div>
        </div>
        <div className="stat-card">
          <h3>Tenants</h3>
          <div className="value">{stats.tenants}</div>
        </div>
        <div className="stat-card">
          <h3>Collections</h3>
          <div className="value">{stats.collections}</div>
        </div>
        <div className="stat-card">
          <h3>Current Tenant</h3>
          <div className="value" style={{ fontSize: 18 }}>{currentTenant?.Name || 'N/A'}</div>
          <p style={{ fontSize: 12, color: 'var(--text-secondary)', fontFamily: 'monospace' }}>{currentTenant?.Id}</p>
        </div>
      </div>
    </div>
  )
}
