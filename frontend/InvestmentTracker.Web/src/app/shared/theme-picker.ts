import { Component, ViewChild, afterNextRender, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule, MatMenuTrigger } from '@angular/material/menu';
import { Theme } from '../core/theme';
import { AppIcon } from './app-icon';

// The first interaction loads the Material menu, then opens it with its usual focus handling.
@Component({
  selector: 'app-theme-picker', standalone: true,
  imports: [MatButtonModule, MatMenuModule, AppIcon],
  template: `
    <button mat-icon-button [matMenuTriggerFor]="themeMenu" aria-label="Escolher tema"><app-icon name="theme" /></button>
    <mat-menu #themeMenu="matMenu">
      <button mat-menu-item (click)="theme.select('light')">Claro {{ theme.preference() === 'light' ? '· selecionado' : '' }}</button>
      <button mat-menu-item (click)="theme.select('dark')">Escuro {{ theme.preference() === 'dark' ? '· selecionado' : '' }}</button>
      <button mat-menu-item (click)="theme.select('system')">Sistema {{ theme.preference() === 'system' ? '· selecionado' : '' }}</button>
    </mat-menu>
  `,
  styles: [':host { display: inline-flex; flex-shrink: 0; } button { color: inherit; }'],
})
export class ThemePicker {
  readonly theme = inject(Theme);
  @ViewChild(MatMenuTrigger) private trigger!: MatMenuTrigger;
  constructor() {
    afterNextRender(() => this.trigger.openMenu());
  }
}
