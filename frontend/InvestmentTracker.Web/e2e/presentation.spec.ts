import { test, expect } from '@playwright/test';
import { selectAsset } from './helpers/asset-selection';
import { testDashboard, testPortfolio } from '../src/app/features/allocation/allocation.test-data';
test.beforeEach(async ({ page }) => { page.on('dialog', dialog => dialog.accept()); });

test('cadastros agrupados e imagens mantêm padrão visual e criação contextual', async ({ page }, info) => {
  const png = await page.evaluate(() => { const c = document.createElement('canvas'); c.width = 80; c.height = 40; const x = c.getContext('2d')!; x.fillStyle = '#315e99'; x.fillRect(0, 0, 80, 40); x.fillStyle = '#f0b429'; x.fillRect(0, 0, 30, 40); return c.toDataURL('image/png').split(',')[1]; });
  const asset = { id: 1, ticker: 'FUND11', name: 'Fundo imobiliário de exemplo', assetTypeId: 1, countryId: 1, currencyId: 1, assetCategoryId: 1, sectorId: 1, createdAt: '2026-11-03T12:00:00Z', imageVersion: 'a'.repeat(64) };
  await page.route('**/api/**', route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/assets') return route.fulfill({ json: [asset, { ...asset, id: 2, ticker: 'SEMIMG', name: 'Ativo com imagem indisponível', imageVersion: 'b'.repeat(64) }] });
    if (path === '/api/assets/1/image') return route.fulfill({ body: Buffer.from(png, 'base64'), contentType: 'image/png' });
    if (path === '/api/assets/2/image') return route.fulfill({ status: 404, body: '' });
    if (path === '/api/currencies') return route.fulfill({ json: [{ id: 1, name: 'Real', code: 'BRL' }] });
    if (path === '/api/countries') return route.fulfill({ json: [{ id: 1, name: 'Brasil' }] });
    if (path === '/api/asset-types') return route.fulfill({ json: [{ id: 1, name: 'FII', instrumentClass: 'fii' }] });
    if (path === '/api/asset-categories') return route.fulfill({ json: [{ id: 1, name: 'FIIs' }] });
    if (path === '/api/sectors') return route.fulfill({ json: [{ id: 1, name: 'Imobiliário', isRealEstate: true }] });
    throw new Error('Unexpected request: ' + path);
  });
  await page.goto('/classifications'); await expect(page.getByRole('heading', { name: 'Classificações', exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Gerenciar setores', exact: true })).toBeVisible(); await page.screenshot({ path: info.outputPath('classifications-hub.png'), fullPage: true });
  await page.goto('/references'); await expect(page.getByRole('heading', { name: 'Referências', exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Gerenciar moedas', exact: true })).toBeVisible();
  await page.goto('/assets'); const card = page.locator('.asset-card').filter({ hasText: 'FUND11' }); await expect(card.locator('img')).toBeVisible();
  await expect.poll(() => card.locator('img').evaluate((img: HTMLImageElement) => img.complete && img.naturalWidth > 0)).toBe(true);
  expect(await card.locator('img').evaluate(img => {
    const css = getComputedStyle(img); return [css.paddingTop, css.paddingRight, css.paddingBottom, css.paddingLeft];
  })).toEqual(['0px', '0px', '0px', '0px']);
  await expect(page.locator('.asset-card').filter({ hasText: 'SEMIMG' }).locator('.fallback')).toBeVisible();
  await page.screenshot({ path: info.outputPath('asset-images-catalog.png'), fullPage: true });
  await page.getByRole('button', { name: 'Editar FUND11', exact: true }).click(); const editor = page.getByRole('dialog', { name: 'Editar ativo', exact: true });
  await editor.getByLabel('Nome', { exact: true }).fill('Rascunho preservado'); await editor.getByRole('button', { name: 'Criar opção de Setor', exact: true }).click();
  const child = page.getByRole('dialog', { name: 'Criar setor', exact: true }); await child.getByLabel('Nome do cadastro', { exact: true }).fill('Setor em rascunho');
  await child.screenshot({ path: info.outputPath('inline-sector-editor.png') }); await child.getByRole('button', { name: 'Cancelar', exact: true }).click();
  await expect(editor.getByLabel('Nome', { exact: true })).toHaveValue('Rascunho preservado');
  await editor.getByRole('button', { name: 'Ajustar enquadramento', exact: true }).click();
  await expect(editor.locator('.frame')).toBeVisible();
  await expect(editor.locator('app-asset-image-picker .identity img')).toHaveAttribute('src', /\/api\/assets\/1\/image/);
  await editor.getByRole('slider', { name: 'Zoom da imagem', exact: true }).focus(); await page.keyboard.press('End');
  await editor.getByRole('button', { name: 'Desfazer alteração da imagem', exact: true }).click();
  await expect(editor.locator('.frame')).toHaveCount(0);
  await editor.locator('input[type=file]').setInputFiles({ name: 'marca.png', mimeType: 'image/png', buffer: Buffer.from(png, 'base64') });
  await expect(editor.locator('app-asset-image-picker .identity img')).toHaveAttribute('src', /^data:image\/png;base64,/);
  const originalPreview = await editor.locator('.frame img').getAttribute('src');
  await editor.getByRole('slider', { name: 'Zoom da imagem', exact: true }).focus(); await page.keyboard.press('End');
  await expect(editor.locator('.frame img')).not.toHaveAttribute('src', originalPreview!);
  const zoomedPreview = await editor.locator('.frame img').getAttribute('src');
  const frame = editor.locator('.frame'); await frame.scrollIntoViewIfNeeded(); const frameBox = await frame.boundingBox();
  await page.mouse.move(frameBox!.x + frameBox!.width / 2, frameBox!.y + frameBox!.height / 2); await page.mouse.down(); await page.mouse.move(frameBox!.x + frameBox!.width / 2 + 30, frameBox!.y + frameBox!.height / 2 + 20, { steps: 4 }); await page.mouse.up();
  await expect(editor.locator('.frame img')).not.toHaveAttribute('src', zoomedPreview!);
  await expect(editor.getByRole('slider')).toHaveCount(1);
  await editor.getByRole('button', { name: 'Redefinir enquadramento', exact: true }).click(); await expect(editor.locator('.frame img')).toHaveAttribute('src', originalPreview!);
  await editor.locator('.frame').screenshot({ path: info.outputPath('asset-image-framing-preview.png') });
  await editor.getByRole('slider', { name: 'Zoom da imagem', exact: true }).scrollIntoViewIfNeeded();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: info.outputPath('asset-image-picker.png'), animations: 'disabled' });
});

test('faixas específicas e diagnósticos mantêm busca, paginação e privacidade', async ({ page }, info) => {
  const strategy = { name: 'Faixas locais', model: 'bands', remainder: 'targets', excludedCategoryIds: [], rules: [], tolerance: '0.05', bands: [{ dimension: 'category', groupId: 1, tolerance: '0.03' }, { dimension: 'sector', groupId: 1, tolerance: '0' }] };
  const diagnostics = [
    { dimension: 'category', groupId: 1, name: 'Ações', state: 'below', hasOverride: true, tolerancePoints: '3', currentPercentage: '40', targetPercentage: '50', lowerPercentage: '47', upperPercentage: '53', requestedRemainder: '100', suggestedRemainder: '40', reservedContribution: '0', reason: 'Abaixo da faixa: os limites de concentração reduziram a sugestão original do restante.' },
    { dimension: 'sector', groupId: 1, name: 'Tecnologia', state: 'below', hasOverride: true, tolerancePoints: '0', currentPercentage: '40', targetPercentage: '50', lowerPercentage: '50', upperPercentage: '50', requestedRemainder: '100', suggestedRemainder: '40', reservedContribution: '0', reason: 'Abaixo da faixa; tolerância específica de zero.' }
  ];
  const categories = Array.from({ length: 12 }, (_, i) => ({ groupId: i + 1, name: i === 0 ? 'Ações' : 'Categoria ' + i, currentValue: '0', currentPercentage: '0', targetPercentage: i === 0 ? '0.5' : '0', difference: '0' }));
  const d = { ...testDashboard, allocation: { ...testDashboard.allocation, categories, sectors: [{ ...categories[0], name: 'Tecnologia' }] } };
  await page.route('**/api/**', route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/portfolios') return route.fulfill({ json: [testPortfolio] });
    if (path.endsWith('/dashboard')) return route.fulfill({ json: d });
    if (path.endsWith('/strategies')) return route.fulfill({ json: [{ profileId: 'bands', revision: 1, config: strategy }] });
    if (path.endsWith('/contribution-plan')) { expect(route.request().postDataJSON().strategy.bands).toEqual([{ dimension: 'category', groupId: 1, tolerance: '0.0300' }, { dimension: 'sector', groupId: 1, tolerance: '0.0000' }]); return route.fulfill({ json: { dashboard: d, category: null, sector: null, plan: { rows: [], unallocated: '100' }, bands: diagnostics } }); }
    throw new Error('Unexpected request: ' + path);
  });
  await page.goto('/contributions'); await page.getByRole('button', { name: 'Configurar estratégia', exact: true }).click(); await page.locator('.rebalancing-bands > summary').click();
  const modal = page.locator('dialog:modal'); await modal.getByRole('searchbox', { name: 'Buscar grupo nas faixas', exact: true }).fill('Setor Tecnologia');
  await expect(modal.getByLabel('Modelo', { exact: true })).toHaveValue('bands');
  await expect(modal.getByRole('textbox', { name: 'Tolerância de setor Tecnologia', exact: true })).toHaveValue('0,00');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await modal.screenshot({ path: info.outputPath('specific-bands-editor.png') });
  await modal.getByRole('button', { name: 'Concluir configuração', exact: true }).click(); await page.locator('#contribution-amount').fill('100');
  await page.getByRole('button', { name: 'Simular aporte', exact: true }).click(); const report = page.locator('.band-result');
  await expect(report).toContainText('Faixa: 47% a 53%'); await expect(report).toContainText('Após limites: BRL 40,00');
  await report.screenshot({ path: info.outputPath('specific-bands-result.png') });
  await page.getByRole('button', { name: 'Ocultar valores', exact: true }).click(); await expect(report).not.toContainText('40,00'); await expect(report).not.toContainText('100,00');
  await page.getByRole('button', { name: 'Mostrar valores', exact: true }).click(); await report.getByLabel('Dimensão das faixas', { exact: true }).selectOption('sector');
  await expect(report.locator('article')).toHaveCount(1); await expect(report).toContainText('0 p.p. (específica)');
  await page.locator('#contribution-amount').fill('200'); await expect(report).toHaveCount(0);
});

test('objetivos mostram progresso e cenário sem rentabilidade com edição e revisões legíveis', async ({ page }, info) => {
  const goal = { goalId: '938313c0-d69b-4a74-9bf8-a8bdd90c1ca8', revision: 2, createdAtUtc: '2026-11-03T15:00:00Z', config: { name: 'Patrimônio para independência', kind: 'wealth', currency: 'BRL', target: '10000', deadline: '2027-01-31', monthlyContribution: '500', archived: false } };
  const incomeGoal = { ...goal, goalId: '938313c0-d69b-4a74-9bf8-a8bdd90c1ca9', config: { ...goal.config, name: 'Renda mensal desejada', kind: 'monthly-income', target: '2000', monthlyContribution: '0' } };
  const result = { items: [{ goal, status: 'in-progress', reason: 'Cenário sem rentabilidade e sem retiradas.', current: '1000', gap: '9000', percentage: '10', monthsRemaining: 3, requiredMonthlyContribution: '3000', scenarioValue: '2500', scenarioGap: '7500' },
    { goal: incomeGoal, status: 'unavailable', reason: 'Há proventos sem equivalente histórico na janela de análise.', current: null, gap: null, percentage: null, monthsRemaining: 3, requiredMonthlyContribution: null, scenarioValue: null, scenarioGap: null }],
    total: 2, pageIndex: 0, pageSize: 10, currency: 'BRL', today: '2026-11-03', incomeFrom: '2025-11-01', incomeTo: '2026-10-31', missingIncomeConversions: 1, hasStaleRates: false, hasFallbackRates: false };
  await page.route('**/api/**', route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/portfolios') return route.fulfill({ json: [testPortfolio] });
    if (path === '/api/asset-categories') return route.fulfill({ json: [{ id: 1, name: 'Ações' }] });
    if (path.endsWith('/category-targets')) return route.fulfill({ json: [{ groupId: 1, name: 'Ações', targetPercentage: '1' }] });
    if (path.endsWith('/planning-goals')) return route.fulfill({ json: result });
    if (path.endsWith('/revisions')) return route.fulfill({ json: [goal, { ...goal, revision: 1, config: { ...goal.config, target: '5000' } }] });
    throw new Error('Unexpected request: ' + path);
  });
  await page.goto('/allocation'); await page.getByRole('button', { name: 'Abrir objetivos', exact: true }).click();
  const section = page.locator('.planning-goals'); await expect(section).toContainText('Aporte mensal necessário sem rentabilidade: BRL 3.000,00'); await expect(section).toContainText('Base indisponível');
  await expect(section.locator('progress')).toHaveCount(1);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await section.screenshot({ path: info.outputPath('planning-goals.png') });
  await page.getByRole('button', { name: 'Ocultar valores', exact: true }).click(); await expect(section).not.toContainText('10.000,00'); await expect(section).not.toContainText('3.000,00');
  await page.getByRole('button', { name: 'Mostrar valores', exact: true }).click();
  await page.getByRole('button', { name: 'Editar ' + goal.config.name, exact: true }).click(); const dialog = page.locator('dialog:modal');
  await expect(dialog.getByLabel('Valor desejado', { exact: true })).toHaveValue('10.000');
  await dialog.screenshot({ path: info.outputPath('planning-goal-editor.png') }); await dialog.getByRole('button', { name: 'Fechar edição', exact: true }).click();
  await page.getByRole('button', { name: 'Revisões de ' + goal.config.name, exact: true }).click(); await expect(dialog.locator('.goal-card')).toHaveCount(2);
  await expect(dialog).toContainText('BRL 5.000,00'); await dialog.screenshot({ path: info.outputPath('planning-goal-revisions.png') });
});

test('acompanhamento de aporte preserva plano, mostra execução e protege valores', async ({ page }, info) => {
  const plan = { id: 7, name: 'Aporte mensal acompanhado', from: '2026-11-01', to: '2026-11-30', createdAtUtc: '2026-11-03T12:00:00Z' };
  const row = { key: 'rule-1-2', categoryId: 1, sectorId: 2, category: 'Ações internacionais', sector: 'Tecnologia', amount: '1000', stage: 'reservation', suggestedCurrency: 'USD', currencies: [{ currency: 'USD', amount: '200', rate: '5', dates: '', stale: false }] };
  const candidate = { tradeId: 9, date: '2026-11-03', ticker: 'TESTUS', currency: 'USD', categoryId: 1, sectorId: 2, amount: '100', baseAmount: null, active: true };
  const detail = { plan, snapshot: { currency: 'BRL', simulation: { amount: '1200' }, plan: { rows: [row], unallocated: '200' }, notes: ['Reserva preservada da estratégia original.'] },
    rows: [{ planned: row, realized: '500', difference: '500', status: 'partial' }], purchases: [{ id: 1, tradeId: 8, rowKey: row.key, date: '2026-11-02', ticker: 'REALIZADO', currency: 'USD', amount: '100', baseAmount: '500', conversionSource: 'historical', active: true, removedAtUtc: null }], realized: '500', difference: '500', status: 'partial' };
  await page.route('**/api/**', route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/portfolios') return route.fulfill({ json: [testPortfolio] });
    if (path.endsWith('/dashboard')) return route.fulfill({ json: testDashboard });
    if (path.endsWith('/strategies')) return route.fulfill({ json: [] });
    if (path.endsWith('/contribution-plans')) return route.fulfill({ json: { items: [plan], total: 1, pageIndex: 0, pageSize: 10 } });
    if (path.endsWith('/contribution-plans/7')) return route.fulfill({ json: detail });
    if (path.endsWith('/contribution-plans/7/purchases')) return route.fulfill({ json: { items: [candidate], total: 1, pageIndex: 0, pageSize: 10 } });
    throw new Error('Unexpected request: ' + path);
  });
  await page.goto('/contributions'); await page.getByRole('button', { name: 'Abrir acompanhamento', exact: true }).click();
  await page.getByRole('button', { name: 'Acompanhar ' + plan.name, exact: true }).click();
  const section = page.locator('.tracking-detail'); await expect(section).toContainText('Parcial'); await expect(section).toContainText('Realizado: BRL 500,00');
  await expect(section).toContainText('Moeda indicada: USD');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await section.screenshot({ path: info.outputPath('tracking-detail.png') });
  await page.getByRole('button', { name: 'Ocultar valores', exact: true }).click();
  await expect(section).not.toContainText('500,00'); await expect(section).not.toContainText('1.000,00');
  await page.getByRole('button', { name: 'Mostrar valores', exact: true }).click();
  await page.getByRole('button', { name: 'Vincular TESTUS', exact: true }).click();
  const dialog = page.locator('dialog:modal'); await expect(dialog).toContainText('Não usamos o câmbio atual');
  await dialog.getByLabel('Equivalente histórico na moeda do plano', { exact: true }).fill('500,1234');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await dialog.screenshot({ path: info.outputPath('tracking-link.png') });
});

