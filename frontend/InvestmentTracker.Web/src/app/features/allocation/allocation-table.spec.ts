import { TestBed } from '@angular/core/testing';
import { AllocationTable } from './allocation-table';
import { AllocationDonut } from './allocation-donut';
import { brazilianLocaleProvider } from '../../core/locale';
import { ValuePrivacy } from '../../core/value-privacy';

describe('Compact dashboard distributions', () => {
  const rows = Array.from({ length: 8 }, (_, i) => ({ groupId: i + 1, name: `Grupo ${i + 1}`, currentValue: '1234.5678', currentPercentage: '0.1', targetPercentage: i === 7 ? null : '0.125', difference: i === 7 ? null : '0.025' }));
  beforeEach(() => TestBed.configureTestingModule({ providers: [brazilianLocaleProvider] }));
  it('limits only the summary and retains every filtered row, target and value in private details', () => {
    const fixture = TestBed.createComponent(AllocationTable);
    fixture.componentRef.setInput('rows', rows); fixture.componentRef.setInput('currency', 'BRL');
    fixture.componentRef.setInput('title', 'Setores'); fixture.componentRef.setInput('visual', true);
    fixture.componentRef.setInput('chartType', 'bars'); fixture.componentRef.setInput('compact', true);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('.allocation-bar')).toHaveLength(5);
    expect(fixture.nativeElement.querySelector('table')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('5 de 8 grupos');
    TestBed.inject(ValuePrivacy).hidden.set(true);
    fixture.componentInstance.detailsOpen.set(true); fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('tbody tr')).toHaveLength(8);
    const text = fixture.nativeElement.querySelector('table').textContent;
    expect(text).toContain('Grupo 8'); expect(text).toContain('Não configurada');
    expect(text).toContain('2,5'); expect(text).toContain('••••••'); expect(text).not.toContain('1.234');
    fixture.componentInstance.detailsOpen.set(false); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('table')).toBeNull();
    fixture.componentRef.setInput('compact', false); fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('.allocation-bar')).toHaveLength(8);
    expect(fixture.nativeElement.querySelector('details')).not.toBeNull();
  });
  it('keeps all donut slices and the whole-portfolio denominator with a shorter, non-monetary legend', () => {
    const fixture = TestBed.createComponent(AllocationDonut);
    fixture.componentRef.setInput('rows', rows); fixture.componentRef.setInput('currency', 'BRL');
    fixture.componentRef.setInput('compact', true); fixture.detectChanges();
    expect(fixture.componentInstance.slices()).toHaveLength(8);
    expect(fixture.nativeElement.querySelectorAll('li')).toHaveLength(5);
    expect(fixture.componentInstance.share()).toBeCloseTo(0.8);
    expect(fixture.nativeElement.querySelector('small')).toBeNull();
    fixture.componentRef.setInput('rows', [rows[0]]); fixture.detectChanges();
    expect(fixture.componentInstance.slices()[0].size).toBe(10);
    expect(fixture.nativeElement.textContent).toContain('área cinza');
    fixture.componentRef.setInput('rows', [...rows.slice(0, 7), { ...rows[7], currentPercentage: null }]); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('svg')).toBeNull();
  });
});
