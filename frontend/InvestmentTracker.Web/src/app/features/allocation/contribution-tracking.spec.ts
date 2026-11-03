import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { brazilianLocaleProvider } from '../../core/locale';
import { ContributionTracking } from './contribution-tracking';
import { TrackingDetail } from './contribution-tracking.service';

const summary = { id: 7, name: 'Novembro', from: '2026-11-01', to: '2026-11-30', createdAtUtc: '2026-11-03T12:00:00Z' };
const row = { key: 'rule-1-2', categoryId: 1, sectorId: 2, category: 'Ações', sector: 'Tecnologia', amount: '100', stage: 'reservation', currencies: [{ currency: 'USD', amount: '20', rate: '5', dates: '', stale: false }] };
const detail: TrackingDetail = { plan: summary, snapshot: { currency: 'BRL', simulation: { amount: '120' }, plan: { rows: [row], unallocated: '20' }, notes: [] },
  rows: [{ planned: row, realized: '50', difference: '50', status: 'partial' }], purchases: [], realized: '50', difference: '50', status: 'partial' };
const candidate = { tradeId: 9, date: '2026-11-03', ticker: 'TESTUS', currency: 'USD', categoryId: 1, sectorId: 2, amount: '10', baseAmount: null, active: true };
const base = '/api/portfolios/1/contribution-plans';
describe('ContributionTracking', () => {
  let http: HttpTestingController;
  beforeEach(async () => { await TestBed.configureTestingModule({ imports: [ContributionTracking], providers: [brazilianLocaleProvider, provideRouter([]), provideHttpClient(), provideHttpClientTesting()] }).compileComponents(); http = TestBed.inject(HttpTestingController); });
  afterEach(() => http.verify());
  function initialize() { const f = TestBed.createComponent(ContributionTracking); f.componentRef.setInput('portfolioId', 1); f.detectChanges(); return f; }
  it('presents the band decisions preserved in a saved plan independently of the current simulation', () => {
    const f = initialize(), c = f.componentInstance; c.open.set(true);
    c.detail.set({ ...detail, snapshot: { ...detail.snapshot, bands: [{ dimension: 'sector', groupId: 2, name: 'Tecnologia antiga', state: 'below', hasOverride: true, tolerancePoints: '0', currentPercentage: '40', targetPercentage: '50', lowerPercentage: '50', upperPercentage: '50', requestedRemainder: '100', suggestedRemainder: '100', reservedContribution: '0', reason: 'Regra preservada.' }] } });
    f.detectChanges(); expect(f.nativeElement.querySelector('.band-result').textContent).toContain('Tecnologia antiga'); expect(f.nativeElement.querySelector('.band-result').textContent).toContain('0 p.p. (específica)');
    f.componentRef.setInput('simulation', { simulation: { amount: '999' }, expectedToken: 'different' }); f.detectChanges(); expect(c.detail()?.snapshot.bands?.[0].name).toBe('Tecnologia antiga');
  });
  function read(f: ReturnType<typeof initialize>) { f.componentInstance.read(7); http.expectOne(base + '/7').flush(detail); http.expectOne(base + '/7/purchases?pageIndex=0&pageSize=10').flush({ items: [candidate], total: 1, pageIndex: 0, pageSize: 10 }); f.detectChanges(); }
  it('loads only on demand and presents server totals without recomputing money', () => {
    const f = initialize(); http.expectNone(base); f.componentInstance.show();
    http.expectOne(base + '?pageIndex=0&pageSize=10').flush({ items: [summary], total: 1, pageIndex: 0, pageSize: 10 }); read(f);
    expect(f.nativeElement.textContent.replace(/\s/g, ' ')).toContain('Realizado: BRL 50,00'); expect(f.nativeElement.textContent).toContain('Parcial');
    expect(f.nativeElement.textContent.replace(/\s/g, ' ')).toContain('Não distribuído: BRL 20,00');
  });
  it('saves the exact simulation token and reuses the request ID after an uncertain response', () => {
    const f = initialize(), c = f.componentInstance; const simulation = { simulation: { amount: '120' }, expectedToken: 'original-token' };
    f.componentRef.setInput('simulation', simulation); f.detectChanges(); c.startSave(); c.saveForm.controls.name.setValue('Novembro'); c.save();
    const first = http.expectOne(base), body = first.request.body;
    expect(body.simulation).toEqual(simulation.simulation); expect(body.expectedToken).toBe('original-token');
    first.flush({ detail: 'Tente novamente' }, { status: 503, statusText: 'Unavailable' }); c.save();
    const retry = http.expectOne(base); expect(retry.request.body.requestId).toBe(body.requestId); retry.flush(detail);
    http.expectOne(base + '?pageIndex=0&pageSize=10').flush({ items: [summary], total: 1, pageIndex: 0, pageSize: 10 });
    http.expectOne(base + '/7/purchases?pageIndex=0&pageSize=10').flush({ items: [], total: 0, pageIndex: 0, pageSize: 10 });
    expect(c.saveOpen()).toBe(false); expect(c.detail()?.plan.id).toBe(7);
  });
  it('requires manual historical conversion only when missing and sends decimal strings', () => {
    const f = initialize(), c = f.componentInstance; read(f); c.beginLink(candidate); c.link(); http.expectNone(base + '/7/purchases');
    c.linkForm.controls.baseAmount.setValue('50,1234'); c.link();
    const request = http.expectOne(base + '/7/purchases'); expect(request.request.body).toEqual({ tradeId: 9, rowKey: row.key, baseAmount: '50.1234' });
    request.flush({ detail: 'Compra já vinculada' }, { status: 409, statusText: 'Conflict' }); expect(c.linking()?.tradeId).toBe(9); expect(c.error()).toContain('Compra já vinculada');
    c.beginLink({ ...candidate, baseAmount: '50' }); c.link();
    const historical = http.expectOne(base + '/7/purchases'); expect(historical.request.body.baseAmount).toBeNull(); historical.flush(null);
    http.expectOne(base + '/7').flush(detail); http.expectOne(base + '/7/purchases?pageIndex=0&pageSize=10').flush({ items: [], total: 0, pageIndex: 0, pageSize: 10 });
  });
  it('cancels previous portfolio reads and clears tracking state', () => {
    const f = initialize(), c = f.componentInstance; c.show(); const pending = http.expectOne(base + '?pageIndex=0&pageSize=10');
    f.componentRef.setInput('portfolioId', 2); f.detectChanges(); expect(pending.cancelled).toBe(true); expect(c.open()).toBe(false); expect(c.detail()).toBeNull(); expect(c.loading()).toBe(false);
  });
  it('unlinks only the association and reloads authoritative totals', () => {
    const f = initialize(), c = f.componentInstance; read(f); c.unlink(3);
    const request = http.expectOne(base + '/7/purchases/3'); expect(request.request.method).toBe('DELETE'); request.flush(null);
    http.expectOne(base + '/7').flush({ ...detail, realized: '0', status: 'pending' });
    http.expectOne(base + '/7/purchases?pageIndex=0&pageSize=10').flush({ items: [candidate], total: 1, pageIndex: 0, pageSize: 10 });
    expect(c.detail()?.realized).toBe('0'); http.expectNone(r => r.url.endsWith('/trades') || r.url.endsWith('/movements'));
  });
});
