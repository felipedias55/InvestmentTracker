import { TestBed } from '@angular/core/testing';
import { brazilianLocaleProvider } from '../../core/locale';
import { ValuePrivacy } from '../../core/value-privacy';
import { RebalancingBandReport } from './rebalancing-band-report';
import { RebalancingBandStatus } from './allocation.service';
const band: RebalancingBandStatus = { dimension: 'category', groupId: 1, name: 'Ações', state: 'below', hasOverride: true, tolerancePoints: '3', currentPercentage: '47', targetPercentage: '50', lowerPercentage: '47', upperPercentage: '53', requestedRemainder: '100', suggestedRemainder: '40', reservedContribution: '25', reason: 'O servidor usa o desvio sem arredondamento.' };
describe('RebalancingBandReport', () => {
  beforeEach(() => { TestBed.configureTestingModule({ imports: [RebalancingBandReport], providers: [brazilianLocaleProvider] }); TestBed.inject(ValuePrivacy).hidden.set(false); });
  function create(rows = [band]) { const f = TestBed.createComponent(RebalancingBandReport); f.componentRef.setInput('currency', 'BRL'); f.componentRef.setInput('rows', rows); f.detectChanges(); return f; }
  it('renders server decisions at rounded boundaries without recategorizing them in the browser', () => {
    const f = create(); const text = f.nativeElement.textContent.replace(/\s/g, ' ');
    expect(text).toContain('Abaixo da faixa'); expect(text).toContain('Faixa: 47% a 53%'); expect(text).toContain('BRL 100,00 → Após limites: BRL 40,00'); expect(text).toContain('Reservas no plano final: BRL 25,00');
  });
  it('filters and paginates diagnostics while preserving explicit zero and source decisions', () => {
    const f = create(Array.from({ length: 12 }, (_, i) => ({ ...band, dimension: i === 11 ? 'sector' : 'category', groupId: i + 1, name: i === 11 ? 'Saúde' : 'Grupo ' + i, tolerancePoints: i === 11 ? '0' : '3' }))), c = f.componentInstance;
    c.page.set(1); f.detectChanges(); expect(c.pageRows()).toHaveLength(2);
    c.dimension.set('sector'); c.query.set('saude'); f.detectChanges(); expect(c.pageRows()).toHaveLength(1); expect(f.nativeElement.textContent).toContain('0 p.p. (específica)');
    f.componentRef.setInput('rows', []); f.detectChanges(); expect(c.query()).toBe(''); expect(c.currentPage()).toBe(0);
  });
  it('uses the shared privacy setting for all monetary diagnostics', () => {
    const f = create(); TestBed.inject(ValuePrivacy).hidden.set(true); f.detectChanges();
    expect(f.nativeElement.textContent).not.toContain('100,00'); expect(f.nativeElement.textContent).not.toContain('40,00'); expect(f.nativeElement.textContent).not.toContain('25,00'); expect(f.nativeElement.textContent).toContain('••••••');
  });
});
