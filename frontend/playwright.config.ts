import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { defineConfig, devices } from '@playwright/test'

// The e2e run uses its OWN database file (in the OS temp folder), so it never touches your
// development data. The test creates a uniquely named bug, so re-runs on the same file are fine.
// To start completely fresh, delete bugboard-e2e.db from your temp folder.
const e2eDb = join(tmpdir(), 'bugboard-e2e.db')

export default defineConfig({
  testDir: './e2e',
  timeout: 30_000,
  fullyParallel: false,
  reporter: [['list']],
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],

  // Playwright starts both servers for us (and stops them afterwards).
  webServer: [
    {
      command: 'dotnet run --project ../backend/BugBoard.Api --launch-profile http',
      url: 'http://localhost:5082/openapi/v1.json',
      timeout: 120_000,
      reuseExistingServer: true, // if you already run the API, use it instead of starting another
      env: {
        // The signing key comes from the environment, exactly like on a real server.
        Jwt__Key: 'e2e-only-signing-key-at-least-32-bytes-long!!',
        ConnectionStrings__Default: `Data Source=${e2eDb}`,
      },
    },
    {
      command: 'npm run dev',
      url: 'http://localhost:5173',
      timeout: 60_000,
      reuseExistingServer: true,
    },
  ],
})
