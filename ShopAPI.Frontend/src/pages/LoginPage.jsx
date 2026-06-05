import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import LanguageToggle from '../components/LanguageToggle'
import { API_BASE } from '../lib/config'

export default function LoginPage({ lang, setLang, t, onLogin }) {
  const navigate = useNavigate()
  const [email, setEmail] = useState('admin@admin.local')
  const [password, setPassword] = useState('Admin123!')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  async function submit(e) {
    e.preventDefault()
    setError('')
    setLoading(true)
    try {
      const response = await fetch(`${API_BASE}/api/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password }),
      })
      if (!response.ok) throw new Error('fail')
      const data = await response.json()
      onLogin(data.token, data.refreshToken, data.role)
      navigate('/', { replace: true })
    } catch {
      setError(t.loginError)
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="auth-page">
      <div className="auth-visual">
        <span className="tag">{t.freeShipping}</span>
        <h1>{t.brand}</h1>
        <p>{t.subtitle}</p>
      </div>
      <div className="auth-card">
        <div className="auth-top">
          <h2>{t.loginTitle}</h2>
          <LanguageToggle lang={lang} setLang={setLang} />
        </div>
        <p className="auth-sub">{t.loginSub}</p>
        <form className="auth-form" onSubmit={submit}>
          <label>{t.email}</label>
          <input
            data-testid="login-email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            type="email"
            required
          />
          <label>{t.password}</label>
          <input
            data-testid="login-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            type="password"
            required
          />
          {error && <div className="error-box">{error}</div>}
          <button data-testid="login-submit" type="submit" className="btn-primary" disabled={loading}>
            {t.loginBtn}
          </button>
          <small>{t.authHint}</small>
        </form>
      </div>
    </div>
  )
}
