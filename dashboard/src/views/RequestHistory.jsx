import React, { useState, useEffect, useRef } from 'react'
import api from '../api/api.js'
import ErrorModal from '../components/ErrorModal.jsx'

const TIME_RANGES = [
  { label: 'Last Hour', value: 'hour', interval: 'minute', stepMs: 60_000, bucketCount: 60 },
  { label: 'Last Day', value: 'day', interval: '15minute', stepMs: 900_000, bucketCount: 96 },
  { label: 'Last Week', value: 'week', interval: 'hour', stepMs: 3_600_000, bucketCount: 168 },
  { label: 'Last Month', value: 'month', interval: '6hour', stepMs: 21_600_000, bucketCount: 120 },
]

const METHOD_COLORS = {
  GET: '#3b82f6',
  POST: '#22c55e',
  PUT: '#f59e0b',
  DELETE: '#ef4444',
  PATCH: '#a855f7',
  HEAD: '#6b7280',
  OPTIONS: '#6b7280',
}

const PAGE_SIZE = 25

// ---------------------------------------------------------------------------
// Bucket-filling helpers
// ---------------------------------------------------------------------------

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

// ---------------------------------------------------------------------------
// Formatting helpers
// ---------------------------------------------------------------------------

function formatTime(isoString, rangeValue) {
  if (!isoString) return ''
  const d = new Date(isoString)
  if (rangeValue === 'hour') return d.toLocaleTimeString()
  return d.toLocaleString()
}

function formatChartTime(ts, rangeValue) {
  const d = new Date(ts)
  if (rangeValue === 'hour') return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
  if (rangeValue === 'day') return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
  if (rangeValue === 'week') return d.toLocaleDateString([], { weekday: 'short', hour: '2-digit' })
  return d.toLocaleDateString([], { month: 'short', day: 'numeric' })
}

function truncateUrl(url, max = 60) {
  if (!url) return ''
  return url.length > max ? url.substring(0, max) + '...' : url
}

