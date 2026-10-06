import { test, expect } from '@playwright/test';
import { testDashboard, testPortfolio } from '../src/app/features/allocation/allocation.test-data';

const row = (groupId: number, name: string, share: number, target: number) => ({ groupId, name, currentValue: String(10000 * share), currentPercentage: String(share), targetPercentage: String(target), difference: String(target - share) });
const dashboard = { ...testDashboard, totalWealth: '12000',
  summary: { ...testDashboard.summary, currentValue: '10000', totalInvested: '8000', totalIncome: '500' },
  externalAssets: { ...testDashboard.externalAssets!, totalValue: '2000' },
  allocation: { categoryTargetsConfigured: true, sectorTargetsConfigured: true,
    categories: [row(1, 'Ações Brasil', 0.4, 0.3), row(2, 'Ações EUA', 0.35, 0.4), row(3, 'Fundos imobiliários', 0.25, 0.3)],
    sectors: [row(1, 'Tecnologia', 0.55, 0.4), row(2, 'Imobiliário', 0.25, 0.3), row(3, 'Consumo', 0.2, 0.3)],
    countries: [row(1, 'Brasil', 0.65, 0), row(2, 'Estados Unidos', 0.35, 0)] } };
const month = (period: string, value: string | null, id: number | null) => ({ period, snapshotId: id, snapshotDate: period + '-28', currencyCode: 'BRL',
  totalWealth: value, portfolioValue: value, externalValue: '0', totalIncome: '100', contributions: '500', withdrawals: '0', change: '1000', netFlowsBetweenSnapshots: '500', changeExcludingFlows: '500',
  comparisonStart: '2026-01-28', comparisonNote: null, hasStaleRates: false, hasFallbackRates: false, retainedIncome: '100', distributedIncome: '20', valuationAndOtherChanges: '400', economicResult: '520', returns: { modifiedDietz: '0.05', xirr: '0.1', note: null } });

test('gráficos, filtros e privacidade permanecem legíveis em todas as telas', async ({ page }, info) => {
  const errors: string[] = []; page.on('pageerror', e => errors.push(e.message));
  const income = ['2026-01', '2026-02', '2026-03'].map((period, i) => ({ period, ticker: 'TEST', assetId: 1, currencyCode: 'BRL', received: String(100 + i * 50), reversed: i === 1 ? '200' : '0', net: i === 1 ? '-50' : String(100 + i * 50) }));
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    const responses: Record<string, unknown> = {
      '/api/portfolios': [testPortfolio], '/api/portfolios/1/dashboard': dashboard,
      '/api/portfolios/1': dashboard.summary, '/api/assets': [], '/api/currencies': [{ id: 1, name: 'Real', code: 'BRL' }],
      '/api/portfolios/1/external-assets': dashboard.externalAssets,
      '/api/portfolios/1/income': [], '/api/portfolios/1/income/analysis': { months: income, years: [], assets: income.slice(0, 1) },
      '/api/portfolios/1/history': { portfolio: testPortfolio, today: '2026-03-28', months: [month('2026-01', '9000', 1), month('2026-02', null, null), month('2026-03', '10000', 3)], years: [month('2026', '10000', 3)], cashFlows: [] },
    };
    if (!(path in responses)) throw new Error('Unexpected API call: ' + path);
    await route.fulfill({ json: responses[path] });
  });
  await page.goto('/dashboard');
  await expect(page.locator('app-allocation-donut')).toHaveCount(2);
  await page.locator('#chart-search').fill('Brasil');
  await expect(page.locator('app-allocation-donut').first()).toContainText('40,0%');
  await page.getByRole('button', { name: 'Limpar filtros', exact: true }).click();
  await page.locator('#chart-type-categories').selectOption('bars');
  await page.reload();
  await expect(page.locator('#chart-type-categories')).toHaveValue('bars');
  await page.locator('#chart-type-categories').selectOption('donut');
  await page.screenshot({ path: info.outputPath('dashboard.png'), fullPage: true });
  await page.getByRole('button', { name: 'Ocultar valores', exact: true }).click();
  await expect(page.locator('.wealth-card')).toContainText('••••••');
  await expect(page.locator('.wealth-card')).not.toContainText('12.000');
  await page.goto('/history');
  await expect(page.locator('#breakdown-period')).toHaveValue('2026-03');
  await page.locator('#breakdown-period').selectOption('2026-02');
  await expect(page.locator('app-money-chart[kind="waterfall"]')).toHaveCount(0);
  await page.locator('#breakdown-period').selectOption('2026-03');
  await expect(page.locator('app-history-chart app-money-chart svg path')).toHaveCount(2);
  await expect(page.locator('app-history-chart app-money-chart svg')).not.toContainText('9.000');
  await page.getByRole('button', { name: 'Mostrar valores', exact: true }).click();
  await page.screenshot({ path: info.outputPath('history.png'), fullPage: true });
  await page.goto('/income');
  await expect(page.locator('app-money-chart svg rect')).toHaveCount(3);
  await expect(page.locator('.analysis-card')).toContainText('250,00');
  await page.screenshot({ path: info.outputPath('income.png'), fullPage: true });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  expect(errors).toEqual([]);
});

