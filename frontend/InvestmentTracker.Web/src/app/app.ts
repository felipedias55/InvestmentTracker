import { Theme } from './core/theme';
import { MatMenuModule } from '@angular/material/menu';
import { afterNextRender, Component, ElementRef, HostListener, Injector, ViewChild, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catalogs } from './features/catalogs/catalog.models';
import { ValuePrivacy } from './core/value-privacy';
import { MatButtonModule } from '@angular/material/button';
import { AppIcon } from './shared/app-icon';
@Component({
  selector: 'app-root', standalone: true, templateUrl: './app.html', styleUrl: './app.css',
  imports: [MatMenuModule, RouterOutlet, RouterLink, RouterLinkActive, MatButtonModule, AppIcon],
})
export class App {
  constructor() {
    history.scrollRestoration = 'manual';
    inject(Router).events.pipe(takeUntilDestroyed()).subscribe(event => {
      if (event instanceof NavigationEnd) window.scrollTo({ top: 0, behavior: 'instant' });
    });
  }
  readonly theme = inject(Theme);
  readonly privacy = inject(ValuePrivacy);
  private readonly injector = inject(Injector);
  readonly catalogs = catalogs;
  readonly mobile = signal(window.innerWidth <= 900);
  readonly menuOpen = signal(window.innerWidth > 900);
  @ViewChild('menuButton', { read: ElementRef }) menuButton?: ElementRef<HTMLButtonElement>;
  @ViewChild('sidebar') sidebar?: ElementRef<HTMLElement>;
  toggleMenu() {
    if (this.menuOpen()) this.closeMenu();
    else {
      this.menuOpen.set(true);
    }
  }
  closeMenu() {
    this.menuOpen.set(false);
    afterNextRender(() => this.menuButton?.nativeElement.focus(), { injector: this.injector });
  }
  navigate(event: Event) {
    if (!(event.target instanceof Element) || !event.target.closest('a')) return;
    if (this.mobile()) this.closeMenu();
  }
  @HostListener('window:resize') resize() {
    const mobile = window.innerWidth <= 900;
    if (mobile !== this.mobile()) { this.mobile.set(mobile); this.menuOpen.set(!mobile); }
  }
  @HostListener('document:keydown', ['$event']) keyboard(event: KeyboardEvent) {
    if (!this.menuOpen() || event.defaultPrevented || (event.target instanceof Element && event.target.closest('dialog, .cdk-overlay-container'))) return;
    if (event.key === 'Escape') { event.preventDefault(); this.closeMenu(); return; }
    if (!this.mobile() || event.key !== 'Tab') return;
    const sidebar = this.sidebar?.nativeElement;
    const button = this.menuButton?.nativeElement;
    if (!sidebar || !button) return;
    const elements = [...button.closest('header')!.querySelectorAll<HTMLElement>('button, a[href]'), ...sidebar.querySelectorAll<HTMLElement>('a[href]')];
    const first = elements[0]; const last = elements[elements.length - 1];
    if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
    else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
  }
}
