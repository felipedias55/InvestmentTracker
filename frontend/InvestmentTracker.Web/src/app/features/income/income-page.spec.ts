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
    http.expectOne(`/api/portfolios/${portfolio}/income/analysis`).flush({ months: [], years: [], assets: [] });
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
  it('filters monthly receipts by year, currency and asset without combining currencies', () => {
    const fixture = initialize(); const component = fixture.componentInstance;
    const brl = { period: '2026-09', ticker: 'TEST', assetId: 1, currencyCode: 'BRL', received: '10', reversed: '2', net: '8' };
    const usd = { ...brl, assetId: 2, ticker: 'OTHER', currencyCode: 'USD' };
    component.analysis.set({ months: [brl, usd, { ...brl, period: '2025-09' }], years: [], assets: [brl, usd] });
    component.analysisYear.set('2026'); component.analysisCurrency.set('BRL');
    expect(component.analysisRows()).toEqual([brl]);
    component.query.set('other'); expect(component.analysisRows()).toEqual([]);
    component.analysisCurrency.set('USD'); expect(component.analysisRows()).toEqual([usd]);
  });

  it('records the manual historical equivalent with exact Brazilian precision', () => {
    const fixture = initialize(); const component = fixture.componentInstance;
    component.assets.set([{ ...asset, currencyId: 2 }]);
    component.currencies.set([{ id: 1, code: 'BRL', name: 'Real' }, { id: 2, code: 'USD', name: 'Dólar' }]);
    component.form.patchValue({ assetId: 10, amount: '10', baseAmount: '51,2345' }); component.save();
    const request = http.expectOne('/api/portfolios/1/income'); expect(request.request.body.baseAmount).toBe('51.2345');
    request.flush(trade); flush();
  });
  it('preserves revision and request ID when supplementing a receipt after a network failure', () => {
    const fixture = initialize(); const component = fixture.componentInstance;
    component.openConversion({ id: 7, date: '2026-09-01', ticker: 'TEST', currencyCode: 'USD', amount: '10', cashAssetName: null, notes: null,
      conversions: [{ id: 1, revision: 2, baseCurrencyCode: 'BRL', baseAmount: '50', rate: '5', rateDate: '2026-09-01', source: 'manual', reason: 'Extrato', createdAtUtc: '' }] });
    component.conversionForm.patchValue({ baseAmount: '52,1234', reason: 'Retificação' }); component.saveConversion();
    const first = http.expectOne('/api/portfolios/1/income/7/conversions');
    expect(first.request.body.expectedRevision).toBe(2); expect(first.request.body.baseAmount).toBe('52.1234');
    const requestId = first.request.body.requestId; first.error(new ProgressEvent('error'));
    component.saveConversion(); const retry = http.expectOne('/api/portfolios/1/income/7/conversions');
    expect(retry.request.body.requestId).toBe(requestId); retry.flush({}); flush();
    expect(component.pendingConversion()).toBeNull();
  });
  it('shows pending converted totals instead of a misleading partial sum', () => {
    const fixture = initialize(); const component = fixture.componentInstance;
    component.analysis.set({ months: [], years: [], assets: [], baseCurrencyCode: 'BRL',
      convertedMonths: [{ period: '2026-09', ticker: 'TEST', assetId: 1, currencyCode: 'BRL', received: null, reversed: null, net: null, missingConversions: 1 }] });
    component.valueMode.set('base'); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Conversão pendente');
    expect(component.analysisRows()[0].net).toBeNull();
  });

});
