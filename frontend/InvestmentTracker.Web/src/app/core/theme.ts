import { Injectable, OnDestroy, signal } from '@angular/core';

export type ThemePreference = 'light' | 'dark' | 'system';
@Injectable({ providedIn: 'root' })
export class Theme implements OnDestroy {
  readonly preference = signal<ThemePreference>('system');
  private readonly media = window.matchMedia?.('(prefers-color-scheme: dark)');
  private readonly changed = () => this.apply();
  constructor() {
    try {
      const saved = localStorage.getItem('investment-tracker.theme');
      if (saved === 'light' || saved === 'dark') this.preference.set(saved);
    } catch { /* Storage may be unavailable; use the system preference. */ }
    this.media?.addEventListener('change', this.changed);
    this.apply();
  }
  select(value: ThemePreference) {
    this.preference.set(value);
    try { localStorage.setItem('investment-tracker.theme', value); } catch { /* Keep the session preference. */ }
    this.apply();
  }
  private apply() {
    const dark = this.preference() === 'dark' || (this.preference() === 'system' && this.media?.matches);
    document.documentElement.dataset['theme'] = dark ? 'dark' : 'light';
  }
  ngOnDestroy() { this.media?.removeEventListener('change', this.changed); }
}
