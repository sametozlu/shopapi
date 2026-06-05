import { useState } from 'react'
import { apiFetch } from '../lib/api'

const DEMO_CARD = '4242 4242 4242 4242'
const DECLINE_CARD = '4000000000000002'

function digitsOnly(value) {
  return value.replace(/\D/g, '')
}

function formatCardNumber(value) {
  const digits = digitsOnly(value).slice(0, 16)
  return digits.replace(/(\d{4})(?=\d)/g, '$1 ').trim()
}

function formatExpiry(value) {
  const digits = digitsOnly(value).slice(0, 4)
  if (digits.length <= 2) return digits
  return `${digits.slice(0, 2)}/${digits.slice(2)}`
}

export default function PaymentModal({
  open,
  order,
  total,
  t,
  lang,
  token,
  onLogin,
  onLogout,
  formatPrice,
  onSuccess,
  onCancel,
}) {
  const [cardHolder, setCardHolder] = useState('Samet Ozlu')
  const [cardNumber, setCardNumber] = useState(DEMO_CARD)
  const [expiry, setExpiry] = useState('12/28')
  const [cvv, setCvv] = useState('123')
  const [error, setError] = useState('')
  const [paying, setPaying] = useState(false)

  if (!open || !order) return null

  function validate() {
    const number = digitsOnly(cardNumber)
    const [mm, yy] = expiry.split('/')
    if (!cardHolder.trim()) return t.paymentNameRequired
    if (number.length !== 16) return t.paymentCardInvalid
    if (!mm || !yy || mm.length !== 2 || yy.length !== 2) return t.paymentExpiryInvalid
    if (cvv.length < 3) return t.paymentCvvInvalid
    if (number === DECLINE_CARD) return t.paymentDeclined
    return null
  }

  async function submitPayment(e) {
    e.preventDefault()
    const validationError = validate()
    if (validationError) {
      setError(validationError)
      return
    }

    setError('')
    setPaying(true)
    try {
      await new Promise((resolve) => setTimeout(resolve, 1200))

      const payResponse = await apiFetch(`/api/orders/${order.id}/pay`, {
        method: 'POST',
        token,
        onLogin,
        onLogout,
      })
      if (!payResponse.ok) {
        const errBody = await payResponse.json().catch(() => ({}))
        throw new Error(errBody.message ?? 'pay failed')
      }
      const payResult = await payResponse.json()
      if (payResult.mode === 'stripe_checkout' && payResult.checkoutUrl) {
        window.location.href = payResult.checkoutUrl
        return
      }
      onSuccess(order.id)
    } catch {
      setError(t.checkoutError)
    } finally {
      setPaying(false)
    }
  }

  return (
    <div className="payment-overlay" role="dialog" aria-modal="true" data-testid="payment-modal">
      <div className="payment-modal">
        <div className="payment-modal-head">
          <div>
            <h2>{t.paymentTitle}</h2>
            <p className="muted">{t.paymentSubtitle}</p>
          </div>
          <button
            type="button"
            className="payment-close"
            data-testid="payment-cancel"
            onClick={onCancel}
            disabled={paying}
            aria-label={t.paymentCancel}
          >
            ×
          </button>
        </div>

        <div className="payment-summary">
          <span>{t.paymentOrderLabel(order.id)}</span>
          <strong>{formatPrice(total, lang)}</strong>
        </div>

        <form className="payment-form" onSubmit={submitPayment}>
          <label>
            {t.paymentCardName}
            <input
              data-testid="payment-card-name"
              value={cardHolder}
              onChange={(e) => setCardHolder(e.target.value)}
              autoComplete="cc-name"
              disabled={paying}
            />
          </label>

          <label>
            {t.paymentCardNumber}
            <input
              data-testid="payment-card-number"
              value={cardNumber}
              onChange={(e) => setCardNumber(formatCardNumber(e.target.value))}
              inputMode="numeric"
              autoComplete="cc-number"
              placeholder="4242 4242 4242 4242"
              disabled={paying}
            />
          </label>

          <div className="payment-form-row">
            <label>
              {t.paymentExpiry}
              <input
                data-testid="payment-expiry"
                value={expiry}
                onChange={(e) => setExpiry(formatExpiry(e.target.value))}
                inputMode="numeric"
                autoComplete="cc-exp"
                placeholder="AA/YY"
                disabled={paying}
              />
            </label>
            <label>
              {t.paymentCvv}
              <input
                data-testid="payment-cvv"
                value={cvv}
                onChange={(e) => setCvv(digitsOnly(e.target.value).slice(0, 4))}
                inputMode="numeric"
                autoComplete="cc-csc"
                placeholder="123"
                disabled={paying}
              />
            </label>
          </div>

          <div className="payment-badges">
            <span>Visa</span>
            <span>Mastercard</span>
            <span>3D Secure</span>
          </div>

          <p className="payment-hint">{t.paymentHint}</p>
          {error && <p className="message error">{error}</p>}

          <button
            type="submit"
            className="btn-primary payment-submit"
            data-testid="payment-submit"
            disabled={paying}
          >
            {paying ? t.paymentProcessing : t.paymentSubmit}
          </button>
        </form>
      </div>
    </div>
  )
}
