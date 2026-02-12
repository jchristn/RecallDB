import React, { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../context/AuthContext.jsx'
import ErrorModal from '../components/ErrorModal.jsx'

export default function Login() {
  const [mode, setMode] = useState('token')
  const [serverUrl, setServerUrl] = useState(localStorage.getItem('recalldb_server_url') || 'http://localhost:8600')
  const [token, setToken] = useState('')
  const [tenantId, setTenantId] = useState('default')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState(null)
  const [loading, setLoading] = useState(false)
  const [theme, setTheme] = useState(localStorage.getItem('recalldb_theme') || 'light')
  const { loginWithToken, loginWithEmail } = useAuth()
  const navigate = useNavigate()

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme)
  }, [theme])

  const handleSubmit = async (e) => {
    e.preventDefault()
    setError(null)
    setLoading(true)
    try {
      if (mode === 'token') {
        await loginWithToken(serverUrl, token)
      } else {
        await loginWithEmail(serverUrl, tenantId, email, password)
      }
      navigate('/')
    } catch (err) {
      setError(err)
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="login-page">
      <div className="login-card">
        <img
          src={theme === 'dark' ? '/logo-white.png' : '/logo-black.png'}
          alt="RecallDB"
          style={{ width: 180, marginBottom: 16 }}
        />
        <p>Sign in to the dashboard</p>

        <ErrorModal error={error} onClose={() => setError(null)} />

        <div style={{ display: 'flex', gap: 8, marginBottom: 16 }}>
          <button className={`btn ${mode === 'token' ? 'btn-primary' : ''}`} onClick={() => setMode('token')} style={mode !== 'token' ? { background: 'var(--border)', color: 'var(--text-secondary)' } : {}}>API Key</button>
          <button className={`btn ${mode === 'email' ? 'btn-primary' : ''}`} onClick={() => setMode('email')} style={mode !== 'email' ? { background: 'var(--border)', color: 'var(--text-secondary)' } : {}}>Email</button>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label>Server URL</label>
            <input type="text" value={serverUrl} onChange={(e) => setServerUrl(e.target.value)} placeholder="http://localhost:8600" />
          </div>

          {mode === 'token' ? (
            <div className="form-group">
              <label>Bearer Token / API Key</label>
              <input type="password" value={token} onChange={(e) => setToken(e.target.value)} placeholder="Enter token or admin API key" />
            </div>
          ) : (
            <>
              <div className="form-group">
                <label>Tenant ID</label>
                <input type="text" value={tenantId} onChange={(e) => setTenantId(e.target.value)} placeholder="default" />
              </div>
              <div className="form-group">
                <label>Email</label>
                <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="admin@recall" />
              </div>
              <div className="form-group">
                <label>Password</label>
                <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
              </div>
            </>
          )}

          <button type="submit" className="btn btn-primary" style={{ width: '100%', marginTop: 8, justifyContent: 'center' }} disabled={loading}>
            {loading ? 'Signing in...' : 'Sign In'}
          </button>
        </form>
      </div>
    </div>
  )
}
