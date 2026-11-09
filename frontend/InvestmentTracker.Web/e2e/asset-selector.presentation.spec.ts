import { test, expect } from '@playwright/test';
import { testDashboard, testPortfolio } from '../src/app/features/allocation/allocation.test-data';

test('seletores visuais preservam o ativo e os valores durante a busca e funcionam por teclado', async ({ page }, info) => {
  page.on('dialog', d => d.accept());
  const png = await page.evaluate(() => { const c = document.createElement('canvas'); c.width = 128; c.height = 128; const x = c.getContext('2d')!; x.fillStyle = '#315e99'; x.fillRect(0, 0, 128, 128); return c.toDataURL('image/png').split(',')[1]; });
  const assets = [{ id: 1, ticker: 'BANK3', name: 'Banco exemplo', currencyId: 1, imageVersion: 'a'.repeat(64) }, { id: 2, ticker: 'CARE', name: 'Saúde internacional', currencyId: 2, imageVersion: 'b'.repeat(64) }, { id: 3, ticker: 'LOCK', name: 'Posição já cadastrada', currencyId: 1 }];
  const position = { id: 30, assetId: 3, ticker: 'LOCK', name: 'Posição já cadastrada', currencyCode: 'BRL', quantity: '1', investedAmount: '100', currentValue: '100', income: '0', baseInvestedAmount: '100', baseCurrentValue: '100', baseIncome: '0', exchangeRate: '1', rateDate: null, isStale: false, isFallback: false };
  const summary = { ...testDashboard.summary, positions: [position] }; const posts: { path: string; body: any }[] = [];
  await page.route('**/api/**', route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/assets/1/image') return route.fulfill({ body: Buffer.from(png, 'base64'), contentType: 'image/png' });
    if (path === '/api/assets/2/image') return route.fulfill({ status: 404, body: '' });
    if (route.request().method() === 'POST') { posts.push({ path, body: route.request().postDataJSON() }); return route.fulfill({ json: {} }); }
    const data: Record<string, unknown> = {
      '/api/portfolios': [testPortfolio], '/api/portfolios/defaults': { currencyCode: 'BRL' }, '/api/portfolios/1': summary,
      '/api/assets': assets, '/api/currencies': [{ id: 1, code: 'BRL', name: 'Real' }, { id: 2, code: 'USD', name: 'Dólar' }],
      '/api/portfolios/1/external-assets': { ...testDashboard.externalAssets, items: [{ id: 5, name: 'Saldo USD', currencyId: 2, currencyCode: 'USD', value: '1000' }] },
      '/api/portfolios/1/trades/page': { items: [], total: 0, pageIndex: 0, pageSize: 10 },
    };
    if (!(path in data)) throw new Error('Unexpected request: ' + path); return route.fulfill({ json: data[path] });
  });
  await page.goto('/trades'); await page.getByRole('button', { name: 'Registrar compra', exact: true }).click();
  const trade = page.getByRole('dialog', { name: 'Registrar compra', exact: true }), selector = trade.locator('app-asset-selector');
  await trade.locator('#trade-quantity').fill('3,25'); await trade.locator('#trade-price').fill('10'); await trade.locator('#trade-fees').fill('1');
  await selector.getByRole('button', { name: 'Selecionar ativo', exact: true }).click();
  const search = selector.getByRole('searchbox', { name: 'Buscar ativo por código ou nome', exact: true }); await expect(search).toBeFocused(); await search.fill('saude');
  await expect(selector.locator('.choice')).toHaveCount(1); await page.keyboard.press('Enter');
  await expect(selector.locator('.selected')).toContainText('CARE'); await expect(selector.locator('.selected .fallback')).toBeVisible();
  await trade.locator('#trade-cash').selectOption({ index: 1 });
  await selector.getByRole('button', { name: 'Trocar ativo', exact: true }).click(); await search.fill('inexistente');
  await expect(selector).toContainText('Nenhum ativo corresponde'); await expect(selector.locator('.selected')).toContainText('CARE'); await expect(trade.locator('#trade-cash option:checked')).toContainText('Saldo USD');
  await page.keyboard.press('Escape'); await expect(trade).toBeVisible(); await expect(selector.getByRole('button', { name: 'Trocar ativo', exact: true })).toBeFocused();
  await selector.getByRole('button', { name: 'Trocar ativo', exact: true }).click(); await search.fill('BANK'); await expect(selector.locator('.choice img')).toBeVisible();
  await selector.locator('.search-panel').screenshot({ path: info.outputPath('visual-asset-results.png') });
  await page.keyboard.press('ArrowDown'); await expect(selector.getByRole('button', { name: 'Selecionar BANK3 — Banco exemplo', exact: true })).toBeFocused(); await page.keyboard.press('Enter');
  await expect(trade.locator('#trade-cash option:checked')).toContainText('Dinheiro novo'); await expect(trade.locator('#trade-quantity')).toHaveValue('3,25'); await expect(trade.locator('#trade-price')).toHaveValue('10');
  await expect(selector.locator('.selected')).toContainText('BRL'); expect(posts).toHaveLength(0);
  await trade.screenshot({ path: info.outputPath('visual-asset-trade.png') }); expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await trade.getByRole('button', { name: 'Confirmar operação', exact: true }).click(); await expect(trade).toHaveCount(0);
  expect(posts[0].body).toMatchObject({ assetId: 1, quantity: '3.25', unitPrice: '10', fees: '1', cashAssetId: null, baseAmount: null });
  await page.goto('/portfolio'); await page.getByRole('button', { name: 'Editar LOCK', exact: true }).click();
  const positionEditor = page.getByRole('dialog', { name: 'Ajustar posição', exact: true });
  await expect(positionEditor.getByRole('button', { name: 'Trocar ativo', exact: true })).toBeDisabled(); await expect(positionEditor.locator('.selected')).toContainText('LOCK');
  await page.keyboard.press('Escape'); await page.getByRole('button', { name: 'Cadastrar saldo inicial', exact: true }).click();
  await positionEditor.getByRole('button', { name: 'Selecionar ativo', exact: true }).click(); await expect(positionEditor.locator('.choice')).toHaveCount(2);
  await expect(positionEditor.getByRole('button', { name: /Selecionar LOCK/ })).toHaveCount(0);
  await positionEditor.getByRole('searchbox', { name: 'Buscar ativo por código ou nome', exact: true }).fill('BANK3'); await positionEditor.getByRole('button', { name: 'Selecionar BANK3 — Banco exemplo', exact: true }).click();
  await positionEditor.locator('#quantity').fill('2'); await positionEditor.locator('#invested').fill('100'); await positionEditor.locator('#current').fill('120'); await positionEditor.locator('#income').fill('0');
  await expect(positionEditor.locator('.selected img')).toBeVisible(); await positionEditor.screenshot({ path: info.outputPath('visual-asset-position.png') });
  const positionSaved = page.waitForResponse(r => new URL(r.url()).pathname === '/api/portfolios/1/assets' && r.request().method() === 'POST');
  await positionEditor.getByRole('button', { name: 'Salvar posição', exact: true }).click(); await positionSaved; await expect(positionEditor).toHaveCount(0);
  expect(posts.at(-1)!.body).toMatchObject({ assetId: 1, quantity: '2', investedAmount: '100', currentValue: '120', income: '0' });
});
