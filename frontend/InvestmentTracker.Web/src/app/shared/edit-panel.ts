import { AfterViewInit, Component, ElementRef, EventEmitter, Input, OnChanges, OnDestroy, Output, ViewChild, inject } from '@angular/core';
import { UnsavedChanges } from './unsaved-registry';
import { MatButtonModule } from '@angular/material/button';
import { AppIcon } from './app-icon';

@Component({
  standalone: true,
  imports: [MatButtonModule, AppIcon],
  selector: 'app-edit-panel',
  template: `
    <dialog #surface open [class.editing]="editing" [class.wide]="wide" [attr.aria-label]="title" (cancel)="cancel($event)">
      @if (editing) {
        <div class="dialog-toolbar"><strong>{{ title }}</strong><button mat-icon-button type="button" [disabled]="busy" (click)="cancel($event)" aria-label="Fechar edição"><app-icon name="close" /></button></div>
        @if (error) { <p class="notice error" role="alert">{{ error }}</p> }
      }
      <ng-content />
    </dialog>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    dialog { position: static; display: block; width: 100%; max-width: none; max-height: none; margin: 0; padding: 0; border: 0; color: inherit; background: transparent; overflow: visible; }
    dialog.editing { position: fixed; inset: 0; width: min(620px, calc(100vw - 24px)); max-height: calc(100dvh - 24px); margin: auto; background: var(--surface); border-radius: 14px; overflow: auto; box-shadow: 0 20px 80px #171a2055; }
    dialog.editing.wide { width: min(1100px, calc(100vw - 24px)); }
    dialog::backdrop { background: #171a2099; }
    .dialog-toolbar { position: sticky; top: 0; display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 12px 18px; background: var(--surface); border-bottom: 1px solid var(--border); z-index: 1; }
    .notice { margin: 12px; }
  `],
})
export class EditPanel implements OnChanges, AfterViewInit, OnDestroy {
  private readonly drafts = inject(UnsavedChanges);
  @Input() editing = false;
  @Input() wide = false;
  @Input() busy = false;
  @Input() error = '';
  @Input() title = 'Editar registro';
  @Output() closed = new EventEmitter<void>();
  @ViewChild('surface') surface?: ElementRef<HTMLDialogElement>;
  private modal = false;
  private opener: HTMLElement | null = null;
  private scrollPosition = { x: 0, y: 0 };
  ngAfterViewInit() { this.sync(); }
  ngOnChanges() {
    this.sync();
    if (this.error) queueMicrotask(() => {
      const message = this.surface?.nativeElement.querySelector<HTMLElement>('[role="alert"]');
      if (message) { message.tabIndex = -1; message.focus(); }
    });
  }
  ngOnDestroy() {
    if (!this.modal) return;
    this.surface?.nativeElement.close();
    if (this.opener?.isConnected) this.opener.focus({ preventScroll: true });
    else document.querySelector<HTMLElement>('main')?.focus({ preventScroll: true });
  }
  private sync() {
    const dialog = this.surface?.nativeElement;
    if (!dialog || this.modal === this.editing) return;
    if (this.editing) {
      this.opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
      this.scrollPosition = { x: window.scrollX, y: window.scrollY };
    }
    dialog.close();
    if (this.editing) dialog.showModal();
    else {
      dialog.setAttribute('open', '');
      if (this.opener?.isConnected) this.opener.focus({ preventScroll: true });
      window.scrollTo({ left: this.scrollPosition.x, top: this.scrollPosition.y, behavior: 'instant' });
    }
    this.modal = this.editing;
  }
  cancel(event: Event) {
    event.preventDefault();
    if (!this.busy && this.drafts.confirmWithin(this.surface?.nativeElement)) this.closed.emit();
  }
}
