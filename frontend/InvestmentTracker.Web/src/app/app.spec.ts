import { vi } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
describe('App', () => {
  it('shows navigation to analysis, portfolio and all six registrations', async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([])],
    }).compileComponents();
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const links = fixture.nativeElement.querySelectorAll('nav a');
    expect(links.length).toBe(14);
    expect(fixture.nativeElement.textContent).toContain('Investment');
    expect(fixture.nativeElement.querySelector('a[href="/currencies"]')).toBeTruthy();
  });
  afterEach(() => vi.unstubAllGlobals());
  it('opens a mobile drawer, traps keyboard navigation and closes with Escape', async () => {
    vi.stubGlobal('innerWidth', 390);
    await TestBed.configureTestingModule({ imports: [App], providers: [provideRouter([])] }).compileComponents();
    const fixture = TestBed.createComponent(App); fixture.detectChanges();
    const button: HTMLButtonElement = fixture.nativeElement.querySelector('.menu-toggle');
    const sidebar: HTMLElement = fixture.nativeElement.querySelector('.sidebar');
    expect(sidebar.hidden).toBe(true);
    button.click(); fixture.detectChanges();
    expect(sidebar.hidden).toBe(false);
    expect(sidebar.querySelector('button')).toBeNull();
    expect(fixture.nativeElement.querySelector('header').inert).not.toBe(true);
    button.click(); fixture.detectChanges();
    expect(sidebar.hidden).toBe(true);
    button.click(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('main').inert).toBe(true);
    const first = button;
    first.focus();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', shiftKey: true, cancelable: true }));
    expect(document.activeElement).toBe(sidebar.querySelectorAll('a')[sidebar.querySelectorAll('a').length - 1]);
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', cancelable: true })); fixture.detectChanges();
    expect(sidebar.hidden).toBe(true);
    expect(document.activeElement).toBe(button);
  });
  it('lets desktop users collapse the sidebar without losing access to the menu', async () => {
    vi.stubGlobal('innerWidth', 1280);
    await TestBed.configureTestingModule({ imports: [App], providers: [provideRouter([])] }).compileComponents();
    const fixture = TestBed.createComponent(App); fixture.detectChanges();
    fixture.nativeElement.querySelector('.menu-toggle').click(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sidebar').hidden).toBe(true);
    expect(fixture.nativeElement.querySelector('.menu-toggle').getAttribute('aria-label')).toBe('Abrir menu');
    expect(fixture.nativeElement.querySelector('.menu-toggle').textContent.trim()).toBe('☰');
    expect(fixture.nativeElement.querySelector('a[href="/assets"] span')).toBeNull();
  });

});
