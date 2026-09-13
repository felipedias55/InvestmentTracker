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

  await navigate(page, 'Evolução e fechamentos');
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
  expect(await json(request, snapshotPath)).toEqual({ ...photo, isOutdated: true });
  await navigate(page, 'Evolução e fechamentos');
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
  await expect(page.locator('section').filter({ has: page.getByRole('heading', { name: 'Histórico de recebimentos', exact: true }) }).locator('tbody tr')).toHaveCount(2);
  expect(Number((await json(request, path)).totalIncome)).toBe(14.34);
  expect(Number((await json(request, path + '/external-assets')).totalValue)).toBe(142.84);
  expect((await json(request, path + '/history')).cashFlows).toEqual(flowsBefore);
  expect(await json(request, snapshotPath)).toEqual({ ...frozen, isOutdated: true });
  await navigate(page, 'Movimentações');
  await choosePortfolio(page, name);
  const ledger = await json(request, path + '/movements');
  const receiptMovement = ledger.find((m: any) => m.kind === 'income' && Number(m.amount) === 2);
  await page.getByRole('button', { name: `Estornar #${receiptMovement.id}`, exact: true }).click();
  await page.getByLabel('Motivo do estorno', { exact: true }).fill('Recebimento duplicado no teste');
  await page.getByRole('button', { name: 'Confirmar estorno', exact: true }).click();
  await expect(page.locator('dialog:modal')).toHaveCount(0);
  await expect(page.getByRole('status').filter({ hasText: 'Estorno registrado' })).toBeVisible();
  expect(Number((await json(request, path)).totalIncome)).toBe(12.34);
  expect((await json(request, path + '/income'))).toHaveLength(2);
  expect(await json(request, snapshotPath)).toEqual({ ...frozen, isOutdated: true });
  await navigate(page, 'Compras e vendas'); await choosePortfolio(page, name);
  await page.locator('#trade-fees').fill('1,25');
  await trade('buy', '1', '10', true);
  const withFees = await json(request, path);
  expect(Number(withFees.positions[0].investedAmount)).toBe(259.25);
  await page.getByText('Registrar desdobramento, grupamento ou bonificação', { exact: true }).click();
  await page.locator('#event-position').selectOption({ index: 1 });
  await page.locator('#event-quantity').fill('20');
  await page.locator('#event-reason').fill('Desdobramento sintético de teste');
  await page.getByRole('button', { name: 'Confirmar evento', exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Evento registrado' })).toBeVisible();
  expect(Number((await json(request, path)).positions[0].quantity)).toBe(20);
  expect(Number((await json(request, path)).positions[0].investedAmount)).toBe(259.25);
  await navigate(page, 'Evolução e fechamentos'); await choosePortfolio(page, name);
  await page.getByText('Reabrir período para lançamentos atrasados', { exact: true }).click();
  await page.locator('#reopen-date').fill(photo.snapshotDate);
  await page.locator('#reopen-reason').fill('Conferência do fechamento');
  await page.getByRole('button', { name: 'Confirmar reabertura', exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Período reaberto' })).toBeVisible();
  expect((await json(request, snapshotPath)).isReopened).toBe(true);
  await page.getByRole('button', { name: 'Atualizar fotografia deste mês', exact: true }).click();
  await page.getByRole('button', { name: 'Confirmar fotografia', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Confirmar fotografia', exact: true })).not.toBeVisible();
  const revised = await json(request, snapshotPath);
  expect(revised.revision).toBe(3); expect(revised.previousVersions).toHaveLength(2);
  expect(revised.isOutdated).toBe(false); expect(revised.previousVersions[0].dashboard).toEqual(photo.dashboard);
  await navigate(page, 'Proventos'); await choosePortfolio(page, name);
  await page.locator('#income-group').selectOption('years');
  const analysis = page.locator('section').filter({ has: page.getByRole('heading', { name: 'Análise dos recebimentos', exact: true }) });
  await expect(analysis.locator('tbody tr')).toHaveCount(1);
  await expect(analysis).toContainText('12,34');
  expect(errors).toEqual([]);
});

test('depósito, retirada, transferência, ajuste e estorno atualizam saldos e trilha', async ({ page, request }, info) => {
  const name = 'Saldos E2E ' + info.project.name;
  const created = await request.post('/api/portfolios', { data: { name } }); expect(created.ok()).toBeTruthy();
  const portfolio = await created.json();
  const path = `portfolios/${portfolio.id}`;
  const currencies = await json(request, 'currencies');
  const currencyId = currencies.find((c: any) => c.code === 'BRL').id;
  for (const balance of ['Conta A', 'Conta B']) {
    const response = await request.post('/api/' + path + '/external-assets', { data: { name: balance, currencyId, value: '100' } });
    expect(response.ok()).toBeTruthy();
  }
  await page.goto('/movements'); await choosePortfolio(page, name);
  const move = async (kind: string, amount: string) => {
    await page.locator('#movement-kind').selectOption(kind);
    await page.locator('#movement-cash').selectOption({ index: 1 });
    if (kind === 'transfer') await page.locator('#movement-destination').selectOption({ index: 1 });
    await page.locator('#movement-amount').fill(amount);
    await page.locator('#movement-reason').fill('Conferência de teste');
    const pending = page.waitForResponse(r => r.url().endsWith('/movements') && r.request().method() === 'POST');
    await page.getByRole('button', { name: 'Registrar movimentação', exact: true }).click();
    const response = await pending; expect(response.ok()).toBeTruthy();
    await expect(page.getByRole('button', { name: 'Atualizar movimentações' })).toBeEnabled();
    return response.json();
  };
  const deposit = await move('deposit', '10,50');
  const withdrawal = await move('withdrawal', '5'); const transfer = await move('transfer', '20');
  const adjustment = await move('adjustment', '90');
  expect(await page.getByRole('button', { name: `Estornar #${deposit.id}`, exact: true }).count()).toBe(0);
  await page.getByRole('button', { name: `Estornar #${adjustment.id}`, exact: true }).click();
  await page.getByLabel('Motivo do estorno', { exact: true }).fill('Desfazer ajuste incorreto');
  await page.getByRole('button', { name: 'Confirmar estorno', exact: true }).click();
  await expect(page.locator('dialog:modal')).toHaveCount(0);
  const balances = await json(request, path + '/external-assets');
  expect(Number(balances.items.find((b: any) => b.name === 'Conta A').value)).toBe(85.5);
  expect(Number(balances.items.find((b: any) => b.name === 'Conta B').value)).toBe(120);
  expect((await json(request, path + '/history')).cashFlows).toHaveLength(2);
  expect((await json(request, path + '/movements'))).toHaveLength(5);
  for (const original of [transfer, withdrawal, deposit]) {
    await page.getByRole('button', { name: `Estornar #${original.id}`, exact: true }).click();
    await page.getByLabel('Motivo do estorno', { exact: true }).fill('Desfazer lançamento de teste');
    await page.getByRole('button', { name: 'Confirmar estorno', exact: true }).click();
    await expect(page.locator('dialog:modal')).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Atualizar movimentações' })).toBeEnabled();
  }
  const restored = await json(request, path + '/external-assets');
  expect(restored.items.map((b: any) => Number(b.value))).toEqual([100, 100]);
  const history = await json(request, path + '/history');
  expect(Number(history.months[0].contributions)).toBe(0);
  expect(Number(history.months[0].withdrawals)).toBe(0);
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
