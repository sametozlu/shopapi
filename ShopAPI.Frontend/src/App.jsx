import { useEffect, useMemo, useState } from 'react'
import { Link, Navigate, Route, Routes, useNavigate, useSearchParams } from 'react-router-dom'
import { LANG_KEY, formatPrice, i18n, orderStatusLabel } from './i18n'
import { localizeCategory, localizeProducts } from './productLocale'

const API_BASE = 'http://localhost:5095'
const TOKEN_KEY = 'shopapi-auth-token'
const REFRESH_TOKEN_KEY = 'shopapi-refresh-token'
const ROLE_KEY = 'shopapi-user-role'

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
        element={
          token ? (
            <Navigate to="/" replace />
          ) : (
            <LoginPage lang={lang} setLang={changeLang} t={t} onLogin={onLogin} />
          )
        }
      />
      <Route
        path="/"
        element={
          token ? (
            <StorePage
              lang={lang}
              setLang={changeLang}
              t={t}
              token={token}
              role={role}
              onLogin={onLogin}
              onLogout={onLogout}
            />
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

function LanguageToggle({ lang, setLang }) {
  return (
    <div className="lang-toggle">
      <button type="button" className={lang === 'tr' ? 'active' : ''} onClick={() => setLang('tr')}>
        TR
      </button>
      <button type="button" className={lang === 'en' ? 'active' : ''} onClick={() => setLang('en')}>
        EN
      </button>
    </div>
  )
}

function LoginPage({ lang, setLang, t, onLogin }) {
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
          <input value={email} onChange={(e) => setEmail(e.target.value)} type="email" required />
          <label>{t.password}</label>
          <input value={password} onChange={(e) => setPassword(e.target.value)} type="password" required />
          {error && <div className="error-box">{error}</div>}
          <button type="submit" className="btn-primary" disabled={loading}>
            {t.loginBtn}
          </button>
          <small>{t.authHint}</small>
        </form>
      </div>
    </div>
  )
}

function mapApiProduct(product) {
  const variants = (product.variants ?? []).map((v) => ({
    id: v.id,
    name: v.name,
    price: Number(v.overridePrice ?? product.price ?? 0),
    stock: v.stock ?? 0,
  }))
  return {
    id: product.id,
    title: product.name,
    description: `Stock: ${product.stock}`,
    category: product.category?.slug ?? 'general',
    categoryName: product.category?.name ?? 'General',
    price: Number(product.price ?? 0),
    rating: Number((4.2 + ((product.stock ?? 0) % 8) / 10).toFixed(1)),
    stock: product.stock ?? 0,
    variants,
    thumbnail: `https://picsum.photos/seed/${product.id}/480/320`,
  }
}

function cartItemUnitPrice(item) {
  return Number(item.productVariant?.overridePrice ?? item.product?.price ?? 0)
}

async function apiFetch(path, { method = 'GET', token, body, onLogin, onLogout } = {}) {
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

function StorePage({ lang, setLang, t, token, role, onLogin, onLogout }) {
  const navigate = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()
  const [rawProducts, setRawProducts] = useState([])
  const [displayProducts, setDisplayProducts] = useState([])
  const [categories, setCategories] = useState([])
  const [searchText, setSearchText] = useState('')
  const [selectedCategory, setSelectedCategory] = useState('all')
  const [sort, setSort] = useState('popular')
  const [loading, setLoading] = useState(true)
  const [translating, setTranslating] = useState(false)
  const [error, setError] = useState('')
  const [orderMessage, setOrderMessage] = useState('')
  const [checkoutBusy, setCheckoutBusy] = useState(false)
  const [completedOrderId, setCompletedOrderId] = useState(null)
  const [cartItems, setCartItems] = useState([])
  const [addresses, setAddresses] = useState([])
  const [couponCode, setCouponCode] = useState('WELCOME10')
  const [selectedVariants, setSelectedVariants] = useState({})

  useEffect(() => {
    async function fetchProductsAndCart() {
      setLoading(true)
      setError('')
      try {
        const [productsResponse, cartResponse, addressesResponse] = await Promise.all([
          apiFetch('/api/products?page=1&pageSize=100&isActive=true', { token, onLogin, onLogout }),
          apiFetch('/api/cart', { token, onLogin, onLogout }),
          apiFetch('/api/addresses', { token, onLogin, onLogout }),
        ])

        if (!productsResponse.ok || !cartResponse.ok || !addressesResponse.ok) throw new Error('load failed')

        const productsData = await productsResponse.json()
        const cartData = await cartResponse.json()
        const addressesData = await addressesResponse.json()
        const list = (productsData.items ?? []).map(mapApiProduct)

        setRawProducts(list)
        setCategories([...new Set(list.map((p) => p.category))])
        setCartItems(cartData ?? [])
        setAddresses(addressesData ?? [])
      } catch {
        setError(t.loadError)
      } finally {
        setLoading(false)
      }
    }
    fetchProductsAndCart()
  }, [token, lang])

  useEffect(() => {
    const paymentStatus = searchParams.get('payment')
    const orderId = searchParams.get('orderId')
    const sessionId = searchParams.get('session_id')
    if (!paymentStatus || !orderId) return

    async function handlePaymentReturn() {
      if (paymentStatus === 'success' && sessionId) {
        setCheckoutBusy(true)
        setOrderMessage(t.paymentConfirming)
        try {
          const confirmResponse = await apiFetch(
            `/api/orders/${orderId}/confirm-payment?sessionId=${encodeURIComponent(sessionId)}`,
            { method: 'POST', token, onLogin, onLogout }
          )
          if (!confirmResponse.ok) throw new Error('confirm failed')
          setCompletedOrderId(orderId)
          await reloadCart()
        } catch {
          setOrderMessage(t.checkoutError)
        } finally {
          setCheckoutBusy(false)
          setSearchParams({}, { replace: true })
        }
        return
      }

      if (paymentStatus === 'cancel') {
        setOrderMessage(t.paymentCancelled)
        setSearchParams({}, { replace: true })
      }
    }

    handlePaymentReturn()
  }, [searchParams, token])

  useEffect(() => {
    if (!rawProducts.length) return

    let cancelled = false
    async function applyLocale() {
      if (lang === 'en') {
        setDisplayProducts(
          rawProducts.map((p) => ({
            ...p,
            displayTitle: p.title,
            displayDescription: p.description,
            displayCategory: p.category,
          }))
        )
        return
      }

      setTranslating(true)
      const localized = await localizeProducts(rawProducts, lang)
      if (!cancelled) {
        setDisplayProducts(localized)
        setTranslating(false)
      }
    }

    applyLocale()
    return () => {
      cancelled = true
    }
  }, [rawProducts, lang])

  const filteredProducts = useMemo(() => {
    let list = displayProducts.filter((product) => {
      const categoryOk = selectedCategory === 'all' || product.category === selectedCategory
      const title = product.displayTitle ?? product.title
      const searchOk = title.toLowerCase().includes(searchText.toLowerCase())
      return categoryOk && searchOk
    })

    if (sort === 'price-asc') list = [...list].sort((a, b) => a.price - b.price)
    if (sort === 'price-desc') list = [...list].sort((a, b) => b.price - a.price)
    if (sort === 'rating') list = [...list].sort((a, b) => b.rating - a.rating)
    if (sort === 'popular') list = [...list].sort((a, b) => b.stock - a.stock)

    return list
  }, [displayProducts, searchText, selectedCategory, sort])

  const cartCount = cartItems.reduce((sum, item) => sum + item.quantity, 0)
  const cartTotal = cartItems.reduce((sum, item) => sum + item.quantity * cartItemUnitPrice(item), 0)

  async function reloadCart() {
    const response = await apiFetch('/api/cart', { token, onLogin, onLogout })
    if (!response.ok) throw new Error('cart load failed')
    setCartItems(await response.json())
  }

  async function addToCart(productId, productVariantId = null) {
    dismissOrderSuccess()
    const response = await apiFetch('/api/cart/items', {
      method: 'POST',
      token,
      body: { productId, productVariantId, quantity: 1 },
      onLogin,
      onLogout,
    })
    if (!response.ok) {
      setError(t.cartActionError)
      return
    }
    await reloadCart()
  }

  async function changeQuantity(productId, nextQuantity) {
    setOrderMessage('')
    if (nextQuantity <= 0) {
      const response = await apiFetch(`/api/cart/items/${productId}`, {
        method: 'DELETE',
        token,
        onLogin,
        onLogout,
      })
      if (!response.ok) setError(t.cartActionError)
      await reloadCart()
      return
    }

    const response = await apiFetch(`/api/cart/items/${productId}`, {
      method: 'PUT',
      token,
      body: { quantity: nextQuantity },
      onLogin,
      onLogout,
    })
    if (!response.ok) {
      setError(t.cartActionError)
      return
    }
    await reloadCart()
  }

  async function clearCart() {
    for (const item of cartItems) {
      // Sequential deletes keep API state predictable for demo flow.
      await apiFetch(`/api/cart/items/${item.productId}`, {
        method: 'DELETE',
        token,
        onLogin,
        onLogout,
      })
    }
    await reloadCart()
  }

  async function checkout() {
    if (checkoutBusy || !cartItems.length) return
    setOrderMessage('')
    setCompletedOrderId(null)
    setCheckoutBusy(true)
    try {
      const selectedAddress = addresses.find((x) => x.isDefault) ?? addresses[0]
      if (!selectedAddress) {
        setOrderMessage(t.addressMissing)
        return
      }
      const createResponse = await apiFetch('/api/orders', {
        method: 'POST',
        token,
        body: {
          shippingAddressId: selectedAddress.id,
          shippingMethod: 0,
          couponCode: couponCode.trim() || null,
        },
        onLogin,
        onLogout,
      })
      if (!createResponse.ok) throw new Error('create order failed')
      const order = await createResponse.json()

      const payResponse = await apiFetch(`/api/orders/${order.id}/pay`, {
        method: 'POST',
        token,
        onLogin,
        onLogout,
      })
      if (!payResponse.ok) {
        const errBody = await payResponse.json().catch(() => ({}))
        throw new Error(errBody.message ?? 'pay order failed')
      }
      const payResult = await payResponse.json()

      if (payResult.mode === 'stripe_checkout' && payResult.checkoutUrl) {
        window.location.href = payResult.checkoutUrl
        return
      }

      setCompletedOrderId(order.id)
      await Promise.all([
        reloadCart(),
        (async () => {
          const productsResponse = await apiFetch('/api/products?page=1&pageSize=100&isActive=true', {
            token,
            onLogin,
            onLogout,
          })
          if (productsResponse.ok) {
            const productsData = await productsResponse.json()
            setRawProducts((productsData.items ?? []).map(mapApiProduct))
          }
        })(),
      ])
    } catch {
      setOrderMessage(t.checkoutError)
      setCompletedOrderId(null)
    } finally {
      setCheckoutBusy(false)
    }
  }

  function dismissOrderSuccess() {
    setCompletedOrderId(null)
    setOrderMessage('')
  }

  function logout() {
    onLogout()
    navigate('/login', { replace: true })
  }

  return (
    <div className="store-page">
      <header className="store-nav">
        <Link className="logo" to="/">
          <span className="logo-mark">N</span>
          {t.brand}
        </Link>
        <div className="nav-search">
          <input
            value={searchText}
            onChange={(e) => setSearchText(e.target.value)}
            placeholder={t.search}
          />
        </div>
        <div className="nav-right">
          <LanguageToggle lang={lang} setLang={setLang} />
          <span className="cart-pill">
            {t.cart} <strong>{cartCount}</strong>
          </span>
          {role === 'Admin' && (
            <Link className="btn-ghost" to="/admin">
              {t.admin}
            </Link>
          )}
          <button type="button" className="btn-ghost" onClick={logout}>
            {t.logout}
          </button>
        </div>
      </header>

      <section className="hero">
        <div>
          <span className="hero-tag">{t.freeShipping}</span>
          <h1>{t.shop}</h1>
          <p>{t.subtitle}</p>
        </div>
      </section>

      <section className="toolbar">
        <select value={selectedCategory} onChange={(e) => setSelectedCategory(e.target.value)}>
          <option value="all">{t.allCategories}</option>
          {categories.map((category) => (
            <option key={category} value={category}>
              {localizeCategory(category, lang)}
            </option>
          ))}
        </select>
        <select value={sort} onChange={(e) => setSort(e.target.value)}>
          <option value="popular">{t.popular}</option>
          <option value="rating">{t.rating}</option>
          <option value="price-asc">{t.priceAsc}</option>
          <option value="price-desc">{t.priceDesc}</option>
        </select>
      </section>

      {loading && <p className="message">{t.loading}</p>}
      {translating && <p className="message info">{t.translating}</p>}
      {error && <p className="message error">{error}</p>}

      <main className="content">
        <section className="catalog">
          <div className="catalog-head">
            <h2>{t.discover}</h2>
            <span>
              {filteredProducts.length} {t.listed}
            </span>
          </div>
          <div className="product-grid">
            {filteredProducts.map((product) => (
              <article key={product.id} className="product-card">
                <div className="img-wrap">
                  <img src={product.thumbnail} alt={product.displayTitle ?? product.title} />
                </div>
                <div className="product-info">
                  <p className="category">{product.displayCategory ?? product.categoryName ?? product.category}</p>
                  <h3>{product.displayTitle ?? product.title}</h3>
                  <p className="desc">{product.displayDescription ?? product.description}</p>
                  {product.variants?.length > 0 && (
                    <label className="variant-select">
                      {t.variantLabel}
                      <select
                        value={selectedVariants[product.id] ?? ''}
                        onChange={(e) =>
                          setSelectedVariants((prev) => ({
                            ...prev,
                            [product.id]: e.target.value || null,
                          }))
                        }
                      >
                        <option value="">{t.noVariant}</option>
                        {product.variants.map((variant) => (
                          <option key={variant.id} value={variant.id}>
                            {variant.name} — {formatPrice(variant.price, lang)}
                          </option>
                        ))}
                      </select>
                    </label>
                  )}
                  <div className="meta">
                    <strong>
                      {formatPrice(
                        product.variants?.length && selectedVariants[product.id]
                          ? product.variants.find((v) => v.id === selectedVariants[product.id])?.price ?? product.price
                          : product.price,
                        lang
                      )}
                    </strong>
                    <span>
                      ★ {product.rating} {t.ratingLabel}
                    </span>
                  </div>
                </div>
                <button
                  type="button"
                  className="btn-primary"
                  onClick={() => addToCart(product.id, selectedVariants[product.id] || null)}
                >
                  {t.addToCart}
                </button>
              </article>
            ))}
          </div>
        </section>

        <aside className="cart-panel">
          <h2>{t.cart}</h2>
          {completedOrderId ? (
            <div className="order-success" role="status">
              <p className="order-success-title">{t.checkoutSuccess(completedOrderId)}</p>
              <p className="muted">{t.cartAfterOrder}</p>
              <button type="button" className="btn-ghost order-success-dismiss" onClick={dismissOrderSuccess}>
                {t.dismissOk}
              </button>
            </div>
          ) : (
            cartItems.length === 0 && <p className="muted">{t.cartEmpty}</p>
          )}
          <ul>
            {cartItems.map((item) => (
              <li key={item.productId}>
                <div>
                  <strong>
                    {item.product?.name}
                    {item.productVariant?.name ? ` (${item.productVariant.name})` : ''}
                  </strong>
                  <p>
                    {formatPrice(cartItemUnitPrice(item), lang)} × {item.quantity}
                  </p>
                </div>
                <div className="qty">
                  <button type="button" onClick={() => changeQuantity(item.productId, item.quantity - 1)}>
                    −
                  </button>
                  <span>{item.quantity}</span>
                  <button type="button" onClick={() => changeQuantity(item.productId, item.quantity + 1)}>
                    +
                  </button>
                </div>
              </li>
            ))}
          </ul>
          {cartItems.length > 0 && (
            <>
              <label className="coupon-field">
                {t.couponLabel}
                <input
                  value={couponCode}
                  onChange={(e) => setCouponCode(e.target.value)}
                  placeholder={t.couponPlaceholder}
                />
              </label>
              <div className="checkout">
                <p>{t.total}</p>
                <strong>{formatPrice(cartTotal, lang)}</strong>
              </div>
            </>
          )}
          {!!orderMessage && <p className="message error">{orderMessage}</p>}
          <button
            type="button"
            className="btn-primary"
            onClick={checkout}
            disabled={!cartItems.length || checkoutBusy || !!completedOrderId}
          >
            {checkoutBusy ? t.checkoutProcessing : t.checkout}
          </button>
          <button
            type="button"
            className="btn-secondary"
            onClick={clearCart}
            disabled={!cartItems.length || checkoutBusy}
          >
            {t.clear}
          </button>
        </aside>
      </main>
    </div>
  )
}

function AdminPage({ lang, setLang, t, token, onLogin, onLogout }) {
  const navigate = useNavigate()
  const [orders, setOrders] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [busyId, setBusyId] = useState(null)

  async function loadOrders() {
    setLoading(true)
    setError('')
    try {
      const response = await apiFetch('/api/orders', { token, onLogin, onLogout })
      if (!response.ok) throw new Error('load failed')
      setOrders(await response.json())
    } catch {
      setError(t.loadError)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    loadOrders()
  }, [token])

  async function updateStatus(orderId, status) {
    setBusyId(orderId)
    try {
      const response = await apiFetch(`/api/orders/${orderId}/status?status=${status}`, {
        method: 'PATCH',
        token,
        onLogin,
        onLogout,
      })
      if (!response.ok) throw new Error('update failed')
      await loadOrders()
    } catch {
      setError(t.cartActionError)
    } finally {
      setBusyId(null)
    }
  }

  async function cancelOrder(orderId) {
    setBusyId(orderId)
    try {
      const response = await apiFetch(`/api/orders/${orderId}/admin/cancel`, {
        method: 'POST',
        token,
        onLogin,
        onLogout,
      })
      if (!response.ok) throw new Error('cancel failed')
      await loadOrders()
    } catch {
      setError(t.cartActionError)
    } finally {
      setBusyId(null)
    }
  }

  return (
    <div className="store-page admin-page">
      <header className="store-nav">
        <Link className="logo" to="/">
          <span className="logo-mark">N</span>
          {t.brand}
        </Link>
        <div className="nav-right">
          <LanguageToggle lang={lang} setLang={setLang} />
          <Link className="btn-ghost" to="/">
            {t.shop}
          </Link>
        </div>
      </header>

      <section className="admin-panel">
        <div className="admin-head">
          <h1>{t.adminTitle}</h1>
          <button type="button" className="btn-secondary" onClick={loadOrders}>
            {t.adminRefresh}
          </button>
        </div>
        {loading && <p className="message">{t.loading}</p>}
        {error && <p className="message error">{error}</p>}
        {!loading && orders.length === 0 && <p className="muted">{t.adminEmpty}</p>}
        <div className="admin-table-wrap">
          <table className="admin-table">
            <thead>
              <tr>
                <th>ID</th>
                <th>{t.adminStatus}</th>
                <th>{t.adminTotal}</th>
                <th>{t.adminDate}</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {orders.map((order) => (
                <tr key={order.id}>
                  <td>{String(order.id).slice(0, 8)}…</td>
                  <td>
                    <span className={`status-pill status-${order.status}`}>
                      {orderStatusLabel(order.status, t)}
                    </span>
                  </td>
                  <td>{formatPrice(order.totalAmount, lang)}</td>
                  <td>{new Date(order.createdAt).toLocaleString(lang === 'tr' ? 'tr-TR' : 'en-US')}</td>
                  <td className="admin-actions">
                    {order.status === 1 && (
                      <button
                        type="button"
                        className="btn-primary"
                        disabled={busyId === order.id}
                        onClick={() => updateStatus(order.id, 2)}
                      >
                        {t.adminShip}
                      </button>
                    )}
                    {order.status !== 3 && order.status !== 2 && (
                      <button
                        type="button"
                        className="btn-secondary"
                        disabled={busyId === order.id}
                        onClick={() => cancelOrder(order.id)}
                      >
                        {t.adminCancel}
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  )
}

export default App
