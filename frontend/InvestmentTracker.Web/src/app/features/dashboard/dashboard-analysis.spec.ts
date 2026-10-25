import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { DashboardAnalysis } from './dashboard-analysis';
import { brazilianLocaleProvider } from '../../core/locale';
import { ValuePrivacy } from '../../core/value-privacy';

describe('DashboardAnalysis', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [DashboardAnalysis], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), brazilianLocaleProvider] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  function create(kind: string) {
    const f = TestBed.createComponent(DashboardAnalysis);
    f.componentRef.setInput('kind', kind); f.componentRef.setInput('portfolioId', 1); f.componentRef.setInput('currency', 'BRL');
    f.detectChanges(); return f;
  }
  it('breaks evolution for stale, reopened, missing or differently denominated snapshots', () => {
    const f = create('evolution');
    const row = { period: '2026-01', snapshotId: 1, currencyCode: 'BRL', totalWealth: '100' };
    http.expectOne('/api/portfolios/1/history').flush({ months: [row, { ...row, period: '2026-02', isOutdated: true }, { ...row, period: '2026-03', currencyCode: 'USD' }, { ...row, period: '2026-04', isReopened: true }], years: [] });
    expect(f.componentInstance.evolutionPoints().map(p => p.value)).toEqual([100, null, null, null]);
    expect(f.componentInstance.validPeriod()).toBe(false);
    f.componentInstance.year.set('2025');
    expect(f.componentInstance.periods()).toEqual([]);
  });
  it('separates currencies and preserves incomplete conversion and negative income', () => {
    const f = create('income');
    http.expectOne('/api/portfolios/1/income/analysis').flush({ months: [
      { period: '2026-01', currencyCode: 'BRL', net: '100' }, { period: '2026-01', currencyCode: 'USD', net: '-20' }
    ], years: [], convertedMonths: [{ period: '2026-01', currencyCode: 'BRL', net: '100', missingConversions: 1 }] });
    expect(f.componentInstance.incomeGroups().map(g => g.points[0].value)).toEqual([100, -20]);
    f.componentInstance.incomeMode.set('base');
    expect(f.componentInstance.incomeGroups()[0].points[0].value).toBeNull();
    expect(f.componentInstance.pendingConversions()).toBe(true);
  });
  it('keeps evolution details outside the card, masks amounts and withholds invalid-period metrics', () => {
    const f = create('evolution');
    const row = { period: '2026-01', snapshotId: 1, snapshotDate: '2026-01-31', comparisonStart: '2025-12-31', currencyCode: 'BRL', totalWealth: '1234.50', netFlowsBetweenSnapshots: '100', valuationAndOtherChanges: '34.5', returns: { modifiedDietz: '0.02', xirr: null, note: 'Sem base anual.' }, comparisonNote: 'Intervalo entre fotografias.' };
    http.expectOne('/api/portfolios/1/history').flush({ months: [row, { ...row, period: '2026-02', snapshotId: null }], years: [] });
    f.detectChanges();
    expect(f.nativeElement.querySelector('.metric-grid')).toBeNull();
    expect(f.nativeElement.querySelector('app-money-chart details')).toBeNull();
    expect(f.nativeElement.querySelector('svg').getAttribute('aria-label')).toContain('nos detalhes');
    f.componentInstance.detailsOpen.set(true); f.detectChanges();
    expect(f.nativeElement.querySelector('dialog select').value).toBe('2026-02');
    f.componentInstance.period.set('2026-01');
    TestBed.inject(ValuePrivacy).hidden.set(true); f.detectChanges();
    const panel = f.nativeElement.querySelector('dialog');
    expect(panel.textContent).toContain('2025-12-31');
    expect(panel.textContent).toContain('Dietz estimado');
    expect(panel.textContent).toContain('Indisponível');
    expect(panel.textContent).toContain('Intervalo entre fotografias.');
    expect(panel.textContent).toContain('••••••');
    expect(panel.textContent).not.toContain('1.234,50');
    expect(panel.querySelectorAll('tbody tr')).toHaveLength(2);
    f.componentInstance.period.set('2026-02'); f.detectChanges();
    expect(panel.querySelector('.metric-grid')).toBeNull();
    expect(panel.textContent).toContain('Comparação indisponível');
    f.componentRef.setInput('portfolioId', 2); f.detectChanges();
    expect(f.nativeElement.querySelector('dialog')).toBeNull();
    http.expectOne('/api/portfolios/2/history').flush({ months: [], years: [] }); f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Sem fotografias');
  });
  it('cancels old portfolio requests and clears data on a failed reload', () => {
    const f = create('income'), old = http.expectOne('/api/portfolios/1/income/analysis');
    f.componentRef.setInput('portfolioId', 2); f.detectChanges();
    expect(old.cancelled).toBe(true);
    http.expectOne('/api/portfolios/2/income/analysis').flush({}, { status: 500, statusText: 'Error' });
    expect(f.componentInstance.income()).toBeNull();
    expect(f.componentInstance.error()).not.toBe('');
  });
  it('windows one income currency without losing older periods or pending conversions', () => {
    const f = create('income');
    const months = Array.from({ length: 14 }, (_, i) => ({ period: `${2025 + Math.floor(i / 12)}-${String(i % 12 + 1).padStart(2, '0')}`, currencyCode: 'BRL', net: i === 2 ? '-50' : '100' }));
    http.expectOne('/api/portfolios/1/income/analysis').flush({ months: [...months, ...months.map(r => ({ ...r, currencyCode: 'USD', net: '20' }))], years: [], convertedMonths: months.map((r, i) => i === 0 ? { ...r, net: null, missingConversions: 1 } : r) });
    f.detectChanges();
    expect(f.componentInstance.incomeWindow()).toHaveLength(5);
    expect(f.componentInstance.incomeWindow()[0].label).toBe('2025-10');
    f.componentInstance.shiftIncomeWindow(1); f.detectChanges();
    expect(f.componentInstance.incomeWindow()[0].label).toBe('2025-05');
    f.componentInstance.selectIncome('original:USD'); f.detectChanges();
    expect(f.componentInstance.incomeWindow().map(p => p.value)).toEqual([20, 20, 20, 20, 20]);
    expect(f.componentInstance.incomeEnd()).toBe(14);
    f.componentInstance.selectIncome('base:BRL'); f.detectChanges();
    expect(f.componentInstance.pendingConversions()).toBe(true);
    expect(f.componentInstance.incomeWindow().some(p => p.value === null)).toBe(false);
    f.componentInstance.detailsOpen.set(true); f.detectChanges();
    expect(f.nativeElement.querySelectorAll('dialog tbody tr')).toHaveLength(14);
    expect(f.nativeElement.querySelector('dialog tbody').textContent).toContain('Indisponível');
    f.componentInstance.selectIncome('original:EUR'); f.detectChanges();
    expect(f.componentInstance.incomeChoice()).toBe('base:BRL');
    f.componentRef.setInput('portfolioId', 2); f.detectChanges();
    http.expectOne('/api/portfolios/2/income/analysis').flush({ months: [], years: [], convertedMonths: [], convertedYears: [] });
    f.componentInstance.selectIncome('original:BRL'); f.detectChanges();
    expect(f.componentInstance.incomeWindow()).toEqual([]);
    expect(f.nativeElement.querySelector('select').value).toBe('original:BRL');
  });
  it('summarizes three leaders per sign but retains the complete ranking and a common scale', () => {
    const f = create('highlights');
    const positions = Array.from({ length: 12 }, (_, i) => ({ id: i + 1, ticker: `TEST${i}`, quantity: '1', currencyCode: 'BRL', pricePerformance: { variation: String((i < 6 ? 1 : -1) * (0.6 - i % 6 * 0.05)), unitPrice: '123.45', averagePrice: '100' } }));
    f.componentRef.setInput('positions', positions); f.detectChanges();
    expect(f.nativeElement.querySelectorAll('.leader')).toHaveLength(6);
    expect(f.nativeElement.querySelectorAll('.leader small')).toHaveLength(0);
    expect(f.componentInstance.leaderWidth(positions[0])).toBe(50);
    expect(f.componentInstance.leaderWidth(positions[6])).toBe(50);
    TestBed.inject(ValuePrivacy).hidden.set(true);
    f.componentInstance.detailsOpen.set(true); f.detectChanges();
    expect(f.nativeElement.querySelectorAll('dialog tbody tr')).toHaveLength(12);
    expect(f.nativeElement.querySelector('dialog').textContent).toContain('••••••');
    expect(f.nativeElement.querySelector('dialog').textContent).not.toContain('123,45');
  });
  it('ranks gains and losses separately and excludes positions with no quantity or basis', () => {
    const f = create('highlights');
    f.componentRef.setInput('positions', [
      { id: 1, ticker: 'A', quantity: '1', pricePerformance: { variation: '0.2' } },
      { id: 2, ticker: 'B', quantity: '1', pricePerformance: { variation: '-0.5' } },
      { id: 3, ticker: 'C', quantity: '0', pricePerformance: { variation: '9' } },
      { id: 4, ticker: 'D', quantity: '1', pricePerformance: { variation: null } }
    ]);
    expect(f.componentInstance.leaders().map(g => g.rows.map(p => p.ticker))).toEqual([['A'], ['B']]);
    http.expectNone(req => true);
  });
});
