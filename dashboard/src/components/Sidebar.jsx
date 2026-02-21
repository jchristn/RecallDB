import React from 'react'
import { NavLink } from 'react-router-dom'
import { useAuth } from '../context/AuthContext.jsx'

export default function Sidebar({ onStartTour, onStartWizard }) {
  const { tenant } = useAuth()
  const tenantId = tenant?.Id || 'default'

  return (
    <div className="sidebar">
      <div className="sidebar-header">
        <img src="/logo-white.png" alt="RecallDB" style={{ width: 140 }} />
      </div>
      <nav>
        <NavLink to="/" end data-tour="dashboard">Dashboard</NavLink>
        <NavLink to="/tenants" end data-tour="tenants">Tenants</NavLink>
        <NavLink to={`/tenants/${tenantId}/users`} data-tour="users">Users</NavLink>
        <NavLink to={`/tenants/${tenantId}/credentials`} data-tour="credentials">Credentials</NavLink>
        <NavLink to={`/tenants/${tenantId}/collections`} data-tour="collections">Collections</NavLink>
        <NavLink to="/documents" data-tour="documents">Documents</NavLink>
        <NavLink to="/search" data-tour="search">Search</NavLink>
        <NavLink to="/query" data-tour="query">Query</NavLink>
      </nav>
      <div className="sidebar-footer">
        <button onClick={onStartTour} style={{ marginBottom: 8 }}>Take Tour</button>
        <button onClick={onStartWizard}>Setup Wizard</button>
      </div>
    </div>
  )
}
