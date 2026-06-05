import { expect } from '@playwright/test'

export const DEMO_EMAIL = 'admin@admin.local'
export const DEMO_PASSWORD = 'Admin123!'

export async function loginAsAdmin(page) {
  await page.goto('/login')
  await page.getByTestId('login-email').fill(DEMO_EMAIL)
  await page.getByTestId('login-password').fill(DEMO_PASSWORD)
  await page.getByTestId('login-submit').click()
  await expect(page.getByTestId('store-heading')).toBeVisible()
  await expect(page.getByTestId('add-to-cart-btn').first()).toBeVisible({ timeout: 15_000 })
}

export async function clearCartIfNeeded(page) {
  const count = Number(await page.getByTestId('cart-count').textContent())
  if (!count) return

  await page.getByRole('button', { name: /Sepeti Temizle|Clear Cart/i }).click()
  await expect(page.getByTestId('cart-count')).toHaveText('0', { timeout: 15_000 })
}

export async function addProductsToCart(page, count = 1) {
  for (let i = 0; i < count; i += 1) {
    const before = Number(await page.getByTestId('cart-count').textContent())
    await page.getByTestId('add-to-cart-btn').nth(i).click()
    await expect(page.getByTestId('cart-count')).toHaveText(String(before + 1), { timeout: 10_000 })
  }
}

export async function openPaymentModal(page) {
  await expect(page.getByTestId('checkout-btn')).toBeEnabled({ timeout: 10_000 })
  await page.getByTestId('checkout-btn').click()
  await expect(page.getByTestId('payment-modal')).toBeVisible({ timeout: 20_000 })
  await expect(page.getByTestId('cart-count')).toHaveText('0', { timeout: 10_000 })
}