test('simulador reúne gráficos, plano em moeda original e marcas temporárias', async ({ page }, info) => {
  const positions = [{ assetCategoryId: 2, sectorId: 3, assetCategoryName: 'Ações EUA', sectorName: 'Consumo', currencyCode: 'USD', quantity: '10', exchangeRate: '5', rateDate: '2026-10-06', isStale: false, isFallback: false }];
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/portfolios') return route.fulfill({ json: [testPortfolio] });
    if (path.endsWith('/dashboard')) return route.fulfill({ json: { ...dashboard, summary: { ...dashboard.summary, positions } } });
    if (path.endsWith('/contribution-analysis')) {
      const dimension = route.request().postDataJSON().dimension;
      const groups = dimension === 'category' ? dashboard.allocation.categories : dashboard.allocation.sectors;
      return route.fulfill({ json: { dimension, currencyCode: 'BRL', amount: '100', unallocatedAmount: '0', sumOfWeights: '0.1', hasStaleRates: false, hasFallbackRates: false,
        rows: groups.map(r => ({ ...r, adjustedWeight: '0.1', suggestedContribution: r.groupId === (dimension === 'category' ? 2 : 3) ? '100' : '0' })) } });
    }
    throw new Error('Unexpected request: ' + path);
  });
  await page.goto('/contributions');
  await expect(page.locator('app-allocation-table')).toHaveCount(2);
  await page.locator('#contribution-amount').fill('100');
  await page.getByRole('button', { name: 'Simular aporte', exact: true }).click();
  await expect(page.locator('.joint-plan')).toContainText('Consumo → Ações EUA');
  await expect(page.locator('.joint-plan')).toContainText('USD 20,00');
  const checkbox = page.locator('.joint-plan input[type=checkbox]');
  await checkbox.check();
  await expect(page.locator('.joint-plan .completed')).toHaveCount(1);
  await page.getByRole('button', { name: 'Ocultar valores', exact: true }).click();
  await expect(page.locator('.joint-plan')).not.toContainText('20,00');
  await page.getByRole('button', { name: 'Mostrar valores', exact: true }).click();
  await page.screenshot({ path: info.outputPath('contributions.png'), fullPage: true });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  if (info.project.name === 'desktop') {
    const boxes = await page.locator('.comparison > section').evaluateAll(elements => elements.map(e => e.getBoundingClientRect().top));
    expect(boxes[0]).toBe(boxes[1]);
  }
  await page.locator('#contribution-amount').fill('200');
  await expect(page.locator('.joint-plan')).toHaveCount(0);
  await expect(page.locator('input[type=checkbox]:checked')).toHaveCount(0);
});
