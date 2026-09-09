import { test, expect, Page, APIRequestContext } from '@playwright/test';

async function json(request: APIRequestContext, path: string) {
  const response = await request.get('/api/' + path);
  expect(response.ok(), await response.text()).toBeTruthy();
  return response.json();
}

async function navigate(page: Page, name: string) {
  const open = page.getByRole('button', { name: 'Abrir menu', exact: true });
  if (await open.isVisible()) await open.click();
  await page.getByRole('navigation').getByRole('link', { name, exact: true }).click();
}

async function choosePortfolio(page: Page, name: string) {
  await page.locator('#analysis-portfolio').selectOption({ label: name + ' · BRL' });
}

test('cadastro, compra, venda, reinvestimento e fotografia preservam os valores', async ({ page, request }, info) => {
  const suffix = info.project.name;
  const name = 'Carteira E2E ' + suffix;
  const ticker = suffix === 'desktop' ? 'E2ED3' : 'E2EC3';
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));

  await page.goto('/portfolio');
  await page.getByRole('button', { name: 'Nova carteira', exact: true }).click();
  await page.locator('#portfolio-name').fill(name);
  await page.locator('#base-currency').selectOption({ label: 'BRL — Real brasileiro' });
  await page.getByRole('button', { name: 'Salvar carteira', exact: true }).click();
  await expect(page.locator('#portfolio-name')).not.toBeVisible();
  const portfolio = (await json(request, 'portfolios')).find((p: any) => p.name === name);
  expect(portfolio).toBeTruthy();
  const path = `portfolios/${portfolio.id}`;
  expect((await json(request, path)).positions).toHaveLength(0);

  await navigate(page, 'Ativos');
  await page.getByLabel('Ticker', { exact: true }).fill(ticker);
  await page.getByLabel('Nome', { exact: true }).fill('Ativo de teste ' + suffix);
  for (const [label, option] of [['Tipo de ativo', 'Ação'], ['País', 'Brasil'], ['Moeda', 'BRL — Real brasileiro'], ['Categoria', 'Ações BR'], ['Setor', 'Tecnologia']]) {
    await page.getByLabel(label, { exact: true }).selectOption({ label: option });
  }
  await page.getByRole('button', { name: 'Salvar ativo', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Editar ' + ticker, exact: true })).toBeVisible();
  // Editing a record must open at its current scroll position, including on mobile.
  await page.getByLabel('Buscar cadastro').fill(ticker);
  await page.getByRole('button', { name: 'Editar ' + ticker, exact: true }).click();
  await expect(page.getByRole('dialog', { name: 'Editar ativo' })).toBeVisible();
  await page.getByLabel('Nome', { exact: true }).fill('Ativo revisado ' + suffix);
  await page.getByRole('button', { name: 'Salvar ativo', exact: true }).click();
  await expect(page.locator('dialog:modal')).toHaveCount(0);
  await expect(page.getByRole('heading', { name: 'Novo ativo', exact: true })).toBeVisible();

  await navigate(page, 'Compras e vendas');
  await choosePortfolio(page, name);
  const trade = async (kind: string, quantity: string, price: string, cash = false, valid = true) => {
    await page.locator('#trade-kind').selectOption(kind);
    await page.locator('#trade-asset').selectOption({ label: `${ticker} — Ativo revisado ${suffix}` });
    await page.locator('#trade-quantity').fill(quantity);
    await page.locator('#trade-price').fill(price);
    await page.locator('#trade-cash').selectOption({ index: cash ? 1 : 0 });
    const response = page.waitForResponse(r => r.url().endsWith('/trades') && r.request().method() === 'POST');
    await page.getByRole('button', { name: 'Confirmar operação' }).click();
    expect((await response).ok()).toBe(valid);
    if (valid) await expect(page.getByRole('status').filter({ hasText: 'Operação registrada' })).toBeVisible();
    else await expect(page.getByRole('alert')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Atualizar saldos e histórico' })).toBeEnabled();
  };
  await trade('buy', '10', '25,50');
  let summary = await json(request, path);
  expect(Number(summary.positions[0].quantity)).toBe(10);
  expect(Number(summary.totalInvested)).toBe(255);
  let history = await json(request, path + '/history');
  expect(history.cashFlows).toHaveLength(1);
  expect(Number(history.cashFlows[0].amount)).toBe(255);

  await navigate(page, 'Patrimônio externo');
  await choosePortfolio(page, name);
  await page.getByLabel('Nome', { exact: true }).fill('Saldo E2E');
  await page.getByLabel('Moeda original', { exact: true }).selectOption({ label: 'BRL — Real brasileiro' });
  await page.getByLabel('Valor na moeda selecionada').fill('100,50');
  await page.getByRole('button', { name: 'Salvar', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Editar Saldo E2E', exact: true })).toBeVisible();

  await navigate(page, 'Compras e vendas');
  await choosePortfolio(page, name);
  await trade('sell', '2', '30', true);
  await trade('buy', '1', '30', true);
  await trade('sell', '1', '30');
  summary = await json(request, path);
  expect(Number(summary.positions[0].quantity)).toBe(8);
  expect(Number(summary.totalInvested)).toBe(208);
  expect(Number(summary.currentValue)).toBe(240);
  expect(Number((await json(request, path + '/external-assets')).totalValue)).toBe(130.5);
  history = await json(request, path + '/history');
  expect(history.cashFlows).toHaveLength(2);
  expect(history.cashFlows.map((f: any) => f.kind).sort()).toEqual(['contribution', 'withdrawal']);
  expect((await json(request, path + '/trades'))).toHaveLength(4);

  // An invalid sale must not change positions, balances or ledger.
  await trade('sell', '100', '30', false, false);
  expect(await json(request, path)).toEqual(summary);
  expect((await json(request, path + '/trades'))).toHaveLength(4);
  expect((await json(request, path + '/history')).cashFlows).toHaveLength(2);

  await navigate(page, 'Evolução e aportes');
  await choosePortfolio(page, name);
  await page.getByRole('button', { name: 'Registrar fotografia', exact: true }).click();
  await page.getByRole('button', { name: 'Confirmar fotografia', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Atualizar fotografia deste mês' })).toBeVisible();
  history = await json(request, path + '/history');
  const snapshotId = history.months.find((m: any) => m.snapshotId).snapshotId;
  const snapshotPath = path + '/history/snapshots/' + snapshotId;
  const photo = await json(request, snapshotPath);
  expect(Number(photo.dashboard.totalWealth)).toBe(370.5);
  await page.getByRole('button', { name: 'Ano a ano', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Ano a ano', exact: true })).toHaveAttribute('aria-pressed', 'true');

  await navigate(page, 'Compras e vendas');
  await choosePortfolio(page, name);
  await trade('buy', '1', '40');
  expect(await json(request, snapshotPath)).toEqual(photo);
  await navigate(page, 'Evolução e aportes');
  await choosePortfolio(page, name);
  await page.getByRole('button', { name: 'Atualizar fotografia deste mês' }).click();
  await page.getByRole('button', { name: 'Confirmar fotografia' }).click();
  await expect(page.getByRole('button', { name: 'Confirmar fotografia' })).not.toBeVisible();
  expect(Number((await json(request, snapshotPath)).dashboard.totalWealth)).toBe(490.5);
  const frozen = await json(request, snapshotPath);
  const flowsBefore = (await json(request, path + '/history')).cashFlows;
  await navigate(page, 'Proventos');
  await choosePortfolio(page, name);
  await page.getByLabel('Ativo', { exact: true }).selectOption({ label: `${ticker} — Ativo revisado ${suffix}` });
  await page.locator('#income-amount').fill('12,34');
  await page.locator('#income-cash').selectOption({ index: 1 });
  await page.getByRole('button', { name: 'Registrar recebimento', exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Recebimento registrado' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Atualizar recebimentos' })).toBeEnabled();
  await page.locator('#income-amount').fill('2');
  await page.locator('#income-cash').selectOption({ index: 0 });
  await page.getByRole('button', { name: 'Registrar recebimento', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Atualizar recebimentos' })).toBeEnabled();
  await expect(page.locator('tbody tr')).toHaveCount(2);
  expect(Number((await json(request, path)).totalIncome)).toBe(14.34);
  expect(Number((await json(request, path + '/external-assets')).totalValue)).toBe(142.84);
  expect((await json(request, path + '/history')).cashFlows).toEqual(flowsBefore);
  expect(await json(request, snapshotPath)).toEqual(frozen);
  expect(errors).toEqual([]);
});

test('menu permanece acessível após rolagem e gráficos mantêm preferências', async ({ page, request }, info) => {
  const name = 'Layout E2E ' + info.project.name;
  const created = await request.post('/api/portfolios', { data: { name } });
  expect(created.ok()).toBeTruthy();
  await page.goto('/dashboard');
  await choosePortfolio(page, name);
  const collapse = page.getByRole('button', { name: 'Recolher', exact: true }).first();
  await collapse.click();
  const expand = page.getByRole('button', { name: 'Expandir', exact: true });
  await expect(expand).toHaveCount(1);
  const chartId = await expand.getAttribute('aria-controls');
  await page.reload();
  await choosePortfolio(page, name);
  await expect(page.locator(`button[aria-controls="${chartId}"]`)).toHaveText('Expandir');
  await page.getByRole('button', { name: 'Restaurar gráficos' }).click();
  await expect(page.getByRole('button', { name: 'Expandir', exact: true })).toHaveCount(0);
  await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
  const toggle = page.locator('.menu-toggle');
  await expect(toggle).toBeInViewport();
  await navigate(page, 'Compras e vendas');
  await expect(page.getByRole('heading', { name: 'Compras e vendas', exact: true })).toBeVisible();
  await expect(page.locator('#trade-quantity')).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBeTruthy();
});
