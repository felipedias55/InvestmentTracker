import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { DashboardAnalysis } from './dashboard-analysis';
import { brazilianLocaleProvider } from '../../core/locale';

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
  it('cancels old portfolio requests and clears data on a failed reload', () => {
    const f = create('income'), old = http.expectOne('/api/portfolios/1/income/analysis');
    f.componentRef.setInput('portfolioId', 2); f.detectChanges();
    expect(old.cancelled).toBe(true);
    http.expectOne('/api/portfolios/2/income/analysis').flush({}, { status: 500, statusText: 'Error' });
    expect(f.componentInstance.income()).toBeNull();
    expect(f.componentInstance.error()).not.toBe('');
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
