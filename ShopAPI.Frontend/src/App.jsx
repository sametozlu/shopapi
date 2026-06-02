import { useEffect, useMemo, useState } from 'react'
import { Link, Navigate, Route, Routes, useNavigate } from 'react-router-dom'
import { LANG_KEY, formatPrice, i18n } from './i18n'
import { localizeCategory, localizeProducts } from './productLocale'

const PRODUCT_API = 'https://dummyjson.com/products?limit=48'
const CART_KEY = 'shopapi-modern-cart'
const TOKEN_KEY = 'shopapi-auth-token'
const REFRESH_TOKEN_KEY = 'shopapi-refresh-token'

function App() {
  const [lang, setLang] = useState(() => localStorage.getItem(LANG_KEY) ?? 'tr')
  const [token, setToken] = useState(localStorage.getItem(TOKEN_KEY) ?? '')
  const t = i18n[lang]

  function changeLang(next) {
    setLang(next)
    localStorage.setItem(LANG_KEY, next)
  }

  function onLogin(newToken, refreshToken) {
    setToken(newToken)
    localStorage.setItem(TOKEN_KEY, newToken)
    if (refreshToken) localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken)
  }

  function onLogout() {
    setToken('')
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(REFRESH_TOKEN_KEY)
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
            <StorePage lang={lang} setLang={changeLang} t={t} onLogout={onLogout} />
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
      const response = await fetch('http://localhost:5095/api/Auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password }),
      })
      if (!response.ok) throw new Error('fail')
      const data = await response.json()
      onLogin(data.token, data.refreshToken)
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

function StorePage({ lang, setLang, t, onLogout }) {
  const navigate = useNavigate()
  const [rawProducts, setRawProducts] = useState([])
  const [displayProducts, setDisplayProducts] = useState([])
  const [categories, setCategories] = useState([])
  const [searchText, setSearchText] = useState('')
  const [selectedCategory, setSelectedCategory] = useState('all')
  const [sort, setSort] = useState('popular')
  const [loading, setLoading] = useState(true)
  const [translating, setTranslating] = useState(false)
  const [error, setError] = useState('')
  const [cart, setCart] = useState(() => {
    const saved = localStorage.getItem(CART_KEY)
    return saved ? JSON.parse(saved) : []
  })

  useEffect(() => {
    localStorage.setItem(CART_KEY, JSON.stringify(cart))
  }, [cart])

  useEffect(() => {
    async function fetchProducts() {
      setLoading(true)
      setError('')
      try {
        const response = await fetch(PRODUCT_API)
        if (!response.ok) throw new Error('load failed')
        const data = await response.json()
        const list = data.products ?? []
        setRawProducts(list)
        setCategories([...new Set(list.map((p) => p.category))])
      } catch {
        setError(t.loadError)
      } finally {
        setLoading(false)
      }
    }
    fetchProducts()
  }, [])

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

  const cartItems = useMemo(
    () =>
      cart
        .map((item) => {
          const product = displayProducts.find((p) => p.id === item.productId)
          return product ? { ...item, product } : null
        })
        .filter(Boolean),
    [cart, displayProducts]
  )

  const cartCount = cartItems.reduce((sum, item) => sum + item.quantity, 0)
  const cartTotal = cartItems.reduce((sum, item) => sum + item.quantity * item.product.price, 0)

  function addToCart(productId) {
    setCart((prev) => {
      const found = prev.find((item) => item.productId === productId)
      if (found) {
        return prev.map((item) =>
          item.productId === productId ? { ...item, quantity: item.quantity + 1 } : item
        )
      }
      return [...prev, { productId, quantity: 1 }]
    })
  }

  function changeQuantity(productId, nextQuantity) {
    if (nextQuantity <= 0) {
      setCart((prev) => prev.filter((item) => item.productId !== productId))
      return
    }
    setCart((prev) =>
      prev.map((item) => (item.productId === productId ? { ...item, quantity: nextQuantity } : item))
    )
  }

  function clearCart() {
    setCart([])
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
                  <p className="category">{product.displayCategory ?? product.category}</p>
                  <h3>{product.displayTitle ?? product.title}</h3>
                  <p className="desc">{product.displayDescription ?? product.description}</p>
                  <div className="meta">
                    <strong>{formatPrice(product.price, lang)}</strong>
                    <span>
                      ★ {product.rating} {t.ratingLabel}
                    </span>
                  </div>
                </div>
                <button type="button" className="btn-primary" onClick={() => addToCart(product.id)}>
                  {t.addToCart}
                </button>
              </article>
            ))}
          </div>
        </section>

        <aside className="cart-panel">
          <h2>{t.cart}</h2>
          {cartItems.length === 0 && <p className="muted">{t.cartEmpty}</p>}
          <ul>
            {cartItems.map((item) => (
              <li key={item.productId}>
                <div>
                  <strong>{item.product.displayTitle ?? item.product.title}</strong>
                  <p>
                    {formatPrice(item.product.price, lang)} × {item.quantity}
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
          <div className="checkout">
            <p>{t.total}</p>
            <strong>{formatPrice(cartTotal, lang)}</strong>
          </div>
          <button type="button" className="btn-secondary" onClick={clearCart} disabled={!cartItems.length}>
            {t.clear}
          </button>
        </aside>
      </main>
    </div>
  )
}

export default App
