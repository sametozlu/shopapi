import { test, expect } from '@playwright/test'

test('admin can login and see store', async ({ page }) => {
  await page.goto('/login')
  await page.getByTestId('login-email').fill('admin@admin.local')
  await page.getByTestId('login-password').fill('Admin123!')
  await page.getByTestId('login-submit').click()
  await expect(page.getByTestId('store-heading')).toBeVisible()
})
