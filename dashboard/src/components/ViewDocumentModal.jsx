import React, { useState, useEffect } from 'react'
import api from '../api/api.js'
import CopyButton from './CopyButton.jsx'
import CopyId from './CopyId.jsx'
import CollapsibleSection from './CollapsibleSection.jsx'

// Content types whose body cannot be rendered as text.
const NON_TEXT_TYPES = ['Binary', 'Image']

function formatBytes(bytes) {
  if (bytes == null) return '-'
  if (bytes === 0) return '0 B'
  const units = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(1024))
  return (bytes / Math.pow(1024, i)).toFixed(i > 0 ? 1 : 0) + ' ' + units[i]
}

function MetaRow({ label, children }) {
  return (
    <div style={{ display: 'flex', gap: 12, padding: '6px 0', borderBottom: '1px solid var(--border)' }}>
      <div style={{ width: 130, flexShrink: 0, fontSize: 13, fontWeight: 600, color: 'var(--text-secondary)' }}>{label}</div>
      <div style={{ flex: 1, fontSize: 13, minWidth: 0, wordBreak: 'break-word' }}>{children}</div>
    </div>
  )
}

function tryPrettyPrint(text) {
  if (!text) return text
  try {
    return JSON.stringify(JSON.parse(text), null, 2)
  } catch {
    return null
  }
}

export default function ViewDocumentModal({ document: initialDoc, tenantId, collectionId, onClose }) {
  const [doc, setDoc] = useState(initialDoc)
  const [loading, setLoading] = useState(false)
  const [formatted, setFormatted] = useState(false)

  // Fetch the full document (content + embeddings) when we can, so the modal is
  // complete even if the source row omitted heavy fields. Falls back silently to
  // the row we already have.
  useEffect(() => {
    let cancelled = false
    if (tenantId && collectionId && initialDoc?.DocumentKey) {
      setLoading(true)
      api.getDocument(tenantId, collectionId, initialDoc.DocumentKey)
        .then((full) => { if (!cancelled && full) setDoc({ ...initialDoc, ...full }) })
        .catch(() => { /* keep the row data we already have */ })
        .finally(() => { if (!cancelled) setLoading(false) })
    }
    return () => { cancelled = true }
  }, [tenantId, collectionId, initialDoc])

  const contentType = doc.ContentType || 'Text'
  const isText = !NON_TEXT_TYPES.includes(contentType)
  const rawContent = doc.Content || ''
  const prettyContent = formatted ? (tryPrettyPrint(rawContent) ?? rawContent) : rawContent
  const canFormat = isText && tryPrettyPrint(rawContent) !== null
  const embeddings = Array.isArray(doc.Embeddings) ? doc.Embeddings : null

  const labels = Array.isArray(doc.Labels) ? doc.Labels : []
  const tags = doc.Tags && typeof doc.Tags === 'object' ? Object.entries(doc.Tags) : []

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal modal-xl" onClick={(e) => e.stopPropagation()}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
          <h2 style={{ margin: 0 }}>View Document{loading ? ' …' : ''}</h2>
          <button type="button" className="btn btn-secondary" onClick={onClose}>Close</button>
        </div>

        <div style={{ flex: 1, overflowY: 'auto', minHeight: 0 }}>
          {/* Top: metadata */}
          <CollapsibleSection title="Metadata">
            <MetaRow label="ID">{doc.Id != null ? String(doc.Id) : '-'}</MetaRow>
            <MetaRow label="Key">{doc.DocumentKey ? <CopyId value={doc.DocumentKey} /> : '-'}</MetaRow>
            <MetaRow label="Document ID">{doc.DocumentId ? <CopyId value={doc.DocumentId} /> : '-'}</MetaRow>
            <MetaRow label="Length">{formatBytes(doc.ContentLength)}</MetaRow>
            <MetaRow label="Position">{doc.Position != null ? String(doc.Position) : '-'}</MetaRow>
            <MetaRow label="Content Type">{contentType}</MetaRow>
            <MetaRow label="Labels">
              {labels.length > 0 ? (
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                  {labels.map((l, i) => <span key={i} className="tag-badge">{l}</span>)}
                </div>
              ) : <span style={{ color: 'var(--text-secondary)' }}>None</span>}
            </MetaRow>
            <MetaRow label="Tags">
              {tags.length > 0 ? (
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                  {tags.map(([k, v], i) => <span key={i} className="tag-badge">{k}{v ? `: ${v}` : ''}</span>)}
                </div>
              ) : <span style={{ color: 'var(--text-secondary)' }}>None</span>}
            </MetaRow>
            <MetaRow label="Created">{doc.CreatedUtc ? new Date(doc.CreatedUtc).toLocaleString() : '-'}</MetaRow>
          </CollapsibleSection>

          {/* Middle: body */}
          <CollapsibleSection
            title="Body"
            controls={isText && rawContent ? (
              <>
                <button
                  type="button"
                  className="btn btn-secondary"
                  style={{ padding: '2px 10px', fontSize: 12 }}
                  disabled={!canFormat}
                  title={canFormat ? 'Pretty-print' : 'Nothing to format'}
                  onClick={() => setFormatted((f) => !f)}
                >
                  {formatted ? 'Raw' : 'Format'}
                </button>
                <CopyButton text={prettyContent} title="Copy content" />
              </>
            ) : null}
          >
            {isText ? (
              rawContent ? (
                <pre style={{ margin: 0, background: 'var(--bg)', padding: 12, borderRadius: 6, fontSize: 12, whiteSpace: 'pre-wrap', wordBreak: 'break-word', overflow: 'auto', maxHeight: '45vh' }}>
                  {prettyContent}
                </pre>
              ) : (
                <div style={{ textAlign: 'center', color: 'var(--text-secondary)', padding: '40px 0', fontStyle: 'italic' }}>
                  No content
                </div>
              )
            ) : (
              <div style={{ textAlign: 'center', color: 'var(--text-secondary)', padding: '60px 0', fontStyle: 'italic' }}>
                Cannot render non-text data
              </div>
            )}
          </CollapsibleSection>

          {/* Bottom: embeddings */}
          <CollapsibleSection
            title={`Embeddings${embeddings ? ` (${embeddings.length})` : ''}`}
            controls={embeddings && embeddings.length > 0 ? (
              <CopyButton text={JSON.stringify(embeddings)} title="Copy embeddings" />
            ) : null}
          >
            {embeddings && embeddings.length > 0 ? (
              <pre style={{ margin: 0, background: 'var(--bg)', padding: 12, borderRadius: 6, fontSize: 12, whiteSpace: 'pre-wrap', wordBreak: 'break-word', overflow: 'auto', maxHeight: '30vh' }}>
                {`[${embeddings.join(', ')}]`}
              </pre>
            ) : (
              <div style={{ textAlign: 'center', color: 'var(--text-secondary)', padding: '30px 0', fontStyle: 'italic' }}>
                {loading ? 'Loading…' : 'No embeddings'}
              </div>
            )}
          </CollapsibleSection>
        </div>
      </div>
    </div>
  )
}
