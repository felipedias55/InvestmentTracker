import { defineConfig, devices } from '@playwright/test';

// Every API request is intercepted by these tests. No database or API process is used.
export default defineConfig({
  testDir: './e2e', testMatch: 'presentation.spec.ts', workers: 1,
  reporter: 'list', use: { baseURL: 'http://127.0.0.1:65455', screenshot: 'only-on-failure' },
  projects: [{ name: 'desktop', use: { ...devices['Desktop Chrome'], viewport: { width: 1440, height: 1000 } } },
    { name: 'celular', use: { ...devices['Pixel 7'] } }],
  webServer: { command: 'npm run start -- --port=65455', url: 'http://127.0.0.1:65455', reuseExistingServer: false, timeout: 120000 },
});
