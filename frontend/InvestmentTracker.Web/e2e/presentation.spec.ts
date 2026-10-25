import { test, expect } from '@playwright/test';
import { testDashboard, testPortfolio } from '../src/app/features/allocation/allocation.test-data';
test.beforeEach(async ({ page }) => { page.on('dialog', dialog => dialog.accept()); });

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
  const positions = Array.from({ length: 12 }, (_, i) => ({ id: i + 1, assetId: i + 1, ticker: `RANK${String(i).padStart(2, '0')}`, quantity: '1', currencyCode: i % 2 ? 'USD' : 'BRL', pricePerformance: { variation: String((i < 6 ? 1 : -1) * (0.6 - i % 6 * 0.05)), unitPrice: '123.45', averagePrice: '100' } }));
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
    if (!(path in data)) throw new Error('Unexpected API call: ' + path);
    await route.fulfill({ json: data[path] });
  });
  await page.goto('/dashboard');
  const ranking = page.locator('[data-chart-id="highlights"]');
  await expect(ranking.locator('.leader')).toHaveCount(6);
  await expect(ranking.locator('.leader-track i.negative')).toHaveCount(3);
  await expect(ranking.locator('.leader').filter({ hasText: '123,45' })).toHaveCount(0);
  await ranking.screenshot({ path: info.outputPath('ranking-card.png') });
  const rankAction = ranking.getByRole('button', { name: 'Ver ranking completo', exact: true });
  await rankAction.click();
  const rankPanel = page.getByRole('dialog', { name: 'Detalhes dos destaques', exact: true });
  await expect(rankPanel.locator('tbody tr')).toHaveCount(12);
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
        expect(sent.strategy.rules).toEqual([{ categoryId: 3, kind: 'fixed', value: '500,00'.replace(',', '.') }]);
      }
      const analysis = (dimension: string) => {
        const groups = dimension === 'category' ? dashboard.allocation.categories : dashboard.allocation.sectors;
        return { dimension, currencyCode: 'BRL', amount: '100', unallocatedAmount: '0', sumOfWeights: '0.1', hasStaleRates: false, hasFallbackRates: false,
          rows: groups.map(r => ({ ...r, currencies: [], adjustedWeight: '0.1', suggestedContribution: r.groupId === (dimension === 'category' ? 2 : 3) ? '100' : '0' })) };
      };
      return route.fulfill({ json: { dashboard: { ...dashboard, summary: { ...dashboard.summary, positions } },
        category: analysis('category'), sector: analysis('sector'),
        plan: { unallocated: '0', rows: [{ key: '2-3', category: 'Ações EUA', sector: 'Consumo', amount: '100',
          currencies: [{ currency: 'USD', amount: '20', rate: '5', dates: '2026-10-06', stale: false }] }] } } });
    }    throw new Error('Unexpected request: ' + path);
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
  if (info.project.name.startsWith('desktop')) {
    const boxes = await page.locator('.comparison > section').evaluateAll(elements => elements.map(e => e.getBoundingClientRect().top));
    expect(boxes[0]).toBe(boxes[1]);
  }
  const beforeLeaving = [...writes];
  if (info.project.name.startsWith('celular')) await page.locator('.menu-toggle').click();
  await page.locator('#app-navigation a[href="/currencies"]').click();
  await expect(page.getByRole('heading', { name: 'Moedas', exact: true })).toBeVisible();
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
  await page.getByLabel('Regras por página', { exact: true }).selectOption('25');
  await expect(page.getByLabel('Regras por página', { exact: true })).toHaveValue('25');
  const estate = page.locator('app-strategy-editor .rule').filter({ hasText: 'Fundos imobiliários' });
  await estate.getByLabel('Regra de Fundos imobiliários', { exact: true }).selectOption('fixed');
  await estate.getByLabel('Valor da regra de Fundos imobiliários', { exact: true }).fill('500,00');
  await estate.getByLabel('Excluir da base do restante').check();
  await page.getByRole('button', { name: 'Salvar como estratégia padrão' }).click();
  await expect(page.locator('app-strategy-editor')).toContainText('Estratégia salva');
  await page.screenshot({ path: info.outputPath('strategy-editor.png'), fullPage: false });
  await page.getByRole('button', { name: 'Concluir configuração', exact: true }).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await page.locator('#contribution-amount').fill('2.000,00');
  await page.getByRole('button', { name: 'Simular aporte', exact: true }).click();
  await expect(page.locator('.joint-plan')).toContainText('Plano da estratégia');
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
  await expect(tradePanel.locator('select[formcontrolname="assetId"] option:checked')).toContainText('TEST10');
  await tradePanel.getByRole('button', { name: 'Cancelar', exact: true }).click();
  await page.locator('#analysis-portfolio').selectOption('2');
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await page.getByRole('button', { name: 'Registrar compra', exact: true }).click();
  await expect(tradePanel.locator('select[formcontrolname="assetId"] option:checked')).toContainText('Selecione');
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
  await expect(receiptPanel.locator('select[formcontrolname="assetId"] option:checked')).toContainText('TEST10');
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
  await page.locator('#app-navigation a[href="/currencies"]').click();
  await expect(page.getByRole('heading', { name: 'Moedas', exact: true })).toBeVisible();
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
  await page.getByRole('searchbox', { name: 'Buscar ativo por código ou nome' }).fill('TEST');
  await page.locator('#trade-asset').selectOption({ label: 'TEST — Ativo de demonstração' });
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
  await page.locator('#event-position').selectOption({ index: 1 });
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
  await page.locator('#income-asset').selectOption({ index: 1 });
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
  await page.locator('nav a[href="/currencies"]').click();
  await expect(page.getByRole('heading', { name: 'Moedas', exact: true })).toBeVisible();
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
  const balanceEditor = page.getByRole('dialog', { name: 'Editar patrimônio', exact: true });
  await expect(balanceEditor.getByLabel('Valor na moeda selecionada', { exact: true })).toHaveValue('1.234,5678');
  await expect(balanceEditor.getByLabel('Valor na moeda selecionada', { exact: true })).not.toBeEditable();
  await page.screenshot({ path: info.outputPath('external-editor.png'), fullPage: false });
  await page.keyboard.press('Escape');
  await expect(page.getByRole('button', { name: 'Editar Reserva 10', exact: true })).toBeFocused();
  await page.getByLabel('Buscar patrimônio', { exact: true }).fill('Reserva 01');
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
  await page.getByRole('button', { name: 'Escolher tema' }).click();
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
