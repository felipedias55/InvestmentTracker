import { Directive, ElementRef, HostListener, inject } from '@angular/core';

@Directive({ selector: 'a[appSectionLink]', standalone: true })
export class SectionLink {
  private readonly link = inject<ElementRef<HTMLAnchorElement>>(ElementRef);
  @HostListener('click', ['$event']) go(event: MouseEvent) {
    if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
    const href = this.link.nativeElement.getAttribute('href');
    if (!href?.startsWith('#')) return;
    event.preventDefault();
    const heading = document.getElementById(href.slice(1));
    if (!heading) return;
    heading.tabIndex = -1; heading.focus({ preventScroll: true });
    heading.scrollIntoView({ block: 'start', behavior: 'instant' });
  }
}
