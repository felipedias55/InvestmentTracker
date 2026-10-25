import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { UnsavedForm } from './unsaved-changes';
import { UnsavedChanges } from './unsaved-registry';

@Component({ standalone: true, imports: [ReactiveFormsModule, UnsavedForm], template: `<form appUnsavedChanges [appFormBusy]="busy" [formGroup]="form"><input formControlName="amount" inputmode="decimal"><button data-discard type="button" (click)="closed = true">Cancelar</button></form>` })
class Host {
  busy = false; closed = false;
  form = new FormGroup({ amount: new FormControl('1.234,50', { nonNullable: true }) });
}
describe('Draft protection', () => {
  async function setup() {
    await TestBed.configureTestingModule({ imports: [Host] }).compileComponents();
    const fixture = TestBed.createComponent(Host); fixture.detectChanges();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input');
    const enter = (value: string) => { input.value = value; input.dispatchEvent(new Event('input')); fixture.detectChanges(); };
    return { fixture, enter, registry: TestBed.inject(UnsavedChanges) };
  }
  it('allows untouched, equivalent monetary formatting and reverted values without confirmation', async () => {
    const { enter, registry } = await setup();
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    expect(registry.confirmWithin()).toBe(true);
    enter('1234,5000'); expect(registry.confirmWithin()).toBe(true);
    enter('2.000,00'); enter('1.234,50'); expect(registry.confirmWithin()).toBe(true);
    expect(confirm).not.toHaveBeenCalled(); confirm.mockRestore();
  });
  it('preserves the draft when discard is rejected, and accepts only after confirmation', async () => {
    const { fixture, enter, registry } = await setup();
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    enter('2.000,00'); fixture.nativeElement.querySelector('button').click();
    expect(fixture.componentInstance.closed).toBe(false);
    expect(fixture.componentInstance.form.controls.amount.value).toBe('2.000,00');
    confirm.mockReturnValue(true); expect(registry.confirmWithin()).toBe(true);
    confirm.mockReturnValue(false); expect(registry.confirmWithin()).toBe(true);
    confirm.mockRestore();
  });
  it('blocks leaving during submission, keeps errors retryable and releases saved forms', async () => {
    const { fixture, enter, registry } = await setup();
    enter('2.000,00'); fixture.componentInstance.busy = true; fixture.detectChanges();
    expect(registry.confirmWithin()).toBe(false);
    fixture.componentInstance.busy = false; fixture.detectChanges();
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    expect(registry.confirmWithin()).toBe(false);
    fixture.componentInstance.form.markAsPristine();
    expect(registry.confirmWithin()).toBe(true);
    fixture.destroy(); expect(registry.drafts.size).toBe(0); confirm.mockRestore();
  });
});
