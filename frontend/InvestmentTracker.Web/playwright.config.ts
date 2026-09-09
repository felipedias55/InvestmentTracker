import { defineConfig, devices } from '@playwright/test';

// Only the wrapper may supply the generated, disposable database.
if (!/^InvestmentTracker_E2E_[a-f0-9]{32}$/.test(process.env['INVESTMENT_E2E_DATABASE'] ?? '') ||
    !process.env['ConnectionStrings__InvestmentTracker']?.includes(process.env['INVESTMENT_E2E_DATABASE']!)) {
  throw new Error('Execute npm run test:e2e para usar uma base temporária.');
}

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  reporter: [['list'], ['html', { open: 'never' }]],
  use: { baseURL: 'http://127.0.0.1:65454', trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  projects: [
    { name: 'desktop', use: { ...devices['Desktop Chrome'] } },
    { name: 'celular', use: { ...devices['Pixel 7'] } },
  ],
  webServer: [
    {
      command: 'dotnet run --no-build --no-launch-profile --project ../../InvestmentTracker.Api -- --urls http://127.0.0.1:5053',
      url: 'http://127.0.0.1:5053/api/portfolios',
      reuseExistingServer: false,
      timeout: 60_000,
      env: { ASPNETCORE_ENVIRONMENT: 'Development' },
    },
    {
      command: 'npm run start -- --port=65454 --proxy-config=proxy.e2e.json',
      url: 'http://127.0.0.1:65454',
      reuseExistingServer: false,
      timeout: 120_000,
    },
  ],
});
