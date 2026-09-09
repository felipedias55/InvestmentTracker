import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { EditPanel } from './edit-panel';
@Component({ standalone: true, imports: [EditPanel], template: `<app-edit-panel [editing]="editing()" [busy]="busy()" [error]="error()" (closed)="editing.set(false)"><input value="Preservado" /></app-edit-panel>` })
class Host { editing = signal(false); busy = signal(false); error = signal(''); }
describe('EditPanel', () => {
  it('opens the same editor as a modal and closes without discarding input', async () => {
    await TestBed.configureTestingModule({ imports: [Host] }).compileComponents();
    const fixture = TestBed.createComponent(Host); fixture.detectChanges();
    const input = fixture.nativeElement.querySelector('input');
    fixture.componentInstance.editing.set(true); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('dialog').dataset.modal).toBe('true');
    expect(fixture.nativeElement.querySelector('input')).toBe(input);
    fixture.nativeElement.querySelector('button').click(); fixture.detectChanges();
    expect(fixture.componentInstance.editing()).toBe(false);
    expect(input.value).toBe('Preservado');
  });
  it('keeps the editor open during saving and displays API errors inside it', async () => {
    await TestBed.configureTestingModule({ imports: [Host] }).compileComponents();
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.editing.set(true); fixture.componentInstance.busy.set(true);
    fixture.componentInstance.error.set('Cadastro em uso'); fixture.detectChanges();
    fixture.nativeElement.querySelector('dialog').dispatchEvent(new Event('cancel', { cancelable: true }));
    expect(fixture.componentInstance.editing()).toBe(true);
    expect(fixture.nativeElement.querySelector('dialog [role="alert"]').textContent).toContain('Cadastro em uso');
    fixture.componentInstance.busy.set(false); fixture.detectChanges();
    fixture.nativeElement.querySelector('dialog').dispatchEvent(new Event('cancel', { cancelable: true }));
    expect(fixture.componentInstance.editing()).toBe(false);
  });
});
