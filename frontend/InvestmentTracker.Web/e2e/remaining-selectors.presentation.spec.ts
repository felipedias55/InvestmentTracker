import { test, expect } from '@playwright/test';
import { testDashboard, testPortfolio } from '../src/app/features/allocation/allocation.test-data';
import { selectAsset } from './helpers/asset-selection';

test('seletores de recebimentos, eventos e correções preservam os IDs e só invalidam resultados ao trocar', async ({ page }, info) => {
  page.on('dialog', dialog => dialog.accept());
  const position = { id: 90, assetId: 10, ticker: 'ZERO', name: 'Posição zerada', currencyCode: 'BRL', quantity: '0', investedAmount: '0', currentValue: '0', income: '0', imageVersion: 'a'.repeat(64) };
  const posts: { path: string; body: any }[] = [];
  const png = await page.evaluate(() => { const c = document.createElement('canvas'); c.width = 128; c.height = 128; c.getContext('2d')!.fillRect(0, 0, 128, 128); return c.toDataURL('image/png').split(',')[1]; });
  await page.route('**/api/**', route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/assets/10/image') return route.fulfill({ body: Buffer.from(png, 'base64'), contentType: 'image/png' });
    if (route.request().method() === 'POST') {
      const body = route.request().postDataJSON(); posts.push({ path, body });
      if (path.endsWith('/correction-preview')) return route.fulfill({ json: { fromDate: body.date, movementId: null, movements: [], snapshots: [], reviewOrder: [], warnings: ['Consulta sem gravação.'] } });
      if (path.endsWith('/correction-simulation')) return route.fulfill({ json: { canApply: false, blockers: ['Bloqueio sintético'], balances: [], snapshots: [], warnings: [] } });
      if (path.endsWith('/income') || path.endsWith('/corporate-events')) return route.fulfill({ json: {} });
      throw new Error('Unexpected write: ' + path);
    }
    const responses: Record<string, unknown> = {
      '/api/portfolios': [testPortfolio], '/api/portfolios/1': { ...testDashboard.summary, positions: [position] },
      '/api/currencies': [{ id: 1, name: 'Real', code: 'BRL' }], '/api/portfolios/1/external-assets': { items: [] },
      '/api/assets': [{ id: 10, ticker: 'ZERO', name: 'Posição zerada', currencyId: 1, imageVersion: position.imageVersion }],
      '/api/portfolios/1/income/page': { items: [], total: 0, pageIndex: 0, pageSize: 10 },
      '/api/portfolios/1/income/analysis': { months: [], years: [], assets: [] },
      '/api/portfolios/1/trades/page': { items: [], total: 0, pageIndex: 0, pageSize: 10 },
      '/api/portfolios/1/movements/page': { items: [], total: 0, pageIndex: 0, pageSize: 10 },
    };
    if (!(path in responses)) throw new Error('Unexpected read: ' + path);
    return route.fulfill({ json: responses[path] });
  });
  await page.goto('/income'); await page.getByRole('button', { name: 'Registrar recebimento', exact: true }).click();
  let dialog = page.getByRole('dialog'); await dialog.locator('#income-amount').fill('12,50'); await selectAsset(dialog, 'ZERO');
  await expect(dialog.locator('.selected img')).toHaveAttribute('src', /\/api\/assets\/10\/image/);
  await dialog.locator('app-asset-selector').getByRole('button', { name: 'Trocar ativo', exact: true }).click();
  await dialog.getByRole('searchbox').fill('inexistente'); await page.keyboard.press('Escape');
  await expect(dialog.locator('#income-amount')).toHaveValue('12,50'); expect(posts).toHaveLength(0);
  await dialog.getByRole('button', { name: 'Registrar recebimento', exact: true }).click();
  await expect(dialog).toHaveCount(0); expect(posts.at(-1)!.body).toMatchObject({ assetId: 10, amount: '12.50' });
  await page.goto('/trades'); await page.getByRole('button', { name: 'Registrar evento societário', exact: true }).click(); dialog = page.getByRole('dialog');
  await selectAsset(dialog, 'ZERO'); await expect(dialog.locator('.selected img')).toHaveAttribute('src', /\/api\/assets\/10\/image/);
  await expect(dialog).toContainText('Quantidade atual: 0 unidades.');
  await dialog.locator('#event-kind').selectOption('bonus'); await dialog.locator('#event-quantity').fill('1'); await dialog.locator('#event-reason').fill('Comunicado');
  await dialog.getByRole('button', { name: 'Confirmar evento', exact: true }).click();
  await expect(dialog).toHaveCount(0); expect(posts.at(-1)!.body.positionId).toBe(90);
  await page.goto('/movements'); await page.getByRole('button', { name: 'Analisar lançamento atrasado', exact: true }).click(); dialog = page.getByRole('dialog');
  await expect(dialog).toContainText('Nenhuma posição adicional.'); await selectAsset(dialog, 'ZERO');
  await dialog.getByRole('button', { name: 'Consultar impactos', exact: true }).click(); await expect(dialog).toContainText('Consulta sem gravação.');
  expect(posts.at(-1)!.body.positionId).toBe(90);
  await selectAsset(dialog, 'ZERO'); await expect(dialog).toContainText('Consulta sem gravação.');
  await dialog.locator('app-asset-selector').getByRole('button', { name: 'Limpar seleção do ativo', exact: true }).click(); await expect(dialog).not.toContainText('Consulta sem gravação.');
  await page.keyboard.press('Escape');
  await page.getByRole('button', { name: 'Registrar operação atrasada', exact: true }).click(); dialog = page.getByRole('dialog');
  await dialog.locator('#correction-op-kind').selectOption('split'); await selectAsset(dialog, 'ZERO');
  await dialog.locator('#correction-op-numerator').fill('2'); await dialog.locator('#correction-op-denominator').fill('1'); await dialog.locator('#correction-op-reason').fill('Comunicado');
  await dialog.getByRole('button', { name: 'Simular correção', exact: true }).click(); await expect(dialog).toContainText('Bloqueio sintético');
  expect(posts.at(-1)!.body.operation.assetId).toBe(10);
  await selectAsset(dialog, 'ZERO'); await expect(dialog).toContainText('Bloqueio sintético');
  await dialog.locator('app-asset-selector').getByRole('button', { name: 'Trocar ativo', exact: true }).click();
  await dialog.locator('.choice').first().scrollIntoViewIfNeeded(); await page.screenshot({ path: info.outputPath('correction-visual-selector.png'), animations: 'disabled' });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.keyboard.press('Escape'); await dialog.getByRole('button', { name: 'Limpar seleção do ativo', exact: true }).click(); await expect(dialog).not.toContainText('Bloqueio sintético');
});