test('filtros analíticos do dashboard retornam por carteira e reiniciam após recarga', async ({ page }, info) => {
  let reducedIncome = false;
  const second = { ...testPortfolio, id: 2, name: 'Segunda carteira' };
  const months = Array.from({ length: 14 }, (_, i) => ({ period: `${2025 + Math.floor(i / 12)}-${String(i % 12 + 1).padStart(2, '0')}`, currencyCode: 'USD', net: '20' }));
  await page.route('**/api/**', async route => {
    expect(route.request().method()).toBe('GET');
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/portfolios') return route.fulfill({ json: [testPortfolio, second] });
    if (['assets', 'asset-types', 'countries', 'currencies', 'asset-categories', 'sectors'].some(key => path === '/api/' + key)) return route.fulfill({ json: [] });
    const match = path.match(/^\/api\/portfolios\/([12])\/(dashboard|history|income\/analysis)$/);
    if (!match) throw new Error('Unexpected API call: ' + path);
    const portfolio = match[1] === '1' ? testPortfolio : second;
    if (match[2] === 'dashboard') return route.fulfill({ json: { ...testDashboard, summary: { ...testDashboard.summary, portfolio }, externalAssets: { ...testDashboard.externalAssets, portfolio } } });
    if (match[2] === 'history') {
      const row = { snapshotId: 1, currencyCode: 'BRL', totalWealth: '100', snapshotDate: '2025-12-31' };
      return route.fulfill({ json: { portfolio, today: '2026-10-27', months: [{ ...row, period: '2025-12' }, { ...row, period: '2026-01' }], years: [{ ...row, period: '2025' }, { ...row, period: '2026' }], cashFlows: [] } });
    }
    const rows = match[1] === '1' && !reducedIncome ? months : [{ period: '2026-01', currencyCode: 'BRL', net: '30' }];
    return route.fulfill({ json: { months: rows, years: [...new Set(rows.map(r => r.period.slice(0, 4)))].map(period => ({ period, currencyCode: rows[0].currencyCode, net: '100' })), convertedMonths: [], convertedYears: [] } });
  });
  const evolution = page.locator('[data-chart-id="evolution"]');
  const income = page.locator('[data-chart-id="income"]');
  const navigateAway = async () => {
    const open = page.getByRole('button', { name: 'Abrir menu', exact: true });
    if (await open.isVisible()) await open.click();
    await page.getByRole('navigation').getByRole('link', { name: 'Ativos', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Ativos', exact: true })).toBeVisible();
  };
  await page.goto('/dashboard');
  await evolution.getByLabel('Período', { exact: true }).selectOption('years');
  await evolution.getByLabel('Ano', { exact: true }).selectOption('2025');
  await evolution.getByRole('button', { name: 'Ver detalhes da evolução', exact: true }).click();
  await page.getByRole('dialog', { name: 'Detalhes da evolução' }).getByLabel('Detalhar fechamento').selectOption('2025');
  await page.keyboard.press('Escape');
  await income.getByLabel('Moeda dos proventos').selectOption('original:USD');
  await income.getByLabel('Períodos no gráfico').selectOption('12');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await income.getByRole('button', { name: 'Ver períodos anteriores de proventos' }).click();
  await expect(income).toContainText('1–2 de 14');
  await navigateAway(); await page.goBack();
  await expect(evolution.getByLabel('Período', { exact: true })).toHaveValue('years');
  await expect(evolution.getByLabel('Ano', { exact: true })).toHaveValue('2025');
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(income.getByLabel('Moeda dos proventos')).toHaveValue('original:USD');
  await expect(income.getByLabel('Períodos no gráfico')).toHaveValue('12');
  await expect(income).toContainText('1–2 de 14');
  await page.locator('#analysis-portfolio').selectOption('2');
  await expect(evolution.getByLabel('Período', { exact: true })).toHaveValue('months');
  await expect(evolution.getByLabel('Ano', { exact: true })).toHaveValue('');
  await expect(income.getByLabel('Moeda dos proventos')).toHaveValue('original:BRL');
  await expect(income.getByLabel('Períodos no gráfico')).toHaveValue('5');
  await page.locator('#analysis-portfolio').selectOption('1');
  await expect(income.getByLabel('Moeda dos proventos')).toHaveValue('original:USD');
  await expect(income).toContainText('1–2 de 14');
  await page.getByRole('button', { name: 'Recarregar dados', exact: true }).click();
  await expect(income).toContainText('1–2 de 14');
  await income.getByRole('button', { name: 'Ver detalhes dos proventos', exact: true }).click();
  const panel = page.getByRole('dialog', { name: 'Detalhes dos proventos' });
  await panel.getByLabel('Período', { exact: true }).selectOption('years');
  await panel.getByLabel('Ano', { exact: true }).selectOption('2025');
  await page.keyboard.press('Escape');
  await navigateAway(); await page.goBack();
  await expect(income).toContainText('Anual · 1–1 de 1');
  await income.getByRole('button', { name: 'Ver detalhes dos proventos', exact: true }).click();
  await expect(panel.getByLabel('Período', { exact: true })).toHaveValue('years');
  await expect(panel.getByLabel('Ano', { exact: true })).toHaveValue('2025');
  await page.keyboard.press('Escape');
  await income.screenshot({ path: info.outputPath('dashboard-income-context.png') });
  await navigateAway(); reducedIncome = true; await page.goBack();
  await expect(income.getByLabel('Moeda dos proventos')).toHaveValue('original:BRL');
  await income.getByRole('button', { name: 'Ver detalhes dos proventos', exact: true }).click();
  await expect(panel.getByLabel('Ano', { exact: true })).toHaveValue('');
  await page.keyboard.press('Escape');
  await page.reload();
  await expect(evolution.getByLabel('Período', { exact: true })).toHaveValue('months');
  await expect(evolution.getByLabel('Ano', { exact: true })).toHaveValue('');
  await expect(income.getByLabel('Períodos no gráfico')).toHaveValue('5');
});

test('grade acompanha conteúdo e movimentações recentes abrem consulta completa', async ({ page }, info) => {
  await page.route('**/api/**', async route => {
    expect(route.request().method()).toBe('GET');
    const data: Record<string, unknown> = {
      '/api/portfolios': [testPortfolio], '/api/portfolios/1/dashboard': dashboard,
      '/api/portfolios/1/history': { portfolio: testPortfolio, today: '2026-10-25', months: [month('2026-01', '9000', 1), month('2026-02', '10000', 2)], years: [], cashFlows: [] },
      '/api/portfolios/1/income/analysis': { months: [], years: [], convertedMonths: [], convertedYears: [] },
      '/api/portfolios/1/movements': Array.from({length:7}, (_,i) => ({ id:i+1, date:'2026-10-25', createdAtUtc:`2026-10-25T12:00:0${i}Z`, kind:i===6?'adjustment':'income', amount:i===6?'99999':'200', currencyCode:'BRL', description:'Descrição completa da movimentação para consulta', reversedById:i===6?8:null, replacesMovementId:i===6?3:null, effects:[{name:'Nome completo bastante extenso da posição ou patrimônio externo', currencyCode:'BRL', beforeValue:'0', afterValue:'1234.5678', isPosition:false}] })),
    };
    const path = new URL(route.request().url()).pathname;
    if (!(path in data)) throw new Error('Unexpected API call: '+path);
    await route.fulfill({json:data[path]});
  });
  await page.goto('/dashboard');
  const movements = page.locator('[data-chart-id="movements"]');
  await movements.getByRole('button', {name:'Expandir', exact:true}).click();
  await expect(movements.locator('article')).toHaveCount(5);
  await expect(movements.locator('article').first()).toContainText('#7');
  await expect(movements.locator('article').first().locator('.movement-amount')).toHaveCount(0);
  await expect(movements).toContainText('Estornado por #8');
  await movements.screenshot({path:info.outputPath('recent-movements.png')});
  const open = movements.getByRole('button', {name:'Ver detalhes do lançamento #7',exact:true});
  await open.focus(); await page.keyboard.press('Enter');
  const panel = page.getByRole('dialog',{name:'Detalhes da movimentação recente',exact:true});
  await expect(panel).toContainText('Nome completo bastante extenso');
  await expect(panel).toContainText('1.234,5678');
  await expect(panel).not.toContainText('99.999');
  await page.screenshot({path:info.outputPath('recent-movement-details.png')});
  await page.keyboard.press('Escape'); await expect(open).toBeFocused();
  await page.getByRole('button',{name:'Ocultar valores',exact:true}).click();
  await open.click(); await expect(panel).toContainText('••••••'); await expect(panel).not.toContainText('1.234,5678');
  await page.keyboard.press('Escape');
  const checkGeometry = async () => {
    const boxes = await page.locator('.chart-card').evaluateAll(cards => cards.map(c => {const r=c.getBoundingClientRect();return {x:r.x,y:r.y,right:r.right,bottom:r.bottom};}));
    for(let i=0;i<boxes.length;i++) for(let j=i+1;j<boxes.length;j++) {
      const a=boxes[i],b=boxes[j]; expect(a.right<=b.x+1 || b.right<=a.x+1 || a.bottom<=b.y+1 || b.bottom<=a.y+1).toBe(true);
      expect(b.y>=a.y-1).toBe(true);
    }
    expect(await page.locator('.chart-body').evaluateAll(nodes=>nodes.every(n=>!['auto','scroll'].includes(getComputedStyle(n).overflowY)))).toBe(true);
    expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  };
  await checkGeometry();
  await page.getByRole('button',{name:'Organizar dashboard',exact:true}).click();
  const evolution=page.locator('[data-chart-id="evolution"]');
  for(const size of ['compact','medium','wide']) {
    await evolution.getByLabel('Tamanho de Evolução patrimonial',{exact:true}).selectOption(size);
    await expect(evolution.locator('app-money-chart svg')).toHaveAttribute('viewBox', new RegExp(size==='compact'?'220$':'300$'));
    await checkGeometry();
  }
  await evolution.getByRole('button',{name:'Recolher',exact:true}).click();
  await page.reload(); await expect(evolution).toHaveClass(/wide/); await expect(evolution.locator('.chart-body')).toBeHidden();
  await evolution.getByRole('button',{name:'Expandir',exact:true}).click();
  await page.getByRole('button',{name:'Mostrar valores',exact:true}).click();
  await page.locator('.dashboard-grid').screenshot({path:info.outputPath('dashboard-natural-grid.png'), style:'.app-toolbar { visibility: hidden; }'});
  if(info.project.name.startsWith('desktop')) { await page.setViewportSize({width:720,height:500}); await checkGeometry(); }
});

test('destaques visuais e proventos por moeda preservam ranking e períodos completos', async ({ page }, info) => {
  const png = await page.evaluate(() => { const c = document.createElement('canvas'); c.width = 80; c.height = 40; const x = c.getContext('2d')!; x.fillStyle = '#315e99'; x.fillRect(0, 0, 80, 40); return c.toDataURL('image/png').split(',')[1]; });
  const positions = Array.from({ length: 12 }, (_, i) => ({ id: i + 1, assetId: i + 100, imageVersion: i === 0 || i === 6 ? 'a'.repeat(64) : null, ticker: `RANK${String(i).padStart(2, '0')}`, quantity: '1', currencyCode: i % 2 ? 'USD' : 'BRL', pricePerformance: { variation: String((i < 6 ? 1 : -1) * (0.6 - i % 6 * 0.05)), unitPrice: '123.45', averagePrice: '100' } }));
  const months = Array.from({ length: 14 }, (_, i) => ({ period: `${2025 + Math.floor(i / 12)}-${String(i % 12 + 1).padStart(2, '0')}`, currencyCode: 'BRL', net: i === 2 ? '-50' : '100' }));
  await page.route('**/api/**', async route => {
    expect(route.request().method()).toBe('GET');
    const data: Record<string, unknown> = {
      '/api/portfolios': [testPortfolio],
      '/api/portfolios/1/dashboard': { ...testDashboard, summary: { ...testDashboard.summary, positions } },
      '/api/portfolios/1/history': { portfolio: testPortfolio, today: '2026-02-28', months: [], years: [], cashFlows: [] },
      '/api/portfolios/1/income/analysis': { months: [...months, ...months.map(r => ({ ...r, currencyCode: 'USD', net: '20' }))], years: [{ period: '2025', currencyCode: 'BRL', net: '1000' }, { period: '2026', currencyCode: 'BRL', net: '200' }], convertedMonths: months.map((r, i) => i === 0 ? { ...r, net: null, missingConversions: 1 } : r), convertedYears: [{ period: '2025', currencyCode: 'BRL', net: '2000' }, { period: '2026', currencyCode: 'BRL', net: '400' }] },
    };
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/assets/100/image') return route.fulfill({ body: Buffer.from(png, 'base64'), contentType: 'image/png' });
    if (path === '/api/assets/106/image') return route.fulfill({ status: 404, body: '' });
    if (!(path in data)) throw new Error('Unexpected API call: ' + path);
    await route.fulfill({ json: data[path] });
  });
  await page.goto('/dashboard');
  const ranking = page.locator('[data-chart-id="highlights"]');
  await expect(ranking.locator('.leader')).toHaveCount(6);
  const firstIdentity = ranking.locator('.leader').filter({ hasText: 'RANK00' }).locator('app-asset-identity');
  await expect(firstIdentity.locator('img')).toHaveAttribute('src', '/api/assets/100/image?v=' + 'a'.repeat(64));
  await expect.poll(() => firstIdentity.locator('img').evaluate((img: HTMLImageElement) => img.complete && img.naturalWidth > 0)).toBe(true);
  await expect(ranking.locator('.leader').filter({ hasText: 'RANK06' }).locator('.fallback')).toBeVisible();
  const codeBox = await firstIdentity.locator('strong').boundingBox(), imageBox = await firstIdentity.locator('.picture').boundingBox();
  expect(codeBox!.x).toBeGreaterThanOrEqual(imageBox!.x + imageBox!.width);
  expect(imageBox!.width).toBe(32);
  await expect(ranking.locator('.leader-track i.negative')).toHaveCount(3);
  await expect(ranking.locator('.leader').filter({ hasText: '123,45' })).toHaveCount(0);
  await ranking.screenshot({ path: info.outputPath('ranking-card.png') });
  const rankAction = ranking.getByRole('button', { name: 'Ver ranking completo', exact: true });
  await rankAction.click();
  const rankPanel = page.getByRole('dialog', { name: 'Detalhes dos destaques', exact: true });
  await expect(rankPanel.locator('tbody tr')).toHaveCount(12);
  await expect(rankPanel.locator('tbody tr').filter({ hasText: 'RANK00' }).locator('img')).toHaveAttribute('src', '/api/assets/100/image?v=' + 'a'.repeat(64));
  await expect(rankPanel).toContainText('RANK11');
  await expect(rankPanel).toContainText('123,45');
  await page.screenshot({ path: info.outputPath('ranking-details.png'), fullPage: false });
  await page.keyboard.press('Escape'); await expect(rankAction).toBeFocused();
  const incomeCard = page.locator('[data-chart-id="income"]');
  await expect(incomeCard.locator('app-money-chart')).toHaveCount(1);
  await expect(incomeCard.locator('svg rect')).toHaveCount(5);
  await expect(incomeCard.getByLabel('Moeda dos proventos', { exact: true })).toHaveValue('original:BRL');
  await expect(incomeCard).toContainText('10–14 de 14');
  await incomeCard.getByRole('button', { name: 'Ver períodos anteriores de proventos', exact: true }).click();
  await expect(incomeCard).toContainText('5–9 de 14');
  await incomeCard.getByLabel('Períodos no gráfico', { exact: true }).selectOption('12');
  await expect(incomeCard.locator('svg rect')).toHaveCount(12);
  await expect(incomeCard).toContainText('3–14 de 14');
  await incomeCard.getByLabel('Períodos no gráfico', { exact: true }).selectOption('5');
  await incomeCard.getByLabel('Moeda dos proventos', { exact: true }).selectOption('original:USD');
  await expect(incomeCard).toContainText('10–14 de 14');
  await expect(incomeCard.locator('app-money-chart svg')).toContainText('US$');
  await incomeCard.getByLabel('Moeda dos proventos', { exact: true }).selectOption('base:BRL');
  await expect(incomeCard).toContainText('Há conversões pendentes');
  await incomeCard.screenshot({ path: info.outputPath('income-card.png') });
  const incomeAction = incomeCard.getByRole('button', { name: 'Ver detalhes dos proventos', exact: true });
  await incomeAction.click();
  const incomePanel = page.getByRole('dialog', { name: 'Detalhes dos proventos', exact: true });
  await expect(incomePanel.locator('tbody tr')).toHaveCount(14);
  await expect(incomePanel.locator('tbody tr').first()).toContainText('Indisponível');
  await expect(incomePanel.locator('tbody tr').filter({ hasText: '2025-03' }).locator('td')).toHaveText(/[-−].*50,00/);
  await page.screenshot({ path: info.outputPath('income-details.png'), fullPage: false });
  await incomePanel.getByLabel('Ano', { exact: true }).selectOption('2025');
  await expect(incomePanel.locator('tbody tr')).toHaveCount(12);
  await incomePanel.getByLabel('Período', { exact: true }).selectOption('years');
  await expect(incomePanel.locator('tbody tr')).toHaveCount(1);
  await incomePanel.getByRole('button', { name: 'Fechar detalhes', exact: true }).click();
  await expect(incomeAction).toBeFocused(); await expect(incomeCard).toContainText('Anual · 1–1 de 1');
  await page.getByRole('button', { name: 'Ocultar valores', exact: true }).click();
  await rankAction.click(); await expect(rankPanel).toContainText('••••••'); await expect(rankPanel).not.toContainText('123,45');
  await page.keyboard.press('Escape');
  await incomeAction.click(); await expect(incomePanel).toContainText('••••••'); await expect(incomePanel).not.toContainText('2.000,00');
  await page.keyboard.press('Escape');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});

test('distribuições compactas mantêm detalhes completos e acessíveis fora dos cartões', async ({ page }, info) => {
  const groups = Array.from({ length: 10 }, (_, i) => ({ groupId: i + 1, name: `Grupo ${String(i + 1).padStart(2, '0')} com nome extenso para conferir a legenda`, currentValue: '1000', currentPercentage: '0.1', targetPercentage: '0.12', difference: '0.02' }));
  await page.route('**/api/**', async route => {
    expect(route.request().method()).toBe('GET');
    const path = new URL(route.request().url()).pathname;
    const data: Record<string, unknown> = {
      '/api/portfolios': [testPortfolio],
      '/api/portfolios/1/dashboard': { ...testDashboard, allocation: { categoryTargetsConfigured: true, sectorTargetsConfigured: true, categories: groups, sectors: groups, countries: groups } },
      '/api/portfolios/1/history': { portfolio: testPortfolio, today: '2026-10-23', months: [], years: [], cashFlows: [] },
      '/api/portfolios/1/income/analysis': { months: [], years: [], assets: [], convertedMonths: [], convertedYears: [] },
    };
    if (!(path in data)) throw new Error('Unexpected API call: ' + path);
    await route.fulfill({ json: data[path] });
  });
  await page.goto('/dashboard');
  const card = page.locator('[data-chart-id="categories"]');
  await expect(card.locator('li')).toHaveCount(5);
  await expect(card.locator('svg circle')).toHaveCount(11);
  await expect(card).toContainText('Rosca com todos os grupos filtrados');
  await expect(card.locator('li').filter({ hasText: 'BRL' })).toHaveCount(0);
  await page.getByRole('button', { name: 'Organizar dashboard', exact: true }).click();
  await page.getByLabel('Tamanho de Distribuição por categoria', { exact: true }).selectOption('compact');
  await page.getByRole('button', { name: 'Concluir organização', exact: true }).click();
  await card.screenshot({ path: info.outputPath('distribution-donut.png') });
  const details = card.getByRole('button', { name: 'Ver detalhes de Distribuição por categoria', exact: true });
  await details.click();
  const panel = page.getByRole('dialog', { name: 'Detalhes · Distribuição por categoria', exact: true });
  await expect(panel.locator('tbody tr')).toHaveCount(10);
  await expect(panel).toContainText(groups[9].name);
  await expect(panel).toContainText('1.000,00');
  await expect(panel).toContainText('12,00%');
  await page.screenshot({ path: info.outputPath('distribution-details.png'), fullPage: false });
  await page.keyboard.press('Escape'); await expect(panel).toHaveCount(0); await expect(details).toBeFocused();
  await card.locator('select[id="chart-type-categories"]').selectOption('bars');
  await expect(card.locator('.allocation-bar')).toHaveCount(5);
  await expect(card.locator('.target-marker')).toHaveCount(5);
  if (info.project.name.startsWith('desktop')) expect(await card.locator('.chart-body').evaluate(body => body.scrollHeight <= body.clientHeight + 1)).toBe(true);
  await card.screenshot({ path: info.outputPath('distribution-bars.png') });
  await page.getByRole('button', { name: 'Ocultar valores', exact: true }).click();
  await details.click();
  await expect(panel).toContainText('••••••'); await expect(panel).not.toContainText('1.000,00');
  await panel.getByRole('button', { name: 'Fechar detalhes', exact: true }).click();
  await expect(details).toBeFocused();
  await page.getByText('Filtrar grupos', { exact: true }).click();
  await page.locator('#chart-search').fill('Grupo 01');
  await card.locator('select[id="chart-type-categories"]').selectOption('donut');
  await expect(card.locator('li')).toHaveCount(1); await expect(card).toContainText('10,0%');
  await details.click(); await expect(panel.locator('tbody tr')).toHaveCount(1);
  await page.keyboard.press('Escape');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});

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
      '/api/portfolios/1/movements': [{ id: 1, date: '2026-03-27', createdAtUtc: '2026-03-27T12:00:00Z', kind: 'income', amount: '200', currencyCode: 'BRL', effects: [{ name: 'TEST' }], reversedById: 2 }],
      '/api/portfolios/1/income': [], '/api/portfolios/1/income/page': { items: [], total: 0, pageIndex: 0, pageSize: 10 }, '/api/portfolios/1/income/analysis': { months: income, years: [], assets: income.slice(0, 1), convertedMonths: income, convertedYears: [] },
      '/api/portfolios/1/history': { portfolio: testPortfolio, today: '2026-03-28', months: [month('2026-01', '9000', 1), month('2026-02', null, null), month('2026-03', '10000', 3)], years: [month('2026', '10000', 3)], cashFlows: [] },
    };
    if (!(path in responses)) throw new Error('Unexpected API call: ' + path);
    await route.fulfill({ json: responses[path] });
  });
  await page.goto('/dashboard');
  await expect(page.locator('app-allocation-donut')).toHaveCount(2);
  await page.getByText('Filtrar grupos', { exact: true }).click();
  await page.locator('#chart-search').fill('Brasil');
  await expect(page.locator('app-allocation-donut').first()).toContainText('40,0%');
  await page.getByRole('button', { name: 'Limpar filtros', exact: true }).click();
  await page.locator('#chart-type-categories').selectOption('bars');
  await page.reload();
  await expect(page.locator('#chart-type-categories')).toHaveValue('bars');
  await page.locator('#chart-type-categories').selectOption('donut');
  await expect(page.locator('app-dashboard-analysis')).toHaveCount(3);
  const evolution = page.locator('[data-chart-id="evolution"]');
  const evolutionDetails = evolution.getByRole('button', { name: 'Ver detalhes da evolução', exact: true });
  const evolutionPanel = page.getByRole('dialog', { name: 'Detalhes da evolução', exact: true });
  await expect(evolution.locator('.metric-grid')).toHaveCount(0);
  await expect(evolution.locator('app-money-chart details')).toHaveCount(0);
  await evolution.screenshot({ path: info.outputPath('evolution-card.png') });
  await evolutionDetails.click();
  await expect(evolutionPanel.getByLabel('Detalhar fechamento', { exact: true })).toHaveValue('2026-03');
  await expect(evolutionPanel).toContainText('Dietz estimado');
  await expect(evolutionPanel).toContainText('Comparação desde 2026-01-28 até 2026-03-28');
  await page.screenshot({ path: info.outputPath('evolution-details.png'), fullPage: false });
  await evolutionPanel.getByLabel('Detalhar fechamento', { exact: true }).selectOption('2026-02');
  await expect(evolutionPanel.locator('.metric-grid')).toHaveCount(0);
  await expect(evolutionPanel).toContainText('Comparação indisponível');
  await evolutionPanel.getByLabel('Detalhar fechamento', { exact: true }).selectOption('2026-03');
  await evolutionPanel.getByText('Ver dados da evolução', { exact: true }).click();
  await expect(evolutionPanel.locator('tbody tr')).toHaveCount(3);
  await expect(evolutionPanel.locator('tbody tr').nth(1)).toContainText('Indisponível');
  await page.keyboard.press('Escape'); await expect(evolutionPanel).toHaveCount(0); await expect(evolutionDetails).toBeFocused();
  await page.getByRole('button', { name: 'Ocultar valores', exact: true }).click();
  await evolutionDetails.click();
  await expect(evolutionPanel.locator('.metric-grid')).toContainText('••••••');
  await expect(evolutionPanel).not.toContainText('10.000,00');
  await evolutionPanel.getByRole('button', { name: 'Fechar detalhes', exact: true }).click();
  await expect(evolutionDetails).toBeFocused();
  await page.getByRole('button', { name: 'Mostrar valores', exact: true }).click();
  await expect(page.locator('[data-chart-id="income"] app-money-chart')).toHaveCount(1);
  await expect(page.locator('app-dashboard-summary')).toContainText('dados parciais');
  await page.locator('app-dashboard-summary summary').click();
  await expect(page.locator('app-dashboard-summary')).toContainText('250,00');
  const recent = page.locator('.chart-card').filter({ has: page.getByRole('heading', { name: 'Movimentações recentes', exact: true }) });
  await expect(recent).toHaveClass(/is-collapsed/);
  await recent.getByRole('button', { name: 'Expandir', exact: true }).click();
  await expect(recent).toContainText('Estornado por #2');
  await page.screenshot({ path: info.outputPath('dashboard-analysis.png'), fullPage: true });
  await page.getByRole('button', { name: 'Organizar dashboard', exact: true }).click();
  await expect(page.locator('.chart-organization')).toHaveCount(7);
  await expect(page.locator('.chart-controls .drag-handle')).toHaveCount(0);
  await expect(page.locator('.chart-card').first().locator('[data-move="up"]')).toBeDisabled();
  await expect(page.locator('.chart-card').last().locator('[data-move="down"]')).toBeDisabled();
  await page.locator('.chart-card').first().screenshot({ path: info.outputPath('dashboard-organization-card.png') });
  const geometry = await page.locator('.chart-card').first().evaluate(card => {
    const box = (node: Element | null) => { const r = node?.getBoundingClientRect(); return r ? { x:r.x, y:r.y, width:r.width, height:r.height } : null; };
    const style = getComputedStyle(card);
    return { card: box(card), heading: box(card.querySelector('.chart-heading')), handle: box(card.querySelector('.drag-handle')), padding: style.padding, border: style.borderWidth, scrollY: window.scrollY };
  });
  await info.attach('organization-geometry', { body: JSON.stringify(geometry, null, 2), contentType: 'application/json' });
  if (info.project.name.startsWith('desktop')) {
    const beforeCancel = await page.locator('.chart-card').evaluateAll(cards => cards.map(card => card.getAttribute('data-chart-id')));
    const cancelHandle = page.getByRole('button', { name: 'Arrastar Distribuição por categoria', exact: true });
    await cancelHandle.scrollIntoViewIfNeeded();
    const bounds = (await cancelHandle.boundingBox())!;
    await page.mouse.move(bounds.x + 24, bounds.y + 24); await page.mouse.down();
    await page.mouse.move(bounds.x + 48, bounds.y + 32, { steps: 6 });
    await page.keyboard.press('Escape'); await page.mouse.up();
    await expect(page.locator('.chart-card.dragging')).toHaveCount(0);
    expect(await page.locator('.chart-card').evaluateAll(cards => cards.map(card => card.getAttribute('data-chart-id')))).toEqual(beforeCancel);
    for (const card of await page.locator('.chart-card').all()) { const close = card.getByRole('button', { name: 'Recolher', exact: true }); if (await close.count()) await close.click(); }
    await page.locator('.dashboard-grid').scrollIntoViewIfNeeded();
    await page.getByRole('button', { name: 'Arrastar Distribuição por país', exact: true }).dragTo(page.locator('.chart-card').first().locator('.chart-heading'), { targetPosition: { x: 15, y: 15 } });
  } else {
    await page.getByRole('button', { name: 'Mover Distribuição por país para cima', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Mover Distribuição por país para cima', exact: true })).toBeFocused();
    await page.getByRole('button', { name: 'Mover Distribuição por país para cima', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Mover Distribuição por país para baixo', exact: true })).toBeFocused();
  }
  await expect(page.locator('.chart-card').first()).toContainText('Distribuição por país');
  if (info.project.name.startsWith('desktop')) await page.locator('.chart-card').first().getByRole('button', { name: 'Expandir', exact: true }).click();
  for (const size of ['compact', 'medium', 'wide']) {
    await page.getByLabel('Tamanho de Distribuição por país', { exact: true }).selectOption(size);
    const card = page.locator('.chart-card').first();
    if (size === 'medium') await expect(card).not.toHaveClass(/compact|wide/);
    else await expect(card).toHaveClass(new RegExp(size));
    const boxes = await card.evaluate(card => ({ organization: card.querySelector('.chart-organization')!.getBoundingClientRect().bottom, heading: card.querySelector('.chart-heading')!.getBoundingClientRect().top }));
    expect(boxes.organization).toBeLessThanOrEqual(boxes.heading);
  }
  if (info.project.name.startsWith('desktop')) {
    await page.getByRole('button', { name: 'Recolher menu', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Abrir menu', exact: true })).toBeFocused();
    const down = page.getByRole('button', { name: 'Mover Distribuição por país para baixo', exact: true });
    await down.focus(); await down.press('Enter');
    await expect(page.locator('.chart-card').nth(1)).toHaveAttribute('data-chart-id', 'countries');
    await expect(down).toBeFocused();
    const up = page.getByRole('button', { name: 'Mover Distribuição por país para cima', exact: true });
    await up.focus(); await expect(up).toBeFocused(); await up.press('Enter');
    await expect(page.locator('.chart-card').first()).toContainText('Distribuição por país');
    await expect(down).toBeFocused();
    await page.locator('.chart-card').first().screenshot({ path: info.outputPath('dashboard-organization-menu-closed.png') });
  }
  await page.locator('.chart-card').first().getByRole('button', { name: 'Recolher', exact: true }).click();
  await page.reload();
  await expect(page.locator('.chart-card').first()).toHaveClass(/is-collapsed/);
  await expect(page.locator('.chart-card').first()).toHaveClass(/wide/);
  await page.locator('.chart-card').first().getByRole('button', { name: 'Expandir', exact: true }).click();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
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
  await page.getByRole('button', { name: 'Análises', exact: true }).click();
  await expect(page.locator('app-money-chart svg rect')).toHaveCount(3);
  await expect(page.locator('.analysis-card')).toContainText('250,00');
  await page.screenshot({ path: info.outputPath('income.png'), fullPage: true });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  expect(errors).toEqual([]);
});

test('simulador reúne gráficos, plano em moeda original e marcas temporárias', async ({ page }, info) => {
  const writes: string[] = [];
  const positions = [{ assetCategoryId: 2, sectorId: 3, assetCategoryName: 'Ações EUA', sectorName: 'Consumo', currencyCode: 'USD', quantity: '10', exchangeRate: '5', rateDate: '2026-10-06', isStale: false, isFallback: false }];
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (route.request().method() !== 'GET') writes.push(path);
    if (path === '/api/portfolios') return route.fulfill({ json: [testPortfolio] });
    if (path === '/api/currencies') return route.fulfill({ json: [] });
    if (path.endsWith('/dashboard')) return route.fulfill({ json: { ...dashboard, summary: { ...dashboard.summary, positions } } });
    if (path.endsWith('/strategies')) {
      if (route.request().method() === 'POST') { const body = route.request().postDataJSON(); return route.fulfill({ json: { profileId: body.profileId, revision: body.expectedRevision + 1, config: body.config } }); }
      return route.fulfill({ json: [] });
    }
    if (path.endsWith('/contribution-plan')) {
      const sent = route.request().postDataJSON();
      if (sent.strategy) {
        expect(sent.strategy.model).toBe('real-estate');
        expect(sent.strategy.excludedCategoryIds).toEqual([3]);
        expect(sent.strategy.rules).toEqual([{ categoryId: 3, kind: 'fixed', value: '500,00'.replace(',', '.'), priority: 5 }]);
        expect(sent.strategy.excludedCombinations).toEqual([{ categoryId: 2, sectorId: 3 }]);
        expect(sent.strategy.concentrationLimits).toEqual([{ dimension: 'category', groupId: 2, maximumPercentage: '35' }]);
        expect(sent.strategy.usePriorities).toBe(true);
        expect(sent.strategy.groupPreferences).toEqual([{ categoryId: 2, sectorId: 3, priority: 1, preferredCurrency: 'USD' }]);
      }
      const analysis = (dimension: string) => {
        const groups = dimension === 'category' ? dashboard.allocation.categories : dashboard.allocation.sectors;
        return { dimension, currencyCode: 'BRL', amount: '100', unallocatedAmount: '0', sumOfWeights: '0.1', hasStaleRates: false, hasFallbackRates: false,
          rows: groups.map(r => ({ ...r, currencies: [], adjustedWeight: '0.1', suggestedContribution: r.groupId === (dimension === 'category' ? 2 : 3) ? '100' : '0' })) };
      };
      return route.fulfill({ json: { dashboard: { ...dashboard, summary: { ...dashboard.summary, positions } },
        prioritiesApplied: !!sent.strategy, ruleFunding: sent.strategy ? [{ categoryId: 3, category: 'Fundos imobiliários', priority: 5, requested: '500', budget: '500', suggested: '0' }] : [],
        category: sent.strategy ? null : analysis('category'), sector: sent.strategy ? null : analysis('sector'),
        concentration: sent.strategy ? [{ dimension: 'category', groupId: 2, name: 'Ações EUA', maximumPercentage: '35', currentPercentage: '40', projectedPercentage: '40',
          requestedContribution: '0', suggestedContribution: '0', initiallyAboveLimit: true, aboveLimit: true }] : [],
        plan: sent.strategy ? { unallocated: '2000', rows: [] } : { unallocated: '0', rows: [{ key: '2-3', category: 'Ações EUA', sector: 'Consumo', amount: '100',
          suggestedCurrency: 'USD', suggestionReason: 'USD é a única moeda elegível deste grupo.',
          currencies: [{ currency: 'USD', amount: '20', rate: '5', dates: '2026-10-06', stale: false, tickers: ['TESTUS'] }] }] } } });
    }    throw new Error('Unexpected request: ' + path);
  });
  await page.goto('/contributions');
  await expect(page.locator('app-allocation-table')).toHaveCount(2);
  await page.locator('#contribution-amount').fill('100');
  await page.getByRole('button', { name: 'Simular aporte', exact: true }).click();
  await expect(page.locator('.joint-plan')).toContainText('Consumo → Ações EUA');
  await expect(page.locator('.joint-plan')).toContainText('USD 20,00');
  await expect(page.locator('.joint-plan')).toContainText('Consumo em dólar (USD)');
  const checkbox = page.locator('.joint-plan input[type=checkbox]');
  await checkbox.check();
  await expect(page.locator('.joint-plan .completed')).toHaveCount(1);
  await page.getByRole('button', { name: 'Ocultar valores', exact: true }).click();
  await expect(page.locator('.joint-plan')).not.toContainText('20,00');
  await page.getByRole('button', { name: 'Mostrar valores', exact: true }).click();
  await page.screenshot({ path: info.outputPath('contributions.png'), fullPage: true });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  if (info.project.name.startsWith('desktop')) {
    const boxes = await page.locator('.comparison > section').evaluateAll(elements => elements.map(e => e.getBoundingClientRect().top));
    expect(boxes[0]).toBe(boxes[1]);
  }
  const beforeLeaving = [...writes];
  if (info.project.name.startsWith('celular')) await page.locator('.menu-toggle').click();
  await page.locator('#app-navigation a[href="/references"]').click();
  await expect(page.getByRole('heading', { name: 'Referências', exact: true })).toBeVisible();
  await page.goBack();
  await expect(page.locator('#contribution-amount')).toHaveValue('0');
  await expect(page.locator('.joint-plan')).toHaveCount(0);
  await expect(page.locator('input[type=checkbox]:checked')).toHaveCount(0);
  expect(writes).toEqual(beforeLeaving);
  await page.screenshot({ path: info.outputPath('simulation-return.png'), fullPage: false });
  await page.locator('#contribution-amount').fill('200');
  await expect(page.locator('.joint-plan')).toHaveCount(0);
  await expect(page.locator('input[type=checkbox]:checked')).toHaveCount(0);
  await page.getByRole('button', { name: 'Configurar estratégia', exact: true }).click();
  await page.getByLabel('Usar estratégia nesta simulação').check();
  await page.getByLabel('Modelo', { exact: true }).selectOption('real-estate');
  await page.getByRole('checkbox', { name: 'Atender regras por prioridade', exact: true }).check();
  await page.getByLabel('Regras por página', { exact: true }).selectOption('25');
  await expect(page.getByLabel('Regras por página', { exact: true })).toHaveValue('25');
  const estate = page.locator('app-strategy-editor .rule').filter({ hasText: 'Fundos imobiliários' });
  await estate.getByLabel('Regra de Fundos imobiliários', { exact: true }).selectOption('fixed');
  await estate.getByLabel('Valor da regra de Fundos imobiliários', { exact: true }).fill('500,00');
  await estate.getByRole('textbox', { name: 'Prioridade da reserva de Fundos imobiliários', exact: true }).fill('5');
  await estate.getByLabel('Excluir da base do restante').check();
  await page.locator('.combination-rules > summary').click();
  await page.getByRole('checkbox', { name: 'Bloquear Ações EUA / Consumo', exact: true }).check();
  await page.getByRole('searchbox', { name: 'Buscar combinação', exact: true }).fill('sem resultados');
  await expect(page.locator('.combination-rules')).toContainText('Nenhuma combinação corresponde');
  await page.getByRole('searchbox', { name: 'Buscar combinação', exact: true }).fill('');
  await expect(page.getByRole('checkbox', { name: 'Bloquear Ações EUA / Consumo', exact: true })).toBeChecked();
  await page.getByRole('textbox', { name: 'Prioridade de Ações EUA / Consumo', exact: true }).fill('1');
  await page.getByRole('combobox', { name: 'Moeda preferida de Ações EUA / Consumo', exact: true }).selectOption('USD');
  await page.locator('.concentration-limits > summary').click();
  await page.getByRole('searchbox', { name: 'Buscar grupo nos limites', exact: true }).fill('Ações EUA');
  await page.getByRole('textbox', { name: 'Teto de categoria Ações EUA', exact: true }).fill('35');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.getByRole('button', { name: 'Salvar como estratégia padrão' }).click();
  await expect(page.locator('app-strategy-editor')).toContainText('Estratégia salva');
  await page.screenshot({ path: info.outputPath('strategy-editor.png'), fullPage: false });
  await page.getByRole('button', { name: 'Concluir configuração', exact: true }).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await page.locator('#contribution-amount').fill('2.000,00');
  await page.getByRole('button', { name: 'Simular aporte', exact: true }).click();
  await expect(page.locator('.joint-plan')).toContainText('Plano da estratégia');
  await expect(page.locator('.concentration-result')).toContainText('Projetado: 40%');
  await expect(page.locator('.concentration-result')).toContainText('nenhuma venda é sugerida');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: info.outputPath('strategy.png'), fullPage: true });
});

