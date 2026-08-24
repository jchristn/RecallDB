import React, { useState, useEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../context/AuthContext.jsx'
import api from '../api/api.js'

const TIME_RANGES = [
  { label: 'Last Hour', value: 'hour', interval: 'minute', stepMs: 60_000, bucketCount: 60 },
  { label: 'Last Day', value: 'day', interval: '15minute', stepMs: 900_000, bucketCount: 96 },
  { label: 'Last Week', value: 'week', interval: 'hour', stepMs: 3_600_000, bucketCount: 168 },
  { label: 'Last Month', value: 'month', interval: '6hour', stepMs: 21_600_000, bucketCount: 120 },
]

const ACTIVITY_SHORTCUTS = [
  {
    eyebrow: 'Organize',
    title: 'Manage Collections',
    description: 'Create, review, and maintain collections for the active tenant.',
    getPath: tenantId => `/tenants/${tenantId}/collections`,
  },
  {
    eyebrow: 'Content',
    title: 'Manage Documents',
    description: 'Browse documents, inspect records, and update stored content.',
    getPath: () => '/documents',
  },
  {
    eyebrow: 'Discover',
    title: 'Search and Find',
    description: 'Run search workflows across your data and inspect the results.',
    getPath: () => '/search',
  },
  {
    eyebrow: 'Analyze',
    title: 'Query Your Data',
    description: 'Build structured queries for precise lookups and filtering.',
    getPath: () => '/query',
  },
]

// External observability services provisioned by docker/compose.yaml. Each card links out (in a new tab) to the
// service and surfaces its default credentials and URL. Hostnames assume the stack is reached from the same host
// the browser runs on; adjust if the observability stack is deployed elsewhere.
const OBSERVABILITY_SERVICES = [
  {
    name: 'Grafana',
    description: 'Dashboards for metrics, traces, and logs, organized into HTTP, MCP, Application, Search, Database, and Runtime sections.',
    url: 'http://localhost:3000',
    credentials: 'admin / admin',
  },
  {
    name: 'Prometheus',
    description: 'Metrics store and query UI. Scrapes the RecallDB server /metrics endpoint.',
    url: 'http://localhost:9090',
    credentials: 'No authentication',
  },
  {
    name: 'Tempo',
    description: 'Distributed tracing backend. Receives OTLP spans from the server; explore traces in Grafana.',
    url: 'http://localhost:3200',
    credentials: 'No authentication',
  },
  {
    name: 'Loki',
    description: 'Log aggregation backend. Container logs are shipped by Grafana Alloy; explore logs in Grafana.',
    url: 'http://localhost:3100',
    credentials: 'No authentication',
  },
  {
    name: 'Alloy',
    description: 'Log shipping agent. Tails container stdout/stderr and forwards to Loki.',
    url: 'http://localhost:12345',
    credentials: 'No authentication',
  },
]

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

function floorToStep(ts, stepMs) {
  return Math.floor(ts / stepMs) * stepMs
}

function getQuickRange(rangeValue) {
  const range = TIME_RANGES.find(r => r.value === rangeValue) || TIME_RANGES[1]
  const endMs = floorToStep(Date.now(), range.stepMs) + range.stepMs
  const startMs = endMs - range.bucketCount * range.stepMs
  return { ...range, startUtc: new Date(startMs), endUtc: new Date(endMs - 1) }
}

function buildBuckets(serverBuckets, range) {
  const map = new Map(
    (serverBuckets || []).map(b => [
      floorToStep(new Date(b.TimestampUtc).getTime(), range.stepMs), b
    ])
  )
  return Array.from({ length: range.bucketCount }, (_, i) => {
    const ts = range.startUtc.getTime() + i * range.stepMs
    const b = map.get(ts)
    return { ts, s: b?.SuccessCount || 0, f: b?.FailureCount || 0 }
  })
}

function formatChartLabel(ts, rangeValue) {
  const d = new Date(ts)
  if (rangeValue === 'hour' || rangeValue === 'day')
    return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
  if (rangeValue === 'week')
    return d.toLocaleString([], { month: 'short', day: 'numeric', hour: '2-digit' })
  return d.toLocaleDateString([], { month: 'short', day: 'numeric' })
}

function formatTooltipTime(ts, rangeValue) {
  const d = new Date(ts)
  if (rangeValue === 'hour')
    return d.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })
  if (rangeValue === 'day')
    return d.toLocaleString([], { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' })
  if (rangeValue === 'week')
    return d.toLocaleString([], { weekday: 'short', month: 'short', day: 'numeric', hour: 'numeric' })
  return d.toLocaleString([], { month: 'short', day: 'numeric', hour: 'numeric' })
}

export default function Dashboard() {
  const { tenant, isAdmin } = useAuth()
  const navigate = useNavigate()
  const [stats, setStats] = useState({ tenants: 0, collections: 0 })
  const [health, setHealth] = useState(null)
  const [currentTenant, setCurrentTenant] = useState(null)

  // Activity chart state
  const [activityRange, setActivityRange] = useState('day')
  const [activityBuckets, setActivityBuckets] = useState([])
  const [activityTotals, setActivityTotals] = useState({ success: 0, failure: 0 })
  const [refreshKey, setRefreshKey] = useState(0)
  const [activityTooltip, setActivityTooltip] = useState(null)
  const activityChartRef = useRef(null)

  useEffect(() => {
    loadData()
  }, [tenant])

  // Reload chart when range or refresh changes
  useEffect(() => {
    loadActivity()
  }, [activityRange, refreshKey])

  const loadData = async () => {
    try {
      const h = await api.health()
      setHealth(h)

      const tenants = await api.listTenants()
      const tenantList = tenants?.Objects || []
      let collectionCount = 0

      if (tenant) {
        setCurrentTenant(tenant)
      } else if (isAdmin && tenantList.length > 0) {
        const defaultTenant = tenantList.find(t => t.Name === 'default' || t.Id === 'default') || tenantList[0]
        setCurrentTenant(defaultTenant)
      }

      if (isAdmin && tenantList.length > 0) {
        for (const t of tenantList) {
          try {
            const cols = await api.listCollections(t.Id)
            collectionCount += cols?.Objects?.length || 0
          } catch (e) { /* skip */ }
        }
      } else if (tenant) {
        try {
          const cols = await api.listCollections(tenant.Id)
          collectionCount = cols?.Objects?.length || 0
        } catch (e) { /* ignore */ }
      }

      setStats({ tenants: tenantList.length, collections: collectionCount })
    } catch (err) {
      console.error('Failed to load dashboard data:', err)
    }
  }

  const loadActivity = async () => {
    try {
      const range = getQuickRange(activityRange)
      const summary = await api.getRequestHistorySummary({
        startUtc: range.startUtc.toISOString(),
        endUtc: range.endUtc.toISOString(),
        interval: range.interval,
      })
      const filled = buildBuckets(summary?.Buckets || [], range)
      setActivityBuckets(filled)
      setActivityTotals({
        success: summary?.TotalSuccess || 0,
        failure: summary?.TotalFailure || 0,
      })
    } catch (e) { /* request history may not be available yet */ }
  }

  // Chart rendering
  const chartW = 900, chartH = Math.round(160 * 4 / 3), padL = 44, padR = 10, padT = 12, padB = 32
  const plotW = chartW - padL - padR
  const plotH = chartH - padT - padB
  const maxCount = Math.max(1, ...activityBuckets.map(b => b.s + b.f))
  const bucketCount = activityBuckets.length || 1
  const slotW = plotW / bucketCount
  const barW = activityBuckets.length > 0 ? Math.max(2, slotW - 1) : 4
  const labelStep = Math.max(1, Math.floor(activityBuckets.length / 8))
  const activityTenantId = currentTenant?.Id || tenant?.Id || 'default'
  const activityShortcuts = ACTIVITY_SHORTCUTS.map(shortcut => ({
    ...shortcut,
    path: shortcut.getPath(activityTenantId),
  }))

  // Y-axis: only maxCount and 0 (2 lines). If maxCount effectively 0, show 0 and 1.
  const realMax = Math.max(0, ...activityBuckets.map(b => b.s + b.f))
  const yLines = realMax > 0
    ? [{ frac: 0, label: '0' }, { frac: 0.25, label: String(Math.round(realMax * 0.25)) }, { frac: 0.5, label: String(Math.round(realMax * 0.5)) }, { frac: 0.75, label: String(Math.round(realMax * 0.75)) }, { frac: 1, label: String(realMax) }]
    : [{ frac: 0, label: '0' }, { frac: 1, label: '1' }]

  const handleActivityBarHover = (event, bucket) => {
    const chartEl = activityChartRef.current
    if (!chartEl) return

    const rect = chartEl.getBoundingClientRect()
    const tooltipWidth = 180
    const tooltipHeight = 90
    const rawX = event.clientX - rect.left
    const rawY = event.clientY - rect.top

    setActivityTooltip({
      left: Math.min(Math.max(rawX + 12, 8), Math.max(8, rect.width - tooltipWidth - 8)),
      top: Math.min(Math.max(rawY - tooltipHeight - 12, 8), Math.max(8, rect.height - tooltipHeight - 8)),
      label: formatTooltipTime(bucket.ts, activityRange),
      total: bucket.s + bucket.f,
      success: bucket.s,
      failure: bucket.f,
    })
  }

  return (
    <div>
      <div className="page-header">
        <h1>Dashboard</h1>
      </div>

      <div className="stats-grid dashboard-stats-grid">
        <div className="stat-card dashboard-stat-card">
          <h3>Server</h3>
          <div className="value">{health ? 'Online' : 'Loading...'}</div>
          {health && <p style={{ fontSize: 12, color: 'var(--text-secondary)' }}>v{health.Version}</p>}
        </div>
        <div className="stat-card dashboard-stat-card">
          <h3>Uptime</h3>
          <div className="value" style={{ fontSize: 22 }}>{health ? formatUptime(health.UptimeMs) : '-'}</div>
        </div>
        <div className="stat-card dashboard-stat-card">
          <h3>Tenants</h3>
          <div className="value">{stats.tenants}</div>
        </div>
        <div className="stat-card dashboard-stat-card">
          <h3>Collections</h3>
          <div className="value">{stats.collections}</div>
        </div>
        <div className="stat-card dashboard-stat-card">
          <h3>Current Tenant</h3>
          <div className="value" style={{ fontSize: 18 }}>{currentTenant?.Name || 'N/A'}</div>
          <p style={{ fontSize: 12, color: 'var(--text-secondary)', fontFamily: 'monospace' }}>{currentTenant?.Id}</p>
        </div>
      </div>

      <div style={{ marginBottom: 12 }}>
        <p className="dashboard-section-label">Typical Activities</p>
        <div className="dashboard-shortcuts">
          {activityShortcuts.map(shortcut => (
            <button
              key={shortcut.title}
              type="button"
              className="dashboard-shortcut-card"
              onClick={() => navigate(shortcut.path)}
            >
              <span className="dashboard-shortcut-eyebrow">{shortcut.eyebrow}</span>
              <strong>{shortcut.title}</strong>
              <span>{shortcut.description}</span>
            </button>
          ))}
        </div>
      </div>

      {/* Request Activity Chart */}
      <div className="card" style={{ marginTop: 20 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
          <h3 style={{ margin: 0, cursor: 'pointer' }} onClick={() => navigate('/request-history')}>
            Request Activity
          </h3>
          <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
            {TIME_RANGES.map(r => (
              <button
                key={r.value}
                className={`btn btn-sm ${activityRange === r.value ? 'btn-primary' : ''}`}
                style={activityRange !== r.value ? { background: 'var(--border)', color: 'var(--text-secondary)' } : undefined}
                onClick={() => setActivityRange(r.value)}
              >
                {r.label}
              </button>
            ))}
            <button
              className="btn btn-secondary btn-sm"
              onClick={() => setRefreshKey(k => k + 1)}
              title="Refresh"
              style={{ fontSize: '1rem', lineHeight: 1, padding: '4px 8px' }}
            >
              &#x21bb;
            </button>
          </div>
        </div>
        <div style={{ display: 'flex', gap: 16, marginBottom: 12, fontSize: 13, color: 'var(--text-secondary)' }}>
          <span>Total: <strong style={{ color: 'var(--text)' }}>{activityTotals.success + activityTotals.failure}</strong></span>
          <span>Success: <strong style={{ color: 'var(--success, #22c55e)' }}>{activityTotals.success}</strong></span>
          <span>Failed: <strong style={{ color: 'var(--danger, #ef4444)' }}>{activityTotals.failure}</strong></span>
        </div>
        <div ref={activityChartRef} style={{ position: 'relative', width: '100%' }}>
          <svg viewBox={`0 0 ${chartW} ${chartH}`} style={{ width: '100%', height: 'auto', display: 'block' }}>
            {/* Y-axis: 2 lines */}
            {yLines.map(({ frac, label }) => {
              const y = padT + plotH - plotH * frac
              return (
                <g key={frac}>
                  <line x1={padL} x2={chartW - padR} y1={y} y2={y} stroke="var(--border)" strokeWidth="0.5" />
                  <text x={padL - 6} y={y + 3} textAnchor="end" fontSize="9" fill="var(--text-secondary)">{label}</text>
                </g>
              )
            })}

            {/* Bars */}
            {activityBuckets.map((b, i) => {
              const total = b.s + b.f
              const h = (total / maxCount) * plotH
              const fh = (b.f / maxCount) * plotH
              const x = padL + i * slotW
              return (
                <g key={i} style={{ cursor: 'pointer' }}>
                  {h - fh > 0 && <rect x={x} y={padT + plotH - h} width={barW} height={h - fh} fill="var(--success, #22c55e)" rx="1" pointerEvents="none" />}
                  {fh > 0 && <rect x={x} y={padT + plotH - h + (h - fh)} width={barW} height={fh} fill="var(--danger, #ef4444)" rx="1" pointerEvents="none" />}
                  <rect
                    x={x}
                    y={padT}
                    width={slotW}
                    height={plotH}
                    fill="#fff"
                    fillOpacity="0"
                    pointerEvents="all"
                    onMouseEnter={event => handleActivityBarHover(event, b)}
                    onMouseMove={event => handleActivityBarHover(event, b)}
                    onMouseLeave={() => setActivityTooltip(null)}
                  />
                </g>
              )
            })}

            {/* X-axis labels */}
            {activityBuckets.map((b, i) => {
              if (i % labelStep !== 0) return null
              const x = padL + i * slotW + barW / 2
              return (
                <text key={i} x={x} y={chartH - 4} textAnchor="middle" fontSize="9" fill="var(--text-secondary)">
                  {formatChartLabel(b.ts, activityRange)}
                </text>
              )
            })}
          </svg>

          {activityTooltip && (
            <div
              className="dashboard-chart-tooltip"
              style={{ left: activityTooltip.left, top: activityTooltip.top }}
            >
              <div style={{ fontWeight: 600, marginBottom: 2 }}>{activityTooltip.label}</div>
              <div>Total: {activityTooltip.total}</div>
              <div style={{ color: 'var(--success, #22c55e)' }}>Success: {activityTooltip.success}</div>
              <div style={{ color: 'var(--danger, #ef4444)' }}>Failed: {activityTooltip.failure}</div>
            </div>
          )}
        </div>
      </div>

      {/* Observability service links */}
      <div style={{ marginTop: 20 }}>
        <p className="dashboard-section-label">Observability</p>
        <div className="dashboard-shortcuts">
          {OBSERVABILITY_SERVICES.map(service => (
            <a
              key={service.name}
              className="dashboard-shortcut-card"
              href={service.url}
              target="_blank"
              rel="noopener noreferrer"
              style={{ textDecoration: 'none' }}
            >
              <span className="dashboard-shortcut-eyebrow">Open in new tab</span>
              <strong>{service.name}</strong>
              <span>{service.description}</span>
              <span style={{ marginTop: 8, fontSize: 12, color: 'var(--text-secondary)' }}>
                <span style={{ fontFamily: 'monospace' }}>{service.url}</span>
                <br />
                Credentials: <strong style={{ color: 'var(--text)' }}>{service.credentials}</strong>
              </span>
            </a>
          ))}
        </div>
      </div>
    </div>
  )
}
