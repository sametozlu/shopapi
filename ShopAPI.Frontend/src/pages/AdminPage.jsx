import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import LanguageToggle from '../components/LanguageToggle'
import { formatPrice, orderStatusLabel } from '../i18n'
import { apiFetch } from '../lib/api'

export default function AdminPage({ lang, setLang, t, token, onLogin, onLogout }) {
  const [activeTab, setActiveTab] = useState('orders')
  const [orders, setOrders] = useState([])
  const [products, setProducts] = useState([])
  const [coupons, setCoupons] = useState([])
  const [productEdits, setProductEdits] = useState({})
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

  async function loadProducts() {
    setLoading(true)
    setError('')
    try {
      const response = await apiFetch('/api/products?page=1&pageSize=100', { token, onLogin, onLogout })
      if (!response.ok) throw new Error('load failed')
      const data = await response.json()
      const items = data.items ?? []
      setProducts(items)
      setProductEdits(
        Object.fromEntries(
          items.map((p) => [p.id, { price: String(p.price), stock: String(p.stock) }])
        )
      )
    } catch {
      setError(t.loadError)
    } finally {
      setLoading(false)
    }
  }

  async function loadCoupons() {
    setLoading(true)
    setError('')
    try {
      const response = await apiFetch('/api/coupons', { token, onLogin, onLogout })
      if (!response.ok) throw new Error('load failed')
      setCoupons(await response.json())
    } catch {
      setError(t.loadError)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect -- load admin data when tab or token changes
    if (activeTab === 'orders') loadOrders()
    else if (activeTab === 'products') loadProducts()
    else if (activeTab === 'coupons') loadCoupons()
  }, [token, activeTab])

  async function refreshActiveTab() {
    if (activeTab === 'orders') await loadOrders()
    else if (activeTab === 'products') await loadProducts()
    else await loadCoupons()
  }

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

  function updateProductEdit(productId, field, value) {
    setProductEdits((prev) => ({
      ...prev,
      [productId]: { ...prev[productId], [field]: value },
    }))
  }

  async function saveProduct(product) {
    const edits = productEdits[product.id]
    if (!edits) return

    setBusyId(product.id)
    setError('')
    try {
      const response = await apiFetch(`/api/products/${product.id}`, {
        method: 'PUT',
        token,
        body: {
          name: product.name,
          price: Number(edits.price),
          stock: Number(edits.stock),
          categoryId: product.categoryId,
          isActive: product.isActive,
        },
        onLogin,
        onLogout,
      })
      if (!response.ok) throw new Error('update failed')
      await loadProducts()
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
          <h1 data-testid="admin-title">{t.adminTitle}</h1>
          <button type="button" className="btn-secondary" onClick={refreshActiveTab}>
            {t.adminRefresh}
          </button>
        </div>

        <div className="admin-tabs">
          <button
            type="button"
            className={activeTab === 'orders' ? 'active' : ''}
            data-testid="admin-tab-orders"
            onClick={() => setActiveTab('orders')}
          >
            {t.adminTabOrders}
          </button>
          <button
            type="button"
            className={activeTab === 'products' ? 'active' : ''}
            data-testid="admin-tab-products"
            onClick={() => setActiveTab('products')}
          >
            {t.adminTabProducts}
          </button>
          <button
            type="button"
            className={activeTab === 'coupons' ? 'active' : ''}
            data-testid="admin-tab-coupons"
            onClick={() => setActiveTab('coupons')}
          >
            {t.adminTabCoupons}
          </button>
        </div>

        {loading && <p className="message">{t.loading}</p>}
        {error && <p className="message error">{error}</p>}

        {activeTab === 'orders' && !loading && orders.length === 0 && (
          <p className="muted">{t.adminEmpty}</p>
        )}

        {activeTab === 'orders' && (
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
        )}

        {activeTab === 'products' && (
          <div className="admin-table-wrap">
            <table className="admin-table">
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Price</th>
                  <th>Stock</th>
                  <th>Active</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {products.map((product) => (
                  <tr key={product.id}>
                    <td>{product.name}</td>
                    <td>
                      <input
                        type="number"
                        min="0"
                        step="0.01"
                        value={productEdits[product.id]?.price ?? product.price}
                        onChange={(e) => updateProductEdit(product.id, 'price', e.target.value)}
                      />
                    </td>
                    <td>
                      <input
                        type="number"
                        min="0"
                        step="1"
                        value={productEdits[product.id]?.stock ?? product.stock}
                        onChange={(e) => updateProductEdit(product.id, 'stock', e.target.value)}
                      />
                    </td>
                    <td>{product.isActive ? '✓' : '—'}</td>
                    <td className="admin-actions">
                      <button
                        type="button"
                        className="btn-primary"
                        disabled={busyId === product.id}
                        onClick={() => saveProduct(product)}
                      >
                        {lang === 'tr' ? 'Kaydet' : 'Save'}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {activeTab === 'coupons' && (
          <div className="admin-table-wrap">
            <table className="admin-table">
              <thead>
                <tr>
                  <th>Code</th>
                  <th>Percentage</th>
                  <th>Min Order</th>
                  <th>{t.adminStatus}</th>
                  <th>{t.adminDate}</th>
                </tr>
              </thead>
              <tbody>
                {coupons.map((coupon) => (
                  <tr key={coupon.id}>
                    <td>{coupon.code}</td>
                    <td>{coupon.percentage != null ? `${coupon.percentage}%` : '—'}</td>
                    <td>{formatPrice(coupon.minOrderAmount, lang)}</td>
                    <td>{coupon.isActive ? '✓' : '—'}</td>
                    <td>
                      {new Date(coupon.expiresAt).toLocaleString(lang === 'tr' ? 'tr-TR' : 'en-US')}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  )
}
