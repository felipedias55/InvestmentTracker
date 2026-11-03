import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { brazilianLocaleProvider } from '../../core/locale';
import { ContributionsPage } from './contributions-page';
import { testDashboard, testPortfolio } from './allocation.test-data';

const result = { currencyCode: 'BRL', dimension: 'category', amount: '1234.50', sumOfWeights: '0',
  unallocatedAmount: '1234.50', hasStaleRates: false, hasFallbackRates: false, rows: [] };
const response = { dashboard: testDashboard, category: result, sector: null, plan: null };
describe('ContributionsPage', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [ContributionsPage],
      providers: [brazilianLocaleProvider, provideRouter([]), provideHttpClient(), provideHttpClientTesting()] }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  function initialize() {
    const fixture = TestBed.createComponent(ContributionsPage); fixture.detectChanges();
    http.expectOne('/api/portfolios').flush([testPortfolio]);
    http.expectOne('/api/portfolios/1/dashboard').flush(testDashboard);
    fixture.detectChanges();
    http.expectOne('/api/portfolios/1/strategies').flush([]);
    fixture.detectChanges(); return fixture;
  }
  it('sends only the amount and renders the server plan without recomputing it', () => {
    const fixture = initialize();
    fixture.componentInstance.form.controls.amount.setValue('1.234,50');
    fixture.componentInstance.simulate();
    const request = http.expectOne('/api/portfolios/1/contribution-plan');
    expect(request.request.body).toEqual({ amount: '1234.50' });
    const plan = { rows: [{ key: '1-2', sector: 'Consumo', category: 'EUA', amount: '100',
      currencies: [{ currency: 'USD', amount: '17.23', rate: '5.804', dates: '2026-10-07', stale: false }] }], unallocated: '1134.50' };
    request.flush({ ...response, sector: { ...result, dimension: 'sector' }, plan });
    fixture.detectChanges();
    expect(fixture.componentInstance.plan()).toEqual(plan);
    expect(fixture.nativeElement.textContent).toContain('17,23');
    fixture.componentInstance.toggle('joint:1-2');
    fixture.componentInstance.form.controls.amount.setValue('200');
    expect(fixture.componentInstance.checked().size).toBe(0);
    expect(fixture.componentInstance.plan()).toBeNull();
    http.expectNone(r => r.method === 'PUT' || r.method === 'DELETE');
  });
  it('rejects invalid amounts locally', () => {
    const fixture = initialize();
    for (const value of ['-1', '1,123', '12.34,5']) {
      fixture.componentInstance.form.controls.amount.setValue(value);
      fixture.componentInstance.simulate();
      http.expectNone('/api/portfolios/1/contribution-plan');
    }
  });
  it('preserves input on failure and uses the returned dashboard for charts', () => {
    const fixture = initialize(); fixture.componentInstance.form.controls.amount.setValue('100');
    fixture.componentInstance.simulate();
    http.expectOne('/api/portfolios/1/contribution-plan').flush({ detail: 'Câmbio indisponível' }, { status: 409, statusText: 'Conflict' });
    expect(fixture.componentInstance.form.controls.amount.value).toBe('100');
    fixture.componentInstance.simulate();
    http.expectOne('/api/portfolios/1/contribution-plan').flush({ ...response,
      category: { ...result, hasFallbackRates: true }, dashboard: { ...testDashboard, totalWealth: '500' } });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('cotações anteriores');
    expect(fixture.componentInstance.dashboard()?.totalWealth).toBe('500');
  });
  it('cancels obsolete requests', () => {
    const fixture = initialize(); fixture.componentInstance.simulate();
    const obsolete = http.expectOne('/api/portfolios/1/contribution-plan');
    fixture.componentInstance.form.controls.amount.setValue('200');
    expect(obsolete.cancelled).toBe(true);
    expect(fixture.componentInstance.saving()).toBe(false);
  });
  it('renders server concentration results and clears them when the simulation input changes', () => {
    const fixture = initialize(), c = fixture.componentInstance;
    c.form.controls.amount.setValue('100'); c.simulate();
    http.expectOne('/api/portfolios/1/contribution-plan').flush({ dashboard: testDashboard, category: null, sector: null,
      plan: { rows: [], unallocated: '100' }, concentration: [{ dimension: 'category', groupId: 1, name: 'Grupo concentrado', maximumPercentage: '40',
        currentPercentage: '60', projectedPercentage: '60', requestedContribution: '100', suggestedContribution: '0', initiallyAboveLimit: true, aboveLimit: true }] });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.concentration-result').textContent).toContain('Atual: 60% → Projetado: 60%');
    expect(fixture.nativeElement.textContent).toContain('nenhuma venda é sugerida');
    c.form.controls.amount.setValue('200'); expect(c.concentration()).toEqual([]);
    http.expectNone(r => r.method === 'PUT' || r.method === 'DELETE');
  });
  it('shows the server sector/currency direction and priority funding without adding alternatives', () => {
    const fixture = initialize(), c = fixture.componentInstance; c.form.controls.amount.setValue('100'); c.simulate();
    http.expectOne('/api/portfolios/1/contribution-plan').flush({ dashboard: testDashboard, category: null, sector: null, prioritiesApplied: true,
      ruleFunding: [{ categoryId: 1, category: 'Exterior', priority: 1, requested: '150', budget: '100', suggested: '100' }],
      plan: { rows: [{ key: 'usd', category: 'Exterior', sector: 'Tecnologia', amount: '100', stage: 'reservation', rulePriority: 1, groupPriority: 2,
        suggestedCurrency: 'USD', suggestionReason: 'Preferência do grupo.', currencies: [{ currency: 'USD', amount: '20', rate: '5', dates: '2026-10-30', stale: false, tickers: ['TESTUS'] }] }], unallocated: '0' } });
    fixture.detectChanges(); expect(fixture.nativeElement.querySelector('.joint-plan').textContent).toContain('Tecnologia em dólar (USD)');
    expect(fixture.nativeElement.textContent).toContain('TESTUS'); expect(fixture.nativeElement.querySelector('.priority-result').textContent).toContain('prioridade 1');
    expect(c.plan()!.rows[0].amount).toBe('100');
    c.form.controls.amount.setValue('200'); expect(c.ruleFunding()).toEqual([]); expect(c.prioritiesApplied()).toBe(false);
  });
});
