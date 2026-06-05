import { test, expect } from '@playwright/test'
import {
  loginAsAdmin,
  clearCartIfNeeded,
  addProductsToCart,
  openPaymentModal,
} from './helpers/auth.js'

test.describe.configure({ mode: 'serial' })

test.describe('Ödeme modalı', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page)
    await clearCartIfNeeded(page)
    await addProductsToCart(page, 1)
  })

  test('sipariş sonrası ödeme ekranı açılır', async ({ page }) => {
    await openPaymentModal(page)

    await expect(page.getByTestId('payment-card-name')).toBeVisible()
    await expect(page.getByTestId('payment-card-number')).toBeVisible()
    await expect(page.getByTestId('payment-expiry')).toBeVisible()
    await expect(page.getByTestId('payment-cvv')).toBeVisible()
    await expect(page.getByTestId('payment-submit')).toBeVisible()
    await expect(page.getByTestId('payment-card-number')).toHaveValue(/4242/)
  })

  test('geçerli kart ile ödeme tamamlanır', async ({ page }) => {
    await openPaymentModal(page)

    await page.getByTestId('payment-card-name').fill('Test Kullanici')
    await page.getByTestId('payment-card-number').fill('4242 4242 4242 4242')
    await page.getByTestId('payment-expiry').fill('12/28')
    await page.getByTestId('payment-cvv').fill('123')
    await page.getByTestId('payment-submit').click()

    await expect(page.getByTestId('payment-modal')).toBeHidden({ timeout: 20_000 })
    await expect(page.getByTestId('order-success')).toBeVisible()
    await expect(page.getByTestId('order-success')).toContainText(/Ödeme başarılı|Payment successful/i)
    await expect(page.getByTestId('cart-count')).toHaveText('0', { timeout: 10_000 })
  })

  test('reddedilen kart hata gösterir', async ({ page }) => {
    await openPaymentModal(page)

    await page.getByTestId('payment-card-number').fill('4000 0000 0000 0002')
    await page.getByTestId('payment-expiry').fill('12/28')
    await page.getByTestId('payment-cvv').fill('123')
    await page.getByTestId('payment-submit').click()

    await expect(page.getByTestId('payment-modal')).toBeVisible()
    await expect(page.locator('.payment-form .message.error')).toContainText(
      /Kart reddedildi|Card declined/i
    )
    await expect(page.getByTestId('order-success')).toBeHidden()
  })

  test('ödeme iptal edilince modal kapanır', async ({ page }) => {
    await openPaymentModal(page)

    await page.getByTestId('payment-cancel').click()
    await expect(page.getByTestId('payment-modal')).toBeHidden()
    await expect(page.getByText(/Ödeme iptal|Payment cancelled/i)).toBeVisible()
    await expect(page.getByTestId('cart-count')).toHaveText('0')
  })
})