test('carteira Material mantém paginação, filtros e escopo das cotações', async ({ page }, info) => {
  const errors: string[] = []; page.on('pageerror', e => errors.push(e.message));
  const positions = Array.from({ length: 32 }, (_, i) => ({ id: i + 1, assetId: i + 1, ticker: `TEST${String(i).padStart(2, '0')}`, name: 'Ativo fictício para validação', currencyCode: 'BRL', quantity: '10', investedAmount: '1000', currentValue: '1200', income: '50', baseInvestedAmount: '1000', baseCurrentValue: '1200', baseIncome: '50', exchangeRate: '1', rateDate: '2026-10-20', updatedOn: '2026-10-20', isStale: false, isFallback: false, assetCategoryName: 'Ações Brasil', sectorName: 'Tecnologia', pricePerformance: { averagePrice: '100', unitPrice: '120', variation: '0.2' } }));
  let queried: number[] = [];
  let savedPosition: unknown;
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/portfolios/1/assets/11' && route.request().method() === 'PUT') {
      savedPosition = route.request().postDataJSON();
      positions[10].quantity = (savedPosition as { quantity: string }).quantity;
      return route.fulfill({ status: 204 });
    }
    if (path.endsWith('/quotes/preview')) {
      queried = route.request().postDataJSON().positionIds;
      await route.fulfill({ json: queried.map(positionId => ({ positionId, quote: { provider: 'brapi', price: null, date: null, message: 'Resposta fictícia', requested: false } })) }); return;
    }
    const responses: Record<string, unknown> = {
      '/api/portfolios': [testPortfolio, { ...testPortfolio, id: 2, name: 'Segunda carteira' }], '/api/portfolios/defaults': { currencyCode: 'BRL' },
      '/api/portfolios/1': { ...testDashboard.summary, positions },
      '/api/portfolios/2': { ...testDashboard.summary, portfolio: { ...testPortfolio, id: 2 }, positions: [] },
      '/api/portfolios/1/trades/page': { items: [], total: 0, pageIndex: 0, pageSize: 10 },
      '/api/portfolios/2/trades/page': { items: [], total: 0, pageIndex: 0, pageSize: 10 },
      '/api/portfolios/1/external-assets': { portfolio: testPortfolio, items: [], totalValue: '0', conversionAvailable: true },
      '/api/portfolios/2/external-assets': { portfolio: { ...testPortfolio, id: 2 }, items: [], totalValue: '0', conversionAvailable: true },
      '/api/portfolios/1/income/page': { items: [], total: 0, pageIndex: 0, pageSize: 10 },
      '/api/portfolios/1/income/analysis': { months: [], years: [], assets: [], convertedMonths: [], convertedYears: [] },
      '/api/assets': positions.map(p => ({ id: p.assetId, ticker: p.ticker, name: p.name, currencyId: 1 })),
      '/api/currencies': [{ id: 1, name: 'Real', code: 'BRL' }],
    };
    if (!(path in responses)) throw new Error('Unexpected API call: ' + path);
    await route.fulfill({ json: responses[path] });
  });
  await page.goto('/portfolio');
  await expect(page.locator('tr.position-card')).toHaveCount(10);
  await page.locator('#position-search').fill('TEST');
  await page.getByRole('button', { name: 'Próxima página', exact: true }).click();
  await expect(page.locator('tr.position-card')).toHaveCount(10);
  await page.getByRole('button', { name: 'Editar TEST10', exact: true }).click();
  await expect(page.locator('dialog.editing #current')).toHaveValue('1.200');
  await page.getByRole('button', { name: 'Fechar edição', exact: true }).click();
  await expect(page.locator('tr.position-card')).toHaveCount(10);
  await expect(page.getByRole('button', { name: 'Editar TEST10', exact: true })).toBeFocused();
  await page.getByRole('button', { name: 'Editar TEST10', exact: true }).click();
  await page.locator('dialog.editing #quantity').fill('12,345678');
  await page.getByRole('button', { name: 'Salvar posição', exact: true }).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  expect(savedPosition).toMatchObject({ assetId: 11, quantity: '12.345678', currentValue: '1200' });
  await expect(page.locator('#position-search')).toHaveValue('TEST');
  await expect(page.locator('app-list-paginator')).toContainText('11–20 de 32');
  await expect(page.locator('tr.position-card').filter({ hasText: 'TEST10' })).toContainText('12,345678');
  await page.screenshot({ path: info.outputPath('portfolio-saved-context.png'), fullPage: false });
  await page.getByRole('button', { name: 'Detalhes de TEST10', exact: true }).click();
  await page.getByRole('link', { name: 'Comprar', exact: true }).click();
  const tradePanel = page.getByRole('dialog', { name: 'Registrar compra', exact: true });
  await expect(tradePanel.locator('app-asset-selector .selected')).toContainText('TEST10');
  await tradePanel.getByRole('button', { name: 'Cancelar', exact: true }).click();
  await page.locator('#analysis-portfolio').selectOption('2');
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await page.getByRole('button', { name: 'Registrar compra', exact: true }).click();
  await expect(tradePanel.locator('app-asset-selector')).toContainText('Nenhum ativo selecionado.');
  await expect(tradePanel.locator('app-asset-selector .selected')).toHaveCount(0);
  await tradePanel.getByRole('button', { name: 'Cancelar', exact: true }).click();
  await page.locator('#analysis-portfolio').selectOption('1');
  await page.getByRole('link', { name: 'Voltar à Carteira', exact: true }).click();
  await expect(page.locator('app-list-paginator')).toContainText('11–20 de 32');
  await page.locator('#portfolio-select').selectOption('2');
  await expect(page.locator('tr.position-card')).toHaveCount(0);
  await page.locator('#portfolio-select').selectOption('1');
  await expect(page.locator('app-list-paginator')).toContainText('11–20 de 32');
  await page.getByRole('button', { name: 'Detalhes de TEST10', exact: true }).click();
  await page.getByRole('link', { name: 'Registrar recebimento', exact: true }).click();
  const receiptPanel = page.getByRole('dialog', { name: 'Registrar recebimento', exact: true });
  await expect(receiptPanel.locator('app-asset-selector .selected')).toContainText('TEST10');
  await receiptPanel.getByRole('button', { name: 'Cancelar', exact: true }).click();
  await page.getByRole('link', { name: 'Voltar à Carteira', exact: true }).click();
  await expect(page.locator('app-list-paginator')).toContainText('11–20 de 32');
  await page.locator('#position-search').fill('TEST0');
  await expect(page.locator('tr.position-card')).toHaveCount(10);
  await page.getByRole('button', { name: 'Limpar filtros', exact: true }).click();
  await page.getByRole('button', { name: 'Atualizar cotações dos 32 resultados filtrados', exact: true }).click();
  await expect(page.locator('#quote-query-scope')).toContainText('próxima consulta enviará 32 posições');
  await page.getByRole('button', { name: 'Buscar cotações', exact: true }).click();
  await expect.poll(() => queried.length).toBe(32);
  await page.getByRole('button', { name: 'Fechar edição', exact: true }).click();
  await page.getByRole('button', { name: 'Ocultar valores', exact: true }).click();
  await expect(page.locator('.totals')).toContainText('••••••');
  await page.getByRole('button', { name: 'Mais ações', exact: true }).click();
  await expect(page.getByRole('menu')).not.toContainText('1.200,00');
  await page.keyboard.press('Escape');
  await page.getByRole('button', { name: 'Detalhes de TEST00', exact: true }).click();
  await expect(page.getByRole('dialog')).toContainText('••••••');
  await expect(page.getByRole('dialog')).not.toContainText('1.200,00');
  await page.screenshot({ path: info.outputPath('portfolio-private-details.png'), fullPage: false });
  await page.getByRole('button', { name: 'Fechar edição', exact: true }).click();
  await page.getByRole('button', { name: 'Mostrar valores', exact: true }).click();
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({ path: info.outputPath('portfolio-material.png'), fullPage: false });
  await page.locator('tr.position-card').first().scrollIntoViewIfNeeded();
  const tableWidth = (await page.locator('.portfolio-table').boundingBox())!.width;
  expect((await page.locator('tr.position-card').first().boundingBox())!.width).toBeGreaterThan(tableWidth * 0.95);
  await page.screenshot({ path: info.outputPath('portfolio-list.png'), fullPage: false });
  if (info.project.name.startsWith('desktop')) await page.setViewportSize({ width: 720, height: 500 });
  await page.getByRole('link', { name: 'Ir para posições e filtros', exact: true }).focus();
  await page.keyboard.press('Enter');
  await expect(page.locator('#portfolio-positions')).toBeFocused();
  expect(new URL(page.url()).hash).toBe('');
  await page.screenshot({ path: info.outputPath('portfolio-section-navigation.png'), fullPage: false });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBe(true);
  expect(errors).toEqual([]);
});


