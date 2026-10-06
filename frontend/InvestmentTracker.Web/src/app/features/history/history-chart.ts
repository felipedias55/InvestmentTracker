import { PrivateCurrencyPipe as CurrencyPipe } from '../../core/value-privacy';
import { Component, computed, input, output, signal } from '@angular/core';
import { MoneyChart } from '../../shared/money-chart';

import { HistoryPeriod } from './history.service';

@Component({
  standalone: true,
  selector: 'app-history-chart',
  imports: [CurrencyPipe, MoneyChart],
  template: `
    <div class="actions" role="group" aria-label="Formato da evolução">
      <button (click)="mode.set('line')" [class.primary]="mode() === 'line'" [attr.aria-pressed]="mode() === 'line'">Linha</button>
      <button (click)="mode.set('columns')" [class.primary]="mode() === 'columns'" [attr.aria-pressed]="mode() === 'columns'">Colunas</button>
    </div>
    @if (mode() === 'line') {
      <app-money-chart [points]="points()" [currency]="currency()" title="Evolução do patrimônio" kind="line" />
      <p class="hint">Pontos representam fotografias. Lacunas, fotografias desatualizadas e outras moedas interrompem a linha. Consulte os valores preservados nos botões abaixo.</p>
    }
    <div
      class="chart-scroll"
      role="region"
      aria-label="Evolução do patrimônio por período"
      tabindex="0"
    >
      <div class="chart" [class.line-mode]="mode() === 'line'">
        @for (row of rows(); track row.period) {
          <div class="column">
            <div class="bar-space">
              @if (row.totalWealth !== null && row.currencyCode === currency()) {
                <button
                  type="button"
                  class="bar"
                  [class.outdated]="row.isOutdated"
                  [style.height.%]="height(row)"
                  (click)="openSnapshot.emit(row.snapshotId!)"
                  [attr.aria-label]="
                    'Abrir fotografia ' +
                    row.period +
                    ': ' +
                    (row.totalWealth | currency: currency() : 'code' : '1.2-2')
                  "
                  [title]="row.totalWealth | currency: currency() : 'code' : '1.2-2'"
                ></button>
              } @else {
                <span class="missing">{{
                  row.snapshotId === null ? 'Sem foto' : 'Outra moeda'
                }}</span>
              }
            </div>
            <strong>{{ row.period }}</strong>
            @if (row.isOutdated) { <small>Desatualizada</small> }
            @if (row.totalWealth !== null && row.currencyCode === currency()) {
              <small>{{ row.totalWealth | currency: currency() : 'code' : '1.2-2' }}</small>
            }
          </div>
        }
      </div>
    </div>
  `,
  styles: [
    `
      .chart-scroll {
        overflow-x: auto;
        padding: 12px 0;
      }
      .chart {
        display: grid;
        grid-auto-flow: column;
        grid-auto-columns: minmax(95px, 1fr);
        gap: 14px;
        align-items: end;
      }
      .column {
        text-align: center;
        min-width: 0;
      }
      .bar-space {
        height: 190px;
        display: flex;
        align-items: end;
        justify-content: center;
        border-bottom: 1px solid var(--border);
        margin-bottom: 10px;
      }
      .line-mode .bar-space { height: 28px; border: 0; }
      .line-mode .bar { height: 24px !important; border-radius: 6px; }
      .bar {
        min-height: 3px;
        max-width: 65px;
        width: 75%;
        border: 0;
        padding: 0;
        background: linear-gradient(#d0bb80, #ac893c);
        border-radius: 7px 7px 0 0;
      }
      .bar.outdated { background: repeating-linear-gradient(45deg, #9da4ad 0 7px, #dce0e5 7px 14px); }
      .bar:hover {
        background: #624811;
      }
      .missing {
        font-size: 11px;
        color: var(--muted);
        padding-bottom: 10px;
      }
      small {
        display: block;
        margin-top: 5px;
        font-size: 10px;
        overflow-wrap: anywhere;
      }
    `,
  ],
})
export class HistoryChart {
  readonly mode = signal<'line' | 'columns'>('line');
  readonly rows = input.required<HistoryPeriod[]>();
  readonly currency = input.required<string>();
  readonly openSnapshot = output<number>();
  readonly points = computed(() => this.rows().map(r => ({ label: r.period,
    value: r.totalWealth === null || r.currencyCode !== this.currency() || r.isOutdated || r.isReopened ? null : Number(r.totalWealth) })));
  private readonly maximum = computed(() =>
    Math.max(
      1,
      ...this.rows()
        .filter((r) => r.currencyCode === this.currency())
        .map((r) => Number(r.totalWealth ?? 0)),
    ),
  );
  height(row: HistoryPeriod) {
    return (Number(row.totalWealth ?? 0) / this.maximum()) * 100;
  }
}
