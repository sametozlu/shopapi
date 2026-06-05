import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import LanguageToggle from '../components/LanguageToggle'
import PaymentModal from '../components/PaymentModal'
import { formatPrice } from '../i18n'
import { apiFetch } from '../lib/api'
import { cartItemUnitPrice, mapApiProduct } from '../lib/productUtils'
import { localizeCategory, localizeProducts } from '../productLocale'

export default function StorePage({ lang, setLang, t, token, role, onLogin, onLogout }) {
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
  const [pendingPaymentOrder, setPendingPaymentOrder] = useState(null)
  const [paymentProvider, setPaymentProvider] = useState('mock')

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
    async function loadPaymentSettings() {
      try {
        const response = await apiFetch('/api/payments/settings', { token, onLogin, onLogout })
        if (!response.ok) return
        const data = await response.json()
        if (data.provider) setPaymentProvider(data.provider)
      } catch {
        /* keep mock default */
      }
    }
    loadPaymentSettings()
  }, [token])

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
      await apiFetch(`/api/cart/items/${item.productId}`, {
        method: 'DELETE',
        token,
        onLogin,
        onLogout,
      })
    }
    await reloadCart()
  }

  async function finalizeOrder(orderId) {
    setCompletedOrderId(orderId)
    setPendingPaymentOrder(null)
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
  }

  async function payWithStripe(order) {
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
    await finalizeOrder(order.id)
  }

  async function checkout() {
    if (checkoutBusy || !cartItems.length || pendingPaymentOrder) return
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
      await reloadCart()

      if (paymentProvider === 'stripe') {
        await payWithStripe(order)
        return
      }

      setPendingPaymentOrder(order)
    } catch {
      setOrderMessage(t.checkoutError)
      setCompletedOrderId(null)
      setPendingPaymentOrder(null)
    } finally {
      setCheckoutBusy(false)
    }
  }

  async function handlePaymentSuccess(orderId) {
    try {
      await finalizeOrder(orderId)
    } catch {
      setOrderMessage(t.checkoutError)
    }
  }

  function cancelPayment() {
    setPendingPaymentOrder(null)
    setOrderMessage(t.paymentCancelled)
  }

  function dismissOrderSuccess() {
    setCompletedOrderId(null)
    setOrderMessage('')
    setPendingPaymentOrder(null)
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
            {t.cart} <strong data-testid="cart-count">{cartCount}</strong>
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
          <h1 data-testid="store-heading">{t.shop}</h1>
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
                  data-testid="add-to-cart-btn"
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
            <div className="order-success" role="status" data-testid="order-success">
              <p className="order-success-title">{t.checkoutSuccess(completedOrderId)}</p>
              <p className="muted">{t.cartAfterOrder}</p>
              <button
                type="button"
                className="btn-ghost order-success-dismiss"
                data-testid="order-success-dismiss"
                onClick={dismissOrderSuccess}
              >
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
            data-testid="checkout-btn"
            onClick={checkout}
            disabled={!cartItems.length || checkoutBusy || !!completedOrderId || !!pendingPaymentOrder}
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

      <PaymentModal
        open={!!pendingPaymentOrder}
        order={pendingPaymentOrder}
        total={pendingPaymentOrder?.totalAmount ?? cartTotal}
        t={t}
        lang={lang}
        token={token}
        onLogin={onLogin}
        onLogout={onLogout}
        formatPrice={formatPrice}
        onSuccess={handlePaymentSuccess}
        onCancel={cancelPayment}
      />
    </div>
  )
}
