import { afterNextRender, Component, ElementRef, HostListener, Injector, ViewChild, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { catalogs } from './features/catalogs/catalog.models';
@Component({
  selector: 'app-root', standalone: true, templateUrl: './app.html', styleUrl: './app.css',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
})
export class App {
  private readonly injector = inject(Injector);
  readonly catalogs = catalogs;
  readonly mobile = signal(window.innerWidth <= 900);
  readonly menuOpen = signal(window.innerWidth > 900);
  @ViewChild('menuButton') menuButton?: ElementRef<HTMLButtonElement>;
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
    window.scrollTo({ top: 0, behavior: 'instant' });
  }
  @HostListener('window:resize') resize() {
    const mobile = window.innerWidth <= 900;
    if (mobile !== this.mobile()) { this.mobile.set(mobile); this.menuOpen.set(!mobile); }
  }
  @HostListener('document:keydown', ['$event']) keyboard(event: KeyboardEvent) {
    if (!this.menuOpen() || event.defaultPrevented || (event.target instanceof Element && event.target.closest('dialog'))) return;
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
