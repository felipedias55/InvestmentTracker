import { test, expect } from '@playwright/test';

test('busca assistida revisa diferenças e imagem sem sobrescrever o cadastro confirmado', async ({ page }, info) => {
  const png = await page.evaluate(() => { const c = document.createElement('canvas'); c.width = 64; c.height = 64; c.getContext('2d')!.fillRect(0, 0, 64, 64); return c.toDataURL('image/png').split(',')[1]; });
  const original = { id: 12, ticker: 'LOCAL', name: 'Meu nome confirmado', assetTypeId: 1, countryId: 1, currencyId: 1, assetCategoryId: 5, sectorId: 1, imageVersion: 'a'.repeat(64), listing: { market: 'US', symbol: 'OLD' }, createdAt: '2026-11-03T12:00:00Z' };
  let saved: any = null; let defaults = 0;
  await page.route('**/api/**', route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/assets/12/image') return route.fulfill({ body: Buffer.from(png, 'base64'), contentType: 'image/png' });
    if (path === '/api/assets/12' && route.request().method() === 'PUT') { saved = route.request().postDataJSON(); return route.fulfill({ json: { ...original, ...saved, imageVersion: 'b'.repeat(64) } }); }
    if (path === '/api/assets') return route.fulfill({ json: [{ ...original, ...(saved ?? {}) }] });
    if (path === '/api/reference-defaults') { defaults++; return route.fulfill({ json: { countriesAdded: 1, currenciesAdded: 1, message: 'Referências disponíveis' } }); }
    if (path === '/api/countries') return route.fulfill({ json: [{ id: 1, name: 'Brasil' }, ...(defaults ? [{ id: 2, name: 'Estados Unidos' }] : [])] });
    if (path === '/api/currencies') return route.fulfill({ json: [{ id: 1, name: 'Meu real', code: 'BRL' }, ...(defaults ? [{ id: 2, name: 'Dólar', code: 'USD' }] : [])] });
    if (path === '/api/asset-types') return route.fulfill({ json: [{ id: 1, name: 'Ação', instrumentClass: 'general' }, { id: 2, name: 'FII', instrumentClass: 'fii' }] });
    if (path === '/api/asset-categories') return route.fulfill({ json: [{ id: 5, name: 'Minha estratégia' }] });
    if (path === '/api/sectors') return route.fulfill({ json: [{ id: 1, name: 'Imobiliário', isRealEstate: true }] });
    if (path === '/api/asset-discovery') return route.fulfill({ json: { provider: 'Fonte sintética', status: 'available', message: 'Revise os campos.', fromCache: true, items: [
      { symbol: 'TEST11', name: 'Nome sugerido', market: 'BR', currency: 'BRL', country: null, instrumentClass: 'fii', sector: 'Real Estate', hasImage: true },
      { symbol: 'TEST3', name: 'Outra empresa', market: 'BR', currency: 'BRL', country: null, instrumentClass: 'general', sector: null, hasImage: false },
    ] } });
    if (path === '/api/asset-discovery/image') return route.fulfill({ json: { status: 'available', message: 'Imagem sugerida', contentType: 'image/svg+xml', data: Buffer.from('<svg xmlns="http://www.w3.org/2000/svg" width="128" height="128" viewBox="0 0 80 40"><path d="M0 0h80v40H0z" fill="#315e99"/></svg>').toString('base64') } });
    throw new Error('Unexpected request: ' + path);
  });
  await page.goto('/assets'); await page.getByRole('button', { name: 'Editar LOCAL', exact: true }).click(); const editor = page.getByRole('dialog', { name: 'Editar ativo', exact: true });
  await editor.getByRole('button', { name: 'Disponibilizar países e moedas BR/US', exact: true }).click();
  await expect(editor.locator('#countryId option')).toHaveCount(3); await expect(editor.getByLabel('Nome', { exact: true })).toHaveValue(original.name);
  const discovery = editor.locator('app-asset-discovery'); await discovery.locator('summary').click(); await discovery.getByLabel('Ticker para buscar', { exact: true }).fill('TEST');
  await discovery.getByRole('button', { name: 'Buscar sugestões', exact: true }).click(); await expect(discovery.locator('.result')).toHaveCount(2);
  expect(saved).toBeNull(); await discovery.getByRole('button', { name: /TEST11/ }).click();
  await expect(discovery).toContainText('Equivalência de nome conhecida');
  await expect(discovery.getByLabel('Setor para revisar', { exact: true })).toHaveValue('1');
  await expect(discovery.getByRole('checkbox', { name: 'Aplicar sugestão de Setor', exact: true })).not.toBeChecked();
  await expect(discovery.getByRole('checkbox', { name: 'Aplicar sugestão de Nome', exact: true })).not.toBeChecked();
  await discovery.getByRole('checkbox', { name: 'Aplicar sugestão de Mercado e símbolo de consulta', exact: true }).check();
  await discovery.getByRole('button', { name: 'Buscar imagem sugerida', exact: true }).click();
  await expect(discovery.locator('img').last()).toHaveAttribute('src', /^data:image\/png;base64,/);
  await expect(editor.locator('app-asset-image-picker .identity img')).toHaveAttribute('src', /\/api\/assets\/12\/image/);
  await discovery.getByRole('checkbox', { name: 'Aplicar imagem sugerida (substitui a atual)', exact: true }).check();
  await discovery.locator('.table-scroll').scrollIntoViewIfNeeded();
  await page.screenshot({ path: info.outputPath('asset-discovery-review.png'), animations: 'disabled' });
  await discovery.getByRole('button', { name: 'Aplicar campos selecionados', exact: true }).click();
  await expect(editor.getByLabel('Nome', { exact: true })).toHaveValue(original.name); await expect(editor.getByLabel('Ticker', { exact: true })).toHaveValue('LOCAL');
  await expect(editor.locator('#assetCategoryId option:checked')).toHaveText('Minha estratégia');
  await expect(editor.locator('app-asset-image-picker .identity img')).toHaveAttribute('src', /^data:image\/png;base64,/);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await editor.getByRole('button', { name: 'Salvar ativo', exact: true }).click(); await expect(editor).toHaveCount(0);
  expect(saved.name).toBe(original.name); expect(saved.ticker).toBe('LOCAL'); expect(saved.assetCategoryId).toBe(5); expect(saved.listing).toEqual({ market: 'BR', symbol: 'TEST11' }); expect(saved.image.expectedVersion).toBe(original.imageVersion); expect(saved.image.data).toBeTruthy();
});

