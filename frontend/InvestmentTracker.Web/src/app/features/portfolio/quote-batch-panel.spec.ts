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
  it('previews quotes without saving and includes source only while the fetched price is unchanged', () => {
    const component = initialize().componentInstance;
    component.fetchQuotes();
    const preview = http.expectOne('/api/portfolios/1/quotes/preview');
    expect(preview.request.body).toEqual({ positionIds: [1] });
    preview.flush([{ positionId: 1, quote: { provider: 'Alpha Vantage', price: '30.1234', date: '2026-10-06', message: 'Fechamento' } }]);
    expect(component.prices()[1]).toBe('30,1234');
    http.expectNone('/api/portfolios/1/quotes');
    component.review.set(true); component.save();
    const save = http.expectOne('/api/portfolios/1/quotes');
    expect(save.request.body.items[0].source).toBe('Alpha Vantage');
    expect(save.request.body.items[0].quoteDate).toBe('2026-10-06');
    save.flush(null);
    component.setPrice(1, '31');
    expect(component.quotes()[1]).toBeUndefined();
  });
  it('keeps failed quotes blank and does not overwrite manual input', () => {
    const fixture = initialize(), component = fixture.componentInstance;
    fixture.componentRef.setInput('positions', [position, { ...position, id: 3 }]);
    component.setPrice(1, '99');
    component.fetchQuotes();
    const preview = http.expectOne('/api/portfolios/1/quotes/preview');
    expect(preview.request.body.positionIds).toEqual([3]);
    preview.flush([{ positionId: 3, quote: { provider: 'Alpha Vantage', price: null, date: null, message: 'Limite' } }]);
    expect(component.prices()[1]).toBe('99');
    expect(component.prices()[3]).toBeUndefined();
    expect(component.quotes()[3].message).toBe('Limite');
  });

  it('shows quote timestamps only in an optional tooltip, with a touch toggle', () => {
    const fixture = initialize(), component = fixture.componentInstance;
    component.quotes.set({ 1: { provider: 'brapi', price: '54.12', date: '2026-10-07',
      marketTime: '2026-10-07T13:19:30Z', retrievedAt: '2026-10-07T13:50:00Z', message: 'Com atraso' } });
    fixture.detectChanges();
    const button = fixture.nativeElement.querySelector('.quote-info-button') as HTMLButtonElement;
    const tooltip = fixture.nativeElement.querySelector('[role=tooltip]') as HTMLElement;
    expect(button.getAttribute('aria-expanded')).toBe('false');
    expect(tooltip.classList.contains('open')).toBe(false);
    expect(tooltip.textContent).toContain('07/10/2026, 10:19:30 (Brasília)');
    expect(tooltip.textContent).toContain('07/10/2026, 10:50:00 (Brasília)');
    button.click(); fixture.detectChanges();
    expect(tooltip.classList.contains('open')).toBe(true);
    button.click(); fixture.detectChanges();
    expect(tooltip.classList.contains('open')).toBe(false);
    expect(component.quoteDetails({ provider: 'Alpha Vantage', price: '10', date: '2026-10-06', message: 'Diário' }))
      .toContain('06/10/2026 · horário não fornecido');
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
