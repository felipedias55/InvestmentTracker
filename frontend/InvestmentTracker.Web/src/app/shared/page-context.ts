import { DestroyRef, Injectable, Injector, afterNextRender, effect, inject, signal } from '@angular/core';

interface Entry<T> { state: T; scroll: number; }
@Injectable({ providedIn: 'root' })
export class PageContexts {
  readonly captures = new Set<() => void>();
  capture() { this.captures.forEach(save => save()); }
  // Deliberately memory-only: searches and navigation state disappear on reload.
  private readonly entries = new Map<string, Entry<unknown>>();
  get<T>(key: string): Entry<T> | undefined { return this.entries.get(key) as Entry<T> | undefined; }
  put<T>(key: string, state: T, scroll: number) { this.entries.set(key, { state: structuredClone(state), scroll }); }
}

export function rememberContext<T>(screen: string, read: () => T, write: (state: T) => void, ready: () => boolean) {
  const store = inject(PageContexts), destroy = inject(DestroyRef), injector = inject(Injector);
  let key = '', defaults: T | undefined, alive = true, captured = false;
  const pending = signal<{ key: string; scroll: number } | null>(null);
  const save = () => { if (key) store.put(key, read(), window.scrollY); };
  const capture = () => { save(); captured = true; };
  store.captures.add(capture);
  effect(() => {
    const target = pending();
    if (!target || !ready()) return;
    afterNextRender(() => {
      if (!alive || pending() !== target || !ready()) return;
      const max = Math.max(0, document.documentElement.scrollHeight - window.innerHeight);
      window.scrollTo({ top: Math.min(target.scroll, max), behavior: 'instant' });
      pending.set(null);
    }, { injector });
  });
  destroy.onDestroy(() => { if (!captured) save(); alive = false; store.captures.delete(capture); });
  return {
    save,
    restore(scope: string | number) {
      const next = `${screen}:${scope}`;
      const same = next === key;
      defaults ??= structuredClone(read());
      key = next;
      captured = false;
      const entry = store.get<T>(key);
      write(structuredClone(entry?.state ?? defaults));
      if (!same) pending.set({ key, scroll: entry?.scroll ?? 0 });
    },
  };
}
