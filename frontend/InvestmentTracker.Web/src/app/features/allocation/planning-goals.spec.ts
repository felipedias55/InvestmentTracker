import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { brazilianLocaleProvider } from '../../core/locale';
import { ValuePrivacy } from '../../core/value-privacy';
import { PlanningGoals } from './planning-goals';
import { PlanningGoalPage } from './planning-goals.service';

const goal = { goalId: '938313c0-d69b-4a74-9bf8-a8bdd90c1ca8', revision: 2, createdAtUtc: '2026-11-03T15:00:00Z', config: { name: 'Reserva de longo prazo', kind: 'wealth', currency: 'BRL', target: '10000', deadline: '2027-01-31', monthlyContribution: '500', archived: false } };
const page: PlanningGoalPage = { items: [{ goal, status: 'in-progress', reason: 'Cenário sem rentabilidade.', current: '1000', gap: '9000', percentage: '10', monthsRemaining: 3, requiredMonthlyContribution: '3000', scenarioValue: '2500', scenarioGap: '7500' }],
  total: 1, pageIndex: 0, pageSize: 10, currency: 'BRL', today: '2026-11-03', incomeFrom: '2025-11-01', incomeTo: '2026-10-31', missingIncomeConversions: 0, hasStaleRates: false, hasFallbackRates: false };
const path = '/api/portfolios/1/planning-goals';
describe('PlanningGoals', () => {
  let http: HttpTestingController;
  beforeEach(async () => { await TestBed.configureTestingModule({ imports: [PlanningGoals], providers: [brazilianLocaleProvider, provideRouter([]), provideHttpClient(), provideHttpClientTesting()] }).compileComponents(); http = TestBed.inject(HttpTestingController); TestBed.inject(ValuePrivacy).hidden.set(false); });
  afterEach(() => http.verify());
  function initialize() { const f = TestBed.createComponent(PlanningGoals); f.componentRef.setInput('portfolioId', 1); f.detectChanges(); return f; }
  function open(f: ReturnType<typeof initialize>, data = page) { f.componentInstance.show(); http.expectOne(r => r.url === path).flush(data); f.detectChanges(); }
  it('loads on demand and displays authoritative scenarios and the exact income window', () => {
    const f = initialize(); http.expectNone(path); open(f);
    const text = f.nativeElement.textContent.replace(/\s/g, ' ');
    expect(text).toContain('Aporte mensal necessário sem rentabilidade: BRL 3.000,00'); expect(text).toContain('BRL 2.500,00 no prazo'); expect(text).toContain('01/11/2025 a 31/10/2026');
  });
  it('preserves revision and sends Brazilian inputs as exact decimal strings', () => {
    const f = initialize(), c = f.componentInstance; open(f); c.start(goal);
    c.form.controls.target.setValue('999.999.999.999.999,99'); c.form.controls.monthlyContribution.setValue('1.234,56'); c.save();
    const request = http.expectOne(path); expect(request.request.body).toMatchObject({ goalId: goal.goalId, expectedRevision: 2, config: { target: '999999999999999.99', monthlyContribution: '1234.56' } });
    request.flush({ detail: 'O objetivo mudou. Recarregue.' }, { status: 409, statusText: 'Conflict' });
    expect(c.editing()).toBe(true); expect(c.form.controls.target.value).toBe('999.999.999.999.999,99'); expect(c.error()).toContain('Recarregue');
  });
  it('does not carry a capital contribution assumption into an income goal', () => {
    const f = initialize(), c = f.componentInstance; open(f); c.start(goal); c.form.controls.kind.setValue('monthly-income'); c.changeKind(); c.save();
    const request = http.expectOne(path); expect(request.request.body.config.monthlyContribution).toBe('0'); request.flush({ ...goal, revision: 3 });
    http.expectOne(r => r.url === path).flush(page); expect(c.editing()).toBe(false);
  });
  it('archives reversibly through a new revision and preserves old currency data', () => {
    const f = initialize(), c = f.componentInstance; open(f, { ...page, currency: 'USD', items: [{ ...page.items[0], status: 'currency-changed', current: null }] });
    c.archive(goal); const request = http.expectOne(path); expect(request.request.body).toEqual({ goalId: goal.goalId, expectedRevision: 2, config: { ...goal.config, archived: true } });
    request.flush({ ...goal, revision: 3, config: { ...goal.config, archived: true } }); http.expectOne(r => r.url === path).flush({ ...page, items: [], total: 0 });
    c.revisions(goal); http.expectOne(path + '/' + goal.goalId + '/revisions').flush([goal]); f.detectChanges(); expect(c.history()?.[0].config.currency).toBe('BRL');
  });
  it('hides monetary values in goals and scenarios using the shared privacy setting', () => {
    const f = initialize(); open(f); TestBed.inject(ValuePrivacy).hidden.set(true); f.detectChanges();
    expect(f.nativeElement.textContent).not.toContain('10.000,00'); expect(f.nativeElement.textContent).not.toContain('3.000,00'); expect(f.nativeElement.textContent).toContain('••••••');
  });
  it('filters on the server and cancels requests on portfolio change', () => {
    const f = initialize(), c = f.componentInstance; open(f); c.search.set('renda'); c.kind.set('monthly-income'); c.status.set('archived'); c.load(2, 25);
    const request = http.expectOne(r => r.url === path); expect(request.request.params.get('pageIndex')).toBe('2'); expect(request.request.params.get('kind')).toBe('monthly-income'); expect(request.request.params.get('status')).toBe('archived');
    f.componentRef.setInput('portfolioId', 2); f.detectChanges(); expect(request.cancelled).toBe(true); expect(c.page()).toBeNull(); expect(c.open()).toBe(false);
  });
  it('keeps missing historical income distinct from zero progress', () => {
    const f = initialize(); open(f, { ...page, missingIncomeConversions: 1, items: [{ ...page.items[0], goal: { ...goal, config: { ...goal.config, kind: 'monthly-income' } }, status: 'unavailable', current: null, percentage: null, requiredMonthlyContribution: null, scenarioValue: null }] });
    expect(f.nativeElement.textContent).toContain('Base indisponível'); expect(f.nativeElement.textContent).toContain('sem equivalente histórico'); expect(f.nativeElement.textContent).not.toContain('Referência atual:'); expect(f.nativeElement.querySelector('progress')).toBeNull();
  });
});
