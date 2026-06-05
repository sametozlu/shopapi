import { API_BASE, REFRESH_TOKEN_KEY } from './config'

export async function apiFetch(path, { method = 'GET', token, body, onLogin, onLogout } = {}) {
  const headers = {}
  if (token) headers.Authorization = `Bearer ${token}`
  if (body !== undefined) headers['Content-Type'] = 'application/json'

  const call = async (accessToken) =>
    fetch(`${API_BASE}${path}`, {
      method,
      headers: accessToken ? { ...headers, Authorization: `Bearer ${accessToken}` } : headers,
      body: body !== undefined ? JSON.stringify(body) : undefined,
    })

  let response = await call(token)
  if (response.status !== 401 || !localStorage.getItem(REFRESH_TOKEN_KEY)) return response

  try {
    const refreshResponse = await fetch(`${API_BASE}/api/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken: localStorage.getItem(REFRESH_TOKEN_KEY) }),
    })
    if (!refreshResponse.ok) throw new Error('refresh failed')

    const refreshData = await refreshResponse.json()
    onLogin(refreshData.token, refreshData.refreshToken, refreshData.role)
    response = await call(refreshData.token)
    return response
  } catch {
    onLogout()
    throw new Error('session expired')
  }
}
