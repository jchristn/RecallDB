import React, { useState, useRef, useEffect } from 'react'
import './ActionMenu.css'

export default function ActionMenu({ actions }) {
  const [isOpen, setIsOpen] = useState(false)
  const [position, setPosition] = useState({ top: 0, left: 0 })
  const menuRef = useRef(null)
  const triggerRef = useRef(null)

  useEffect(() => {
    const handleClickOutside = (event) => {
      if (menuRef.current && !menuRef.current.contains(event.target)) {
        setIsOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  useEffect(() => {
    if (!isOpen) return
    const handleScroll = () => setIsOpen(false)
    window.addEventListener('scroll', handleScroll, true)
    return () => window.removeEventListener('scroll', handleScroll, true)
  }, [isOpen])

  const handleTriggerClick = (e) => {
    e.stopPropagation()
    e.preventDefault()
    if (!isOpen && triggerRef.current) {
      const rect = triggerRef.current.getBoundingClientRect()
      setPosition({ top: rect.bottom + 2, left: rect.right })
    }
    setIsOpen(!isOpen)
  }

  const handleAction = (e, action) => {
    e.stopPropagation()
    e.preventDefault()
    setIsOpen(false)
    if (action.onClick) {
      action.onClick()
    }
  }

  return (
    <div className="action-menu" ref={menuRef} onClick={(e) => e.stopPropagation()}>
      <button className="action-menu-trigger" ref={triggerRef} onClick={handleTriggerClick}>
        <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor">
          <circle cx="8" cy="3" r="1.5" />
          <circle cx="8" cy="8" r="1.5" />
          <circle cx="8" cy="13" r="1.5" />
        </svg>
      </button>
      {isOpen && (
        <div
          className="action-menu-dropdown"
          style={{ top: position.top, left: position.left }}
        >
          {actions.map((action, index) =>
            action.divider ? (
              <div key={index} className="action-menu-divider"></div>
            ) : (
              <button
                key={index}
                className={`action-menu-item ${action.danger ? 'danger' : ''}`}
                onClick={(e) => handleAction(e, action)}
                disabled={action.disabled}
              >
                {action.icon && <span className="action-icon">{action.icon}</span>}
                {action.label}
              </button>
            )
          )}
        </div>
      )}
    </div>
  )
}
