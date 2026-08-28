import React, { useState } from 'react'

// A collapsible panel. Optional `controls` render on the right of the header and
// do not toggle the section when clicked.
export default function CollapsibleSection({ title, controls, defaultOpen = true, children }) {
  const [open, setOpen] = useState(defaultOpen)
  return (
    <div className="collapsible-section">
      <div className="collapsible-header" onClick={() => setOpen(!open)}>
        <span>{title}</span>
        <span style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          {open && controls && (
            <span style={{ display: 'flex', alignItems: 'center', gap: 4 }} onClick={(e) => e.stopPropagation()}>
              {controls}
            </span>
          )}
          <span>{open ? '▲' : '▼'}</span>
        </span>
      </div>
      {open && <div className="collapsible-body">{children}</div>}
    </div>
  )
}
