import { test, expect } from '@playwright/test';

test('sugestões confirmadas salvam imagem e mercado sem alterar os valores da carteira', async ({ page, request }, info) => {
  const suffix = info.project.name === 'desktop' ? 'D' : 'C';
  const read = async (path: string) => { const response = await request.get('/api/' + path); expect(response.ok()).toBeTruthy(); return response.json(); };
  const [currencies, countries, types, categories, sectors] = await Promise.all(['currencies', 'countries', 'asset-types', 'asset-categories', 'sectors'].map(read));
  const sectorResponse = await request.post('/api/sectors', { data: { name: 'Setor revisado ' + suffix, isRealEstate: false } });
  expect(sectorResponse.ok()).toBeTruthy(); const reviewedSector = await sectorResponse.json();
  const png = await page.evaluate(() => { const c = document.createElement('canvas'); c.width = 128; c.height = 128; const x = c.getContext('2d')!; x.fillStyle = '#123456'; x.fillRect(0, 0, 128, 128); return c.toDataURL('image/png').split(',')[1]; });
  const created = await request.post('/api/assets', { data: { ticker: 'DISCOV' + suffix, name: 'Meu nome confirmado ' + suffix, assetTypeId: types.find((t: any) => t.instrumentClass === 'general').id,
    currencyId: currencies.find((c: any) => c.code === 'BRL').id, countryId: countries.find((c: any) => c.name === 'Brasil').id, assetCategoryId: categories[0].id, sectorId: sectors[0].id, image: { data: png, expectedVersion: null } } });
  expect(created.ok()).toBeTruthy(); const asset = await created.json();
  const portfolioResponse = await request.post('/api/portfolios', { data: { name: 'Busca assistida ' + suffix } }); expect(portfolioResponse.ok()).toBeTruthy(); const portfolio = await portfolioResponse.json();
  expect((await request.post(`/api/portfolios/${portfolio.id}/assets`, { data: { assetId: asset.id, quantity: '2', investedAmount: '100', currentValue: '120', income: '5' } })).ok()).toBeTruthy();
  const before = await read(`portfolios/${portfolio.id}`), count = (await read('assets')).length;
  await page.route('**/api/asset-discovery**', route => new URL(route.request().url()).pathname.endsWith('/image')
    ? route.fulfill({ json: { status: 'available', message: 'Imagem sintética', contentType: 'image/svg+xml', data: Buffer.from('<svg xmlns="http://www.w3.org/2000/svg" width="128" height="128" viewBox="0 0 80 40"><path d="M0 0h80v40H0z" fill="#315e99"/></svg>').toString('base64') } })
    : route.fulfill({ json: { status: 'available', provider: 'Fonte sintética', message: 'Revise os dados', items: [{ symbol: 'SYNTH11', name: 'Nome sugerido', market: 'BR', currency: 'BRL', country: null, instrumentClass: 'fii', sector: null, hasImage: true }] } }));
  await page.goto('/assets'); await page.getByRole('searchbox', { name: 'Buscar cadastro', exact: true }).fill(asset.ticker); await page.getByRole('button', { name: 'Editar ' + asset.ticker, exact: true }).click();
  const editor = page.getByRole('dialog', { name: 'Editar ativo', exact: true });
  const referenceSaved = page.waitForResponse(r => r.url().endsWith('/api/reference-defaults') && r.request().method() === 'POST');
  await editor.getByRole('button', { name: 'Disponibilizar países e moedas BR/US', exact: true }).click(); const references = await (await referenceSaved).json(); expect(references.countriesAdded).toBe(0); expect(references.currenciesAdded).toBe(0);
  await expect(editor.getByLabel('Nome', { exact: true })).toHaveValue(asset.name);
  const discovery = editor.locator('app-asset-discovery'); await discovery.locator('summary').click(); await discovery.getByLabel('Ticker para buscar', { exact: true }).fill('SYNTH'); await discovery.getByRole('button', { name: 'Buscar sugestões', exact: true }).click();
  await discovery.getByRole('button', { name: /SYNTH11/ }).click();
  await discovery.getByLabel('Setor para revisar', { exact: true }).selectOption(String(reviewedSector.id));
  await discovery.getByRole('checkbox', { name: 'Aplicar sugestão de Setor', exact: true }).check();
  await discovery.getByRole('button', { name: 'Buscar imagem sugerida', exact: true }).click();
  await expect(discovery.locator('img').last()).toHaveAttribute('src', /^data:image\/png;base64,/); expect((await read('assets')).length).toBe(count); expect((await read(`assets/${asset.id}`)).imageVersion).toBe(asset.imageVersion);
  await discovery.getByRole('checkbox', { name: 'Aplicar imagem sugerida (substitui a atual)', exact: true }).check(); await discovery.getByRole('button', { name: 'Aplicar campos selecionados', exact: true }).click();
  await expect(editor.getByLabel('Nome', { exact: true })).toHaveValue(asset.name); await expect(editor.getByLabel('Ticker', { exact: true })).toHaveValue(asset.ticker);
  expect((await read(`assets/${asset.id}`)).sectorId).toBe(asset.sectorId);
  await editor.locator('app-asset-image-picker').screenshot({ path: info.outputPath('suggested-image-confirmation.png') });
  await editor.getByRole('button', { name: 'Salvar ativo', exact: true }).click(); await expect(editor).toHaveCount(0);
  const saved = await read(`assets/${asset.id}`); expect(saved.listing).toEqual({ market: 'BR', symbol: 'SYNTH11' }); expect(saved.name).toBe(asset.name); expect(saved.assetCategoryId).toBe(asset.assetCategoryId); expect(saved.imageVersion).not.toBe(asset.imageVersion);
  expect(saved.sectorId).toBe(reviewedSector.id);
  const after = await read(`portfolios/${portfolio.id}`); expect(after.positions[0].sectorId).toBe(reviewedSector.id); expect(after.positions[0].sectorName).toBe(reviewedSector.name);
  const clean = (summary: any) => ({ ...summary, positions: summary.positions.map((p: any) => { const copy = { ...p }; delete copy.imageVersion; delete copy.sectorId; delete copy.sectorName; delete copy.isRealEstateSector; return copy; }) });
  expect(clean(after)).toEqual(clean(before)); expect(await read(`portfolios/${portfolio.id}/movements`)).toHaveLength(0);
});
