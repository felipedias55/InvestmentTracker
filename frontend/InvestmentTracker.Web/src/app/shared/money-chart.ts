import { Component, computed, input } from '@angular/core';
import { PrivateCurrencyPipe } from '../core/value-privacy';

export interface MoneyPoint { label: string; value: number | null; total?: boolean; }

@Component({
  selector: 'app-money-chart', standalone: true, imports: [PrivateCurrencyPipe],
  template: `
    @if (points().length) {
      @if (kind() === 'bars') {
        <div class="rankings">
          @for (point of points(); track $index) {
            <div class="ranking"><div><strong>{{ point.label }}</strong><span>{{ point.value === null ? 'Indisponível' : (point.value | currency:currency():'code') }}</span></div>
              @if (point.value !== null) { <div class="rank-track" aria-hidden="true"><span [style.width.%]="rankWidth(point.value)" [class.negative]="point.value < 0"></span></div> }
            </div>
          }
        </div>
        <p class="hint">Comprimento indica a magnitude. Valores negativos representam estornos líquidos, com sinal explícito.</p>
      } @else {
      <div class="chart-scroll" tabindex="0" role="region" [attr.aria-label]="title() + ' · ' + currency()">
        <svg [attr.viewBox]="'0 0 ' + width() + ' 300'" [style.min-width.px]="points().length > 5 ? width() : 0" role="img" [attr.aria-label]="title() + '. Valores disponíveis na tabela abaixo.'">
          @for (tick of ticks(); track tick) {
            <line x1="105" [attr.x2]="width() - 20" [attr.y1]="y(tick)" [attr.y2]="y(tick)" stroke="#d9dde2" />
            <text x="96" [attr.y]="y(tick) + 4" text-anchor="end" class="axis">{{ tick | currency:currency():'symbol':'1.2-2' }}</text>
          }
          <line x1="105" [attr.x2]="width() - 20" [attr.y1]="y(0)" [attr.y2]="y(0)" stroke="#626871" />
          @if (kind() === 'line') {
            @for (path of paths(); track $index) { <path [attr.d]="path" fill="none" stroke="#ac893c" stroke-width="3" /> }
          }
          @for (point of points(); track $index; let i = $index) {
            @if (point.value !== null) {
              @if (kind() === 'line') {
                <circle [attr.cx]="x(i)" [attr.cy]="y(point.value)" r="5" fill="#ac893c"><title>{{ point.label }}: {{ point.value | currency:currency():'code' }}</title></circle>
              } @else {
                <rect [attr.x]="x(i) - 17" [attr.y]="barTop(i)" width="34" [attr.height]="barHeight(i)" rx="3" [attr.fill]="point.value < 0 ? '#ac893c' : '#536c86'"><title>{{ point.label }}: {{ point.value | currency:currency():'code' }}</title></rect>
              }
            } @else { <text [attr.x]="x(i)" y="132" text-anchor="middle" class="axis">Pendente</text> }
            <text [attr.x]="x(i)" y="258" text-anchor="middle" class="axis">{{ point.label.length > 14 ? point.label.slice(0, 12) + '…' : point.label }}</text>
          }
        </svg>
      </div>
      @if (points().length > 5) { <p class="hint">Deslize o gráfico horizontalmente para consultar todos os períodos.</p> }
      }
      <details><summary>Ver dados de {{ title() }}</summary>
        <div class="table-scroll"><table><thead><tr><th scope="col">Referência</th><th scope="col">Valor · {{ currency() }}</th></tr></thead>
        <tbody>@for (point of points(); track $index) { <tr><th scope="row">{{ point.label }}</th><td>{{ point.value === null ? 'Indisponível' : (point.value | currency:currency():'code') }}</td></tr> }</tbody></table></div>
      </details>
    } @else { <p class="empty">Sem dados para estes filtros.</p> }
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .chart-scroll { overflow-x: auto; margin: 16px 0; }
    svg { display: block; width: 100%; max-height: 340px; }
    .axis { fill: #626871; font-size: 11px; font-family: inherit; }
    @media (max-width: 600px) { .axis { font-size: 13px; } }
    summary { cursor: pointer; color: var(--accent); padding: 8px 0; }
    .ranking { margin: 18px 0; } .ranking > div:first-child { display: flex; justify-content: space-between; gap: 12px; flex-wrap: wrap; margin-bottom: 8px; }
    .rank-track { height: 12px; background: #eceef1; border-radius: 4px; } .rank-track span { display: block; height: 100%; background: #536c86; border-radius: 4px; } .rank-track .negative { background: #ac893c; }
  `],
})
export class MoneyChart {
  readonly points = input.required<MoneyPoint[]>();
  readonly currency = input.required<string>();
  readonly title = input.required<string>();
  readonly kind = input<'line' | 'columns' | 'waterfall' | 'bars'>('columns');
  rankWidth(value: number) { return Math.abs(value) / Math.max(1, ...this.points().map(p => Math.abs(p.value ?? 0))) * 100; }
  readonly width = computed(() => Math.max(400, this.points().length * 82 + 130));
  readonly levels = computed(() => {
    let total = 0;
    return this.points().map(p => {
      const start = this.kind() === 'waterfall' && !p.total ? total : 0;
      total = start + (p.value ?? 0);
      return { start, end: total };
    });
  });
  readonly limits = computed(() => {
    const values = this.levels().flatMap(p => [p.start, p.end]);
    const min = Math.min(0, ...values); const max = Math.max(0, ...values);
    return { min, max: min === max ? max + 1 : max };
  });
  readonly ticks = computed(() => { const { min, max } = this.limits(); return [min, (min + max) / 2, max]; });
  x(i: number) { return 140 + i * (this.width() - 180) / Math.max(1, this.points().length - 1); }
  y(value: number) { const { min, max } = this.limits(); return 230 - (value - min) / (max - min) * 200; }
  barTop(i: number) { const p = this.levels()[i]; return this.y(Math.max(p.start, p.end)); }
  barHeight(i: number) { const p = this.levels()[i]; return Math.max(1, Math.abs(this.y(p.start) - this.y(p.end))); }
  readonly paths = computed(() => {
    const result: string[] = []; let segment = '';
    this.points().forEach((p, i) => {
      if (p.value === null) { if (segment) result.push(segment); segment = ''; }
      else segment += `${segment ? ' L' : 'M'}${this.x(i)},${this.y(p.value)}`;
    });
    if (segment) result.push(segment);
    return result;
  });
}
