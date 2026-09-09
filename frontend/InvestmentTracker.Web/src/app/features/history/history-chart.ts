import { Component, computed, input, output } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { HistoryPeriod } from './history.service';

@Component({
  standalone: true,
  selector: 'app-history-chart',
  imports: [CurrencyPipe],
  template: `
    <div
      class="chart-scroll"
      role="region"
      aria-label="Evolução do patrimônio por período"
      tabindex="0"
    >
      <div class="chart">
        @for (row of rows(); track row.period) {
          <div class="column">
            <div class="bar-space">
              @if (row.totalWealth !== null && row.currencyCode === currency()) {
                <button
                  type="button"
                  class="bar"
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
      .bar {
        min-height: 3px;
        max-width: 65px;
        width: 75%;
        border: 0;
        padding: 0;
        background: linear-gradient(#c58089, #97515d);
        border-radius: 7px 7px 0 0;
      }
      .bar:hover {
        background: #823f49;
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
  readonly rows = input.required<HistoryPeriod[]>();
  readonly currency = input.required<string>();
  readonly openSnapshot = output<number>();
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
