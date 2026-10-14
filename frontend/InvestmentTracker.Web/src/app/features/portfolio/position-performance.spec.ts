import { TestBed } from '@angular/core/testing';
import { PositionPerformance } from './position-performance';
import { ValuePrivacy } from '../../core/value-privacy';
import { brazilianLocaleProvider } from '../../core/locale';

describe('PositionPerformance', () => {
  beforeEach(() => TestBed.configureTestingModule({ imports: [PositionPerformance], providers: [brazilianLocaleProvider] }));
  function create(variation: string | null) {
    const fixture = TestBed.createComponent(PositionPerformance);
    TestBed.inject(ValuePrivacy).hidden.set(false);
    fixture.componentRef.setInput('currency', 'USD');
    fixture.componentRef.setInput('performance', { averagePrice: '20', unitPrice: '23', variation });
    fixture.detectChanges(); return fixture;
  }
  it('shows the direction with accessible text rather than color alone', () => {
    const fixture = create('0.15');
    expect(fixture.nativeElement.querySelector('.gain').textContent).toContain('▲');
    expect(fixture.nativeElement.textContent).toContain('15,00%');
    expect(fixture.nativeElement.textContent).toContain('Valorização');
    fixture.componentRef.setInput('performance', { averagePrice: '20', unitPrice: '18', variation: '-0.1' });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.loss').textContent).toContain('▼');
    expect(fixture.nativeElement.textContent).toContain('-10,00%');
  });
  it('distinguishes equality from missing cost basis', () => {
    const fixture = create('0');
    expect(fixture.nativeElement.textContent).toContain('Sem variação');
    expect(fixture.nativeElement.querySelector('.gain, .loss')).toBeNull();
    fixture.componentRef.setInput('performance', { averagePrice: null, unitPrice: '23', variation: null });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Sem base de cálculo');
    expect(fixture.nativeElement.querySelector('.indicator')).toBeNull();
  });
  it('hides prices but keeps percentages visible', () => {
    const fixture = create('0.15');
    TestBed.inject(ValuePrivacy).hidden.set(true);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('20,00');
    expect(fixture.nativeElement.textContent).not.toContain('23,00');
    expect(fixture.nativeElement.textContent).toContain('••••••');
    expect(fixture.nativeElement.textContent).toContain('15,00%');
  });
});