test('correções de eventos mantêm formulário e confirmação legíveis e exibem bloqueios', async ({ page }, info) => {
  const errors: string[] = []; page.on('pageerror', error => errors.push(error.message));
  let simulations = 0;
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith('/correction-simulation')) {
      const op = route.request().postDataJSON().operation; simulations++;
      if (op.kind === 'bonus') return route.fulfill({ json: { canApply: false, blockers: ['Confira o comunicado: dados históricos insuficientes.'] } });
      expect(op.ratioNumerator).toBe('2'); expect(op.ratioDenominator).toBe('1'); expect(op.cashAssetId).toBeNull();
      return route.fulfill({ json: { canApply: true, token: 'event-preview', fromDate: '2026-10-01', replacedMovementIds: [3, 4],
        balances: [{ name: 'ATIVO', currencyCode: 'BRL', isPosition: true, beforeValue: '120', afterValue: '120', beforeQuantity: '6', afterQuantity: '12',
          beforeCost: '60', afterCost: '60', beforeIncome: '0', afterIncome: '0' }], snapshots: [],
        warnings: ['Evento informado: 2 novas unidades para cada 1 antigas. Confira o comunicado e as frações antes de confirmar.'], blockers: [] } });
    }
    const responses: Record<string, unknown> = {
      '/api/portfolios': [testPortfolio], '/api/portfolios/1': testDashboard.summary,
      '/api/portfolios/1/external-assets': { items: [] }, '/api/currencies': [{ id: 1, code: 'BRL', name: 'Real' }],
      '/api/portfolios/1/movements/page': { items: [], total: 0, pageIndex: 0, pageSize: 10 },
      '/api/assets': [{ id: 2, ticker: 'ATIVO', name: 'Ativo de demonstração', currencyId: 1 }],
    };
    if (!(path in responses)) throw new Error('Unexpected API call: ' + path);
    await route.fulfill({ json: responses[path] });
  });
  await page.goto('/movements');
  await page.getByRole('button', { name: 'Registrar operação atrasada', exact: true }).click();
  await page.locator('#correction-op-kind').selectOption('split');
  await selectAsset(page.getByRole('dialog'));
  await page.locator('#correction-op-numerator').fill('2'); await page.locator('#correction-op-denominator').fill('1');
  await page.locator('#correction-op-reason').fill('Conferência do comunicado');
  await page.getByRole('button', { name: 'Simular correção', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Resultado da simulação' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Confirmar correção', exact: true })).toBeDisabled();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  expect(await page.locator('dialog:modal').evaluate(element => element.scrollWidth <= element.clientWidth)).toBe(true);
  await page.screenshot({ path: info.outputPath('corporate-correction-preview.png'), fullPage: true });
  await page.locator('#correction-op-kind').selectOption('bonus');
  await page.locator('#correction-op-quantity').fill('0,5'); await page.locator('#correction-op-cost').fill('1,25');
  await expect(page.getByRole('button', { name: 'Confirmar correção', exact: true })).toHaveCount(0);
  await page.getByRole('button', { name: 'Simular correção', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('dados históricos insuficientes');
  await expect(page.getByRole('button', { name: 'Confirmar correção', exact: true })).toHaveCount(0);
  expect(simulations).toBe(2); expect(errors).toEqual([]);
});

