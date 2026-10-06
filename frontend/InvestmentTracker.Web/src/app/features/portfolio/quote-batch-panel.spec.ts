import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { QuoteBatchPanel, quoteTotal } from './quote-batch-panel';
import { brazilianLocaleProvider } from '../../core/locale';
import { Position } from './portfolio.service';

const position: Position = { id: 1, assetId: 1, ticker: 'TEST', name: 'Synthetic', currencyCode: 'USD', quantity: '1.123456',
  currentValue: '20', investedAmount: '15', income: '3', baseCurrentValue: '100', baseInvestedAmount: '75', baseIncome: '15',
  exchangeRate: '5', rateDate: null, isStale: false, isFallback: false, updatedOn: '2026-10-01' };

describe('QuoteBatchPanel', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [QuoteBatchPanel], providers: [brazilianLocaleProvider, provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  function initialize() {
    const fixture = TestBed.createComponent(QuoteBatchPanel);
    fixture.componentRef.setInput('portfolioId', 1);
    fixture.componentRef.setInput('positions', [position, { ...position, id: 2, ticker: 'ZERO', quantity: '0' }]);
    fixture.detectChanges(); return fixture;
  }
  it('requires review and submits only filled rows in original currency', () => {
    const component = initialize().componentInstance;
    component.setPrice(1, '30,1234'); component.save(); http.expectNone('/api/portfolios/1/quotes');
    expect(component.changed()[0].total).toBe('33.8423');
    component.review.set(true); component.save();
    const request = http.expectOne('/api/portfolios/1/quotes');
    expect(request.request.body.items).toEqual([{ positionId: 1, currencyCode: 'USD', unitPrice: '30.1234', expectedQuantity: '1.123456', expectedValue: '20', expectedUpdatedOn: '2026-10-01' }]);
    request.flush(null); expect(component.saving()).toBe(false);
  });
  it('retains the same request id after an uncertain response', () => {
    const component = initialize().componentInstance;
    component.setPrice(1, '30'); component.review.set(true); component.save();
    const first = http.expectOne('/api/portfolios/1/quotes'); const body = first.request.body;
    first.error(new ProgressEvent('error')); expect(component.uncertain()).toBe(true);
    component.save(); const retry = http.expectOne('/api/portfolios/1/quotes');
    expect(retry.request.body).toEqual(body); retry.flush(null);
  });
  it('rejects invalid decimals and total overflow without sending anything', () => {
    const component = initialize().componentInstance;
    for (const price of ['0', '-1', '1,23456', 'texto', '999.999.999.999.999,9999']) {
      component.setPrice(1, price); expect(component.valid()).toBe(false);
      component.review.set(true); component.save(); http.expectNone('/api/portfolios/1/quotes');
    }
  });
  it('matches server rounding for half-way values without floating point drift', () => {
    expect(quoteTotal('0.000001', '50')).toBe('0.0000');
    expect(quoteTotal('0.000003', '50')).toBe('0.0002');
    expect(quoteTotal('9999999999999', '0.0001')).toBe('999999999.9999');
  });
});
