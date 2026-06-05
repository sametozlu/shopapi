import { test, expect } from '@playwright/test'

test.beforeEach(async ({ page }) => {
  await page.goto('/login')
  await page.getByTestId('login-email').fill('admin@admin.local')
  await page.getByTestId('login-password').fill('Admin123!')
  await page.getByTestId('login-submit').click()
  await expect(page.getByTestId('store-heading')).toBeVisible()
})

test('can add product to cart', async ({ page }) => {
  await page.getByTestId('add-to-cart-btn').first().click()
  await expect(page.getByTestId('cart-count')).toHaveText('1')
})

test('admin can open admin panel', async ({ page }) => {
  await page.getByRole('link', { name: /Yönetim|Admin/i }).click()
  await expect(page.getByTestId('admin-title')).toBeVisible()
  await page.getByTestId('admin-tab-products').click()
  await expect(page.getByTestId('admin-tab-products')).toHaveClass(/active/)
})
