import { Directive, ElementRef, HostListener, Input, OnDestroy, OnInit, inject } from '@angular/core';
import { FormGroupDirective } from '@angular/forms';
import { Subscription } from 'rxjs';
import { UnsavedChanges, Draft } from './unsaved-registry';

/** Keeps drafts only in memory. Financial submission and retries remain with each feature. */
@Directive({ selector: 'form[appUnsavedChanges]', standalone: true, exportAs: 'unsavedForm' })
export class UnsavedForm implements OnInit, OnDestroy {
  private readonly group = inject(FormGroupDirective);
  private readonly element = inject<ElementRef<HTMLFormElement>>(ElementRef).nativeElement;
  private readonly registry = inject(UnsavedChanges);
  @Input() appFormBusy = false;
  private baseline = '';
  cancel() { this.element.closest('dialog')?.dispatchEvent(new Event('cancel', { cancelable: true })); }
  private subscription?: Subscription;
  private readonly draft: Draft = {
    element: this.element, changed: () => this.group.form.dirty && this.snapshot() !== this.baseline,
    busy: () => this.appFormBusy, accept: () => { this.baseline = this.snapshot(); },
  };
  private snapshot(): string {
    const decimals = new Set([...this.element.querySelectorAll<HTMLElement>('[inputmode="decimal"][formControlName]')].map(e => e.getAttribute('formControlName')));
    return JSON.stringify(this.group.form.getRawValue(), (key, value) => {
      if (!decimals.has(key) || typeof value !== 'string' || !/^-?\d+(?:\.\d{3})*(?:,\d+)?$/.test(value)) return value;
      const [whole, fraction = ''] = value.replaceAll('.', '').split(',');
      return whole.replace(/^(-?)0+(?=\d)/, '$1') + (fraction.replace(/0+$/, '') ? '.' + fraction.replace(/0+$/, '') : '');
    });
  }
  ngOnInit() {
    this.baseline = this.snapshot();
    this.registry.drafts.add(this.draft);
    this.subscription = this.group.form.events.subscribe(() => {
      if (this.group.form.pristine) this.baseline = this.snapshot();
    });
    this.element.addEventListener('click', this.cancelClick, true);
  }
  private readonly cancelClick = (event: Event) => {
    if (event.target instanceof Element && event.target.closest('[data-discard]') && !this.registry.confirmWithin(this.element)) {
      event.preventDefault(); event.stopImmediatePropagation();
    }
  };
  @HostListener('window:beforeunload', ['$event']) beforeUnload(event: BeforeUnloadEvent) {
    if (this.draft.changed() || this.appFormBusy) { event.preventDefault(); event.returnValue = ''; }
  }
  ngOnDestroy() {
    this.subscription?.unsubscribe(); this.registry.drafts.delete(this.draft);
    this.element.removeEventListener('click', this.cancelClick, true);
  }
}
