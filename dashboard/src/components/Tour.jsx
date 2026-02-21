import React, { useState, useEffect, useCallback } from 'react'
import './Tour.css'

const TOUR_STEPS = [
  { target: '[data-tour="dashboard"]', title: 'Dashboard', description: 'View system health, statistics, and an overview of your RecallDB instance at a glance.' },
  { target: '[data-tour="tenants"]', title: 'Tenants', description: 'Tenants are isolated workspaces. Each tenant has its own users, credentials, and collections.' },
  { target: '[data-tour="users"]', title: 'Users', description: 'Manage users within each tenant. Users can authenticate via email/password or API credentials.' },
  { target: '[data-tour="credentials"]', title: 'Credentials', description: 'Create and manage API bearer tokens for programmatic access to your tenant\'s data.' },
  { target: '[data-tour="collections"]', title: 'Collections', description: 'Collections store vector embeddings. Each collection has a fixed dimensionality for its vectors.' },
  { target: '[data-tour="documents"]', title: 'Documents', description: 'Browse and manage documents across all your tenants and collections.' },
  { target: '[data-tour="search"]', title: 'Search', description: 'Perform vector similarity searches to find the most relevant documents in your collections.' },
  { target: '[data-tour="query"]', title: 'Query', description: 'Build advanced compound queries combining vector search with metadata filters.' },
  { target: '[data-tour="server-badge"]', title: 'Server URL', description: 'Shows the RecallDB server instance you are currently connected to.' },
  { target: '[data-tour="email-badge"]', title: 'Your Identity', description: 'Displays the email address or role of the currently authenticated user.' },
  { target: '[data-tour="tenant-badge"]', title: 'Active Tenant', description: 'Shows which tenant workspace is currently selected for navigation.' },
  { target: '[data-tour="theme-toggle"]', title: 'Theme Toggle', description: 'Switch between light and dark mode to suit your preference.' },
  { target: '[data-tour="logout"]', title: 'Logout', description: 'Sign out of the dashboard and return to the login screen.' },
]

export default function Tour({ onClose, onComplete }) {
  const [phase, setPhase] = useState('welcome') // 'welcome' | 'touring'
  const [stepIndex, setStepIndex] = useState(0)
  const [spotlightRect, setSpotlightRect] = useState(null)

  const getVisibleSteps = useCallback(() => {
    return TOUR_STEPS.filter(step => document.querySelector(step.target))
  }, [])

  const updateSpotlight = useCallback(() => {
    const visibleSteps = getVisibleSteps()
    if (stepIndex >= visibleSteps.length) return
    const el = document.querySelector(visibleSteps[stepIndex].target)
    if (el) {
      const rect = el.getBoundingClientRect()
      setSpotlightRect({
        top: rect.top - 4,
        left: rect.left - 4,
        width: rect.width + 8,
        height: rect.height + 8,
      })
    }
  }, [stepIndex, getVisibleSteps])

  useEffect(() => {
    if (phase !== 'touring') return
    updateSpotlight()
    window.addEventListener('resize', updateSpotlight)
    return () => window.removeEventListener('resize', updateSpotlight)
  }, [phase, updateSpotlight])

  const visibleSteps = getVisibleSteps()
  const currentStep = visibleSteps[stepIndex]

  const handleNext = () => {
    if (stepIndex < visibleSteps.length - 1) {
      setStepIndex(stepIndex + 1)
    } else {
      localStorage.setItem('recalldb_tour_completed', 'true')
      onComplete()
    }
  }

  const handlePrev = () => {
    if (stepIndex > 0) setStepIndex(stepIndex - 1)
  }

  const handleSkip = () => {
    localStorage.setItem('recalldb_tour_completed', 'true')
    onClose()
  }

  const handleStartTour = () => {
    setPhase('touring')
    setStepIndex(0)
  }

  // Calculate tooltip position
  const getTooltipStyle = () => {
    if (!spotlightRect) return { top: '50%', left: '50%', transform: 'translate(-50%, -50%)' }
    const tooltipWidth = 320
    const padding = 16
    let top, left

    // Prefer placing tooltip to the right of sidebar items, below topbar items
    if (spotlightRect.left < 250) {
      // Sidebar element - place to the right
      left = spotlightRect.left + spotlightRect.width + padding
      top = spotlightRect.top
      // Ensure doesn't go off bottom
      if (top + 200 > window.innerHeight) top = window.innerHeight - 220
    } else {
      // Topbar element - place below
      left = spotlightRect.left
      top = spotlightRect.top + spotlightRect.height + padding
      // Ensure doesn't go off right
      if (left + tooltipWidth > window.innerWidth) left = window.innerWidth - tooltipWidth - padding
    }

    if (top < padding) top = padding
    if (left < padding) left = padding

    return { top, left }
  }

  if (phase === 'welcome') {
    return (
      <div className="tour-welcome-overlay">
        <div className="tour-welcome-modal">
          <h2>Welcome to RecallDB</h2>
          <p>
            RecallDB is an opinionated data persistence and retrieval layer built on pgvector to power AI and RAG use cases with intelligent multi-modal retrieval. It stores raw content, metadata, labels, tags, and hashes alongside vector embeddings — so your retrieval pipeline has full context, not just similarity scores.
          </p>
          <p>
            Multi-tenant, vendor-agnostic, and deployable in seconds — RecallDB is the persistence and retrieval layer your RAG pipeline is missing. Take a quick tour to learn your way around the dashboard.
          </p>
          <div className="tour-welcome-actions">
            <button className="btn btn-secondary" onClick={handleSkip}>Skip Tour</button>
            <button className="btn btn-primary" onClick={handleStartTour}>Start Tour</button>
          </div>
        </div>
      </div>
    )
  }

  return (
    <>
      <div className="tour-spotlight-overlay" onClick={handleSkip} />
      {spotlightRect && (
        <div className="tour-spotlight" style={spotlightRect} />
      )}
      {currentStep && (
        <div className="tour-tooltip" style={getTooltipStyle()}>
          <h3>{currentStep.title}</h3>
          <p>{currentStep.description}</p>
          <div className="tour-tooltip-footer">
            <span className="tour-step-counter">{stepIndex + 1} of {visibleSteps.length}</span>
            <div className="tour-tooltip-buttons">
              {stepIndex > 0 && (
                <button className="btn btn-secondary btn-sm" onClick={handlePrev}>Back</button>
              )}
              <button className="btn btn-primary btn-sm" onClick={handleNext}>
                {stepIndex === visibleSteps.length - 1 ? 'Finish' : 'Next'}
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}
