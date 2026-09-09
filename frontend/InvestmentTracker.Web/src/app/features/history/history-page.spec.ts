import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { brazilianLocaleProvider } from '../../core/locale';
import { HistoryPage } from './history-page';
import { CashFlow, HistoryPeriod, PortfolioHistory } from './history.service';
import { testDashboard, testPortfolio } from '../allocation/allocation.test-data';

const row: HistoryPeriod = {
  period: '2026-09',
  snapshotId: 4,
  snapshotDate: '2026-09-08',
  currencyCode: 'BRL',
  portfolioValue: '1000',
  externalValue: '234.50',
  totalWealth: '1234.50',
  totalIncome: '10',
  contributions: '100',
  withdrawals: '0',
  change: '150',
  netFlowsBetweenSnapshots: '100',
  changeExcludingFlows: '50',
  comparisonStart: '2026-08-31',
  comparisonNote: null,
  hasStaleRates: false,
  hasFallbackRates: false,
};
const flow: CashFlow = {
  id: 8,
  date: '2026-09-01',
  kind: 'contribution',
  currencyId: 2,
  currencyCode: 'USD',
  amount: '20.1234',
  baseCurrencyCode: 'BRL',
  baseAmount: '100.5678',
  notes: 'Aporte real',
};
const data: PortfolioHistory = {
  portfolio: testPortfolio,
  today: '2026-09-08',
  months: [row],
  years: [{ ...row, period: '2026' }],
  cashFlows: [flow],
};

describe('HistoryPage', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HistoryPage],
      providers: [
        brazilianLocaleProvider,
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  function flush(history: PortfolioHistory = data) {
    http.expectOne('/api/portfolios/1/history').flush(history);
    http.expectOne('/api/currencies').flush([
      { id: 1, name: 'Real', code: 'BRL' },
      { id: 2, name: 'Dólar', code: 'USD' },
    ]);
  }
  function initialize(history: PortfolioHistory = data) {
    const fixture = TestBed.createComponent(HistoryPage);
    fixture.detectChanges();
    http.expectOne('/api/portfolios').flush([testPortfolio]);
    flush(history);
    fixture.detectChanges();
    return fixture;
  }
  it('renders Brazilian totals, switches to annual rows and opens the frozen photograph', () => {
    const fixture = initialize();
    expect(fixture.nativeElement.textContent).toContain('1.234,50');
    fixture.nativeElement.querySelector('app-history-chart button').click();
    http
      .expectOne('/api/portfolios/1/history/snapshots/4')
      .flush({
        id: 4,
        month: '2026-09-01',
        snapshotDate: '2026-09-08',
        capturedAtUtc: '2026-09-08T15:00:00Z',
        payloadVersion: 1,
        dashboard: {
          ...testDashboard,
          summary: {
            ...testDashboard.summary,
            portfolio: { ...testPortfolio, name: 'Nome preservado' },
          },
        },
      });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#snapshot-detail').textContent).toContain(
      'Nome preservado',
    );
    fixture.componentInstance.mode.set('years');
    fixture.detectChanges();
    expect(fixture.componentInstance.rows()[0].period).toBe('2026');
    http.expectNone('/api/portfolios/1/history');
  });
  it('requires a confirmation before replacing the current photograph', () => {
    const fixture = initialize();
    fixture.nativeElement.querySelector('.capture-panel button').click();
    fixture.detectChanges();
    http.expectNone('/api/portfolios/1/history/snapshots/current');
    expect(fixture.nativeElement.querySelector('.confirmation').textContent).toContain(
      'Substituir',
    );
    fixture.nativeElement.querySelector('.confirmation button').click();
    const request = http.expectOne('/api/portfolios/1/history/snapshots/current');
    expect(request.request.method).toBe('PUT');
    request.flush({});
    flush();
  });
  it('keeps missing periods as gaps and creates the first photograph using POST', () => {
    const missing = {
      ...data,
      months: [{ ...row, snapshotId: null, totalWealth: null, snapshotDate: null }],
    };
    const fixture = initialize(missing);
    expect(fixture.nativeElement.querySelector('app-history-chart').textContent).toContain(
      'Sem foto',
    );
    expect(fixture.nativeElement.querySelector('app-history-chart button')).toBeNull();
    fixture.componentInstance.capture();
    const request = http.expectOne('/api/portfolios/1/history/snapshots');
    expect(request.request.method).toBe('POST');
    request.flush(
      { detail: 'Atualize o câmbio antes de fotografar.' },
      { status: 409, statusText: 'Conflict' },
    );
    expect(fixture.componentInstance.error()).toContain('câmbio');
    expect(fixture.componentInstance.saving()).toBe(false);
  });
  it('edits original values precisely and preserves the form when validation fails', () => {
    const fixture = initialize();
    fixture.componentInstance.editFlow(flow);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#flow-amount').value).toBe('20,1234');
    expect(fixture.nativeElement.querySelector('#flow-base').value).toBe('100,5678');
    fixture.componentInstance.form.controls.amount.setValue('1.234,5678');
    fixture.componentInstance.saveFlow();
    const request = http.expectOne('/api/portfolios/1/history/cash-flows/8');
    expect(request.request.body.amount).toBe('1234.5678');
    expect(request.request.body.baseAmount).toBe('100.5678');
    request.flush({ detail: 'Data inválida.' }, { status: 400, statusText: 'Bad Request' });
    expect(fixture.componentInstance.form.controls.amount.value).toBe('1.234,5678');
    expect(fixture.componentInstance.editingFlow()?.id).toBe(8);
  });
  it('allows unknown foreign equivalent and keeps the all-years filter after saving', () => {
    const fixture = initialize();
    fixture.componentInstance.selectedYear.set('');
    fixture.componentInstance.form.patchValue({
      date: '2026-09-02',
      currencyId: 2,
      amount: '10',
      baseAmount: '',
    });
    fixture.componentInstance.saveFlow();
    const request = http.expectOne('/api/portfolios/1/history/cash-flows');
    expect(request.request.body.baseAmount).toBeNull();
    request.flush({ id: 9 });
    flush();
    expect(fixture.componentInstance.selectedYear()).toBe('');
    http.expectNone((r) => r.url.includes('/assets'));
  });
  it('cancels an old detail when switching portfolios and scopes removal to the active portfolio', () => {
    const fixture = initialize();
    fixture.componentInstance.openSnapshot(4);
    const oldRequest = http.expectOne('/api/portfolios/1/history/snapshots/4');
    fixture.componentInstance.select(2);
    expect(oldRequest.cancelled).toBe(true);
    http
      .expectOne('/api/portfolios/2/history')
      .flush({ ...data, portfolio: { ...testPortfolio, id: 2 } });
    http.expectOne('/api/currencies').flush([{ id: 1, name: 'Real', code: 'BRL' }]);
    fixture.componentInstance.pendingDelete.set(flow);
    fixture.componentInstance.deleteFlow();
    const request = http.expectOne('/api/portfolios/2/history/cash-flows/8');
    expect(request.request.method).toBe('DELETE');
    request.flush({ detail: 'Não encontrado.' }, { status: 404, statusText: 'Not Found' });
    expect(fixture.componentInstance.detail()).toBeNull();
  });
});
