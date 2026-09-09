import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { brazilianLocaleProvider } from '../../core/locale';
import { IncomePage } from './income-page';
import { testDashboard, testPortfolio } from '../allocation/allocation.test-data';

const asset = { id: 10, ticker: 'TEST', name: 'Teste', currencyId: 1, assetTypeId: 1, countryId: 1, assetCategoryId: 1, sectorId: 1, createdAt: '' };
const trade = { id: 1, date: '2026-09-08', kind: 'buy', ticker: 'TEST', currencyCode: 'BRL', quantity: '2', unitPrice: '10.1234', amount: '20.2468', cashAssetName: null };
describe('IncomePage', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [IncomePage], providers: [brazilianLocaleProvider,
      provideRouter([]), provideHttpClient(), provideHttpClientTesting()] }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  function flush(portfolio = 1, trades: unknown[] = []) {
    http.expectOne(`/api/portfolios/${portfolio}`).flush(testDashboard.summary);
    http.expectOne('/api/assets').flush([asset]);
    http.expectOne('/api/currencies').flush([{ id: 1, code: 'BRL', name: 'Real' }]);
    http.expectOne(`/api/portfolios/${portfolio}/external-assets`).flush({ ...testDashboard.externalAssets,
      items: [{ id: 5, name: 'Saldo', currencyId: 1, currencyCode: 'BRL', value: '1000' }] });
    http.expectOne(`/api/portfolios/${portfolio}/income`).flush(trades);
  }
  function initialize() {
    const fixture = TestBed.createComponent(IncomePage); fixture.detectChanges();
    http.expectOne('/api/portfolios').flush([testPortfolio]); flush(); fixture.detectChanges(); return fixture;
  }
  it('submits one operation with exact Brazilian decimals and refreshes the ledger', () => {
    const fixture = initialize(); const component = fixture.componentInstance;
    component.form.patchValue({ assetId: 10, amount: '20,2468' });
    fixture.detectChanges(); fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    const request = http.expectOne('/api/portfolios/1/income');
    expect(request.request.method).toBe('POST');
    expect(request.request.body.amount).toBe('20.2468');
    expect(request.request.body.cashAssetId).toBeNull();
    component.save(); http.expectNone('/api/portfolios/1/income');
    request.flush(trade); flush(1, [trade]); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('20,2468');
    expect(fixture.nativeElement.textContent).toContain('Fora dos saldos da carteira');
    expect(component.form.controls.amount.value).toBe('');
    http.expectNone('/api/portfolios/1/history/cash-flows');
  });
  it('keeps the request ID after a network failure to avoid repeating a purchase', () => {
    const fixture = initialize(); const component = fixture.componentInstance;
    component.form.patchValue({ assetId: 10, amount: '20' }); component.save();
    const first = http.expectOne('/api/portfolios/1/income'); const id = first.request.body.requestId;
    first.error(new ProgressEvent('error'));
    component.save(); const retry = http.expectOne('/api/portfolios/1/income');
    expect(retry.request.body.requestId).toBe(id);
    retry.flush(trade); flush(1, [trade]);
  });
  it('credits the chosen balance and blocks nonpositive income', () => {
    const fixture = initialize(); const component = fixture.componentInstance;
    component.form.patchValue({ assetId: 10, amount: '0', cashAssetId: 5 });
    component.save(); http.expectNone('/api/portfolios/1/income');
    component.form.patchValue({ amount: '1' }); component.save();
    const request = http.expectOne('/api/portfolios/1/income');
    expect(request.request.body.cashAssetId).toBe(5);
    request.flush({ ...trade, cashAssetName: 'Saldo' }); flush();
  });
});