test('movimentações paginadas com auditoria e formulários sob demanda', async ({ page }, info) => {
  const errors: string[] = []; page.on('pageerror', e => errors.push(e.message));
  const calls: string[] = [];
  const records = Array.from({ length: 23 }, (_, i) => ({ id: 23 - i, date: '2026-10-20', kind: 'deposit',
    description: 'Depósito para investimento com descrição longa de demonstração', amount: '1234.5678', currencyCode: 'BRL', reversalOfId: null, reversedById: i === 0 ? 24 : null }));
  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url()); const path = url.pathname; calls.push(path);
    if (path.endsWith('/movements/page')) {
      const index = Number(url.searchParams.get('pageIndex') || 0);
      const items = url.searchParams.get('search') === 'Depósito' ? records.slice(0, 1) : records;
      await route.fulfill({ json: { items: items.slice(index * 10, index * 10 + 10), total: items.length, pageIndex: index, pageSize: 10 } }); return;
    }
    if (path === '/api/portfolios/1/movements/13') return route.fulfill({ json: { ...records[10], createdAtUtc: '2026-10-20T12:00:00Z', canReverse: false, reversalBlockedReason: 'Auditoria fictícia.', effects: [] } });
    const responses: Record<string, unknown> = {
      '/api/portfolios': [testPortfolio, { ...testPortfolio, id: 2, name: 'Segunda carteira' }], '/api/portfolios/1': testDashboard.summary,
      '/api/portfolios/2': { ...testDashboard.summary, portfolio: { ...testPortfolio, id: 2 } },
      '/api/currencies': [{ id: 1, name: 'Real', code: 'BRL' }],
      '/api/portfolios/1/external-assets': { items: [{ id: 5, name: 'Saldo de demonstração', currencyId: 1, currencyCode: 'BRL', value: '1000' }] },
      '/api/portfolios/2/external-assets': { items: [] },
      '/api/portfolios/1/movements/23': { ...records[0], createdAtUtc: '2026-10-20T12:00:00Z', canReverse: false, reversalBlockedReason: 'Já estornado.', effects: [{ name: 'Saldo', currencyCode: 'BRL', beforeValue: '0', afterValue: '1234.5678', isPosition: false }] },
    };
    if (!(path in responses)) throw new Error('Unexpected API call: ' + path);
    await route.fulfill({ json: responses[path] });
  });
  await page.goto('/movements');
  await expect(page.locator('.movement-row')).toHaveCount(10);
  await expect(page.getByRole('dialog')).toHaveCount(0);
  expect(calls).not.toContain('/api/portfolios/1/movements/23');
  await page.getByRole('button', { name: 'Próxima página', exact: true }).click();
  await expect(page.locator('.movement-row').first()).toContainText('#13');
  await page.getByRole('searchbox', { name: 'Buscar movimentação' }).fill('Depósito');
  await expect(page.locator('app-filter-summary')).toContainText('Há filtros em edição');
  await expect(page.locator('app-filter-summary')).toContainText('Filtros aplicados: Nenhum');
  await page.getByRole('button', { name: 'Aplicar filtros', exact: true }).click();
  await expect(page.locator('app-filter-summary')).toContainText('Busca: Depósito');
  await expect(page.locator('app-filter-summary')).not.toContainText('Há filtros em edição');
  await expect(page.locator('.movement-row')).toHaveCount(1);
  await expect(page.locator('.movement-row')).toContainText('Estornado por #24');
  await page.getByRole('button', { name: 'Ver detalhes do lançamento #23', exact: true }).click();
  await expect(page.getByRole('dialog')).toContainText('Já estornado.');
  await expect(page.getByRole('dialog')).toContainText('1.234,5678');
  await page.screenshot({ path: info.outputPath('movements-audit.png'), fullPage: true });
  await page.getByRole('button', { name: 'Fechar edição', exact: true }).click();
  await page.getByRole('button', { name: 'Movimentar saldo', exact: true }).click();
  await page.getByLabel('Saldo', { exact: true }).selectOption({ index: 1 });
  await page.getByLabel('Valor da movimentação (BRL)', { exact: true }).fill('12,50');
  await page.screenshot({ path: info.outputPath('movements-form.png'), fullPage: true });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await page.getByRole('searchbox', { name: 'Buscar movimentação' }).fill('demonstração');
  await page.getByRole('button', { name: 'Aplicar filtros', exact: true }).click();
  await page.getByRole('button', { name: 'Próxima página', exact: true }).click();
  await page.getByRole('button', { name: 'Ocultar valores', exact: true }).click();
  await page.getByRole('button', { name: 'Ver detalhes do lançamento #13', exact: true }).click();
  await expect(page.getByRole('dialog')).toContainText('Auditoria fictícia.');
  await expect(page.getByRole('dialog')).toContainText('••••••');
  await expect(page.getByRole('dialog')).not.toContainText('1.234,5678');
  await page.screenshot({ path: info.outputPath('movement-private-audit.png'), fullPage: false });
  await page.getByRole('button', { name: 'Fechar edição', exact: true }).click();
  if (info.project.name.startsWith('celular')) await page.locator('.menu-toggle').click();
  await page.locator('#app-navigation a[href="/references"]').click();
  await expect(page.getByRole('heading', { name: 'Referências', exact: true })).toBeVisible();
  await page.goBack();
  await expect(page.getByRole('searchbox', { name: 'Buscar movimentação' })).toHaveValue('demonstração');
  await expect(page.locator('app-list-paginator')).toContainText('11–20 de 23');
  await page.locator('#analysis-portfolio').selectOption('2');
  await expect(page.getByRole('searchbox', { name: 'Buscar movimentação' })).toHaveValue('');
  await expect(page.locator('app-list-paginator')).toContainText('1–10 de 23');
  await page.locator('#analysis-portfolio').selectOption('1');
  await expect(page.getByRole('searchbox', { name: 'Buscar movimentação' })).toHaveValue('demonstração');
  await expect(page.locator('app-list-paginator')).toContainText('11–20 de 23');
  await page.getByRole('button', { name: 'Mostrar valores', exact: true }).click();
  await page.screenshot({ path: info.outputPath('movements-list.png'), fullPage: true });
  expect(errors).toEqual([]);
});


