import { TestBed } from '@angular/core/testing';
import { MoneyChart } from './money-chart';
import { ValuePrivacy } from '../core/value-privacy';
import { AllocationDonut } from '../features/allocation/allocation-donut';

describe('Financial charts', () => {
  afterEach(() => localStorage.removeItem('investment-tracker.hide-values'));
  it('breaks lines at missing values and gives negative columns a real zero baseline', () => {
    const fixture = TestBed.createComponent(MoneyChart);
    fixture.componentRef.setInput('points', [{ label: 'Jan', value: 100 }, { label: 'Fev', value: null }, { label: 'Mar', value: -20 }]);
    fixture.componentRef.setInput('currency', 'BRL'); fixture.componentRef.setInput('title', 'Teste');
    fixture.componentRef.setInput('kind', 'line'); fixture.detectChanges();
    expect(fixture.componentInstance.paths()).toHaveLength(2);
    expect(fixture.componentInstance.y(-20)).toBeGreaterThan(fixture.componentInstance.y(0));
    expect(fixture.nativeElement.textContent).toContain('Indisponível');
  });
  it('computes waterfall endpoints without adding the final total again', () => {
    const fixture = TestBed.createComponent(MoneyChart);
    fixture.componentRef.setInput('points', [{ label: 'Inicial', value: 100, total: true }, { label: 'Saída', value: -20 }, { label: 'Final', value: 80, total: true }]);
    fixture.componentRef.setInput('kind', 'waterfall');
    expect(fixture.componentInstance.levels()).toEqual([{ start: 0, end: 100 }, { start: 100, end: 80 }, { start: 0, end: 80 }]);
  });
  it('masks currency values in chart labels, tooltips and tables', () => {
    localStorage.removeItem('investment-tracker.hide-values');
    const fixture = TestBed.createComponent(MoneyChart);
    fixture.componentRef.setInput('points', [{ label: 'Jan', value: 1234 }]);
    fixture.componentRef.setInput('currency', 'BRL'); fixture.componentRef.setInput('title', 'Teste');
    TestBed.inject(ValuePrivacy).hidden.set(true); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('1,234');
    expect(fixture.nativeElement.querySelector('title').textContent).toContain('••••••');
  });
  it('preserves whole-portfolio shares in filtered donuts and rejects unknown percentages', () => {
    const fixture = TestBed.createComponent(AllocationDonut);
    const row = { groupId: 1, name: 'Ações', currentValue: '100', currentPercentage: '0.25', targetPercentage: null, difference: null };
    fixture.componentRef.setInput('rows', [row]); fixture.componentRef.setInput('currency', 'BRL'); fixture.detectChanges();
    expect(fixture.componentInstance.slices()[0].size).toBe(25);
    expect(fixture.nativeElement.textContent).toContain('área cinza');
    fixture.componentRef.setInput('rows', [{ ...row, currentPercentage: null }]); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('svg')).toBeNull();
  });
});
