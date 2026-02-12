import React, { createContext, useContext, useState, useEffect } from 'react'
import api from '../api/api.js'

const AuthContext = createContext(null)

export function AuthProvider({ children }) {
  const [isAuthenticated, setIsAuthenticated] = useState(false)
  const [user, setUser] = useState(null)
  const [tenant, setTenant] = useState(null)
  const [isAdmin, setIsAdmin] = useState(false)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const token = localStorage.getItem('recalldb_token')
    if (token) {
      const info = api.getUserInfo()
      if (info.user) {
        setUser(info.user)
        setTenant(info.tenant)
        setIsAuthenticated(true)
        setIsAdmin(info.user?.IsAdmin || false)
      }
    }
    setLoading(false)
  }, [])

  const loginWithToken = async (serverUrl, token) => {
    api.setBaseUrl(serverUrl)
    api.setBearerToken(token)
    const result = await api.authenticateBearer(token)
    if (result.Success) {
      const adminApiKey = !result.User && !result.Credential
      const user = result.User || (adminApiKey ? { Email: 'admin@recalldb', IsAdmin: true } : null)
      setUser(user)
      setTenant(result.Tenant)
      setIsAuthenticated(true)
      setIsAdmin(user?.IsAdmin || false)
      api.setUserInfo(user, result.Tenant)
      return true
    }
    throw new Error(result.ErrorMessage || 'Authentication failed')
  }

  const loginWithEmail = async (serverUrl, tenantId, email, password) => {
    api.setBaseUrl(serverUrl)
    const result = await api.authenticateEmailPassword(tenantId, email, password)
    if (result.Success && result.Credential) {
      api.setBearerToken(result.Credential.BearerToken)
      setUser(result.User)
      setTenant(result.Tenant)
      setIsAuthenticated(true)
      setIsAdmin(result.User?.IsAdmin || false)
      api.setUserInfo(result.User, result.Tenant)
      return true
    }
    throw new Error(result.ErrorMessage || 'Authentication failed')
  }

  const logout = () => {
    api.clearAuth()
    setUser(null)
    setTenant(null)
    setIsAuthenticated(false)
    setIsAdmin(false)
  }

  if (loading) return null

  return (
    <AuthContext.Provider value={{ isAuthenticated, user, tenant, isAdmin, loginWithToken, loginWithEmail, logout }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  return useContext(AuthContext)
}
