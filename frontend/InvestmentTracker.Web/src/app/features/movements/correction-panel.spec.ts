import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { brazilianLocaleProvider } from '../../core/locale';
import { CorrectionPanel } from './correction-panel';

describe('CorrectionPanel', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [CorrectionPanel], providers: [brazilianLocaleProvider, provideHttpClient(), provideHttpClientTesting()] }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  function initialize() {
    const fixture = TestBed.createComponent(CorrectionPanel); fixture.componentRef.setInput('portfolioId', 1); fixture.detectChanges();
    http.expectOne('/api/assets').flush([{ id: 2, ticker: 'TEST', name: 'Teste', currencyId: 1 }]);
    const c = fixture.componentInstance;
    c.form.patchValue({ date: '2026-09-01', kind: 'buy', assetId: 2, quantity: '12', unitPrice: '1.234,5678', fees: '0', reason: 'Corrigir extrato' });
    return fixture;
  }
  function simulate(c: CorrectionPanel) {
    c.simulate(); const request = http.expectOne('/api/portfolios/1/movements/correction-simulation');
    expect(request.request.body.operation.unitPrice).toBe('1234.5678');
    request.flush({ canApply: true, token: 'version-one', fromDate: '2026-09-01', replacedMovementIds: [3], balances: [], snapshots: [], warnings: [], blockers: [] });
  }
  it('requires explicit confirmation and retries an uncertain result with the same request identifier', () => {
    const c = initialize().componentInstance; simulate(c);
    c.apply(); http.expectNone('/api/portfolios/1/movements/corrections');
    c.accepted.set(true); c.apply(); const request = http.expectOne('/api/portfolios/1/movements/corrections');
    const id = request.request.body.requestId;
    expect(request.request.body.token).toBe('version-one');
    c.apply(); http.expectNone('/api/portfolios/1/movements/corrections');
    request.error(new ProgressEvent('error'));
    c.apply(); const retry = http.expectOne('/api/portfolios/1/movements/corrections');
    expect(retry.request.body.requestId).toBe(id); retry.flush({ correctionId: 8 });
  });
  it('invalidates the preview and equivalent when economic values change and discards a stale server result', () => {
    const c = initialize().componentInstance; c.form.controls.baseAmount.setValue('55'); simulate(c);
    c.form.controls.quantity.setValue('13'); expect(c.result()).toBeNull(); expect(c.form.controls.baseAmount.value).toBe('');
    simulate(c); c.accepted.set(true); c.apply();
    http.expectOne('/api/portfolios/1/movements/corrections').flush({ detail: 'A carteira mudou. Simule novamente.' }, { status: 409, statusText: 'Conflict' });
    expect(c.result()).toBeNull(); expect(c.accepted()).toBe(false); expect(c.error()).toContain('Simule novamente');
  });
  it('shows blockers without providing an apply action', () => {
    const fixture = initialize(); const c = fixture.componentInstance; c.simulate();
    http.expectOne('/api/portfolios/1/movements/correction-simulation').flush({ canApply: false, blockers: ['Quantidade insuficiente.'] });
    fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain('Quantidade insuficiente');
    expect(fixture.nativeElement.textContent).not.toContain('Confirmar correção');
  });
});
