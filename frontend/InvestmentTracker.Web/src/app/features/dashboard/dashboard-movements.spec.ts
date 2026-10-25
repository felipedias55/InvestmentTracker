import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { brazilianLocaleProvider } from '../../core/locale';
import { DashboardMovements } from './dashboard-movements';
import { ValuePrivacy } from '../../core/value-privacy';

describe('DashboardMovements', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [DashboardMovements], providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(), brazilianLocaleProvider] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  it('loads only on expansion, sorts the latest five and identifies reversals', () => {
    const f = TestBed.createComponent(DashboardMovements); f.componentRef.setInput('portfolioId', 1); f.detectChanges();
    http.expectNone('/api/portfolios/1/movements');
    f.componentRef.setInput('active', true); f.detectChanges();
    http.expectOne('/api/portfolios/1/movements').flush(Array.from({ length: 7 }, (_, i) => ({
      id: i+1, date: '2026-10-14', createdAtUtc: '2026-10-14T12:00:00Z', kind: 'buy', amount: '100', currencyCode: 'BRL', effects: [], reversedById: i === 6 ? 10 : null
    })));
    f.detectChanges();
    expect(f.componentInstance.recent().map(m => m.id)).toEqual([7,6,5,4,3]);
    expect(f.nativeElement.textContent).toContain('Estornado por #10');
    f.componentRef.setInput('portfolioId', 2); f.detectChanges();
    http.expectOne('/api/portfolios/2/movements').flush([]);
    expect(f.componentInstance.recent()).toEqual([]);
  });
  it('opens full read-only data without presenting administrative amounts as cash flows', () => {
    const f = TestBed.createComponent(DashboardMovements);
    f.componentRef.setInput('portfolioId', 1); f.componentRef.setInput('active', true); f.detectChanges();
    http.expectOne('/api/portfolios/1/movements').flush([{ id: 9, date: '2026-10-25', createdAtUtc: '2026-10-25T12:00:00Z', kind: 'adjustment', amount: '99999', currencyCode: 'BRL', description: 'Ajuste administrativo de demonstração', correctionId: 4, replacesMovementId: 3, effects: [{ name: 'Nome completo do saldo de demonstração', currencyCode: 'BRL', beforeValue: '100', afterValue: '1234.5678', isPosition: false }] }]);
    f.detectChanges();
    expect(f.nativeElement.querySelector('.movement-amount')).toBeNull();
    expect(f.nativeElement.querySelector('dialog')).toBeNull();
    f.nativeElement.querySelector('.movement-open').click(); f.detectChanges();
    const panel = f.nativeElement.querySelector('dialog');
    expect(panel.textContent).toContain('Nome completo do saldo');
    expect(panel.textContent).toContain('1.234,5678');
    expect(panel.textContent).toContain('Parte da correção #4');
    expect(panel.textContent).not.toContain('99.999');
    TestBed.inject(ValuePrivacy).hidden.set(true); f.detectChanges();
    expect(panel.textContent).toContain('••••••'); expect(panel.textContent).not.toContain('1.234,5678');
    f.componentRef.setInput('active', false); f.detectChanges();
    expect(f.nativeElement.querySelector('dialog')).toBeNull();
    http.expectNone(req => req.method !== 'GET');
  });
});
