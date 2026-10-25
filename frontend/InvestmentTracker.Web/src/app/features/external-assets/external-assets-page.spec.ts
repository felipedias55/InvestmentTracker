import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { brazilianLocaleProvider } from '../../core/locale';
import { testPortfolio } from '../allocation/allocation.test-data';
import { ExternalAssetsPage } from './external-assets-page';

const items = Array.from({ length: 12 }, (_, i) => ({ id: i + 1, name: 'Reserva ' + String(i).padStart(2, '0'), currencyId: 1, currencyCode: 'BRL', value: '1234.5678', baseValue: '1234.5678', description: '', rateDate: null, exchangeRate: '1', isStale: false, isFallback: false }));
const summary = { portfolio: testPortfolio, items, totalValue: '14814.8136', conversionAvailable: true, hasStaleRates: false, hasFallbackRates: false };
describe('ExternalAssetsPage', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [ExternalAssetsPage], providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(), brazilianLocaleProvider] }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  function flush() {
    http.expectOne('/api/portfolios/1/external-assets').flush(summary);
    http.expectOne('/api/currencies').flush([{ id: 1, name: 'Real', code: 'BRL' }]);
  }
  function initialize() {
    const fixture = TestBed.createComponent(ExternalAssetsPage); fixture.detectChanges();
    http.expectOne('/api/portfolios').flush([testPortfolio]); flush(); fixture.detectChanges(); return fixture;
  }
  it('keeps the full total and the audited adjustment link while filtering and paging', () => {
    const fixture = initialize(); const c = fixture.componentInstance;
    c.changePage({ pageIndex: 1, pageSize: 10 }); fixture.detectChanges();
    expect(c.pageItems()).toHaveLength(2); expect(c.summary()?.totalValue).toBe('14814.8136');
    expect(fixture.nativeElement.querySelector('a[href="/movements"]')).not.toBeNull();
    c.search('Reserva 00'); fixture.detectChanges();
    expect(c.currentPage()).toBe(0); expect(c.pageItems()).toHaveLength(1);
    expect(c.summary()?.totalValue).toBe('14814.8136');
  });
  it('preserves precise initial balance and currency during metadata editing and retains errors', () => {
    const fixture = initialize(); const c = fixture.componentInstance;
    c.edit(items[0]); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#external-value').readOnly).toBe(true);
    expect(fixture.nativeElement.querySelector('#external-value').value).toBe('1.234,5678');
    expect(fixture.nativeElement.querySelector('#external-currency').hasAttribute('inert')).toBe(true);
    c.form.controls.name.setValue('FGTS'); c.save();
    const request = http.expectOne('/api/portfolios/1/external-assets/1');
    expect(request.request.body.value).toBe('1234.5678'); expect(request.request.body.currencyId).toBe(1);
    request.flush({ detail: 'Registro alterado, atualize.' }, { status: 409, statusText: 'Conflict' });
    expect(c.formOpen()).toBe(true); expect(c.form.controls.name.value).toBe('FGTS');
    expect(c.error()).toContain('Registro alterado');
  });
  it('cancels stale loads when changing portfolios and resets list context', () => {
    const fixture = initialize(); const c = fixture.componentInstance;
    c.search('Reserva'); c.load(); const old = http.expectOne('/api/portfolios/1/external-assets'); http.expectOne('/api/currencies');
    c.select(2); expect(old.cancelled).toBe(true);
    http.expectOne('/api/portfolios/2/external-assets').flush({ ...summary, portfolio: { ...testPortfolio, id: 2 }, totalValue: null, conversionAvailable: false });
    http.expectOne('/api/currencies').flush([]); fixture.detectChanges();
    expect(c.query()).toBe(''); expect(fixture.nativeElement.textContent).toContain('Câmbio indisponível');
  });
});
