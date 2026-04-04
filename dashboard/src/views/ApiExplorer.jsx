import React, { useState, useEffect, useCallback } from 'react'
import api from '../api/api.js'
import ErrorModal from '../components/ErrorModal.jsx'

const METHOD_COLORS = {
  GET: '#3b82f6',
  POST: '#22c55e',
  PUT: '#f59e0b',
  DELETE: '#ef4444',
  HEAD: '#6b7280',
}

const HISTORY_KEY = 'recalldb_api_explorer_history'
const MAX_HISTORY = 12

function methodBadgeStyle(method) {
  return {
    display: 'inline-block',
    background: METHOD_COLORS[method] || '#6b7280',
    color: 'white',
    padding: '2px 8px',
    borderRadius: 4,
    fontSize: 12,
    fontWeight: 700,
    fontFamily: 'monospace',
    marginRight: 8,
  }
}

function statusColor(status) {
  if (status >= 200 && status < 300) return '#22c55e'
  if (status >= 400 && status < 500) return '#f59e0b'
  if (status >= 500) return '#ef4444'
  return '#6b7280'
}

function parseOperations(spec) {
  if (!spec || !spec.paths) return []
  const operations = []
  for (const [path, methods] of Object.entries(spec.paths)) {
    for (const [method, op] of Object.entries(methods)) {
      if (['get', 'post', 'put', 'delete', 'head'].includes(method)) {
        operations.push({
          id: op.operationId || `${method}_${path}`,
          method: method.toUpperCase(),
          path,
          summary: op.summary || '',
          tag: op.tags?.[0] || 'Other',
          parameters: [...(methods.parameters || []), ...(op.parameters || [])],
          hasBody: ['post', 'put', 'patch'].includes(method),
        })
      }
    }
  }
  return operations
}

function groupByTag(operations) {
  const groups = {}
  for (const op of operations) {
    if (!groups[op.tag]) groups[op.tag] = []
    groups[op.tag].push(op)
  }
  return groups
}

function extractPathParams(path) {
  const matches = path.match(/\{([^}]+)\}/g)
  if (!matches) return []
  return matches.map((m) => m.slice(1, -1))
}

function loadHistory() {
  try {
    const raw = localStorage.getItem(HISTORY_KEY)
    return raw ? JSON.parse(raw) : []
  } catch {
    return []
  }
}

function saveHistory(history) {
  localStorage.setItem(HISTORY_KEY, JSON.stringify(history.slice(0, MAX_HISTORY)))
}

