import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

// Local, consistent 24px outline icons: no external fonts or network dependency.
const paths: Record<string, string> = {
  drag: 'M8 5h1m6 0h1M8 12h1m6 0h1M8 19h1m6 0h1',
  up: 'm5 12 7-7 7 7M12 5v15',
  down: 'm5 12 7 7 7-7M12 19V4',
  theme: 'M12 3a9 9 0 1 0 9 9 7 7 0 0 1-9-9Z',
  menu: 'M4 6h16M4 12h16M4 18h16',
  home: 'm3 10 9-7 9 7v11h-6v-7H9v7H3Z',
  wallet: 'M3 5h16v4H3V5Zm0 4h18v12H3V9Zm13 5h5',
  transfer: 'M3 7h17l-4-4M21 17H4l4 4',
  chart: 'M4 20V10m6 10V4m6 16v-8m6 8H2',
  income: 'M4 6c0-4 16-4 16 0s-16 4-16 0Zm0 0v6c0 4 16 4 16 0V6M4 12v6c0 4 16 4 16 0v-6',
  trend: 'm3 17 6-7 5 3 7-9M16 4h5v5',
  target: 'M20 12a8 8 0 1 1-8-8m4 8a4 4 0 1 1-4-4m0 4 9-9m-4 0h4v4',
  calculator: 'M5 3h14v18H5ZM8 7h8M8 11h1m3 0h1m3 0h1M8 15h1m3 0h1m3 0h1M8 18h1m3 0h1m3 0h1',
  folder: 'M3 5h7l2 3h9v13H3Z',
  eye: 'M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12Zm13 0a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z',
  hidden: 'm3 3 18 18M9 5a13 13 0 0 1 13 7l-3 4M6 6a20 20 0 0 0-4 6s4 7 10 7a12 12 0 0 0 5-1',
  refresh: 'M20 8a8 8 0 0 0-14-3L3 8m0-5v5h5M4 16a8 8 0 0 0 14 3l3-3m0 5v-5h-5',
  plus: 'M12 4v16M4 12h16',
  info: 'M12 10v7m0-11v1m9 5a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z',
  edit: 'm4 16 12-12 4 4L8 20H4Zm10-10 4 4',
  close: 'm5 5 14 14M19 5 5 19',
  more: 'M5 12h.01M12 12h.01M19 12h.01',
  filter: 'M3 4h18l-7 8v7l-4 2v-9Z',
};

@Component({
  selector: 'app-icon', standalone: true, imports: [MatIconModule],
  template: `<mat-icon aria-hidden="true"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" focusable="false"><path [attr.d]="path()" /></svg></mat-icon>`,
  styles: [':host { display:inline-flex; width:24px; height:24px; flex-shrink:0; vertical-align:middle; } svg { width:100%; height:100%; }'],
})
export class AppIcon {
  readonly name = input('info');
  path() { return paths[this.name()] ?? paths['info']; }
}
