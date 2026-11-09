import { Component, DestroyRef, computed, effect, inject, input, signal } from '@angular/core';
import { DatePipe, DecimalPipe, PercentPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { apiError } from '../../core/services/api-error';
import { HistoryPeriod } from './history.service';

export interface BenchmarkResult {
  code: string; name: string; source: string; currencyCode: string; return: string | null; difference: string | null;
  firstObservation: string | null; lastObservation: string | null; note: string; retrievedAt: string | null; fromCache: boolean;
}
export interface BenchmarkResponse {
  period: string; start: string | null; end: string | null; currencyCode: string; portfolioReturn: string | null; method: string; items: BenchmarkResult[];
}

@Component({ selector: 'app-benchmark-comparison', standalone: true,
  imports: [DatePipe, PercentPipe, DecimalPipe, MatButtonModule, MatFormFieldModule, MatSelectModule, MatInputModule],
  template: `<section class="panel benchmarks" aria-label="Comparação com benchmarks"><h2>Benchmarks</h2>
    <p>Compare o Dietz estimado da carteira com CDI, IPCA, Ibovespa e S&amp;P 500 no intervalo entre fotografias.</p>
    <mat-form-field appearance="outline"><mat-label>Período dos benchmarks</mat-label><select matNativeControl (change)="choice.set($any($event.target).value)" [disabled]="busy()">
      @for (period of periods(); track period.period) { <option [value]="period.period" [selected]="period.period === row()?.period">{{ period.period }} · {{ period.currencyCode }}</option> }
    </select></mat-form-field>
    @if (row(); as row) { <p class="hint">{{ row.comparisonStart | date:'dd/MM/yyyy':'UTC' }} → {{ row.snapshotDate | date:'dd/MM/yyyy':'UTC' }} · {{ row.currencyCode }}</p> }
    <button mat-stroked-button type="button" [disabled]="busy() || !row() || row()?.returns?.modifiedDietz == null" (click)="compare()">{{ busy() ? 'Consultando benchmarks…' : 'Comparar benchmarks' }}</button>
    @if (!row() || row()?.returns?.modifiedDietz == null) { <p class="notice">{{ row()?.returns?.note || 'São necessárias duas fotografias válidas e rentabilidade disponível para comparar.' }}</p> }
    <p class="hint">Consulta somente ao clicar. Ibovespa usa a cota brapi compartilhada com cotações e cadastro assistido; BCB e FRED são fontes separadas. Resultados disponíveis ficam em cache por 24 horas.</p>
    @if (error()) { <p role="alert" class="notice error">{{ error() }}</p> }
    @if (result(); as result) {
      <p role="status">Carteira · Dietz estimado: {{ result.portfolioReturn == null ? 'Indisponível' : (result.portfolioReturn | percent:'1.2-2':'pt-BR') }} · {{ result.currencyCode }}</p>
      <p class="hint">{{ result.method }}</p>
      <div class="table-scroll" tabindex="0" role="region" aria-label="Resultados dos benchmarks"><table><thead><tr><th>Referência</th><th>Retorno no intervalo</th><th>Carteira menos referência (p.p.)</th><th>Cobertura e fonte</th></tr></thead><tbody>
      @for (item of result.items; track item.code) { <tr><th scope="row">{{ item.name }}</th><td>{{ item.return == null ? 'Indisponível' : (item.return | percent:'1.2-2':'pt-BR') }}<small>{{ item.currencyCode }}</small></td>
        <td>{{ item.difference == null ? '—' : (points(item.difference) | number:'1.2-2':'pt-BR') }}</td>
        <td><p>{{ item.note }}</p><small>{{ item.source }}{{ item.fromCache ? ' · cache' : '' }} @if (item.retrievedAt) { · consultado em {{ item.retrievedAt | date:'dd/MM/yyyy HH:mm' }} }</small>
          @if (item.firstObservation && item.lastObservation) { <small>Observações: {{ item.firstObservation | date:'dd/MM/yyyy':'UTC' }} → {{ item.lastObservation | date:'dd/MM/yyyy':'UTC' }}.</small> }</td></tr> }
      </tbody></table></div>
      <details><summary>Como interpretar os benchmarks</summary><p>Diferença positiva indica Dietz estimado acima da referência; não representa alfa ou uma carteira que recebeu os mesmos fluxos. XIRR anualizada não entra nesta comparação. Custos, impostos, composição da carteira e proventos podem diferir das referências.</p>
        <p>Índices usam o último fechamento disponível até cada fotografia, no máximo sete dias antes, com as datas exibidas. Conversões BRL/USD usam câmbio histórico; nunca o câmbio atual. O IPCA exige fins de mês e publicação de todos os meses. Dados ausentes não equivalem a retorno zero.</p>
        <p>Fontes: <a href="https://www.bcb.gov.br/estatisticas/indecoreestruturacao" target="_blank" rel="noopener noreferrer">BCB/SGS</a>, <a href="https://brapi.dev/docs/acoes/historico" target="_blank" rel="noopener noreferrer">brapi/B3</a> e <a href="https://fred.stlouisfed.org/series/SP500" target="_blank" rel="noopener noreferrer">S&amp;P Dow Jones Indices, via FRED</a>. Dados e disponibilidade podem ser revisados.</p>
      </details>
    }
  </section>`,
  styles: [':host { display:block; margin-block:24px; min-width:0; } .benchmarks { min-width:0; } mat-form-field { width:100%; max-width:360px; display:block; } table { width:100%; border-collapse:collapse; } th,td { text-align:left; padding:12px; vertical-align:top; border-bottom:1px solid var(--border); } td p { margin:0 0 8px; } small { display:block; color:var(--muted); } .table-scroll { overflow-x:auto; } @media(max-width:600px) { thead { position:absolute; width:1px; height:1px; overflow:hidden; clip-path:inset(50%); } tbody { display:grid; gap:16px; } tr { display:grid; grid-template-columns:1fr 1fr; padding:8px; border:1px solid var(--border); border-radius:8px; } th { grid-column:1/-1; } td { border:0; padding:8px; min-width:0; overflow-wrap:anywhere; } td:nth-child(2)::before { content:"Retorno no intervalo"; display:block; font-size:12px; color:var(--muted); } td:nth-child(3)::before { content:"Diferença (p.p.)"; display:block; font-size:12px; color:var(--muted); } td:last-child { grid-column:1/-1; } }']
})
export class BenchmarkComparison {
  readonly portfolioId = input.required<number>(); readonly periods = input.required<readonly HistoryPeriod[]>(); readonly mode = input.required<'months' | 'years'>();
  readonly choice = signal(''); readonly row = computed(() => this.periods().find(p => p.period === this.choice()) ?? this.periods().at(-1));
  readonly result = signal<BenchmarkResponse | null>(null); readonly busy = signal(false); readonly error = signal('');
  private readonly http = inject(HttpClient); private readonly destroyRef = inject(DestroyRef); private request?: Subscription;
  constructor() { effect(() => { this.portfolioId(); this.mode(); this.row(); this.request?.unsubscribe(); this.result.set(null); this.error.set(''); this.busy.set(false); }); }
  points(value: string) { return Number(value) * 100; }
  compare() {
    const row = this.row(); if (!row || row.returns?.modifiedDietz == null || this.busy()) return;
    this.error.set(''); this.result.set(null); this.busy.set(true);
    this.request = this.http.get<BenchmarkResponse>(`/api/portfolios/${this.portfolioId()}/history/benchmarks`, { params: { mode: this.mode(), period: row.period } })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: result => { this.result.set(result); this.busy.set(false); }, error: error => { this.error.set(apiError(error)); this.busy.set(false); } });
  }
}