export default function ApiExplorer() {
  const [spec, setSpec] = useState(null)
  const [operations, setOperations] = useState([])
  const [loading, setLoading] = useState(true)
  const [specError, setSpecError] = useState(null)
  const [error, setError] = useState(null)

  const [selectedOpId, setSelectedOpId] = useState('')
  const [pathParams, setPathParams] = useState({})
  const [queryParams, setQueryParams] = useState({})
  const [bodyText, setBodyText] = useState('')

  const [sending, setSending] = useState(false)
  const [response, setResponse] = useState(null)
  const [headersExpanded, setHeadersExpanded] = useState(false)

  const [history, setHistory] = useState(() => loadHistory())

  const [specRetryKey, setSpecRetryKey] = useState(0)

  // Load OpenAPI spec
  useEffect(() => {
    let cancelled = false
    async function fetchSpec() {
      setLoading(true)
      setSpecError(null)
      try {
        // Try authenticated API request first
        const data = await api.request('GET', '/openapi.json')
        if (cancelled) return
        setSpec(data)
        setOperations(parseOperations(data))
      } catch (firstErr) {
        if (cancelled) return
        // Fallback: direct fetch without custom headers (OpenAPI endpoint may have its own CORS)
        try {
          const res = await fetch(api.baseUrl + '/openapi.json')
          if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`)
          const data = await res.json()
          if (cancelled) return
          setSpec(data)
          setOperations(parseOperations(data))
        } catch (secondErr) {
          if (!cancelled) {
            const msg = secondErr.message || firstErr.message || 'Failed to load OpenAPI specification'
            setSpecError(`${msg} (tried authenticated and direct fetch)`)
          }
        }
      } finally {
        if (!cancelled) setLoading(false)
      }
    }
    fetchSpec()
    return () => { cancelled = true }
  }, [specRetryKey])

  const selectedOp = operations.find((op) => op.id === selectedOpId) || null

  // Reset param state when operation changes
  useEffect(() => {
    if (!selectedOp) {
      setPathParams({})
      setQueryParams({})
      setBodyText('')
      return
    }
    const pParams = extractPathParams(selectedOp.path)
    const initial = {}
    for (const p of pParams) initial[p] = ''
    setPathParams(initial)

    const qParams = (selectedOp.parameters || []).filter((p) => p.in === 'query')
    const qInitial = {}
    for (const p of qParams) qInitial[p.name] = ''
    setQueryParams(qInitial)

    setBodyText(selectedOp.hasBody ? '{\n  \n}' : '')
  }, [selectedOpId])

  const handleSelectOperation = useCallback((e) => {
    setSelectedOpId(e.target.value)
    setResponse(null)
    setHeadersExpanded(false)
  }, [])

  const handlePathParamChange = useCallback((name, value) => {
    setPathParams((prev) => ({ ...prev, [name]: value }))
  }, [])

  const handleQueryParamChange = useCallback((name, value) => {
    setQueryParams((prev) => ({ ...prev, [name]: value }))
  }, [])

  const handleSend = useCallback(async () => {
    if (!selectedOp) return
    setSending(true)
    setResponse(null)
    setHeadersExpanded(false)

    try {
      // Build resolved path
      let resolvedPath = selectedOp.path
      for (const [key, val] of Object.entries(pathParams)) {
        resolvedPath = resolvedPath.replace(`{${key}}`, encodeURIComponent(val))
      }

      // Build query string
      const qParts = []
      for (const [key, val] of Object.entries(queryParams)) {
        if (val !== '') {
          qParts.push(encodeURIComponent(key) + '=' + encodeURIComponent(val))
        }
      }
      const queryString = qParts.length > 0 ? '?' + qParts.join('&') : ''

      const url = api.baseUrl + resolvedPath + queryString
      const headers = {
        'Content-Type': 'application/json',
      }
      if (api.bearerToken) {
        headers['Authorization'] = 'Bearer ' + api.bearerToken
      }

      const fetchOpts = {
        method: selectedOp.method,
        headers,
      }
      if (selectedOp.hasBody && bodyText.trim()) {
        fetchOpts.body = bodyText
      }

      const startTime = performance.now()
      const res = await fetch(url, fetchOpts)
      const elapsed = Math.round(performance.now() - startTime)

      const resHeaders = {}
      res.headers.forEach((value, key) => {
        resHeaders[key] = value
      })

      const text = await res.text()
      let json = null
      try {
        json = JSON.parse(text)
      } catch {}

      setResponse({
        status: res.status,
        statusText: res.statusText,
        elapsed,
        headers: resHeaders,
        body: json !== null ? JSON.stringify(json, null, 2) : text,
        isJson: json !== null,
      })

      // Add to history
      const entry = {
        timestamp: new Date().toISOString(),
        method: selectedOp.method,
        path: resolvedPath + queryString,
        status: res.status,
        opId: selectedOp.id,
      }
      const updated = [entry, ...history.filter((h) => !(h.method === entry.method && h.path === entry.path))].slice(0, MAX_HISTORY)
      setHistory(updated)
      saveHistory(updated)
    } catch (err) {
      setError(err)
    } finally {
      setSending(false)
    }
  }, [selectedOp, pathParams, queryParams, bodyText, history])

  const handleHistoryClick = useCallback((entry) => {
    if (entry.opId && operations.find((op) => op.id === entry.opId)) {
      setSelectedOpId(entry.opId)
      setResponse(null)
    }
  }, [operations])

  const handleClearHistory = useCallback(() => {
    setHistory([])
    localStorage.removeItem(HISTORY_KEY)
  }, [])

  const grouped = groupByTag(operations)
  const pathParamKeys = selectedOp ? extractPathParams(selectedOp.path) : []
  const queryParamDefs = selectedOp
    ? (selectedOp.parameters || []).filter((p) => p.in === 'query')
    : []

  // Loading state
  if (loading) {
    return (
      <div>
        <div className="page-header">
          <h1>API Explorer</h1>
        </div>
        <div className="card" style={{ padding: 32, textAlign: 'center', color: 'var(--text-secondary)' }}>
          Loading OpenAPI specification...
        </div>
      </div>
    )
  }

  // Error loading spec
  if (specError) {
    return (
      <div>
        <div className="page-header">
          <h1>API Explorer</h1>
        </div>
        <div className="card" style={{ padding: 32 }}>
          <p style={{ color: '#ef4444', marginBottom: 12 }}>Failed to load OpenAPI specification</p>
          <p style={{ color: 'var(--text-secondary)', fontSize: 14 }}>{specError}</p>
          <button
            className="btn btn-primary"
            style={{ marginTop: 16 }}
            onClick={() => setSpecRetryKey(k => k + 1)}
          >
            Retry
          </button>
        </div>
      </div>
    )
  }

  return (
    <div>
      <div className="page-header">
        <h1>API Explorer</h1>
      </div>

      <ErrorModal error={error} onClose={() => setError(null)} />

      {/* Operation Selector */}
      <div className="card" style={{ marginBottom: 16 }}>
        <div className="form-group">
          <label>Endpoint</label>
          <select
            value={selectedOpId}
            onChange={handleSelectOperation}
            style={{
              width: '100%',
              padding: '8px 12px',
              borderRadius: 6,
              border: '1px solid var(--border)',
              background: 'var(--bg)',
              color: 'var(--text)',
              fontSize: 14,
              fontFamily: 'monospace',
            }}
          >
            <option value="">-- Select an endpoint --</option>
            {Object.entries(grouped).sort(([a], [b]) => a.localeCompare(b)).map(([tag, ops]) => (
              <optgroup key={tag} label={tag}>
                {ops.map((op) => (
                  <option key={op.id} value={op.id}>
                    {op.method.padEnd(7)} {op.path}{op.summary ? ` - ${op.summary}` : ''}
                  </option>
                ))}
              </optgroup>
            ))}
          </select>
        </div>
        {operations.length > 0 && (
          <p style={{ margin: '8px 0 0', fontSize: 12, color: 'var(--text-secondary)' }}>
            {operations.length} endpoints available
          </p>
        )}
      </div>

      {/* Request Builder */}
      {selectedOp && (
        <div className="card" style={{ marginBottom: 16 }}>
          <div style={{ marginBottom: 16, display: 'flex', alignItems: 'center', gap: 8 }}>
            <span style={methodBadgeStyle(selectedOp.method)}>{selectedOp.method}</span>
            <span style={{ fontFamily: 'monospace', fontSize: 14 }}>{selectedOp.path}</span>
            {selectedOp.summary && (
              <span style={{ color: 'var(--text-secondary)', fontSize: 13, marginLeft: 8 }}>
                {selectedOp.summary}
              </span>
            )}
          </div>

          {/* Path Parameters */}
          {pathParamKeys.length > 0 && (
            <div style={{ marginBottom: 16 }}>
              <h3 style={{ fontSize: 14, fontWeight: 600, marginBottom: 8 }}>Path Parameters</h3>
              {pathParamKeys.map((name) => (
                <div className="form-group" key={name} style={{ marginBottom: 8 }}>
                  <label style={{ fontFamily: 'monospace', fontSize: 13 }}>{`{${name}}`}</label>
                  <input
                    type="text"
                    value={pathParams[name] || ''}
                    onChange={(e) => handlePathParamChange(name, e.target.value)}
                    placeholder={name}
                    style={{
                      width: '100%',
                      padding: '6px 10px',
                      borderRadius: 4,
                      border: '1px solid var(--border)',
                      background: 'var(--bg)',
                      color: 'var(--text)',
                      fontSize: 13,
                      fontFamily: 'monospace',
                    }}
                  />
                </div>
              ))}
            </div>
          )}

          {/* Query Parameters */}
          {queryParamDefs.length > 0 && (
            <div style={{ marginBottom: 16 }}>
              <h3 style={{ fontSize: 14, fontWeight: 600, marginBottom: 8 }}>Query Parameters</h3>
              {queryParamDefs.map((param) => (
                <div className="form-group" key={param.name} style={{ marginBottom: 8 }}>
                  <label style={{ fontFamily: 'monospace', fontSize: 13 }}>
                    {param.name}
                    {param.required && <span style={{ color: '#ef4444', marginLeft: 4 }}>*</span>}
                  </label>
                  <input
                    type="text"
                    value={queryParams[param.name] || ''}
                    onChange={(e) => handleQueryParamChange(param.name, e.target.value)}
                    placeholder={param.description || param.name}
                    style={{
                      width: '100%',
                      padding: '6px 10px',
                      borderRadius: 4,
                      border: '1px solid var(--border)',
                      background: 'var(--bg)',
                      color: 'var(--text)',
                      fontSize: 13,
                      fontFamily: 'monospace',
                    }}
                  />
                </div>
              ))}
            </div>
          )}

          {/* Request Body */}
          {selectedOp.hasBody && (
            <div style={{ marginBottom: 16 }}>
              <h3 style={{ fontSize: 14, fontWeight: 600, marginBottom: 8 }}>Request Body (JSON)</h3>
              <div className="form-group">
                <textarea
                  value={bodyText}
                  onChange={(e) => setBodyText(e.target.value)}
                  rows={10}
                  style={{
                    width: '100%',
                    padding: '8px 10px',
                    borderRadius: 4,
                    border: '1px solid var(--border)',
                    background: 'var(--bg)',
                    color: 'var(--text)',
                    fontSize: 13,
                    fontFamily: 'monospace',
                    resize: 'vertical',
                  }}
                  placeholder='{ "key": "value" }'
                />
              </div>
            </div>
          )}

          {/* Send Button */}
          <button
            className="btn btn-primary"
            onClick={handleSend}
            disabled={sending}
            style={{ minWidth: 120 }}
          >
            {sending ? 'Sending...' : 'Send Request'}
          </button>
        </div>
      )}

      {/* Response Viewer */}
      {response && (
        <div className="card" style={{ marginBottom: 16 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 12 }}>
            <span
              style={{
                display: 'inline-block',
                background: statusColor(response.status),
                color: 'white',
                padding: '2px 10px',
                borderRadius: 4,
                fontSize: 13,
                fontWeight: 700,
                fontFamily: 'monospace',
              }}
            >
              {response.status} {response.statusText}
            </span>
            <span style={{ fontSize: 13, color: 'var(--text-secondary)' }}>
              {response.elapsed}ms
            </span>
          </div>

          <h3 style={{ fontSize: 14, fontWeight: 600, marginBottom: 8 }}>Response Body</h3>
          <pre
            style={{
              background: 'var(--bg)',
              padding: 12,
              borderRadius: 6,
              fontSize: 12,
              overflow: 'auto',
              maxHeight: '60vh',
              margin: 0,
            }}
          >
            {response.body || '(empty response)'}
          </pre>

          {/* Response Headers */}
          <div style={{ marginTop: 12 }}>
            <button
              className="btn btn-sm"
              onClick={() => setHeadersExpanded(!headersExpanded)}
              style={{ fontSize: 12 }}
            >
              {headersExpanded ? 'Hide' : 'Show'} Response Headers
            </button>
            {headersExpanded && (
              <pre
                style={{
                  background: 'var(--bg)',
                  padding: 12,
                  borderRadius: 6,
                  fontSize: 12,
                  overflow: 'auto',
                  maxHeight: '30vh',
                  marginTop: 8,
                }}
              >
                {Object.entries(response.headers)
                  .map(([k, v]) => `${k}: ${v}`)
                  .join('\n')}
              </pre>
            )}
          </div>
        </div>
      )}

      {/* Request History */}
      {history.length > 0 && (
        <div className="card">
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 12 }}>
            <h3 style={{ fontSize: 14, fontWeight: 600, margin: 0 }}>Recent Requests</h3>
            <button className="btn btn-sm" onClick={handleClearHistory} style={{ fontSize: 11 }}>
              Clear
            </button>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            {history.map((entry, idx) => (
              <div
                key={idx}
                onClick={() => handleHistoryClick(entry)}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 8,
                  padding: '6px 8px',
                  borderRadius: 4,
                  cursor: 'pointer',
                  fontSize: 13,
                  background: 'var(--bg)',
                  transition: 'opacity 0.15s',
                }}
                onMouseEnter={(e) => { e.currentTarget.style.opacity = '0.8' }}
                onMouseLeave={(e) => { e.currentTarget.style.opacity = '1' }}
              >
                <span style={methodBadgeStyle(entry.method)}>{entry.method}</span>
                <span
                  style={{
                    fontFamily: 'monospace',
                    fontSize: 12,
                    flex: 1,
                    overflow: 'hidden',
                    textOverflow: 'ellipsis',
                    whiteSpace: 'nowrap',
                  }}
                >
                  {entry.path}
                </span>
                <span
                  style={{
                    color: statusColor(entry.status),
                    fontWeight: 600,
                    fontSize: 12,
                    fontFamily: 'monospace',
                  }}
                >
                  {entry.status}
                </span>
                <span style={{ color: 'var(--text-secondary)', fontSize: 11, whiteSpace: 'nowrap' }}>
                  {new Date(entry.timestamp).toLocaleTimeString()}
                </span>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
