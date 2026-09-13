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
    http.expectOne('/api/portfolios/1/movements').flush([]);
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
});
