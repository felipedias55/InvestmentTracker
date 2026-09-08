import { brazilianLocaleProvider } from '../../core/locale';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PortfolioPage } from './portfolio-page';
import { PortfolioSummary } from './portfolio.service';

const portfolio = {
  id: 1,
  name: 'Principal',
  description: null,
  baseCurrencyId: 1,
  baseCurrencyCode: 'BRL',
};
const sample: PortfolioSummary = {
  portfolio,
  positions: [
    {
      id: 5,
      assetId: 2,
      ticker: 'MSFT',
      name: 'Microsoft',
      currencyCode: 'USD',
      quantity: '1.123456',
      investedAmount: '10.1234',
      currentValue: '20.5678',
      baseInvestedAmount: '50.6170',
      baseCurrentValue: '102.8390',
      exchangeRate: '5',
      rateDate: '2026-09-07',
      isStale: false,
      isFallback: false,
    },
  ],
  originalSubtotals: [{ currencyCode: 'USD', investedAmount: '10.1234', currentValue: '20.5678' }],
  totalInvested: '50.6170',
  currentValue: '102.8390',
  conversionAvailable: true,
  hasStaleRates: false,
  hasFallbackRates: false,
};

describe('PortfolioPage', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PortfolioPage],
      providers: [brazilianLocaleProvider, provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  function initialize(summary: PortfolioSummary = sample) {
    const fixture = TestBed.createComponent(PortfolioPage);
    fixture.detectChanges();
    http.expectOne('/api/portfolios').flush([portfolio]);
    http.expectOne('/api/currencies').flush([
      { id: 1, name: 'Real', code: 'BRL' },
      { id: 2, name: 'Dólar', code: 'USD' },
    ]);
    http
      .expectOne('/api/assets')
      .flush([{ id: 2, ticker: 'MSFT', name: 'Microsoft', currencyId: 2 }]);
    http.expectOne('/api/portfolios/defaults').flush({ currencyCode: 'BRL' });
    http.expectOne('/api/portfolios/1').flush(summary);
    fixture.detectChanges();
    return fixture;
  }
  it('switches display without changing totals or requesting another rate', () => {
    const fixture = initialize();
    const buttons = Array.from(
      fixture.nativeElement.querySelectorAll('button'),
    ) as HTMLButtonElement[];
    buttons.find((b) => b.textContent?.trim() === 'Moeda original')!.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Subtotais por moeda original');
    expect(fixture.nativeElement.querySelector('.totals').textContent).toContain('BRL');
    expect(fixture.nativeElement.querySelector('.position-card').textContent).toContain('USD');
    expect(fixture.componentInstance.summary()?.currentValue).toBe('102.8390');
    http.expectNone('/api/portfolios/1');
  });
  it('edits original amounts even while converted values are displayed', () => {
    const fixture = initialize();
    fixture.componentInstance.editPosition(sample.positions[0]);
    fixture.detectChanges();
    expect(fixture.componentInstance.positionForm.controls.currentValue.value).toBe('20,5678');
    expect(fixture.nativeElement.textContent).toContain('Entrada sempre em USD');
    expect(fixture.componentInstance.positionForm.controls.assetId.disabled).toBe(true);
    fixture.componentInstance.positionForm.controls.currentValue.setValue('30,1234');
    fixture.componentInstance.savePosition();
    const request = http.expectOne('/api/portfolios/1/assets/5');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body.currentValue).toBe('30.1234');
    expect(request.request.body.quantity).toBe('1.123456');
    request.flush(null);
    http.expectOne('/api/portfolios/1').flush(sample);
  });
  it('shows unavailable totals without hiding original values', () => {
    const missing = {
      ...sample,
      conversionAvailable: false,
      currentValue: null,
      totalInvested: null,
      positions: [
        {
          ...sample.positions[0],
          baseInvestedAmount: null,
          baseCurrentValue: null,
          exchangeRate: null,
          rateDate: null,
        },
      ],
    };
    const fixture = initialize(missing);
    expect(fixture.nativeElement.querySelector('.totals').textContent).toContain('Indisponível');
    fixture.componentInstance.view.set('original');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.position-card').textContent).toContain('USD');
    expect(fixture.componentInstance.summary()?.positions[0].currentValue).toBe('20.5678');
  });
  it('shows fallback and reference date explicitly', () => {
    const fixture = initialize({
      ...sample,
      hasStaleRates: true,
      hasFallbackRates: true,
      positions: [
        { ...sample.positions[0], isStale: true, isFallback: true, rateDate: '2026-09-04' },
      ],
    });
    expect(fixture.nativeElement.textContent).toContain('Usando a última cotação armazenada');
    expect(fixture.nativeElement.textContent).toContain('04/09/2026');
  });
  it('rejects negatives without sending a request and preserves state on conflict', () => {
    const fixture = initialize();
    fixture.componentInstance.editPosition(sample.positions[0]);
    fixture.componentInstance.positionForm.controls.quantity.setValue('-1');
    fixture.componentInstance.savePosition();
    http.expectNone('/api/portfolios/1/assets/5');
    fixture.componentInstance.positionForm.controls.quantity.setValue('1');
    fixture.componentInstance.savePosition();
    http
      .expectOne('/api/portfolios/1/assets/5')
      .flush({ detail: 'Posição alterada.' }, { status: 409, statusText: 'Conflict' });
    expect(fixture.componentInstance.error()).toBe('Posição alterada.');
    expect(fixture.componentInstance.editingPosition()).toBe(5);
  });
  it('formats Brazilian amounts and preserves precision when submitting grouped input', () => {
    const fixture = initialize();
    expect(fixture.nativeElement.querySelector('.totals').textContent).toContain('102,84');
    expect(fixture.nativeElement.querySelector('.position-card').textContent).toContain('1,123456');
    fixture.componentInstance.editPosition(sample.positions[0]);
    const control = fixture.componentInstance.positionForm.controls.currentValue;
    control.setValue('999999999999999,1234');
    fixture.componentInstance.formatInput('currentValue');
    expect(control.value).toBe('999.999.999.999.999,1234');
    fixture.componentInstance.savePosition();
    const request = http.expectOne('/api/portfolios/1/assets/5');
    expect(request.request.body.currentValue).toBe('999999999999999.1234');
    request.flush(null);
    http.expectOne('/api/portfolios/1').flush(sample);
  });
  it('rejects malformed grouping and American decimal input', () => {
    const fixture = initialize();
    fixture.componentInstance.editPosition(sample.positions[0]);
    for (const value of ['12.34,5', '20.5678', '1,234.56', '1.000,12345']) {
      fixture.componentInstance.positionForm.controls.currentValue.setValue(value);
      fixture.componentInstance.savePosition();
      expect(fixture.componentInstance.positionForm.invalid).toBe(true);
      http.expectNone('/api/portfolios/1/assets/5');
    }
  });
  it('defaults a new portfolio to BRL', () => {
    const fixture = initialize();
    fixture.componentInstance.newPortfolio();
    expect(fixture.componentInstance.portfolioForm.controls.baseCurrencyId.value).toBe(1);
  });
});
