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

// Edit modal that only allows labels and tags to be changed. Content and
// embeddings are shown read-only and are preserved verbatim on save, since the
// server's document update rewrites every column (a partial payload would wipe
// embeddings and content length).
export default function EditDocumentModal({ document: initialDoc, tenantId, collectionId, onClose, onSaved, onError }) {
  const [doc, setDoc] = useState(initialDoc)
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)
  const [labels, setLabels] = useState(Array.isArray(initialDoc.Labels) ? [...initialDoc.Labels] : [])
  const [tags, setTags] = useState(
    initialDoc.Tags && typeof initialDoc.Tags === 'object' && Object.keys(initialDoc.Tags).length > 0
      ? Object.entries(initialDoc.Tags).map(([key, value]) => ({ key, value: value || '' }))
      : [{ key: '', value: '' }]
  )

  // Fetch the authoritative document (content + embeddings) so we can display it
  // read-only and send it back unchanged on save.
  useEffect(() => {
    let cancelled = false
    if (tenantId && collectionId && initialDoc?.DocumentKey) {
      setLoading(true)
      api.getDocument(tenantId, collectionId, initialDoc.DocumentKey)
        .then((full) => { if (!cancelled && full) setDoc((prev) => ({ ...prev, ...full })) })
        .catch(() => { /* fall back to the row we already have */ })
        .finally(() => { if (!cancelled) setLoading(false) })
    }
    return () => { cancelled = true }
  }, [tenantId, collectionId, initialDoc])

  const contentType = doc.ContentType || 'Text'
  const isText = !NON_TEXT_TYPES.includes(contentType)
  const rawContent = doc.Content || ''
  const embeddings = Array.isArray(doc.Embeddings) ? doc.Embeddings : null

  const handleSave = async () => {
    setSaving(true)
    try {
      const cleanLabels = labels.filter((l) => l.trim())
      const cleanTags = {}
      tags.forEach((t) => { if (t.key.trim()) cleanTags[t.key.trim()] = t.value })

      // Re-read the authoritative record right before saving so content, embeddings
      // and content length are never clobbered, even if the open-time fetch failed.
      let base = doc
      try {
        const full = await api.getDocument(tenantId, collectionId, initialDoc.DocumentKey)
        if (full) base = { ...doc, ...full }
      } catch { /* use what we have */ }

      await api.updateDocument(tenantId, collectionId, initialDoc.DocumentKey, {
        ...base,
        Labels: cleanLabels,
        Tags: cleanTags
      })
      onSaved && onSaved()
    } catch (err) {
      onError && onError(err)
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal modal-xl" onClick={(e) => e.stopPropagation()}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
          <h2 style={{ margin: 0 }}>Edit Document{loading ? ' …' : ''}</h2>
          <button type="button" className="btn btn-secondary" onClick={onClose}>Close</button>
        </div>

        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 8,
            padding: '10px 14px',
            marginBottom: 12,
            borderRadius: 6,
            background: 'var(--hover-bg)',
            border: '1px solid var(--border)',
            fontSize: 13,
            color: 'var(--text-secondary)'
          }}
        >
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ flexShrink: 0 }}>
            <circle cx="12" cy="12" r="10" />
            <line x1="12" y1="16" x2="12" y2="12" />
            <line x1="12" y1="8" x2="12.01" y2="8" />
          </svg>
          <span>Document content and embeddings cannot be edited. Only <strong>labels</strong> and <strong>tags</strong> can be changed.</span>
        </div>

        <div style={{ flex: 1, overflowY: 'auto', minHeight: 0 }}>
          {/* Metadata (read-only) */}
          <CollapsibleSection title="Metadata">
            <MetaRow label="ID">{doc.Id != null ? String(doc.Id) : '-'}</MetaRow>
            <MetaRow label="Key">{doc.DocumentKey ? <CopyId value={doc.DocumentKey} /> : '-'}</MetaRow>
            <MetaRow label="Document ID">{doc.DocumentId ? <CopyId value={doc.DocumentId} /> : '-'}</MetaRow>
            <MetaRow label="Length">{formatBytes(doc.ContentLength)}</MetaRow>
            <MetaRow label="Position">{doc.Position != null ? String(doc.Position) : '-'}</MetaRow>
            <MetaRow label="Content Type">{contentType}</MetaRow>
            <MetaRow label="Created">{doc.CreatedUtc ? new Date(doc.CreatedUtc).toLocaleString() : '-'}</MetaRow>
          </CollapsibleSection>

          {/* Labels (editable) */}
          <CollapsibleSection title="Labels">
            {labels.map((label, i) => (
              <div key={i} style={{ display: 'flex', gap: 8, marginBottom: 4, alignItems: 'center' }}>
                <input type="text" value={label} onChange={(e) => { const next = [...labels]; next[i] = e.target.value; setLabels(next) }} placeholder="Label" style={{ flex: 1 }} />
                <button type="button" className="btn btn-secondary" style={{ padding: '4px 8px' }} onClick={() => setLabels(labels.filter((_, j) => j !== i))}>×</button>
              </div>
            ))}
            <button type="button" className="btn btn-secondary" style={{ marginTop: 4, fontSize: 12 }} onClick={() => setLabels([...labels, ''])}>+ Add Label</button>
          </CollapsibleSection>

          {/* Tags (editable) */}
          <CollapsibleSection title="Tags">
            {tags.map((tag, i) => (
              <div key={i} style={{ display: 'flex', gap: 8, marginBottom: 4 }}>
                <input type="text" placeholder="Key" value={tag.key} onChange={(e) => { const next = [...tags]; next[i] = { ...next[i], key: e.target.value }; setTags(next) }} style={{ flex: 1 }} />
                <input type="text" placeholder="Value" value={tag.value} onChange={(e) => { const next = [...tags]; next[i] = { ...next[i], value: e.target.value }; setTags(next) }} style={{ flex: 1 }} />
                <button type="button" className="btn btn-secondary" style={{ padding: '4px 8px' }} onClick={() => { const next = tags.filter((_, j) => j !== i); setTags(next.length ? next : [{ key: '', value: '' }]) }}>×</button>
              </div>
            ))}
            <button type="button" className="btn btn-secondary" style={{ marginTop: 4, fontSize: 12 }} onClick={() => setTags([...tags, { key: '', value: '' }])}>+ Add Tag</button>
          </CollapsibleSection>

          {/* Content (read-only) */}
          <CollapsibleSection
            title="Body (read-only)"
            controls={isText && rawContent ? <CopyButton text={rawContent} title="Copy content" /> : null}
          >
            {isText ? (
              rawContent ? (
                <pre style={{ margin: 0, background: 'var(--bg)', padding: 12, borderRadius: 6, fontSize: 12, whiteSpace: 'pre-wrap', wordBreak: 'break-word', overflow: 'auto', maxHeight: '35vh' }}>
                  {rawContent}
                </pre>
              ) : (
                <div style={{ textAlign: 'center', color: 'var(--text-secondary)', padding: '40px 0', fontStyle: 'italic' }}>
                  {loading ? 'Loading…' : 'No content'}
                </div>
              )
            ) : (
              <div style={{ textAlign: 'center', color: 'var(--text-secondary)', padding: '60px 0', fontStyle: 'italic' }}>
                Cannot render non-text data
              </div>
            )}
          </CollapsibleSection>

          {/* Embeddings (read-only) */}
          <CollapsibleSection
            title={`Embeddings (read-only)${embeddings ? ` — ${embeddings.length}` : ''}`}
            controls={embeddings && embeddings.length > 0 ? <CopyButton text={JSON.stringify(embeddings)} title="Copy embeddings" /> : null}
          >
            {embeddings && embeddings.length > 0 ? (
              <pre style={{ margin: 0, background: 'var(--bg)', padding: 12, borderRadius: 6, fontSize: 12, whiteSpace: 'pre-wrap', wordBreak: 'break-word', overflow: 'auto', maxHeight: '25vh' }}>
                {`[${embeddings.join(', ')}]`}
              </pre>
            ) : (
              <div style={{ textAlign: 'center', color: 'var(--text-secondary)', padding: '30px 0', fontStyle: 'italic' }}>
                {loading ? 'Loading…' : 'No embeddings'}
              </div>
            )}
          </CollapsibleSection>
        </div>

        <div className="modal-actions" style={{ marginTop: 16 }}>
          <button type="button" className="btn btn-secondary" onClick={onClose} disabled={saving}>Cancel</button>
          <button type="button" className="btn btn-primary" onClick={handleSave} disabled={saving || loading}>
            {saving ? 'Saving...' : 'Save'}
          </button>
        </div>
      </div>
    </div>
  )
}
