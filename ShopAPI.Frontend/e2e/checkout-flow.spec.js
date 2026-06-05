import { test, expect } from '@playwright/test'
import {
  loginAsAdmin,
  clearCartIfNeeded,
  addProductsToCart,
  openPaymentModal,
} from './helpers/auth.js'

test('tam alışveriş akışı: giriş → sepet → ödeme → başarı', async ({ page }) => {
  await test.step('Giriş yap', async () => {
    await loginAsAdmin(page)
  })

  await test.step('Sepete ürün ekle', async () => {
    await clearCartIfNeeded(page)
    await addProductsToCart(page, 2)
  })

  await test.step('Sipariş oluştur ve ödeme ekranını aç', async () => {
    const couponInput = page.locator('.coupon-field input')
    await expect(couponInput).toBeVisible()
    await couponInput.fill('WELCOME10')
    await openPaymentModal(page)
  })

  await test.step('Kart ile öde', async () => {
    await page.getByTestId('payment-card-name').fill('Test Kullanici')
    await page.getByTestId('payment-card-number').fill('4242 4242 4242 4242')
    await page.getByTestId('payment-expiry').fill('12/28')
    await page.getByTestId('payment-cvv').fill('123')
    await page.getByTestId('payment-submit').click()
    await expect(page.getByTestId('order-success')).toBeVisible({ timeout: 20_000 })
  })

  await test.step('Başarı mesajını kapat', async () => {
    await expect(page.getByTestId('cart-count')).toHaveText('0')
    await page.getByTestId('order-success-dismiss').click()
    await expect(page.getByTestId('order-success')).toBeHidden()
  })
})
