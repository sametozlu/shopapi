import { defineConfig } from '@playwright/test'
import base from './playwright.config.js'

export default defineConfig({
  ...base,
  use: {
    ...base.use,
    launchOptions: { slowMo: 500 },
  },
  webServer: base.webServer.map((server) => ({
    ...server,
    reuseExistingServer: false,
  })),
})
