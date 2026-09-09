import { AfterViewInit, Component, ElementRef, EventEmitter, Input, OnChanges, Output, ViewChild } from '@angular/core';

@Component({
  standalone: true,
  selector: 'app-edit-panel',
  template: `
    <dialog #surface open [class.editing]="editing" [attr.aria-label]="title" (cancel)="cancel($event)">
      @if (editing) {
        <div class="dialog-toolbar"><strong>{{ title }}</strong><button type="button" [disabled]="busy" (click)="cancel($event)" aria-label="Fechar edição">Fechar ×</button></div>
        @if (error) { <p class="notice error" role="alert">{{ error }}</p> }
      }
      <ng-content />
    </dialog>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    dialog { position: static; display: block; width: 100%; max-width: none; max-height: none; margin: 0; padding: 0; border: 0; color: inherit; background: transparent; overflow: visible; }
    dialog.editing { position: fixed; inset: 0; width: min(620px, calc(100vw - 24px)); max-height: calc(100dvh - 24px); margin: auto; background: white; border-radius: 14px; overflow: auto; box-shadow: 0 20px 80px #20151c55; }
    dialog::backdrop { background: #20151c99; }
    .dialog-toolbar { position: sticky; top: 0; display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 12px 18px; background: white; border-bottom: 1px solid var(--border); z-index: 1; }
    .notice { margin: 12px; }
  `],
})
export class EditPanel implements OnChanges, AfterViewInit {
  @Input() editing = false;
  @Input() busy = false;
  @Input() error = '';
  @Input() title = 'Editar registro';
  @Output() closed = new EventEmitter<void>();
  @ViewChild('surface') surface?: ElementRef<HTMLDialogElement>;
  private modal = false;
  ngAfterViewInit() { this.sync(); }
  ngOnChanges() { this.sync(); }
  private sync() {
    const dialog = this.surface?.nativeElement;
    if (!dialog || this.modal === this.editing) return;
    dialog.close();
    if (this.editing) dialog.showModal();
    else dialog.setAttribute('open', '');
    this.modal = this.editing;
  }
  cancel(event: Event) {
    event.preventDefault();
    if (!this.busy) this.closed.emit();
  }
}
