import React from 'react'
import { NavLink } from 'react-router-dom'
import { useAuth } from '../context/AuthContext.jsx'

export default function Sidebar() {
  const { tenant } = useAuth()
  const tenantId = tenant?.Id || 'default'

  return (
    <div className="sidebar">
      <div className="sidebar-header">
        <img src="/logo-white.png" alt="RecallDB" style={{ width: 140 }} />
      </div>
      <nav>
        <NavLink to="/" end>Dashboard</NavLink>
        <NavLink to="/tenants" end>Tenants</NavLink>
        <NavLink to={`/tenants/${tenantId}/users`}>Users</NavLink>
        <NavLink to={`/tenants/${tenantId}/credentials`}>Credentials</NavLink>
        <NavLink to={`/tenants/${tenantId}/collections`}>Collections</NavLink>
        <NavLink to="/documents">Documents</NavLink>
        <NavLink to="/search">Search</NavLink>
        <NavLink to="/query">Query</NavLink>
      </nav>
    </div>
  )
}
