import React, { useState } from 'react'

function copyToClipboard(text) {
  if (navigator.clipboard && window.isSecureContext) {
    return navigator.clipboard.writeText(text)
  }
  const textarea = document.createElement('textarea')
  textarea.value = text
  textarea.style.position = 'fixed'
  textarea.style.left = '-9999px'
  document.body.appendChild(textarea)
  textarea.select()
  document.execCommand('copy')
  document.body.removeChild(textarea)
  return Promise.resolve()
}

export default function ErrorModal({ error, onClose }) {
  const [copied, setCopied] = useState(false)

  if (!error) return null

  const message = error.message || String(error)
  const status = error.status
  const data = error.responseData

  const fullText = data ? JSON.stringify(data, null, 2) : message

  const handleCopy = () => {
    copyToClipboard(fullText).then(() => {
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    })
  }

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()} style={{ maxWidth: 560 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <h2 style={{ color: 'var(--danger)' }}>
            {data?.Error || 'Error'}{status ? ` (${status})` : ''}
          </h2>
          <button
            type="button"
            onClick={handleCopy}
            title="Copy error details"
            style={{
              background: 'none',
              border: 'none',
              cursor: 'pointer',
              padding: 6,
              borderRadius: 4,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: copied ? 'var(--success)' : 'var(--text-secondary)',
              transition: 'color 0.2s'
            }}
          >
            {copied ? (
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="20 6 9 17 4 12" />
              </svg>
            ) : (
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <rect x="9" y="9" width="13" height="13" rx="2" ry="2" />
                <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1" />
              </svg>
            )}
          </button>
        </div>
        {data?.Description && (
          <p style={{ color: 'var(--text-secondary)', margin: '0 0 8px' }}>{data.Description}</p>
        )}
        {data?.Message && (
          <p style={{ margin: '0 0 12px', fontWeight: 500 }}>{data.Message}</p>
        )}
        {data?.Data && (
          <pre style={{ background: 'var(--bg)', padding: 12, borderRadius: 6, fontSize: 12, overflow: 'auto', maxHeight: '40vh', margin: '0 0 12px' }}>
            {JSON.stringify(data.Data, null, 2)}
          </pre>
        )}
        {!data && (
          <p style={{ margin: '0 0 12px' }}>{message}</p>
        )}
        <div className="modal-actions">
          <button type="button" className="btn btn-secondary" onClick={onClose}>Close</button>
        </div>
      </div>
    </div>
  )
}
