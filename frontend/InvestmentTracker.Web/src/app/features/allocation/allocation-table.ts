import { PrivateCurrencyPipe as CurrencyPipe } from '../../core/value-privacy';
import { Component, computed, input, signal } from '@angular/core';
import { DecimalPipe, NgTemplateOutlet, PercentPipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { EditPanel } from '../../shared/edit-panel';
import { AllocationRow } from './allocation.service';
import { AllocationDonut } from './allocation-donut';

@Component({
  standalone: true,
  selector: 'app-allocation-table',
  host: { '[class.compact-view]': 'compact()' },
  imports: [CurrencyPipe, DecimalPipe, PercentPipe, AllocationDonut, NgTemplateOutlet, MatButtonModule, EditPanel],
  template: `
    <section [class.panel]="!visual()" [class.compact-summary]="compact()">
      @if (!visual()) { <h2>{{ title() }}</h2> }
      @if (!rows().length) {
        <p>Sem posições ou metas neste grupo.</p>
      } @else {
        @if (visual()) {
          @if (chartType() === 'donut') {
            <app-allocation-donut [rows]="rows()" [currency]="currency()" [compact]="compact()" />
          } @else {
          <p class="chart-legend"><span class="legend-current"></span> Participação atual @if (showTargets()) { <span class="legend-target"></span> Meta }</p>
          <div class="chart-scale" aria-hidden="true"><span>0%</span><span>50%</span><span>100%</span></div>
          @for (row of visibleRows(); track row.groupId) {
            <div class="allocation-bar">
              <div class="bar-label"><strong [title]="row.name">{{ row.name }}</strong><span>{{ row.currentPercentage === null ? 'Indisponível' : (row.currentPercentage | percent: '1.2-2') }} @if (showTargets() && !compact()) { · Meta: {{ row.targetPercentage === null ? 'Não configurada' : (row.targetPercentage | percent: '1.2-2') }} }</span></div>
              @if (row.currentPercentage !== null) {
                <div class="bar-track" aria-hidden="true"><div class="bar-fill" [style.width.%]="number(row.currentPercentage) * 100"></div>@if (showTargets() && row.targetPercentage !== null) { <span class="target-marker" [style.left.%]="number(row.targetPercentage) * 100"></span> }</div>
              }
              @if (compact() && showTargets() && row.targetPercentage === null) { <small class="hint">Sem meta</small> }
            </div>
          }
          }
        }
        @if (compact()) {
          @if (unavailable()) { <p class="summary-note" role="status">{{ unavailable() }} grupos com dados indisponíveis. Consulte os detalhes.</p> }
          @if (rows().length > 5) { <p class="summary-note">{{ chartType() === 'donut' ? 'Legenda' : 'Barras' }}: 5 de {{ rows().length }} grupos. {{ chartType() === 'donut' ? 'Rosca com todos os grupos filtrados.' : 'Todos os grupos nos detalhes.' }}</p> }
          <button mat-button type="button" (click)="detailsOpen.set(true)" [attr.aria-label]="'Ver detalhes de ' + title()">Ver detalhes @if (rows().length > 5) { ({{ rows().length }} grupos) }</button>
          @if (detailsOpen()) {
            <app-edit-panel [editing]="true" [wide]="true" [title]="'Detalhes · ' + title()" (closed)="detailsOpen.set(false)">
              <div class="allocation-details"><p>Percentuais referentes à carteira inteira. Esta tabela contém todos os grupos dos filtros atuais.</p>
                <p class="table-hint hint">Deslize a tabela para consultar as outras colunas.</p>
                <ng-container [ngTemplateOutlet]="dataTable" />
                <div class="actions"><button mat-stroked-button type="button" (click)="detailsOpen.set(false)">Fechar detalhes</button></div>
              </div>
            </app-edit-panel>
          }
        } @else {
        <details [open]="!visual()">
        <summary>Ver valores e diferenças em tabela</summary>
        <ng-container [ngTemplateOutlet]="dataTable" />
        </details>
        }
        <ng-template #dataTable>
        <div class="table-scroll" [attr.tabindex]="compact() ? 0 : null" [attr.role]="compact() ? 'region' : null" [attr.aria-label]="compact() ? 'Dados de ' + title() : null">
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
        </ng-template>
      }
    </section>
  `,
  styles: [
    `
      :host {
        display: block;
        margin: 24px 0;
      }
      :host(.compact-view) { margin: 8px 0 0; }
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
      .compact-summary .chart-legend { margin: 0 0 4px; }
      .compact-summary .chart-scale { margin: 4px 0; }
      .compact-summary .allocation-bar { margin-bottom: 8px; }
      .compact-summary .bar-label { flex-wrap: nowrap; gap: 12px; margin-bottom: 3px; }
      .compact-summary .bar-label strong { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; min-width: 0; font-size: 13px; }
      .compact-summary .bar-label span { flex-shrink: 0; }
      .compact-summary .bar-track { height: 8px; }
      .compact-summary .target-marker { height: 14px; }
      .summary-note { color: var(--muted); font-size: 12px; margin: 8px 0; }
      .allocation-details { padding: 16px; }
      .allocation-details table { min-width: 680px; }
      .allocation-details td { white-space: nowrap; }
      .allocation-details th[scope=row] { min-width: 200px; text-transform: none; }
      .table-hint { display: none; }
      @media(max-width: 700px) { .table-hint { display: block; } }
      .allocation-details .actions { justify-content: flex-end; margin-top: 16px; }
    `,
  ],
})
export class AllocationTable {
  readonly rows = input.required<AllocationRow[]>();
  readonly title = input.required<string>();
  readonly currency = input.required<string>();
  readonly showTargets = input(true);
  readonly visual = input(false);
  readonly compact = input(false);
  readonly detailsOpen = signal(false);
  readonly visibleRows = computed(() => this.compact() ? this.rows().slice(0, 5) : this.rows());
  readonly unavailable = computed(() => this.rows().filter(row => row.currentPercentage === null || row.currentValue === null).length);
  readonly chartType = input<'bars' | 'donut'>('bars');
  readonly number = Number;
}
