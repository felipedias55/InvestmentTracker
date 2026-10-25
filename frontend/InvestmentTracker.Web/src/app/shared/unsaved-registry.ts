import { Injectable } from '@angular/core';
export interface Draft { element: HTMLElement; changed(): boolean; busy(): boolean; accept(): void; }

@Injectable({ providedIn: 'root' })
export class UnsavedChanges {
  readonly drafts = new Set<Draft>();
  confirmWithin(element?: HTMLElement): boolean {
    const drafts = [...this.drafts].filter(d => !element || element.contains(d.element));
    if (drafts.some(d => d.busy())) return false;
    const changed = drafts.filter(d => d.changed());
    if (!changed.length) return true;
    if (!window.confirm('Há alterações não salvas. Deseja descartá-las e sair?')) return false;
    changed.forEach(d => d.accept());
    return true;
  }
}


