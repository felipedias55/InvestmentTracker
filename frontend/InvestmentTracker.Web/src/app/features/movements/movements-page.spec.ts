import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { brazilianLocaleProvider } from '../../core/locale';
import { Movement, MovementsPage } from './movements-page';
import { testDashboard, testPortfolio } from '../allocation/allocation.test-data';

describe('MovementsPage', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [MovementsPage], providers: [brazilianLocaleProvider,
      provideRouter([]), provideHttpClient(), provideHttpClientTesting()] }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  function flush() {
    http.expectOne('/api/portfolios/1').flush(testDashboard.summary);
    http.expectOne('/api/currencies').flush([{ id: 1, code: 'BRL', name: 'Real' }]);
    http.expectOne('/api/portfolios/1/external-assets').flush({ items: [{ id: 5, name: 'Saldo', currencyId: 1, currencyCode: 'BRL', value: '1000' }] });
    http.expectOne(r => r.url === '/api/portfolios/1/movements/page').flush({ items: [], total: 0, pageIndex: 0, pageSize: 10 });
  }
  function initialize() {
    const fixture = TestBed.createComponent(MovementsPage); fixture.detectChanges();
    http.expectOne('/api/portfolios').flush([testPortfolio]); flush(); fixture.detectChanges(); return fixture;
  }
  it('preserves decimal precision and reuses the request on an uncertain deposit result', () => {
    const c = initialize().componentInstance;
    c.form.patchValue({ cashAssetId: 5, amount: '1.234,5678' }); c.save();
    const first = http.expectOne('/api/portfolios/1/movements');
    expect(first.request.body.amount).toBe('1234.5678');
    const id = first.request.body.requestId;
    c.save(); http.expectNone('/api/portfolios/1/movements');
    first.error(new ProgressEvent('error'));
    c.save(); const retry = http.expectOne('/api/portfolios/1/movements');
    expect(retry.request.body.requestId).toBe(id);
    retry.flush({}); flush();
  });
  it('requires a reversal reason and keeps the confirmation open on dependency conflicts', () => {
    const c = initialize().componentInstance;
    c.confirm({ id: 9, kind: 'deposit' } as Movement);
    c.reverse(); http.expectNone('/api/portfolios/1/movements/9/reversal');
    c.reverseReason.set('Depósito duplicado'); c.reverse();
    const request = http.expectOne('/api/portfolios/1/movements/9/reversal');
    c.reverse(); http.expectNone('/api/portfolios/1/movements/9/reversal');
    request.flush({ detail: 'Existem movimentações posteriores dependentes.' }, { status: 409, statusText: 'Conflict' });
    expect(c.pending()?.id).toBe(9);
    expect(c.reverseReason()).toBe('Depósito duplicado');
    expect(c.error()).toContain('dependentes');
  });
  it('previews an existing correction without sending a reversal and discards stale results when the date changes', () => {
    const fixture = initialize(); const c = fixture.componentInstance;
    c.openPreview({ id: 9, kind: 'buy', date: '2026-09-01', description: 'Compra' } as Movement);
    c.analyzeCorrection();
    const request = http.expectOne('/api/portfolios/1/movements/correction-preview');
    expect(request.request.body).toEqual({ date: '2026-09-01', movementId: 9, positionId: null, cashAssetId: null, destinationId: null });
    request.flush({ fromDate: '2026-09-01', movementId: 9,
      movements: [{ id: 10, date: '2026-09-02', kind: 'sell', description: 'Venda', dependency: 'indirect', isReversed: false }],
      snapshots: [{ id: 2, month: '2026-09-01', revision: 1, requiresReopening: true }], reviewOrder: [10, 9], warnings: ['Consulta sem gravação.'] });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Dependência indireta');
    expect(fixture.nativeElement.textContent).toContain('Período fechado');
    expect(fixture.nativeElement.textContent).toContain('Consulta sem gravação.');
    c.previewForm.patchValue({ date: '2026-09-02' });
    expect(c.preview()).toBeNull();
    http.expectNone('/api/portfolios/1/movements/9/reversal');
  });
  it('requires references for a late entry and cancels requests when the preview is closed', () => {
    const c = initialize().componentInstance; c.openPreview(); c.analyzeCorrection();
    http.expectNone('/api/portfolios/1/movements/correction-preview');
    expect(c.previewError()).toContain('Selecione');
    c.previewForm.patchValue({ cashAssetId: 5, destinationId: 6 }); c.analyzeCorrection();
    const request = http.expectOne('/api/portfolios/1/movements/correction-preview');
    expect(request.request.body.cashAssetId).toBe(5); expect(request.request.body.destinationId).toBe(6);
    c.closePreview(); expect(request.cancelled).toBe(true); expect(c.previewLoading()).toBe(false);
    expect(c.preview()).toBeNull(); expect(c.previewOpen()).toBe(false);
  });
  it('filters on the server, resets the page and cancels superseded queries', () => {
    const c = initialize().componentInstance;
    c.changePage({ pageIndex: 2, pageSize: 10 });
    const old = http.expectOne(r => r.url.endsWith('/movements/page'));
    expect(old.request.params.get('pageIndex')).toBe('2');
    c.filters.patchValue({ search: 'PETR4', kind: 'buy', status: 'active', from: '2026-01-01' }); c.applyFilters();
    expect(old.cancelled).toBe(true);
    const filtered = http.expectOne(r => r.url.endsWith('/movements/page'));
    expect(filtered.request.params.get('pageIndex')).toBe('0');
    expect(filtered.request.params.get('search')).toBe('PETR4');
    expect(filtered.request.params.get('kind')).toBe('buy');
    expect(filtered.request.params.get('status')).toBe('active');
    filtered.flush({ items: [{ id: 42, kind: 'buy' }], total: 1, pageIndex: 0, pageSize: 10 });
    expect(c.movements()[0].id).toBe(42);
    http.expectNone('/api/portfolios/1/movements/42');
  });
  it('loads audit only on request and cancels it when the panel closes', () => {
    const c = initialize().componentInstance;
    c.showDetail(42); const first = http.expectOne('/api/portfolios/1/movements/42');
    c.closeDetail(); expect(first.cancelled).toBe(true);
    c.showDetail(43); http.expectOne('/api/portfolios/1/movements/43').flush({ id: 43, effects: [], canReverse: false, reversalBlockedReason: 'Já estornado.' });
    expect(c.detail()?.id).toBe(43); expect(c.detailLoading()).toBe(false);
    c.closeDetail(); expect(c.detail()).toBeNull();
  });
  it('preserves the page when reloading after a change and rejects invalid date ranges', () => {
    const c = initialize().componentInstance;
    c.pageIndex.set(2); c.load();
    const page = http.expectOne(r => r.url.endsWith('/movements/page'));
    expect(page.request.params.get('pageIndex')).toBe('2');
    page.flush({ items: [], total: 0, pageIndex: 0, pageSize: 10 });
    http.expectOne('/api/portfolios/1').flush(testDashboard.summary);
    http.expectOne('/api/currencies').flush([]);
    http.expectOne('/api/portfolios/1/external-assets').flush({ items: [] });
    expect(c.pageIndex()).toBe(0);
    c.filters.patchValue({ from: '2026-10-20', to: '2026-01-01' }); c.applyFilters();
    http.expectNone(r => r.url.endsWith('/movements/page'));
    expect(c.listError()).toContain('data inicial');
  });

});
