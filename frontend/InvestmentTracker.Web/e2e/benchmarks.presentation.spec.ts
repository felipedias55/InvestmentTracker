import { test, expect } from '@playwright/test';
import { testPortfolio } from '../src/app/features/allocation/allocation.test-data';

test('benchmarks comparam somente sob demanda e preservam moeda, intervalo e indisponibilidades', async ({ page }, info) => {
  let queries = 0; let writes = 0;
  const row = { period: '2026-02', snapshotId: 2, snapshotDate: '2026-02-28', currencyCode: 'BRL', portfolioValue: '110', externalValue: '0', totalWealth: '110', totalIncome: '0', contributions: '0', withdrawals: '0', change: '10', netFlowsBetweenSnapshots: '0', changeExcludingFlows: '10', comparisonStart: '2026-01-31', comparisonNote: null, hasStaleRates: false, hasFallbackRates: false, returns: { modifiedDietz: '0.1', xirr: '9', note: null } };
  await page.route('**/api/**', route => {
    const url = new URL(route.request().url()), path = url.pathname;
    if (route.request().method() !== 'GET') { writes++; throw new Error('Unexpected write: ' + path); }
    if (path.endsWith('/benchmarks')) {
      queries++; expect(url.searchParams.get('period')).toBe('2026-02');
      return route.fulfill({ json: { period: row.period, start: row.comparisonStart, end: row.snapshotDate, currencyCode: 'BRL', portfolioReturn: '0.1', method: 'Diferença em pontos percentuais; referências não replicam os aportes da carteira.', items: [
        { code: 'CDI', name: 'CDI', return: '0.01', difference: '0.09', currencyCode: 'BRL', source: 'BCB/SGS 12', note: 'CDI bruto composto.', fromCache: true, retrievedAt: '2026-03-01T12:00:00Z', firstObservation: '2026-02-02', lastObservation: '2026-02-27' },
        { code: 'IPCA', name: 'IPCA', return: null, difference: null, currencyCode: 'BRL', source: 'BCB/SGS 433', note: 'Mês não publicado.', fromCache: false },
        { code: 'IBOV', name: 'Ibovespa', return: '0.05', difference: '0.05', currencyCode: 'BRL', source: 'brapi/B3', note: 'Índice Ibovespa.', fromCache: true },
        { code: 'SP500', name: 'S&P 500', return: '-0.02', difference: '0.12', currencyCode: 'BRL', source: 'FRED/S&P Dow Jones Indices', note: 'Índice de preços sem dividendos; convertido com câmbio histórico USD/BRL.', fromCache: true },
      ] } });
    }
    if (path === '/api/portfolios') return route.fulfill({ json: [testPortfolio] });
    if (path === '/api/currencies') return route.fulfill({ json: [{ id: 1, name: 'Real', code: 'BRL' }] });
    if (path === '/api/portfolios/1/history') return route.fulfill({ json: { portfolio: testPortfolio, today: '2026-11-05', months: [row, { ...row, period: '2026-03', snapshotId: null, snapshotDate: null, returns: { modifiedDietz: null, xirr: null, note: 'Fotografia desatualizada.' } }], years: [{ ...row, period: '2026' }], cashFlows: [] } });
    throw new Error('Unexpected read: ' + path);
  });
  await page.goto('/history'); const panel = page.getByRole('region', { name: 'Comparação com benchmarks', exact: true });
  await expect(panel.getByRole('button', { name: 'Comparar benchmarks', exact: true })).toBeDisabled(); expect(queries).toBe(0);
  await panel.getByLabel('Período dos benchmarks', { exact: true }).selectOption('2026-02'); await panel.getByRole('button', { name: 'Comparar benchmarks', exact: true }).click();
  await expect(panel.getByRole('status')).toContainText('10,00%');
  await expect(panel.getByRole('row', { name: /IPCA/ })).toContainText('Indisponível');
  await expect(panel.getByRole('row', { name: /Ibovespa/ })).toContainText('5,00%');
  await expect(panel).toContainText('câmbio histórico USD/BRL'); await expect(panel).not.toContainText('900,00%');
  await panel.getByRole('region', { name: 'Resultados dos benchmarks', exact: true }).scrollIntoViewIfNeeded(); await page.screenshot({ path: info.outputPath('benchmark-comparison.png'), animations: 'disabled' });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true); expect(writes).toBe(0); expect(queries).toBe(1);
  await panel.getByLabel('Período dos benchmarks', { exact: true }).selectOption('2026-03'); await expect(panel.getByRole('table')).toHaveCount(0); expect(queries).toBe(1);
  await page.getByRole('button', { name: 'Ano a ano', exact: true }).click(); await expect(panel.getByRole('button', { name: 'Comparar benchmarks', exact: true })).toBeEnabled(); expect(queries).toBe(1);
});
