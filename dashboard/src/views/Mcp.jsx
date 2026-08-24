import React, { useState, useEffect } from 'react'
import api from '../api/api.js'
import ErrorModal from '../components/ErrorModal.jsx'

function formatUptime(ms) {
  if (ms == null) return '-'
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

function buildEndpointUrl(info) {
  if (!info) return ''
  let host = info.Hostname
  if (host === '*' || host === '0.0.0.0') {
    host = window.location.hostname
  }
  return `http://${host}:${info.Port}${info.Endpoint || ''}`
}

// Static reference of MCP tool families and their operations.
const TOOL_FAMILIES = [
  { family: 'server', operations: ['info'] },
  { family: 'auth', operations: ['authenticate'] },
  { family: 'tenant', operations: ['read', 'exists', 'enumerate', 'create', 'update', 'delete'] },
  { family: 'user', operations: ['read', 'exists', 'enumerate', 'create', 'update', 'delete'] },
  { family: 'credential', operations: ['read', 'exists', 'enumerate', 'create', 'update', 'delete'] },
  { family: 'collection', operations: ['read', 'exists', 'enumerate', 'create', 'update', 'delete', 'stats'] },
  {
    family: 'document',
    operations: ['read', 'readByPosition', 'exists', 'enumerate', 'create', 'update', 'delete', 'batchCreate', 'batchDelete', 'deleteByFilter', 'stats'],
  },
  { family: 'label', operations: ['read', 'enumerate', 'create', 'delete'] },
  { family: 'tag', operations: ['read', 'enumerate', 'create', 'delete'] },
  { family: 'search', operations: ['query'] },
  { family: 'requestHistory', operations: ['enumerate', 'read', 'summary', 'delete'] },
]

const thStyle = {
  textAlign: 'left',
  padding: '10px 12px',
  fontSize: 12,
  color: 'var(--text-secondary)',
  fontWeight: 600,
  textTransform: 'uppercase',
  letterSpacing: '0.5px',
  borderBottom: '1px solid var(--border)',
  whiteSpace: 'nowrap',
}

const tdStyle = { padding: '10px 12px', fontSize: 14, verticalAlign: 'top' }

export default function Mcp() {
  const [info, setInfo] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  useEffect(() => {
    let cancelled = false
    const load = async () => {
      setLoading(true)
      try {
        const data = await api.getMcpInfo()
        if (!cancelled) setInfo(data)
      } catch (err) {
        if (!cancelled) setError(err)
      } finally {
        if (!cancelled) setLoading(false)
      }
    }
    load()
    return () => { cancelled = true }
  }, [])

  const endpointUrl = buildEndpointUrl(info)

  return (
    <div>
      <ErrorModal error={error} onClose={() => setError(null)} />

      <div className="page-header">
        <h1>MCP</h1>
      </div>

      {/* Status cards */}
      <div className="stats-grid" style={{ marginBottom: 20 }}>
        <div className="stat-card">
          <h3>Enabled</h3>
          <div className="value" style={{ color: info?.Enabled ? 'var(--success, #22c55e)' : 'var(--danger, #ef4444)' }}>
            {loading ? '...' : (info?.Enabled ? 'Yes' : 'No')}
          </div>
        </div>
        <div className="stat-card">
          <h3>Transport</h3>
          <div className="value" style={{ fontSize: 18 }}>{loading ? '...' : (info?.Transport || '-')}</div>
        </div>
        <div className="stat-card">
          <h3>Server</h3>
          <div className="value" style={{ fontSize: 18 }}>{loading ? '...' : (info?.Name || '-')}</div>
          {info?.Version && <p style={{ fontSize: 12, color: 'var(--text-secondary)' }}>v{info.Version}</p>}
        </div>
        <div className="stat-card">
          <h3>Uptime</h3>
          <div className="value" style={{ fontSize: 18 }}>{loading ? '...' : formatUptime(info?.UptimeMs)}</div>
        </div>
      </div>

      {/* Endpoint */}
      <div className="card" style={{ padding: 16, marginBottom: 20 }}>
        <h3 style={{ marginTop: 0 }}>Endpoint</h3>
        {loading ? (
          <div style={{ color: 'var(--text-secondary)' }}>Loading...</div>
        ) : (
          <code style={{
            display: 'block', background: 'var(--bg)', padding: '10px 14px',
            borderRadius: 6, fontSize: 14, fontFamily: 'monospace', wordBreak: 'break-all',
            border: '1px solid var(--border)',
          }}>
            {endpointUrl || '-'}
          </code>
        )}
      </div>

      {/* Explanation */}
      <div className="card" style={{ padding: 16, marginBottom: 20 }}>
        <h3 style={{ marginTop: 0 }}>About the MCP Server</h3>
        <p style={{ color: 'var(--text-secondary)', lineHeight: 1.6 }}>
          RecallDB hosts an in-process MCP (Model Context Protocol) server that exposes the same
          operations available through the REST API, letting MCP-aware clients and agents work
          directly with tenants, collections, documents, and search.
        </p>
        <p style={{ color: 'var(--text-secondary)', lineHeight: 1.6 }}>
          Listing operations are always paginated &mdash; there is no "get all" tool. Use the
          <code style={{ fontFamily: 'monospace' }}> enumerate</code> operations with paging arguments
          to page through results.
        </p>
        <p style={{ color: 'var(--text-secondary)', lineHeight: 1.6 }}>
          Authentication is per-caller: each tool invocation carries its own bearer token supplied via
          a <code style={{ fontFamily: 'monospace' }}>bearerToken</code> tool argument, rather than a
          shared connection-level credential.
        </p>
        <p style={{ color: 'var(--text-secondary)', lineHeight: 1.6, marginBottom: 0 }}>
          See MCP_API.md for full details.
        </p>
      </div>

      {/* Tool families reference */}
      <div className="card" style={{ padding: 16 }}>
        <h3 style={{ marginTop: 0 }}>Tool Families &amp; Operations</h3>
        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr>
                <th style={thStyle}>Family</th>
                <th style={thStyle}>Operations</th>
              </tr>
            </thead>
            <tbody>
              {TOOL_FAMILIES.map(({ family, operations }) => (
                <tr key={family} style={{ borderBottom: '1px solid var(--border)' }}>
                  <td style={{ ...tdStyle, fontFamily: 'monospace', fontWeight: 600, whiteSpace: 'nowrap' }}>
                    {family}
                  </td>
                  <td style={tdStyle}>
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                      {operations.map(op => (
                        <span key={op} style={{
                          display: 'inline-block',
                          background: 'var(--bg)',
                          border: '1px solid var(--border)',
                          borderRadius: 4,
                          padding: '2px 8px',
                          fontSize: 12,
                          fontFamily: 'monospace',
                        }}>
                          {op}
                        </span>
                      ))}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  )
}
