import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { rememberContext } from './page-context';

@Component({ standalone: true, template: '' })
class Host {
  readonly query = signal(''); readonly page = signal(0); readonly ready = signal(false);
  readonly draft = signal('');
  readonly context = rememberContext('test', () => ({ query: this.query(), page: this.page() }), value => {
    this.query.set(value.query); this.page.set(value.page);
  }, () => this.ready());
  select(id: number) { this.context.save(); this.context.restore(id); }
}
describe('Page context', () => {
  it('restores each portfolio independently and excludes financial drafts across remounts', async () => {
    await TestBed.configureTestingModule({ imports: [Host] }).compileComponents();
    const first = TestBed.createComponent(Host), a = first.componentInstance;
    a.select(1); a.query.set('filtro'); a.page.set(1); a.draft.set('valor sensível');
    a.select(2); expect(a.query()).toBe(''); expect(a.page()).toBe(0);
    a.query.set('outra carteira'); a.select(1);
    expect(a.query()).toBe('filtro'); expect(a.page()).toBe(1);
    first.destroy();
    const second = TestBed.createComponent(Host), b = second.componentInstance;
    b.select(1); expect(b.query()).toBe('filtro'); expect(b.page()).toBe(1); expect(b.draft()).toBe('');
    b.select(2); expect(b.query()).toBe('outra carteira'); second.destroy();
  });
});