test('compras, vendas e eventos com histórico compacto e formulários Material', async ({ page }, info) => {
  const errors: string[] = []; page.on('pageerror', e => errors.push(e.message));
  const trades = Array.from({ length: 23 }, (_, i) => ({ trade: { id: 23 - i, date: '2026-10-20', kind: 'buy', ticker: 'TEST', currencyCode: 'BRL', quantity: '2', unitPrice: '12.5', amount: '26', fees: '1', cashAssetName: null }, movementId: 100 - i, reversedById: i === 0 ? 101 : null }));
  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url()); const path = url.pathname;
    if (path.endsWith('/trades/page')) {
      const index = Number(url.searchParams.get('pageIndex') || 0);
      await route.fulfill({ json: { items: trades.slice(index * 10, index * 10 + 10), total: 23, pageIndex: index, pageSize: 10 } }); return;
    }
    const responses: Record<string, unknown> = {
      '/api/portfolios': [testPortfolio], '/api/portfolios/1': { ...testDashboard.summary, positions: [{ id: 7, assetId: 10, ticker: 'TEST', quantity: '10' }] },
      '/api/assets': [{ id: 10, ticker: 'TEST', name: 'Ativo de demonstração', currencyId: 1 }, { id: 11, ticker: 'OTHER', name: 'Outro ativo', currencyId: 1 }],
      '/api/currencies': [{ id: 1, name: 'Real', code: 'BRL' }],
      '/api/portfolios/1/external-assets': { items: [{ id: 5, name: 'Saldo de demonstração', currencyId: 1, currencyCode: 'BRL', value: '1000' }] },
    };
    if (!(path in responses)) throw new Error('Unexpected API call: ' + path);
    await route.fulfill({ json: responses[path] });
  });
  await page.goto('/trades');
  await expect(page.locator('.trade-row')).toHaveCount(10);
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(page.locator('.trade-row').first()).toContainText('Estornada pelo lançamento #101');
  await page.getByLabel('Detalhes da operação #23').click();
  await expect(page.locator('.trade-row').first()).toContainText('Aporte automático');
  await page.getByRole('button', { name: 'Próxima página', exact: true }).click();
  await expect(page.locator('.trade-row').first()).toContainText('#13');
  await page.getByRole('button', { name: 'Registrar compra', exact: true }).click();
  await page.getByRole('button', { name: 'Selecionar ativo', exact: true }).click();
  await page.getByRole('searchbox', { name: 'Buscar ativo por código ou nome' }).fill('TEST');
  await page.getByRole('button', { name: 'Selecionar TEST — Ativo de demonstração', exact: true }).click();
  await page.getByLabel('Quantidade comprada').fill('2');
  await page.locator('#trade-price').fill('12,50');
  await page.locator('#trade-fees').fill('1');
  await expect(page.getByRole('dialog')).toContainText('26,00');
  await page.screenshot({ path: info.outputPath('trade-form.png'), fullPage: false });
  await page.getByRole('button', { name: 'Fechar edição', exact: true }).click();
  await expect(page.locator('.trade-row').first()).toContainText('#13');
  await page.getByRole('button', { name: 'Registrar venda', exact: true }).click();
  await expect(page.locator('#trade-kind')).toHaveValue('sell');
  await expect(page.getByRole('dialog')).toContainText('Retirar da carteira');
  await page.keyboard.press('Escape');
  await page.getByRole('button', { name: 'Registrar evento societário', exact: true }).click();
  await page.locator('#event-kind').selectOption('bonus');
  await selectAsset(page.getByRole('dialog'));
  await page.locator('#event-quantity').fill('1');
  await page.locator('#event-cost').fill('10');
  await page.locator('#event-reason').fill('Comunicado fictício');
  await page.screenshot({ path: info.outputPath('event-form.png'), fullPage: false });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1)).toBe(true);
  await page.keyboard.press('Escape');
  await page.getByRole('button', { name: 'Ocultar valores', exact: true }).click();
  await expect(page.locator('.trade-row').first()).not.toContainText('26,00');
  await page.screenshot({ path: info.outputPath('trades-list.png'), fullPage: true });
  expect(errors).toEqual([]);
});


