import { CurrencyPipe } from '@angular/common';
import { Injectable, LOCALE_ID, Pipe, PipeTransform, inject, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ValuePrivacy {
  readonly hidden = signal(this.read());
  private read() {
    try { return localStorage.getItem('investment-tracker.hide-values') === 'true'; }
    catch { return false; }
  }
  toggle() {
    this.hidden.update(value => !value);
    try { localStorage.setItem('investment-tracker.hide-values', String(this.hidden())); }
    catch { /* Privacy remains available when browser storage is disabled. */ }
  }
}

@Pipe({ name: 'currency', standalone: true, pure: false })
export class PrivateCurrencyPipe implements PipeTransform {
  private readonly privacy = inject(ValuePrivacy);
  private readonly formatter = new CurrencyPipe(inject(LOCALE_ID));
  transform(value: number | string | null | undefined, code?: string, display?: string | boolean, digits?: string, locale?: string): string | null {
    if (value == null) return null;
    return this.privacy.hidden() ? '••••••' : this.formatter.transform(value, code, display, digits, locale);
  }
}