function formatBytes(bytes) {
  if (!bytes) return '0 B'
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

function getStatusColor(status) {
  if (status >= 200 && status < 300) return 'var(--success, #22c55e)'
  if (status >= 400 && status < 500) return '#f59e0b'
  if (status >= 500) return 'var(--danger, #ef4444)'
  return 'inherit'
}

function methodBadge(method) {
  return {
    display: 'inline-block',
    background: METHOD_COLORS[method] || '#6b7280',
    color: '#fff',
    padding: '2px 8px',
    borderRadius: 4,
    fontSize: 12,
    fontWeight: 700,
    fontFamily: 'monospace',
  }
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export default function RequestHistory() {
  const [rangeValue, setRangeValue] = useState('day')
  const [refreshKey, setRefreshKey] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  // Summary
  const [buckets, setBuckets] = useState([])
  const [totalSuccess, setTotalSuccess] = useState(0)
  const [totalFailure, setTotalFailure] = useState(0)

  // Table
  const [entries, setEntries] = useState([])
  const [totalRecords, setTotalRecords] = useState(0)
  const [offset, setOffset] = useState(0)

  // Tooltip
  const [tooltip, setTooltip] = useState(null)
  const chartRef = useRef(null)

  // Detail modal
  const [detailEntry, setDetailEntry] = useState(null)

  const refresh = () => setRefreshKey(k => k + 1)

  // Reset offset when range changes
  useEffect(() => { setOffset(0) }, [rangeValue])

  // Fetch data
  useEffect(() => {
    let cancelled = false
    const fetchData = async () => {
      setLoading(true)
      setError(null)
      try {
        const range = getQuickRange(rangeValue)
        const startUtc = range.startUtc.toISOString()
        const endUtc = range.endUtc.toISOString()

        const [summary, list] = await Promise.all([
          api.getRequestHistorySummary({ startUtc, endUtc, interval: range.interval }),
          api.getRequestHistory({ startUtc, endUtc, maxResults: PAGE_SIZE, offset }),
        ])
        if (cancelled) return

        const filled = buildBuckets(summary?.Buckets || [], range)
        setBuckets(filled)
        setTotalSuccess(summary?.TotalSuccess ?? 0)
        setTotalFailure(summary?.TotalFailure ?? 0)

        setEntries(list?.Objects || [])
        setTotalRecords(list?.TotalRecords || 0)
      } catch (err) {
        if (!cancelled) setError(err)
      } finally {
        if (!cancelled) setLoading(false)
      }
    }
    fetchData()
    return () => { cancelled = true }
  }, [rangeValue, refreshKey, offset])

  const handleDelete = async (guid) => {
    if (!guid) return
    try {
      await api.deleteRequestHistoryEntry(guid)
      refresh()
    } catch (err) {
      setError(err)
    }
  }

  // ---------------------------------------------------------------------------
  // Chart geometry
  // ---------------------------------------------------------------------------
  const svgW = 900, svgH = 240
  const padL = 55, padR = 10, padT = 10, padB = 30
  const innerW = svgW - padL - padR
  const innerH = svgH - padT - padB

  const maxCount = buckets.reduce((m, b) => Math.max(m, b.s + b.f), 0)
  const yMax = maxCount > 0 ? maxCount : 1
  const barCount = buckets.length || 1
  const barW = Math.max(4, (innerW / barCount) - 2)

  // Y-axis: 3 values (0, half, max). If maxCount=0, show 0 and 1.
  const yLines = maxCount > 0
    ? [{ frac: 0, label: '0' }, { frac: 0.5, label: String(Math.round(maxCount / 2)) }, { frac: 1, label: String(maxCount) }]
    : [{ frac: 0, label: '0' }, { frac: 1, label: '1' }]

  // X-axis: ~8 evenly spaced labels
  const labelCount = Math.min(8, barCount)
  const labelStep = Math.max(1, Math.floor(barCount / labelCount))
  const labelIndices = []
  for (let i = 0; i < barCount; i += labelStep) labelIndices.push(i)

  const handleBarHover = (e, bucket) => {
    const el = chartRef.current
    if (!el) return
    const rect = el.getBoundingClientRect()
    setTooltip({
      x: e.clientX - rect.left,
      y: e.clientY - rect.top - 10,
      time: new Date(bucket.ts).toLocaleString(),
      total: bucket.s + bucket.f,
      success: bucket.s,
      failure: bucket.f,
    })
  }

  // Pagination
  const showingStart = totalRecords > 0 ? offset + 1 : 0
  const showingEnd = Math.min(offset + PAGE_SIZE, totalRecords)
  const canPrev = offset > 0
  const canNext = offset + PAGE_SIZE < totalRecords

  // ---------------------------------------------------------------------------
  // Render
  // ---------------------------------------------------------------------------
  return (
    <div>
      <ErrorModal error={error} onClose={() => setError(null)} />

      {/* Header */}
      <div className="page-header" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <h2 style={{ margin: 0 }}>Request History</h2>
        <button
          className="btn btn-secondary btn-sm"
          onClick={refresh}
          title="Refresh"
          style={{ fontSize: '1.2rem', lineHeight: 1, padding: '6px 10px' }}
        >
          &#x21bb;
        </button>
      </div>

      {/* Time Range Tabs */}
      <div style={{ display: 'flex', gap: 8, marginBottom: 20 }}>
        {TIME_RANGES.map(r => (
          <button
            key={r.value}
            className={`btn ${rangeValue === r.value ? 'btn-primary' : ''}`}
            style={rangeValue !== r.value ? { background: 'var(--border)', color: 'var(--text-secondary)' } : undefined}
            onClick={() => setRangeValue(r.value)}
          >
            {r.label}
          </button>
        ))}
      </div>

      {/* Stat Cards */}
      <div className="stats-grid" style={{ marginBottom: 20 }}>
        <div className="stat-card">
          <div style={{ fontSize: '0.85rem', color: 'var(--text-secondary)' }}>Total Requests</div>
          <div style={{ fontSize: '1.8rem', fontWeight: 700 }}>
            {loading ? '...' : (totalSuccess + totalFailure).toLocaleString()}
          </div>
        </div>
        <div className="stat-card">
          <div style={{ fontSize: '0.85rem', color: 'var(--text-secondary)' }}>Successful</div>
          <div style={{ fontSize: '1.8rem', fontWeight: 700, color: 'var(--success, #22c55e)' }}>
            {loading ? '...' : totalSuccess.toLocaleString()}
          </div>
        </div>
        <div className="stat-card">
          <div style={{ fontSize: '0.85rem', color: 'var(--text-secondary)' }}>Failed</div>
          <div style={{ fontSize: '1.8rem', fontWeight: 700, color: 'var(--danger, #ef4444)' }}>
            {loading ? '...' : totalFailure.toLocaleString()}
          </div>
        </div>
      </div>

      {/* SVG Chart */}
      <div className="card" style={{ padding: 16, marginBottom: 20 }}>
        <div ref={chartRef} style={{ position: 'relative', width: '100%' }}>
          <svg viewBox={`0 0 ${svgW} ${svgH}`} width="100%" style={{ display: 'block' }}>
            {/* Y-axis grid lines and labels */}
            {yLines.map(({ frac, label }) => {
              const y = padT + innerH * (1 - frac)
              return (
                <g key={frac}>
                  <line
                    x1={padL} y1={y} x2={svgW - padR} y2={y}
                    stroke="var(--text-secondary, #888)"
                    strokeOpacity={0.2}
                    strokeDasharray={frac > 0 && frac < 1 ? '4 4' : 'none'}
                  />
                  <text x={padL - 8} y={y + 4} textAnchor="end" fontSize="11" fill="var(--text-secondary, #888)">
                    {label}
                  </text>
                </g>
              )
            })}

            {/* Stacked Bars */}
            {buckets.map((bucket, i) => {
              const total = bucket.s + bucket.f
              const succH = yMax > 0 ? (bucket.s / yMax) * innerH : 0
              const failH = yMax > 0 ? (bucket.f / yMax) * innerH : 0
              const x = padL + (innerW / barCount) * i
              const succY = padT + innerH - succH
              const failY = succY - failH
              return (
                <g
                  key={i}
                  onMouseMove={e => handleBarHover(e, bucket)}
                  onMouseLeave={() => setTooltip(null)}
                  style={{ cursor: 'pointer' }}
                >
                  {bucket.s > 0 && (
                    <rect x={x} y={succY} width={barW} height={succH} fill="var(--success, #22c55e)" rx={1} />
                  )}
                  {bucket.f > 0 && (
                    <rect x={x} y={failY} width={barW} height={failH} fill="var(--danger, #ef4444)" rx={1} />
                  )}
                  {total === 0 && (
                    <rect x={x} y={padT} width={barW} height={innerH} fill="transparent" />
                  )}
                </g>
              )
            })}

            {/* X-axis labels */}
            {labelIndices.map(idx => {
              if (!buckets[idx]) return null
              const x = padL + (innerW / barCount) * idx + barW / 2
              return (
                <text key={idx} x={x} y={svgH - 6} textAnchor="middle" fontSize="10" fill="var(--text-secondary, #888)">
                  {formatChartTime(buckets[idx].ts, rangeValue)}
                </text>
              )
            })}
          </svg>

          {/* Tooltip */}
          {tooltip && (
            <div style={{
              position: 'absolute',
              left: tooltip.x + 12,
              top: tooltip.y - 60,
              background: 'rgba(0,0,0,0.88)',
              color: '#fff',
              padding: '8px 12px',
              borderRadius: 6,
              fontSize: 12,
              lineHeight: 1.5,
              pointerEvents: 'none',
              whiteSpace: 'nowrap',
              zIndex: 10,
            }}>
              <div style={{ fontWeight: 600, marginBottom: 2 }}>{tooltip.time}</div>
              <div>Total: {tooltip.total}</div>
              <div style={{ color: '#22c55e' }}>Success: {tooltip.success}</div>
              <div style={{ color: '#ef4444' }}>Failure: {tooltip.failure}</div>
            </div>
          )}
        </div>
      </div>

      {/* Entries Table */}
      <div className="card" style={{ padding: 16 }}>
        {loading ? (
          <div style={{ textAlign: 'center', padding: '40px 0', color: 'var(--text-secondary)' }}>Loading...</div>
        ) : entries.length === 0 ? (
          <div style={{ textAlign: 'center', padding: '40px 0', color: 'var(--text-secondary)' }}>
            No request history entries found for this time range.
          </div>
        ) : (
          <>
            <div style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                <thead>
                  <tr>
                    {['Time', 'Method', 'URL', 'Status', 'Duration', 'Source IP', 'Actions'].map(col => (
                      <th key={col} style={{
                        textAlign: 'left',
                        padding: '10px 12px',
                        fontSize: 12,
                        color: 'var(--text-secondary)',
                        fontWeight: 600,
                        textTransform: 'uppercase',
                        letterSpacing: '0.5px',
                        borderBottom: '1px solid var(--border)',
                        whiteSpace: 'nowrap',
                      }}>
                        {col}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {entries.map((entry, idx) => {
                    const method = (entry.HttpMethod || '').toUpperCase()
                    const status = entry.StatusCode || 0
                    return (
                      <tr
                        key={entry.Guid || entry.Id || idx}
                        style={{ borderBottom: '1px solid var(--border)' }}
                        onMouseEnter={e => { e.currentTarget.style.background = 'var(--bg)' }}
                        onMouseLeave={e => { e.currentTarget.style.background = '' }}
                      >
                        <td style={{ padding: '10px 12px', fontSize: 14, whiteSpace: 'nowrap' }}>
                          {formatTime(entry.CreatedUtc, rangeValue)}
                        </td>
                        <td style={{ padding: '10px 12px', fontSize: 14 }}>
                          <span style={methodBadge(method)}>{method}</span>
                        </td>
                        <td style={{
                          padding: '10px 12px', fontSize: 14, fontFamily: 'monospace',
                          maxWidth: 400, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
                        }} title={entry.RequestUrl}>
                          {truncateUrl(entry.RequestUrl)}
                        </td>
                        <td style={{ padding: '10px 12px', fontSize: 14, fontWeight: 600, color: getStatusColor(status) }}>
                          {status || ''}
                        </td>
                        <td style={{ padding: '10px 12px', fontSize: 14, whiteSpace: 'nowrap' }}>
                          {entry.DurationMs != null ? `${entry.DurationMs}ms` : ''}
                        </td>
                        <td style={{ padding: '10px 12px', fontSize: 14, whiteSpace: 'nowrap' }}>
                          {entry.SourceIp || ''}
                        </td>
                        <td style={{ padding: '10px 12px', fontSize: 14, whiteSpace: 'nowrap' }}>
                          <button
                            className="btn btn-secondary btn-sm"
                            style={{ marginRight: 6 }}
                            onClick={() => setDetailEntry(entry)}
                          >
                            View
                          </button>
                          <button
                            className="btn btn-danger btn-sm"
                            onClick={() => handleDelete(entry.Guid)}
                            title="Delete"
                            style={{ padding: '3px 8px', fontSize: 12, lineHeight: 1 }}
                          >
                            &#x2715;
                          </button>
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>

            {/* Pagination */}
            <div style={{
              display: 'flex', alignItems: 'center', justifyContent: 'space-between',
              marginTop: 16, paddingTop: 12, borderTop: '1px solid var(--border)',
            }}>
              <span style={{ fontSize: 13, color: 'var(--text-secondary)' }}>
                Showing {showingStart}-{showingEnd} of {totalRecords}
              </span>
              <div style={{ display: 'flex', gap: 8 }}>
                <button className="btn btn-secondary btn-sm" disabled={!canPrev} onClick={() => setOffset(o => Math.max(0, o - PAGE_SIZE))}>
                  Previous
                </button>
                <button className="btn btn-secondary btn-sm" disabled={!canNext} onClick={() => setOffset(o => o + PAGE_SIZE)}>
                  Next
                </button>
              </div>
            </div>
          </>
        )}
      </div>

      {/* Detail Modal - fullscreen, matching Verbex pattern */}
      {detailEntry && (() => {
        const method = (detailEntry.HttpMethod || '').toUpperCase()
        const status = detailEntry.StatusCode || 0
        const copyToClipboard = (text) => {
          if (navigator.clipboard && window.isSecureContext) navigator.clipboard.writeText(text)
          else { const ta = document.createElement('textarea'); ta.value = text; ta.style.position = 'fixed'; ta.style.left = '-9999px'; document.body.appendChild(ta); ta.select(); document.execCommand('copy'); document.body.removeChild(ta) }
        }
        const CopyBtn = ({ value }) => {
          const [copied, setCopied] = React.useState(false)
          return (
            <button
              onClick={() => { copyToClipboard(value); setCopied(true); setTimeout(() => setCopied(false), 1500) }}
              title="Copy"
              style={{ background: 'none', border: 'none', cursor: 'pointer', padding: '2px 6px', color: copied ? 'var(--success, #22c55e)' : 'var(--text-secondary)', fontSize: 14 }}
            >
              {copied ? '\u2713' : '\u2398'}
            </button>
          )
        }
        const DetailBlock = ({ title, children }) => {
          const [expanded, setExpanded] = React.useState(true)
          return (
            <div style={{ border: '1px solid var(--border)', borderRadius: 8, overflow: 'hidden' }}>
              <button
                type="button"
                onClick={() => setExpanded(!expanded)}
                style={{
                  display: 'flex', justifyContent: 'space-between', alignItems: 'center', width: '100%',
                  padding: '12px 16px', background: 'var(--bg)', border: 'none', cursor: 'pointer',
                  fontWeight: 600, fontSize: 14, color: 'var(--text)', textAlign: 'left',
                }}
              >
                <span>{title}</span>
                <span style={{ color: 'var(--text-secondary)', fontSize: 12 }}>{expanded ? 'Hide' : 'Show'}</span>
              </button>
              {expanded && (
                <div style={{ padding: 16, borderTop: '1px solid var(--border)' }}>
                  {children}
                </div>
              )}
            </div>
          )
        }
        const preStyle = {
          background: 'var(--bg)', padding: 12, borderRadius: 6, fontSize: 12,
          fontFamily: 'monospace', overflow: 'auto', maxHeight: '40vh', margin: 0,
          whiteSpace: 'pre-wrap', wordBreak: 'break-all',
        }
        return (
          <div className="modal-overlay" onClick={() => setDetailEntry(null)}>
            <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: '95vw', width: '95vw', maxHeight: '95vh', overflow: 'auto', padding: '24px 32px' }}>
              {/* Modal Header */}
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 24 }}>
                <h2 style={{ margin: 0 }}>Request Details</h2>
                <button
                  className="btn btn-secondary btn-sm"
                  onClick={() => setDetailEntry(null)}
                  style={{ fontSize: 18, lineHeight: 1, padding: '4px 12px' }}
                >
                  &#x2715;
                </button>
              </div>

              {/* Summary Grid - 4 columns like Verbex */}
              <div style={{
                display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12, marginBottom: 24,
              }}>
                {[
                  { label: 'Entry ID', value: detailEntry.Guid || '', mono: true, copyable: true },
                  { label: 'Status', value: String(status), color: getStatusColor(status) },
                  { label: 'Method', value: method },
                  { label: 'Duration', value: detailEntry.DurationMs != null ? `${detailEntry.DurationMs} ms` : '-' },
                ].map(item => (
                  <div key={item.label} style={{
                    display: 'flex', flexDirection: 'column', gap: 6, padding: 14,
                    border: '1px solid var(--border)', borderRadius: 10,
                    background: 'var(--card-bg)',
                  }}>
                    <span style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>{item.label}</span>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                      <strong style={{
                        fontSize: '1.05rem', color: item.color || 'inherit',
                        fontFamily: item.mono ? 'monospace' : 'inherit',
                        fontSize: item.mono ? '0.8rem' : '1.05rem',
                        wordBreak: 'break-all',
                      }}>
                        {item.value}
                      </strong>
                      {item.copyable && <CopyBtn value={item.value} />}
                    </div>
                  </div>
                ))}
              </div>

              {/* Route / URL */}
              <div style={{ marginBottom: 24 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 6 }}>
                  <span style={{ color: 'var(--text-secondary)', fontSize: 12, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.5px' }}>URL</span>
                  <CopyBtn value={detailEntry.RequestUrl || ''} />
                </div>
                <code style={{
                  display: 'block', background: 'var(--bg)', padding: '10px 14px',
                  borderRadius: 6, fontSize: 13, fontFamily: 'monospace', wordBreak: 'break-all',
                  border: '1px solid var(--border)',
                }}>
                  {detailEntry.RequestUrl}
                </code>
              </div>

              {/* Metadata Grid - 2 columns */}
              <div style={{
                display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, marginBottom: 24,
              }}>
                {[
                  { label: 'Source IP', value: detailEntry.SourceIp || '-' },
                  { label: 'Created UTC', value: detailEntry.CreatedUtc ? new Date(detailEntry.CreatedUtc).toLocaleString() : '-' },
                  { label: 'Success', value: detailEntry.Success ? 'Yes' : 'No', color: detailEntry.Success ? 'var(--success, #22c55e)' : 'var(--danger, #ef4444)' },
                  { label: 'Response Size', value: detailEntry.ResponseBodyLength != null ? formatBytes(detailEntry.ResponseBodyLength) : '-' },
                ].map(item => (
                  <div key={item.label} style={{ padding: '10px 14px', border: '1px solid var(--border)', borderRadius: 8, background: 'var(--card-bg)' }}>
                    <div style={{ fontSize: 12, color: 'var(--text-secondary)', marginBottom: 4 }}>{item.label}</div>
                    <div style={{ fontWeight: 600, fontSize: 14, color: item.color || 'inherit' }}>{item.value}</div>
                  </div>
                ))}
              </div>

              {/* 3-column info grid */}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 16, marginBottom: 24 }}>
                <DetailBlock title="Request Info">
                  <pre style={preStyle}>{JSON.stringify({
                    HttpMethod: detailEntry.HttpMethod,
                    RequestUrl: detailEntry.RequestUrl,
                    RequestContentType: detailEntry.RequestContentType || null,
                    RequestBodyLength: detailEntry.RequestBodyLength,
                    SourceIp: detailEntry.SourceIp,
                  }, null, 2)}</pre>
                </DetailBlock>

                <DetailBlock title="Response Info">
                  <pre style={preStyle}>{JSON.stringify({
                    StatusCode: detailEntry.StatusCode,
                    Success: detailEntry.Success,
                    ResponseContentType: detailEntry.ResponseContentType || null,
                    ResponseBodyLength: detailEntry.ResponseBodyLength,
                    DurationMs: detailEntry.DurationMs,
                  }, null, 2)}</pre>
                </DetailBlock>

                <DetailBlock title="Metadata">
                  <pre style={preStyle}>{JSON.stringify({
                    Id: detailEntry.Id,
                    Guid: detailEntry.Guid,
                    CreatedUtc: detailEntry.CreatedUtc,
                  }, null, 2)}</pre>
                </DetailBlock>
              </div>

              {/* Request Body */}
              <div style={{ marginBottom: 16 }}>
                <DetailBlock title="Request Body">
                  {detailEntry.RequestBody ? (
                    <div>
                      <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 4 }}>
                        <CopyBtn value={detailEntry.RequestBody} />
                      </div>
                      <pre style={preStyle}>{(() => {
                        try { return JSON.stringify(JSON.parse(detailEntry.RequestBody), null, 2) }
                        catch { return detailEntry.RequestBody }
                      })()}</pre>
                    </div>
                  ) : (
                    <p style={{ color: 'var(--text-secondary)', fontStyle: 'italic', margin: 0 }}>(empty)</p>
                  )}
                </DetailBlock>
              </div>

              {/* Response Body */}
              <div style={{ marginBottom: 24 }}>
                <DetailBlock title="Response Body">
                  {detailEntry.ResponseBody ? (
                    <div>
                      <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 4 }}>
                        <CopyBtn value={detailEntry.ResponseBody} />
                      </div>
                      <pre style={preStyle}>{(() => {
                        try { return JSON.stringify(JSON.parse(detailEntry.ResponseBody), null, 2) }
                        catch { return detailEntry.ResponseBody }
                      })()}</pre>
                    </div>
                  ) : (
                    <p style={{ color: 'var(--text-secondary)', fontStyle: 'italic', margin: 0 }}>(empty)</p>
                  )}
                </DetailBlock>
              </div>

              {/* Close */}
              <div className="modal-actions">
                <button className="btn btn-secondary" onClick={() => setDetailEntry(null)}>Close</button>
              </div>
            </div>
          </div>
        )
      })()}
    </div>
  )
}
