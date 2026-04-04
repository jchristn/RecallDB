import React, { useState, useEffect } from 'react'
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { AuthProvider, useAuth } from './context/AuthContext.jsx'
import Login from './views/Login.jsx'
import Dashboard from './views/Dashboard.jsx'
import Tenants from './views/Tenants.jsx'
import Users from './views/Users.jsx'
import Credentials from './views/Credentials.jsx'
import Collections from './views/Collections.jsx'
import Documents from './views/Documents.jsx'
import DocumentsBrowser from './views/DocumentsBrowser.jsx'
import Search from './views/Search.jsx'
import QueryBuilder from './views/QueryBuilder.jsx'
import SearchQuery from './views/SearchQuery.jsx'
import RequestHistory from './views/RequestHistory.jsx'
import ApiExplorer from './views/ApiExplorer.jsx'
import Sidebar from './components/Sidebar.jsx'
import Topbar from './components/Topbar.jsx'
import Tour from './components/Tour.jsx'
import SetupWizard from './components/SetupWizard.jsx'

function ProtectedRoute({ children }) {
  const { isAuthenticated } = useAuth()
  if (!isAuthenticated) return <Navigate to="/login" replace />
  return children
}

function AppLayout() {
  const [theme, setTheme] = useState(localStorage.getItem('recalldb_theme') || 'light')
  const [showTour, setShowTour] = useState(false)
  const [showWizard, setShowWizard] = useState(false)

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme)
    localStorage.setItem('recalldb_theme', theme)
  }, [theme])

  useEffect(() => {
    if (!localStorage.getItem('recalldb_tour_completed')) {
      setShowTour(true)
    }
  }, [])

  const toggleTheme = () => setTheme(prev => prev === 'light' ? 'dark' : 'light')

  const handleTourComplete = () => {
    setShowTour(false)
    if (!localStorage.getItem('recalldb_setup_completed')) {
      setShowWizard(true)
    }
  }

  const handleTourClose = () => {
    setShowTour(false)
  }

  const handleWizardClose = () => {
    setShowWizard(false)
  }

  return (
    <div className="app-layout">
      <Sidebar onStartTour={() => setShowTour(true)} onStartWizard={() => setShowWizard(true)} />
      <div className="main-wrapper">
        <Topbar theme={theme} toggleTheme={toggleTheme} />
        <main className="main-content">
          <Routes>
            <Route path="/" element={<Dashboard />} />
            <Route path="/tenants" element={<Tenants />} />
            <Route path="/tenants/:tenantId/users" element={<Users />} />
            <Route path="/tenants/:tenantId/credentials" element={<Credentials />} />
            <Route path="/tenants/:tenantId/collections" element={<Collections />} />
            <Route path="/documents" element={<DocumentsBrowser />} />
            <Route path="/tenants/:tenantId/collections/:collectionId/documents" element={<Documents />} />
            <Route path="/search" element={<SearchQuery mode="search" />} />
            <Route path="/query" element={<SearchQuery mode="query" />} />
            <Route path="/tenants/:tenantId/collections/:collectionId/search" element={<Search />} />
            <Route path="/tenants/:tenantId/collections/:collectionId/query" element={<QueryBuilder />} />
            <Route path="/request-history" element={<RequestHistory />} />
            <Route path="/api-explorer" element={<ApiExplorer />} />
          </Routes>
        </main>
      </div>
      {showTour && <Tour onClose={handleTourClose} onComplete={handleTourComplete} />}
      {showWizard && <SetupWizard onClose={handleWizardClose} />}
    </div>
  )
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route path="/*" element={
            <ProtectedRoute>
              <AppLayout />
            </ProtectedRoute>
          } />
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  )
}
