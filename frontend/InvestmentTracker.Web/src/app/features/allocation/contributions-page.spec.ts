import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { brazilianLocaleProvider } from '../../core/locale';
import { ContributionsPage } from './contributions-page';
import { testDashboard, testPortfolio } from './allocation.test-data';

const result = {
  currencyCode: 'BRL',
  dimension: 'category',
  amount: '1234.50',
  sumOfWeights: '0',
  unallocatedAmount: '1234.50',
  hasStaleRates: false,
  hasFallbackRates: false,
  rows: [],
};
describe('ContributionsPage', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ContributionsPage],
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
  function initialize() {
    const fixture = TestBed.createComponent(ContributionsPage);
    fixture.detectChanges();
    http.expectOne('/api/portfolios').flush([testPortfolio]);
    http.expectOne('/api/portfolios/1/dashboard').flush(testDashboard);
    fixture.detectChanges();
    return fixture;
  }
  it('submits a Brazilian amount and removes stale results when the input changes', () => {
    const fixture = initialize();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('#contribution-amount');
    input.value = '1.234,50';
    input.dispatchEvent(new Event('input'));
    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    const request = http.expectOne('/api/portfolios/1/contribution-analysis');
    expect(request.request.body).toEqual({ dimension: 'category', amount: '1234.50' });
    request.flush(result);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Valor sem distribuição');
    expect(fixture.nativeElement.textContent).toContain('1.234,50');
    fixture.componentInstance.form.controls.dimension.setValue('sector');
    expect(fixture.componentInstance.result()).toBeNull();
    http.expectNone((r) => r.method === 'PUT' || r.method === 'DELETE');
  });
  it('does not submit negative or overprecision values', () => {
    const fixture = initialize();
    for (const value of ['-1', '1,123', '12.34,5']) {
      fixture.componentInstance.form.controls.amount.setValue(value);
      fixture.componentInstance.simulate();
      http.expectNone('/api/portfolios/1/contribution-analysis');
    }
  });
  it('preserves input on unavailable currency errors and shows fallback warnings', () => {
    const fixture = initialize();
    fixture.componentInstance.form.controls.amount.setValue('100');
    fixture.componentInstance.simulate();
    http
      .expectOne('/api/portfolios/1/contribution-analysis')
      .flush({ detail: 'O câmbio está indisponível.' }, { status: 409, statusText: 'Conflict' });
    expect(fixture.componentInstance.form.controls.amount.value).toBe('100');
    expect(fixture.componentInstance.result()).toBeNull();
    fixture.componentInstance.simulate();
    http
      .expectOne('/api/portfolios/1/contribution-analysis')
      .flush({ ...result, hasFallbackRates: true });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('cotações anteriores');
  });
});
