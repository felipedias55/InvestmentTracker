import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { brazilianLocaleProvider } from '../../core/locale';
import { ValuePrivacy } from '../../core/value-privacy';
import { DashboardSummary, decimalTotal, previousPeriod, incomeTotal } from './dashboard-summary';
import type { IncomeTotal } from '../income/income-page';

describe('DashboardSummary', () => {
  beforeEach(() => TestBed.configureTestingModule({ imports: [DashboardSummary], providers: [provideRouter([]), brazilianLocaleProvider] }));
  function create() {
    const f = TestBed.createComponent(DashboardSummary);
    f.componentRef.setInput('currency', 'BRL');
    f.componentRef.setInput('history', { today: '2026-01-14', months: [
      { period: '2026-01', currencyCode: 'BRL', snapshotId: 1, snapshotDate: '2026-01-10', totalWealth: '500', contributions: '100.1', withdrawals: '20.2', valuationAndOtherChanges: '10' }
    ], years: [] });
    f.componentRef.setInput('income', { months: [], years: [], convertedMonths: [
      { period: '2025-12', currencyCode: 'BRL', net: '10' },
      { period: '2026-01', currencyCode: 'BRL', net: '15' }
    ] });
    f.detectChanges(); return f;
  }
  it('uses exact amounts and compares January to December, marking partial periods', () => {
    const f = create(), c = f.componentInstance;
    expect(c.flows()).toBe('79.900000');
    expect(c.previous()).toBe('2025-12');
    expect(c.incomeChange()).toBe('5.000000');
    expect(c.incomeRatio()).toBe(.5);
    expect(c.partial()).toBe(true);
    expect(f.nativeElement.textContent).toContain('dados parciais');
    expect(previousPeriod('2026')).toBe('2025');
    expect(decimalTotal(['999999999999999.1234', '0.0001'])).toBe('999999999999999.123500');
  });
  it('does not invent zero for missing references, pending conversions or zero denominators', () => {
    const f = create();
    f.componentRef.setInput('income', { months: [], years: [], convertedMonths: [
      { period: '2025-12', currencyCode: 'BRL', net: '0' },
      { period: '2026-01', currencyCode: 'BRL', net: '-5' }
    ] });
    expect(f.componentInstance.incomeRatio()).toBeNull();
    expect(f.componentInstance.incomeChange()).toBe('-5.000000');
    expect(incomeTotal([], '2026-01', 'BRL')).toBeNull();
    expect(incomeTotal([{ period: '2026-01', currencyCode: 'BRL', net: '10', missingConversions: 1 } as IncomeTotal], '2026-01', 'BRL')).toBeNull();
    expect(decimalTotal(['1', null])).toBeNull();
  });
  it('blocks stale snapshots and other currencies and respects privacy', () => {
    const f = create();
    TestBed.inject(ValuePrivacy).hidden.set(true); f.detectChanges();
    expect(f.nativeElement.textContent).toContain('••••••');
    expect(f.nativeElement.textContent).not.toContain('500,00');
    f.componentRef.setInput('history', { today: '2026-02-01', years: [], months: [{ period: '2026-01', currencyCode: 'USD', snapshotId: 1, totalWealth: '500', isOutdated: true }] });
    expect(f.componentInstance.wealth()).toBeNull();
    expect(f.componentInstance.valuation()).toBeNull();
    expect(f.componentInstance.flows()).toBeNull();
    expect(f.componentInstance.outdated()).toBe(1);
  });
});
