import { Directive, ElementRef, HostListener, Input, OnChanges, OnDestroy, OnInit, inject } from '@angular/core';
import { Draft, UnsavedChanges } from './unsaved-registry';

@Directive({ selector: '[appDraftState]', standalone: true, exportAs: 'draftState' })
export class DraftState implements OnInit, OnChanges, OnDestroy {
  @Input() appDraftState: unknown;
  @Input() draftBusy = false;
  @Input() draftSaved: unknown;
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly registry = inject(UnsavedChanges);
  private baseline = '';
  private touched = false;
  private saved: unknown;
  private readonly draft: Draft = { element: this.element,
    changed: () => this.touched && this.baseline !== JSON.stringify(this.appDraftState),
    busy: () => this.draftBusy,
    accept: () => { this.baseline = JSON.stringify(this.appDraftState); this.touched = false; },
  };
  ngOnChanges() {
    if (!this.touched || (this.draftSaved && this.saved !== this.draftSaved)) this.draft.accept();
    this.saved = this.draftSaved;
  }
  ngOnInit() { this.registry.drafts.add(this.draft); }
  @HostListener('input') @HostListener('change') change() { this.touched = true; }
  cancel() {
    if (this.registry.confirmWithin(this.element)) this.element.closest('dialog')?.dispatchEvent(new Event('cancel', { cancelable: true }));
  }
  @HostListener('window:beforeunload', ['$event']) unload(event: BeforeUnloadEvent) {
    if (this.draft.changed() || this.draftBusy) { event.preventDefault(); event.returnValue = ''; }
  }
  ngOnDestroy() { this.registry.drafts.delete(this.draft); }
}