test('setor ambíguo exige revisão explícita e mantém a categoria da estratégia', async ({ page }, info) => {
  const original = { id: 1, ticker: 'TEST', name: 'Empresa local', assetTypeId: 1, countryId: 1, currencyId: 1, assetCategoryId: 1, sectorId: 1 };
  let saved: any = null;
  await page.route('**/api/**', route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/assets/1' && route.request().method() === 'PUT') { saved = route.request().postDataJSON(); return route.fulfill({ json: { ...original, ...saved } }); }
    if (path === '/api/assets') return route.fulfill({ json: [original] });
    if (path === '/api/currencies') return route.fulfill({ json: [{ id: 1, name: 'Real', code: 'BRL' }] });
    if (path === '/api/countries') return route.fulfill({ json: [{ id: 1, name: 'Brasil' }] });
    if (path === '/api/asset-types') return route.fulfill({ json: [{ id: 1, name: 'Ação', instrumentClass: 'general' }] });
    if (path === '/api/asset-categories') return route.fulfill({ json: [{ id: 1, name: 'Minha estratégia' }] });
    if (path === '/api/sectors') return route.fulfill({ json: [{ id: 1, name: 'Energia' }, { id: 2, name: 'Tecnologia' }, { id: 3, name: 'Technology' }] });
    if (path === '/api/asset-discovery') return route.fulfill({ json: { provider: 'Fonte sintética', status: 'available', message: 'Revise.', items: [
      { symbol: 'TEST', name: 'Empresa local', market: 'BR', currency: 'BRL', country: null, instrumentClass: 'general', sector: 'Technology', hasImage: false },
      { symbol: 'OTHER', name: 'Sem setor', market: 'BR', currency: 'BRL', country: null, instrumentClass: 'general', sector: null, hasImage: false },
    ] } });
    throw new Error('Unexpected request: ' + path);
  });
  await page.goto('/assets'); await page.getByRole('button', { name: 'Editar TEST', exact: true }).click();
  const editor = page.getByRole('dialog', { name: 'Editar ativo', exact: true }), discovery = editor.locator('app-asset-discovery');
  await discovery.locator('summary').click(); await discovery.getByLabel('Ticker para buscar', { exact: true }).fill('TEST'); await discovery.getByRole('button', { name: 'Buscar sugestões', exact: true }).click();
  await discovery.getByRole('button', { name: /TEST/ }).click();
  await expect(discovery).toContainText('Mais de um setor corresponde');
  await expect(discovery.getByRole('checkbox', { name: 'Aplicar sugestão de Setor', exact: true })).toHaveCount(0);
  await discovery.getByLabel('Setor para revisar', { exact: true }).selectOption('2');
  const checkbox = discovery.getByRole('checkbox', { name: 'Aplicar sugestão de Setor', exact: true });
  await checkbox.check(); await discovery.getByLabel('Setor para revisar', { exact: true }).selectOption('3'); await expect(checkbox).not.toBeChecked();
  await discovery.getByRole('button', { name: /OTHER/ }).click(); await expect(discovery).toContainText('A fonte não informou setor');
  await discovery.getByLabel('Setor para revisar', { exact: true }).selectOption('2'); await checkbox.check();
  await discovery.getByRole('button', { name: 'Aplicar campos selecionados', exact: true }).click();
  await expect(editor.locator('#sectorId option:checked')).toHaveText('Tecnologia'); await expect(editor.locator('#assetCategoryId option:checked')).toHaveText('Minha estratégia'); expect(saved).toBeNull();
  await discovery.getByRole('button', { name: 'Descartar sugestões', exact: true }).click();
  await expect(editor.locator('#sectorId option:checked')).toHaveText('Tecnologia');
  await editor.locator('#sectorId').scrollIntoViewIfNeeded(); await page.screenshot({ path: info.outputPath('reviewed-sector.png'), animations: 'disabled' });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await editor.getByRole('button', { name: 'Salvar ativo', exact: true }).click(); await expect(editor).toHaveCount(0);
  expect(saved.sectorId).toBe(2); expect(saved.assetCategoryId).toBe(1); expect(saved.ticker).toBe('TEST');
});
