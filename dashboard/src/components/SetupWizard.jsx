import React, { useState } from 'react'
import api from '../api/api.js'
import './SetupWizard.css'

const STEPS = [
  { title: 'Verify Server', description: 'Let\'s make sure your RecallDB server is reachable.' },
  { title: 'Create Tenant', description: 'A tenant is an isolated workspace for your data.' },
  { title: 'Create User', description: 'Create a user account within your new tenant.' },
  { title: 'Create Credentials', description: 'Generate an API bearer token for programmatic access.' },
  { title: 'Create Collection', description: 'A collection stores your vector embeddings.' },
  { title: 'Summary', description: 'Here\'s everything that was set up.' },
]

export default function SetupWizard({ onClose }) {
  const [step, setStep] = useState(0)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)

  // Step results
  const [serverHealthy, setServerHealthy] = useState(false)
  const [tenantName, setTenantName] = useState('')
  const [createdTenant, setCreatedTenant] = useState(null)
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [createdUser, setCreatedUser] = useState(null)
  const [createdCredential, setCreatedCredential] = useState(null)
  const [collectionName, setCollectionName] = useState('')
  const [vectorDimensions, setVectorDimensions] = useState('384')
  const [createdCollection, setCreatedCollection] = useState(null)
  const [copied, setCopied] = useState(false)

  const serverUrl = localStorage.getItem('recalldb_server_url') || window.__RECALLDB_CONFIG__?.RECALLDB_SERVER_URL || 'http://localhost:8601'

  const handleVerifyServer = async () => {
    setLoading(true)
    setError(null)
    try {
      await api.health()
      setServerHealthy(true)
    } catch (err) {
      setError('Could not reach the server: ' + err.message)
    }
    setLoading(false)
  }

  const handleCreateTenant = async () => {
    if (!tenantName.trim()) { setError('Tenant name is required'); return }
    setLoading(true)
    setError(null)
    try {
      const result = await api.createTenant({ Name: tenantName.trim() })
      setCreatedTenant(result)
    } catch (err) {
      setError('Failed to create tenant: ' + err.message)
    }
    setLoading(false)
  }

  const handleCreateUser = async () => {
    if (!email.trim()) { setError('Email is required'); return }
    if (!password.trim()) { setError('Password is required'); return }
    setLoading(true)
    setError(null)
    try {
      const tid = createdTenant.Id || createdTenant.GUID
      const result = await api.createUser(tid, { Email: email.trim(), Password: password })
      setCreatedUser(result)
    } catch (err) {
      setError('Failed to create user: ' + err.message)
    }
    setLoading(false)
  }

  const handleCreateCredential = async () => {
    setLoading(true)
    setError(null)
    try {
      const tid = createdTenant.Id || createdTenant.GUID
      const uid = createdUser.Id || createdUser.GUID
      const result = await api.createCredential(tid, { UserGUID: uid })
      setCreatedCredential(result)
    } catch (err) {
      setError('Failed to create credential: ' + err.message)
    }
    setLoading(false)
  }

  const handleCreateCollection = async () => {
    if (!collectionName.trim()) { setError('Collection name is required'); return }
    const dims = parseInt(vectorDimensions, 10)
    if (!dims || dims < 1) { setError('Vector dimensions must be a positive number'); return }
    setLoading(true)
    setError(null)
    try {
      const tid = createdTenant.Id || createdTenant.GUID
      const result = await api.createCollection(tid, { Name: collectionName.trim(), VectorDimensions: dims })
      setCreatedCollection(result)
    } catch (err) {
      setError('Failed to create collection: ' + err.message)
    }
    setLoading(false)
  }

  const handleCopyToken = () => {
    const token = createdCredential?.BearerToken || createdCredential?.AccessKey || ''
    if (token) {
      navigator.clipboard.writeText(token)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    }
  }

  const canProceed = () => {
    switch (step) {
      case 0: return serverHealthy
      case 1: return !!createdTenant
      case 2: return !!createdUser
      case 3: return !!createdCredential
      case 4: return !!createdCollection
      case 5: return true
      default: return false
    }
  }

  const handleNext = () => {
    setError(null)
    if (step < STEPS.length - 1) setStep(step + 1)
  }

  const handleFinish = () => {
    localStorage.setItem('recalldb_setup_completed', 'true')
    onClose()
  }

  const renderStepContent = () => {
    switch (step) {
      case 0:
        return (
          <div className="wizard-step">
            <h3>Verify Server Connection</h3>
            <p>We'll check that the RecallDB server is running and accessible.</p>
            <div className="wizard-info-box">
              <strong>Server URL</strong>
              {serverUrl}
            </div>
            {serverHealthy ? (
              <div className="wizard-success">Server is healthy and reachable!</div>
            ) : (
              <button className="btn btn-primary" onClick={handleVerifyServer} disabled={loading}>
                {loading ? 'Checking...' : 'Verify Connection'}
              </button>
            )}
          </div>
        )
      case 1:
        return (
          <div className="wizard-step">
            <h3>Create a Tenant</h3>
            <p>Tenants provide data isolation. All users, credentials, and collections belong to a tenant.</p>
            {createdTenant ? (
              <div className="wizard-success">Tenant "{createdTenant.Name}" created successfully!</div>
            ) : (
              <>
                <div className="form-group">
                  <label>Tenant Name</label>
                  <input type="text" value={tenantName} onChange={e => setTenantName(e.target.value)} placeholder="e.g. My Organization" />
                </div>
                <button className="btn btn-primary" onClick={handleCreateTenant} disabled={loading}>
                  {loading ? 'Creating...' : 'Create Tenant'}
                </button>
              </>
            )}
          </div>
        )
      case 2:
        return (
          <div className="wizard-step">
            <h3>Create a User</h3>
            <p>Users can authenticate via email and password to access the dashboard or API.</p>
            {createdUser ? (
              <div className="wizard-success">User "{createdUser.Email}" created successfully!</div>
            ) : (
              <>
                <div className="form-group">
                  <label>Email</label>
                  <input type="email" value={email} onChange={e => setEmail(e.target.value)} placeholder="user@example.com" />
                </div>
                <div className="form-group">
                  <label>Password</label>
                  <input type="password" value={password} onChange={e => setPassword(e.target.value)} placeholder="Enter a password" />
                </div>
                <button className="btn btn-primary" onClick={handleCreateUser} disabled={loading}>
                  {loading ? 'Creating...' : 'Create User'}
                </button>
              </>
            )}
          </div>
        )
      case 3:
        return (
          <div className="wizard-step">
            <h3>Create API Credentials</h3>
            <p>A bearer token will be generated for programmatic API access. Save it securely — it cannot be retrieved later.</p>
            {createdCredential ? (
              <>
                <div className="wizard-token-display">
                  {createdCredential.BearerToken || createdCredential.AccessKey || 'Token generated'}
                  <button className="wizard-token-copy" onClick={handleCopyToken}>
                    {copied ? 'Copied!' : 'Copy'}
                  </button>
                </div>
                <div className="wizard-token-warning">Save this token now. It will not be shown again.</div>
              </>
            ) : (
              <button className="btn btn-primary" onClick={handleCreateCredential} disabled={loading}>
                {loading ? 'Generating...' : 'Generate Credentials'}
              </button>
            )}
          </div>
        )
      case 4:
        return (
          <div className="wizard-step">
            <h3>Create a Collection</h3>
            <p>Collections store vector embeddings. Set the dimensionality to match your embedding model (e.g. 384 for MiniLM, 1536 for OpenAI).</p>
            {createdCollection ? (
              <div className="wizard-success">Collection "{createdCollection.Name}" created successfully!</div>
            ) : (
              <>
                <div className="form-group">
                  <label>Collection Name</label>
                  <input type="text" value={collectionName} onChange={e => setCollectionName(e.target.value)} placeholder="e.g. my-embeddings" />
                </div>
                <div className="form-group">
                  <label>Vector Dimensions</label>
                  <input type="number" value={vectorDimensions} onChange={e => setVectorDimensions(e.target.value)} min="1" placeholder="384" />
                </div>
                <button className="btn btn-primary" onClick={handleCreateCollection} disabled={loading}>
                  {loading ? 'Creating...' : 'Create Collection'}
                </button>
              </>
            )}
          </div>
        )
      case 5:
        return (
          <div className="wizard-step">
            <h3>Setup Complete!</h3>
            <p>Your RecallDB instance is ready to use. Here's a summary of what was created:</p>
            <ul className="wizard-summary">
              <li>
                <span className="wizard-summary-label">Tenant</span>
                <span className="wizard-summary-value">{createdTenant?.Name}</span>
              </li>
              <li>
                <span className="wizard-summary-label">User</span>
                <span className="wizard-summary-value">{createdUser?.Email}</span>
              </li>
              <li>
                <span className="wizard-summary-label">Credentials</span>
                <span className="wizard-summary-value">Bearer token created</span>
              </li>
              <li>
                <span className="wizard-summary-label">Collection</span>
                <span className="wizard-summary-value">{createdCollection?.Name} ({vectorDimensions}d)</span>
              </li>
            </ul>
            <p>You can now start adding documents and performing vector searches!</p>
          </div>
        )
      default:
        return null
    }
  }

  return (
    <div className="wizard-overlay">
      <div className="wizard-modal">
        <h2>Setup Wizard</h2>
        <div className="wizard-progress">
          {STEPS.map((_, i) => (
            <div
              key={i}
              className={`wizard-progress-step${i < step ? ' completed' : ''}${i === step ? ' active' : ''}`}
            />
          ))}
        </div>
        {renderStepContent()}
        {error && <div className="wizard-error">{error}</div>}
        <div className="wizard-actions">
          <button className="btn btn-secondary btn-sm" onClick={handleFinish}>
            {step === STEPS.length - 1 ? 'Close' : 'Skip Setup'}
          </button>
          <div className="wizard-actions-right">
            {step > 0 && step < STEPS.length - 1 && (
              <button className="btn btn-secondary btn-sm" onClick={() => { setStep(step - 1); setError(null) }}>
                Back
              </button>
            )}
            {step < STEPS.length - 1 && (
              <button className="btn btn-primary btn-sm" onClick={handleNext} disabled={!canProceed()}>
                Next
              </button>
            )}
            {step === STEPS.length - 1 && (
              <button className="btn btn-primary btn-sm" onClick={handleFinish}>
                Finish
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