test('proventos separa análise, paginação e conversões pendentes', async ({ page }, info) => {
  const errors: string[] = []; page.on('pageerror', e => errors.push(e.message));
  const receipts = Array.from({ length: 23 }, (_, i) => ({ id: 23 - i, date: '2026-10-20', ticker: 'TEST', currencyCode: 'USD', amount: '10', cashAssetName: null, notes: 'Dados fictícios', conversions: [], reversedById: i === 0 ? 80 : null }));
  const row = { period: '2026-10', ticker: 'TEST', assetId: 10, currencyCode: 'USD', received: '230', reversed: '10', net: '220' };
  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url()); const path = url.pathname;
    if (path.endsWith('/income/page')) {
      const index = Number(url.searchParams.get('pageIndex') || 0);
      await route.fulfill({ json: { items: receipts.slice(index * 10, index * 10 + 10), total: 23, pageIndex: index, pageSize: 10 } }); return;
    }
    const responses: Record<string, unknown> = {
      '/api/portfolios': [testPortfolio], '/api/portfolios/1': { ...testDashboard.summary, positions: [{ id: 7, assetId: 10, ticker: 'TEST', name: 'Ativo fictício', quantity: '0' }] },
      '/api/assets': [{ id: 10, ticker: 'TEST', name: 'Ativo fictício', currencyId: 2 }],
      '/api/currencies': [{ id: 1, name: 'Real', code: 'BRL' }, { id: 2, name: 'Dólar', code: 'USD' }],
      '/api/portfolios/1/external-assets': { items: [] },
      '/api/portfolios/1/income/analysis': { months: [row], years: [], assets: [row], convertedMonths: [{ ...row, currencyCode: 'BRL', received: null, reversed: null, net: null }] },
    };
    if (!(path in responses)) throw new Error('Unexpected API call: ' + path);
    await route.fulfill({ json: responses[path] });
  });
  await page.goto('/income');
  await expect(page.locator('.income-row')).toHaveCount(10);
  await expect(page.locator('.income-row').first()).toContainText('Estornado pelo lançamento #80');
  await page.getByRole('button', { name: 'Completar conversão #23', exact: true }).click();
  await page.getByLabel('Equivalente real (opcional)').fill('51,2345');
  await page.getByLabel('Motivo da complementação ou correção').fill('Conferência fictícia');
  await page.screenshot({ path: info.outputPath('income-conversion.png'), fullPage: false });
  await page.keyboard.press('Escape');
  await page.getByRole('button', { name: 'Próxima página', exact: true }).click();
  await expect(page.locator('.income-row').first()).toContainText('#13');
  await page.getByRole('button', { name: 'Análises', exact: true }).click();
  await expect(page.locator('.analysis-card')).toContainText('220,00');
  await page.locator('#income-view').selectOption('base');
  await expect(page.locator('.analysis-card')).toContainText('Conversão pendente');
  await page.getByRole('button', { name: 'Recebimentos', exact: true }).click();
  await expect(page.locator('.income-row').first()).toContainText('#13');
  await page.getByRole('button', { name: 'Registrar recebimento', exact: true }).click();
  await selectAsset(page.getByRole('dialog'));
  await page.locator('#income-amount').fill('10,50');
  await page.screenshot({ path: info.outputPath('income-receipt.png'), fullPage: false });
  await page.keyboard.press('Escape');
  await page.getByRole('button', { name: 'Ocultar valores', exact: true }).click();
  await expect(page.locator('.income-row').first()).not.toContainText('10,00');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1)).toBe(true);
  await page.screenshot({ path: info.outputPath('income-list.png'), fullPage: true });
  expect(errors).toEqual([]);
});


test('histórico separa evolução, fotografias e fluxos com paginação e confirmação', async ({ page }, info) => {
  const errors: string[] = []; page.on('pageerror', e => errors.push(e.message));
  const months = Array.from({ length: 12 }, (_, i) => ({ ...month('2026-' + String(i + 1).padStart(2, '0'), String(9000 + i * 200), i + 1), isOutdated: i === 8, isReopened: i === 8, revision: 2 }));
  const flows = Array.from({ length: 12 }, (_, i) => ({ id: 12 - i, date: '2026-09-20', kind: 'contribution', currencyId: 2, currencyCode: 'USD', amount: '20.1234', baseAmount: '100.5678', baseCurrencyCode: 'BRL', notes: 'Registro histórico de demonstração' }));
  let captureCount = 0;
  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url()), path = url.pathname;
    if (path.endsWith('/cash-flows/page')) {
      const index = Number(url.searchParams.get('pageIndex') || 0), size = Number(url.searchParams.get('pageSize') || 10);
      const filtered = url.searchParams.get('kind') === 'withdrawal' ? [] : flows;
      await route.fulfill({ json: { items: filtered.slice(index * size, (index + 1) * size), total: filtered.length, pageIndex: index, pageSize: size } }); return;
    }
    if (path.endsWith('/snapshots/current')) { captureCount++; await route.fulfill({ json: {} }); return; }
    if (/\/snapshots\/\d+$/.test(path)) {
      const previous = { id: 9, month: '2026-09-01', snapshotDate: '2026-09-20', capturedAtUtc: '2026-09-20T15:00:00Z', payloadVersion: 1, revision: 1, dashboard };
      await route.fulfill({ json: { ...previous, revision: 2, isOutdated: true, previousVersions: [previous] } }); return;
    }
    const responses: Record<string, unknown> = {
      '/api/portfolios': [testPortfolio],
      '/api/currencies': [{ id: 1, code: 'BRL', name: 'Real' }, { id: 2, code: 'USD', name: 'Dólar' }],
      '/api/portfolios/1/history': { portfolio: testPortfolio, today: '2026-09-28', months, years: [{ ...months[8], period: '2026' }], cashFlows: [] },
    };
    if (!(path in responses)) throw new Error('Unexpected API call: ' + path);
    await route.fulfill({ json: responses[path] });
  });
  await page.goto('/history');
  await expect(page.locator('app-history-chart')).toBeVisible();
  await expect(page.locator('.capture-panel')).toHaveCount(0);
  await page.getByRole('button', { name: 'Fotografias', exact: true }).click();
  await expect(page.locator('app-history-chart')).toHaveCount(0);
  await expect(page.locator('app-list-paginator')).toContainText('1–10 de 12');
  await expect(page.getByText('Reaberta / desatualizada', { exact: false })).toBeVisible();
  await page.getByRole('button', { name: 'Ver fotografia', exact: true }).first().click();
  const photo = page.getByRole('dialog', { name: 'Consultar fotografia', exact: true });
  await expect(photo.getByText('Fotografia desatualizada: valores preservados para auditoria.')).toBeVisible();
  await photo.getByLabel('Versão da fotografia').selectOption('1');
  await expect(photo.getByLabel('Versão da fotografia')).toHaveValue('1');
  await photo.getByLabel('Versão da fotografia').selectOption('2');
  await page.screenshot({ path: info.outputPath('history-photo.png'), fullPage: true });
  await page.keyboard.press('Escape'); await expect(photo).toHaveCount(0);
  await page.getByRole('button', { name: 'Próxima página', exact: true }).click();
  await expect(page.locator('app-list-paginator')).toContainText('11–12 de 12');
  await page.getByRole('button', { name: 'Atualizar fotografia deste mês', exact: true }).click();
  expect(captureCount).toBe(0);
  await expect(page.getByRole('dialog', { name: 'Confirmar fotografia', exact: true })).toContainText('versão anterior será preservada');
  await page.getByRole('button', { name: 'Confirmar fotografia', exact: true }).click();
  await expect(page.getByRole('dialog')).toHaveCount(0); expect(captureCount).toBe(1);
  await expect(page.locator('app-list-paginator')).toContainText('11–12 de 12');
  await page.getByRole('button', { name: 'Aportes e retiradas', exact: true }).click();
  await expect(page.getByRole('table', { name: 'Aportes e retiradas', exact: true }).locator('tbody tr')).toHaveCount(10);
  await page.getByRole('button', { name: 'Próxima página', exact: true }).click();
  await expect(page.locator('app-list-paginator')).toContainText('11–12 de 12');
  await page.getByRole('button', { name: 'Editar', exact: true }).first().click();
  const editor = page.getByRole('dialog', { name: 'Editar histórico antigo', exact: true });
  await expect(editor.getByLabel('Valor original', { exact: true })).toHaveValue('20,1234');
  await expect(editor.getByLabel('Equivalente em BRL (opcional)', { exact: true })).toHaveValue('100,5678');
  await page.screenshot({ path: info.outputPath('history-form.png'), fullPage: true });
  await page.keyboard.press('Escape'); await expect(editor).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Editar', exact: true }).first()).toBeFocused();
  await page.getByLabel('Tipo', { exact: true }).selectOption('withdrawal');
  await expect(page.locator('app-list-paginator')).toContainText('0 registros');
  await page.getByLabel('Tipo', { exact: true }).selectOption('');
  await expect(page.locator('app-list-paginator')).toContainText('1–10 de 12');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: info.outputPath('history-flows.png'), fullPage: true });
  expect(errors).toEqual([]);
});


