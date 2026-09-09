import { Component, input } from '@angular/core';
import { CurrencyPipe, DecimalPipe, PercentPipe } from '@angular/common';
import { AllocationRow } from './allocation.service';

@Component({
  standalone: true,
  selector: 'app-allocation-table',
  imports: [CurrencyPipe, DecimalPipe, PercentPipe],
  template: `
    <section [class.panel]="!visual()">
      @if (!visual()) { <h2>{{ title() }}</h2> }
      @if (!rows().length) {
        <p>Sem posições ou metas neste grupo.</p>
      } @else {
        @if (visual()) {
          <p class="chart-legend"><span class="legend-current"></span> Participação atual @if (showTargets()) { <span class="legend-target"></span> Meta }</p>
          <div class="chart-scale" aria-hidden="true"><span>0%</span><span>50%</span><span>100%</span></div>
          @for (row of rows(); track row.groupId) {
            <div class="allocation-bar">
              <div class="bar-label"><strong>{{ row.name }}</strong><span>{{ row.currentPercentage === null ? 'Indisponível' : (row.currentPercentage | percent: '1.2-2') }} @if (showTargets()) { · Meta: {{ row.targetPercentage === null ? 'Não configurada' : (row.targetPercentage | percent: '1.2-2') }} }</span></div>
              @if (row.currentPercentage !== null) {
                <div class="bar-track" aria-hidden="true"><div class="bar-fill" [style.width.%]="number(row.currentPercentage) * 100"></div>@if (showTargets() && row.targetPercentage !== null) { <span class="target-marker" [style.left.%]="number(row.targetPercentage) * 100"></span> }</div>
              }
            </div>
          }
        }
        <details [open]="!visual()">
        <summary>Ver valores e diferenças em tabela</summary>
        <div class="table-scroll">
          <table>
            <thead>
              <tr>
                <th scope="col">Grupo</th>
                <th scope="col">Valor atual ({{ currency() }})</th>
                <th scope="col">Atual</th>
                @if (showTargets()) {
                  <th scope="col">Meta</th>
                  <th scope="col">Diferença (p.p.)</th>
                }
              </tr>
            </thead>
            <tbody>
              @for (row of rows(); track row.groupId) {
                <tr>
                  <th scope="row">{{ row.name }}</th>
                  <td>
                    {{
                      row.currentValue === null
                        ? 'Indisponível'
                        : (row.currentValue | currency: currency() : 'code' : '1.2-2')
                    }}
                  </td>
                  <td>
                    {{
                      row.currentPercentage === null
                        ? 'Indisponível'
                        : (row.currentPercentage | percent: '1.2-2')
                    }}
                    @if (row.currentPercentage !== null) {
                      <meter
                        min="0"
                        max="1"
                        [value]="number(row.currentPercentage)"
                        [attr.aria-label]="'Participação de ' + row.name"
                      ></meter>
                    }
                  </td>
                  @if (showTargets()) {
                    <td>
                      {{
                        row.targetPercentage === null
                          ? 'Não configurada'
                          : (row.targetPercentage | percent: '1.2-4')
                      }}
                    </td>
                    <td>
                      {{
                        row.difference === null
                          ? '—'
                          : (number(row.difference) * 100 | number: '1.2-4')
                      }}
                    </td>
                  }
                </tr>
              }
            </tbody>
          </table>
        </div>
        </details>
      }
    </section>
  `,
  styles: [
    `
      :host {
        display: block;
        margin: 24px 0;
      }
      .chart-legend, .bar-label { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
      .chart-legend { color: var(--muted); font-size: 12px; }
      .legend-current { width: 18px; height: 8px; background: var(--accent); border-radius: 3px; }
      .legend-target { width: 3px; height: 14px; background: var(--ink); margin-left: 12px; }
      .chart-scale { display: flex; justify-content: space-between; font-size: 11px; color: var(--muted); margin: 24px 0 12px; }
      .allocation-bar { margin-bottom: 22px; }
      .bar-label { justify-content: space-between; margin-bottom: 9px; }
      .bar-label span { font-size: 12px; color: var(--muted); }
      .bar-track { position: relative; height: 14px; background: var(--accent-soft); border-radius: 4px; }
      .bar-fill { height: 100%; background: var(--accent); border-radius: 4px; max-width: 100%; }
      .target-marker { position: absolute; top: -3px; height: 20px; width: 3px; background: var(--ink); transform: translateX(-50%); }
      details { margin-top: 24px; }
      summary { cursor: pointer; color: var(--accent); margin-bottom: 16px; }
      meter {
        display: block;
        width: 100%;
        min-width: 70px;
        accent-color: var(--accent);
      }
    `,
  ],
})
export class AllocationTable {
  readonly rows = input.required<AllocationRow[]>();
  readonly title = input.required<string>();
  readonly currency = input.required<string>();
  readonly showTargets = input(true);
  readonly visual = input(false);
  readonly number = Number;
}
