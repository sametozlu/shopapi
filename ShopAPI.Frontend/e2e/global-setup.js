import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

export default async function globalSetup() {
  const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..')
  const e2eDb = path.join(root, 'ShopAPI.API', 'shopapi-e2e.db')

  for (const file of [e2eDb, `${e2eDb}-shm`, `${e2eDb}-wal`]) {
    if (!fs.existsSync(file)) continue
    try {
      fs.unlinkSync(file)
    } catch (error) {
      if (error?.code !== 'EBUSY' && error?.code !== 'EPERM') throw error
    }
  }
}