test('cadastros e patrimônio preservam contexto, totais e formulários no desktop e celular', async ({ page }, info) => {
  const errors: string[] = []; page.on('pageerror', e => errors.push(e.message));
  const assets = Array.from({ length: 12 }, (_, i) => ({ id: i + 1, ticker: 'TEST' + String(i).padStart(2, '0'), name: 'Ativo de demonstração ' + i, assetTypeId: 1, countryId: 1, currencyId: 1, assetCategoryId: i < 11 ? 1 : 2, sectorId: 1, createdAt: '2026-10-01T12:00:00Z' }));
  const catalogs = Array.from({ length: 12 }, (_, i) => ({ id: i + 1, name: 'Setor ' + String(i).padStart(2, '0') }));
  const external = Array.from({ length: 12 }, (_, i) => ({ id: i + 1, name: 'Reserva ' + String(i).padStart(2, '0'), currencyId: 1, currencyCode: 'BRL', value: '1234.5678', baseValue: '1234.5678', description: 'Saldo fictício para revisão visual', rateDate: null, exchangeRate: '1', isStale: false, isFallback: false }));
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (route.request().method() === 'PUT' && path.startsWith('/api/assets/')) {
      const item = assets.find(x => x.id === Number(path.split('/').at(-1)))!; Object.assign(item, route.request().postDataJSON());
      await route.fulfill({ status: 204 }); return;
    }
    if (route.request().method() === 'DELETE' && path.startsWith('/api/sectors/')) {
      await route.fulfill({ status: 409, json: { detail: 'O setor está em uso.' } }); return;
    }
    const responses: Record<string, unknown> = {
      '/api/portfolios': [testPortfolio], '/api/assets': assets, '/api/sectors': catalogs,
      '/api/currencies': [{ id: 1, name: 'Real', code: 'BRL' }],
      '/api/asset-types': [{ id: 1, name: 'Ações' }], '/api/countries': [{ id: 1, name: 'Brasil' }],
      '/api/asset-categories': [{ id: 1, name: 'Ações Brasil' }, { id: 2, name: 'Fundos' }],
      '/api/portfolios/1/external-assets': { portfolio: testPortfolio, items: external, totalValue: '14814.8136', conversionAvailable: true, hasStaleRates: false, hasFallbackRates: false },
    };
    if (!(path in responses)) throw new Error('Unexpected API call: ' + path);
    await route.fulfill({ json: responses[path] });
  });
  await page.goto('/assets');
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(page.locator('app-list-paginator')).toContainText('1–10 de 12');
  await page.getByLabel('Categoria', { exact: true }).selectOption('1');
  await expect(page.locator('app-list-paginator')).toContainText('1–10 de 11');
  await page.getByRole('button', { name: 'Próxima página', exact: true }).click();
  await page.getByRole('button', { name: 'Editar TEST10', exact: true }).click();
  const editor = page.getByRole('dialog', { name: 'Editar ativo', exact: true });
  await editor.getByLabel('Nome', { exact: true }).fill('Ativo revisado');
  page.removeAllListeners('dialog');
  page.once('dialog', dialog => dialog.dismiss());
  await page.keyboard.press('Escape');
  await expect(editor).toBeVisible();
  await expect(editor.getByLabel('Nome', { exact: true })).toHaveValue('Ativo revisado');
  page.on('dialog', dialog => dialog.accept());
  await page.screenshot({ path: info.outputPath('asset-editor.png'), fullPage: false });
  await editor.getByRole('button', { name: 'Salvar ativo', exact: true }).click();
  await expect(editor).toHaveCount(0);
  await expect(page.locator('app-list-paginator')).toContainText('11–11 de 11');
  await expect(page.getByLabel('Categoria', { exact: true })).toHaveValue('1');
  await expect(page.getByRole('heading', { name: 'Ativo revisado', exact: true })).toBeVisible();
  await page.screenshot({ path: info.outputPath('assets-actions.png'), fullPage: true });
  const scrollBeforeLeaving = await page.evaluate(() => window.scrollY);
  if (info.project.name.startsWith('celular')) await page.locator('.menu-toggle').click();
  await page.locator('nav a[href="/references"]').click();
  await expect(page.getByRole('heading', { name: 'Referências', exact: true })).toBeVisible();
  await page.goBack();
  await expect(page.getByRole('heading', { name: 'Ativos', exact: true })).toBeVisible();
  await expect(page.locator('app-list-paginator')).toContainText('11–11 de 11');
  await expect(page.getByLabel('Categoria', { exact: true })).toHaveValue('1');
  await expect.poll(() => page.evaluate(() => window.scrollY)).toBeCloseTo(scrollBeforeLeaving, 0);
  await page.getByRole('button', { name: 'Editar TEST10', exact: true }).click();
  await editor.getByLabel('Nome', { exact: true }).fill('Alteração a descartar');
  await editor.getByRole('button', { name: 'Cancelar', exact: true }).click();
  await expect(editor).toHaveCount(0);
  await expect(page.getByRole('heading', { name: 'Ativo revisado', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Editar TEST10', exact: true }).click();
  await expect(editor.getByLabel('Nome', { exact: true })).toHaveValue('Ativo revisado');
  await page.keyboard.press('Escape');
  await page.getByRole('button', { name: 'Novo ativo', exact: true }).click();
  await expect(page.getByRole('dialog', { name: 'Novo ativo', exact: true })).toBeVisible();
  await page.keyboard.press('Escape');
  await page.goto('/sectors');
  await page.getByRole('button', { name: 'Próxima página', exact: true }).click();
  await page.getByRole('button', { name: 'Excluir Setor 10', exact: true }).click();
  await page.getByRole('button', { name: 'Confirmar exclusão', exact: true }).click();
  await expect(page.getByRole('dialog')).toContainText('O setor está em uso.');
  await page.keyboard.press('Escape');
  await expect(page.locator('app-list-paginator')).toContainText('11–12 de 12');
  await page.goto('/currencies');
  await page.getByRole('button', { name: 'Editar Real', exact: true }).click();
  await expect(page.getByRole('dialog').getByLabel('Código da moeda')).toHaveValue('BRL');
  await page.keyboard.press('Escape');
  await expect(page.getByRole('button', { name: 'Editar Real', exact: true })).toBeFocused();
  await page.screenshot({ path: info.outputPath('currencies-actions.png'), fullPage: true });
  await page.goto('/external-assets');
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(page.locator('app-external-assets-page')).toContainText('14.814,81');
  await page.getByRole('button', { name: 'Próxima página', exact: true }).click();
  await page.getByRole('button', { name: 'Editar Reserva 10', exact: true }).click();
  const balanceEditor = page.getByRole('dialog', { name: 'Editar saldo ou patrimônio', exact: true });
  await expect(balanceEditor.getByLabel('Valor na moeda selecionada', { exact: true })).toHaveValue('1.234,5678');
  await expect(balanceEditor.getByLabel('Valor na moeda selecionada', { exact: true })).not.toBeEditable();
  await page.screenshot({ path: info.outputPath('external-editor.png'), fullPage: false });
  await page.keyboard.press('Escape');
  await expect(page.getByRole('button', { name: 'Editar Reserva 10', exact: true })).toBeFocused();
  await page.getByLabel('Buscar saldo ou patrimônio', { exact: true }).fill('Reserva 01');
  await expect(page.locator('app-list-paginator')).toContainText('1–1 de 1');
  await expect(page.locator('app-external-assets-page')).toContainText('14.814,81');
  await expect(page.getByRole('link', { name: 'Depositar, retirar, transferir ou ajustar saldo', exact: true })).toHaveAttribute('href', '/movements');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: info.outputPath('external-filtered.png'), fullPage: true });
  expect(errors).toEqual([]);
});


test('metas paginadas salvam o conjunto completo e preservam percentuais ocultos', async ({ page }, info) => {
  const groups = Array.from({length: 12}, (_, i) => ({ id: i + 1, name: 'Grupo ' + String(i).padStart(2, '0') }));
  let saved: { targets: { groupId: number; targetPercentage: string }[] } | undefined;
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/portfolios') return route.fulfill({json: [testPortfolio]});
    if (path === '/api/asset-categories') return route.fulfill({json: groups});
    if (path.endsWith('/category-targets')) {
      if (route.request().method() === 'PUT') { saved = route.request().postDataJSON(); return route.fulfill({status: 204}); }
      return route.fulfill({json: [{ groupId: 1, name: 'Grupo 00', targetPercentage: '0.5' }, {groupId: 12, name: 'Grupo 11', targetPercentage: '0.5'}]});
    }
    throw new Error('Unexpected request: ' + path);
  });
  await page.goto('/allocation');
  await page.getByLabel('Grupo 00 (%)', {exact: true}).fill('40');
  await page.getByRole('button', {name:'Próxima página',exact:true}).click();
  await page.getByLabel('Grupo 11 (%)', {exact:true}).fill('60');
  await expect(page.getByText('Total do conjunto: 100,0000%', {exact:true})).toBeVisible();
  await page.getByLabel('Buscar grupo', {exact:true}).fill('Grupo 11');
  await expect(page.locator('app-list-paginator')).toContainText('1–1 de 1');
  await page.getByRole('button', {name:'Salvar conjunto de metas',exact:true}).click();
  await expect(page.getByRole('status').filter({hasText:'Metas salvas'})).toBeVisible();
  expect(saved?.targets).toEqual([{ groupId: 1, targetPercentage: '0.400000' }, {groupId:12,targetPercentage:'0.600000'}]);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({path:info.outputPath('targets.png'),fullPage:true});
});


test('navegação por teclado mantém foco e isola o menu móvel', async ({ page }, info) => {
  await page.route('**/api/**', route => route.fulfill({ json: [] }));
  await page.goto('/dashboard');
  await expect(page.getByRole('heading', { name: 'Dashboard', exact: true })).toBeVisible();
  const toggle = page.locator('.menu-toggle');
  if (info.project.name.startsWith('desktop')) await toggle.click();
  await expect(page.locator('#app-navigation')).toBeHidden();
  const skip = page.getByRole('button', { name: 'Ir para o conteúdo' });
  await skip.focus();
  await page.keyboard.press('Enter');
  await expect(page.locator('main')).toBeFocused();
  await toggle.focus();
  await page.keyboard.press('Enter');
  await expect(page.locator('#app-navigation')).toBeVisible();
  if (info.project.name.startsWith('celular')) {
    await expect(page.locator('main')).toHaveJSProperty('inert', true);
    await page.keyboard.press('Shift+Tab');
    await expect(page.locator('#app-navigation nav a').last()).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(toggle).toBeFocused();
  }
  await page.keyboard.press('Escape');
  await expect(page.locator('#app-navigation')).toBeHidden();
  await expect(toggle).toBeFocused();
  await expect(page.locator('main')).toHaveJSProperty('inert', false);
});


test('tema persiste e acompanha o sistema sem sobrescrever escolha explícita', async ({ page }, info) => {
  await page.route('**/api/**', route => route.fulfill({ json: [] }));
  await page.emulateMedia({ colorScheme: 'dark' });
  await page.goto('/dashboard');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await page.getByRole('button', { name: 'Escolher tema' }).click();
  await page.getByRole('menuitem', { name: /^Claro/ }).click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  await page.reload();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  await page.emulateMedia({ colorScheme: 'light' });
  await page.emulateMedia({ colorScheme: 'dark' });
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  // The menu is deferred again after reload; its first keyboard interaction must open it.
  await page.getByRole('button', { name: 'Escolher tema' }).focus();
  await page.keyboard.press('ArrowDown');
  await expect(page.getByRole('menu')).toBeVisible();
  await page.getByRole('menuitem', { name: /^Sistema/ }).click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await page.emulateMedia({ colorScheme: 'light' });
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  await page.getByRole('button', { name: 'Escolher tema' }).click();
  await page.getByRole('menuitem', { name: /^Escuro/ }).click();
  await expect(page.locator('body')).toHaveCSS('background-color', 'rgb(18, 24, 32)');
  await page.getByRole('button', { name: 'Escolher tema' }).click();
  await expect(page.getByRole('menu')).toBeVisible();
  await page.screenshot({ path: info.outputPath('theme-menu.png'), animations: 'disabled' });
  await page.keyboard.press('Escape');
  await expect(page.getByRole('button', { name: 'Escolher tema' })).toBeFocused();
});
