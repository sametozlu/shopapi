import { useEffect, useState } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { LANG_KEY, i18n } from './i18n'
import { API_BASE, REFRESH_TOKEN_KEY, ROLE_KEY, TOKEN_KEY } from './lib/config'
import LoginPage from './pages/LoginPage'
import StorePage from './pages/StorePage'
import AdminPage from './pages/AdminPage'

function App() {
  const [lang, setLang] = useState(() => localStorage.getItem(LANG_KEY) ?? 'tr')
  const [token, setToken] = useState(localStorage.getItem(TOKEN_KEY) ?? '')
  const [role, setRole] = useState(localStorage.getItem(ROLE_KEY) ?? '')
  const [bootstrapping, setBootstrapping] = useState(true)
  const t = i18n[lang]

  function changeLang(next) {
    setLang(next)
    localStorage.setItem(LANG_KEY, next)
  }

  function onLogin(newToken, refreshToken, userRole) {
    setToken(newToken)
    localStorage.setItem(TOKEN_KEY, newToken)
    if (refreshToken) localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken)
    if (userRole) {
      setRole(userRole)
      localStorage.setItem(ROLE_KEY, userRole)
    }
  }

  function onLogout() {
    setToken('')
    setRole('')
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(REFRESH_TOKEN_KEY)
    localStorage.removeItem(ROLE_KEY)
  }

  useEffect(() => {
    async function bootstrapSession() {
      if (token) {
        setBootstrapping(false)
        return
      }

      const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY)
      if (!refreshToken) {
        setBootstrapping(false)
        return
      }

      try {
        const response = await fetch(`${API_BASE}/api/auth/refresh`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ refreshToken }),
        })
        if (!response.ok) throw new Error('refresh failed')
        const data = await response.json()
        onLogin(data.token, data.refreshToken, data.role)
      } catch {
        onLogout()
      } finally {
        setBootstrapping(false)
      }
    }

    bootstrapSession()
  }, [])

  if (bootstrapping) {
    return <p className="message">{t.loading}</p>
  }

  return (
    <Routes>
      <Route
        path="/login"
        element={token ? <Navigate to="/" replace /> : <LoginPage lang={lang} setLang={changeLang} t={t} onLogin={onLogin} />}
      />
      <Route
        path="/"
        element={
          token ? (
            <StorePage lang={lang} setLang={changeLang} t={t} token={token} role={role} onLogin={onLogin} onLogout={onLogout} />
          ) : (
            <Navigate to="/login" replace />
          )
        }
      />
      <Route
        path="/admin"
        element={
          token && role === 'Admin' ? (
            <AdminPage lang={lang} setLang={changeLang} t={t} token={token} onLogin={onLogin} onLogout={onLogout} />
          ) : token ? (
            <Navigate to="/" replace />
          ) : (
            <Navigate to="/login" replace />
          )
        }
      />
    </Routes>
  )
}

export default App
