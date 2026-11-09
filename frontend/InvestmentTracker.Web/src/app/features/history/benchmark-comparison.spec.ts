import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { brazilianLocaleProvider } from '../../core/locale';
import { BenchmarkComparison } from './benchmark-comparison';

const period = { period: '2026-02', currencyCode: 'BRL', comparisonStart: '2026-01-31', snapshotDate: '2026-02-28', returns: { modifiedDietz: '0.1', xirr: '9', note: null } };
const response = { period: '2026-02', start: '2026-01-31', end: '2026-02-28', currencyCode: 'BRL', portfolioReturn: '0.1', method: 'Diferença em pontos percentuais.', items: [
  { code: 'IBOV', name: 'Ibovespa', source: 'Fonte sintética', currencyCode: 'BRL', return: '0.05', difference: '0.05', firstObservation: '2026-01-30', lastObservation: '2026-02-27', note: 'Índice de referência.', fromCache: true, retrievedAt: '2026-03-01T12:00:00Z' },
  { code: 'IPCA', name: 'IPCA', source: 'BCB / SGS 433', currencyCode: 'BRL', return: null, difference: null, note: 'Mês não publicado.' },
] };

describe('BenchmarkComparison', () => {
  let http: HttpTestingController;
  beforeEach(() => { TestBed.configureTestingModule({ imports: [BenchmarkComparison], providers: [brazilianLocaleProvider, provideHttpClient(), provideHttpClientTesting()] }); http = TestBed.inject(HttpTestingController); });
  afterEach(() => http.verify());
  function create() {
    const f = TestBed.createComponent(BenchmarkComparison); f.componentRef.setInput('portfolioId', 1); f.componentRef.setInput('mode', 'months'); f.componentRef.setInput('periods', [period]); f.detectChanges(); return f;
  }
  it('queries only explicitly and renders backend interval returns and percentage points instead of XIRR', () => {
    const f = create(); http.expectNone(() => true); f.componentInstance.compare();
    const request = http.expectOne(r => r.url === '/api/portfolios/1/history/benchmarks');
    expect(request.request.params.get('period')).toBe('2026-02'); expect(request.request.params.get('mode')).toBe('months'); request.flush(response); f.detectChanges();
    expect(f.nativeElement.textContent).toContain('10,00%'); expect(f.nativeElement.textContent).toContain('5,00');
    expect(f.nativeElement.textContent).toContain('Mês não publicado.'); expect(f.nativeElement.textContent).toContain('cache');
    expect(f.nativeElement.textContent).not.toContain('900,00%');
  });
  it('cancels requests and removes stale results when the portfolio or interval changes', () => {
    const f = create(); f.componentInstance.compare(); const request = http.expectOne(r => r.url.endsWith('/benchmarks'));
    f.componentRef.setInput('portfolioId', 2); f.detectChanges(); expect(request.cancelled).toBe(true); expect(f.componentInstance.busy()).toBe(false); expect(f.componentInstance.result()).toBeNull();
    f.componentInstance.compare(); http.expectOne(r => r.url === '/api/portfolios/2/history/benchmarks').flush(response); f.detectChanges();
    f.componentRef.setInput('mode', 'years'); f.componentRef.setInput('periods', [{ ...period, period: '2026' }]); f.detectChanges();
    expect(f.componentInstance.result()).toBeNull(); http.expectNone(() => true);
  });
  it('disables invalid intervals without consuming requests and preserves the selection after errors', () => {
    const f = create(); f.componentRef.setInput('periods', [{ ...period, returns: { modifiedDietz: null, xirr: null, note: 'Fotografia desatualizada.' } }]); f.detectChanges();
    f.componentInstance.compare(); http.expectNone(() => true); expect(f.nativeElement.textContent).toContain('Fotografia desatualizada.');
    f.componentRef.setInput('periods', [period]); f.detectChanges(); f.componentInstance.compare();
    http.expectOne(r => r.url.endsWith('/benchmarks')).flush({ detail: 'Tente novamente.' }, { status: 503, statusText: 'Unavailable' });
    expect(f.componentInstance.error()).toContain('Tente novamente.'); expect(f.componentInstance.row()?.period).toBe('2026-02'); expect(f.componentInstance.busy()).toBe(false);
  });
});
