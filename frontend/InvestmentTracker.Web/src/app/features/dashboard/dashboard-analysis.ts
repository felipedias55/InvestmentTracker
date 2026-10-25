import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { PercentPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PrivateCurrencyPipe } from '../../core/value-privacy';
import { MoneyChart, MoneyPoint } from '../../shared/money-chart';
import type { PortfolioHistory } from '../history/history.service';
import type { IncomeAnalysis } from '../income/income-page';
import type { Position } from '../portfolio/portfolio.service';
import { apiError } from '../../core/services/api-error';
import { EditPanel } from '../../shared/edit-panel';

@Component({
  selector: 'app-dashboard-analysis', standalone: true,
  imports: [EditPanel, MatButtonModule, MatFormFieldModule, MatInputModule, MoneyChart, PrivateCurrencyPipe, PercentPipe, RouterLink],
  template: `
    @if (loading()) { <p role="status">Carregando análise…</p> }
    @if (error()) { <p class="notice error" role="alert">{{ error() }}</p><button mat-stroked-button (click)="retry.update(increment)">Tentar novamente</button> }
    @if (kind() === 'evolution' && !loading() && !error()) {
      <div class="analysis-options">
        <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Período</mat-label><select matNativeControl [value]="mode()" (change)="mode.set($any($event.target).value)"><option value="months">Mensal</option><option value="years">Anual</option></select></mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Ano</mat-label><select matNativeControl [value]="year()" (change)="year.set($any($event.target).value)"><option value="">Todos</option>@for (y of years(); track y) { <option [value]="y">{{ y }}</option> }</select></mat-form-field>
      </div>
    }
    @if (kind() === 'evolution' && history(); as h) {
      @if (periods().length) {
        <app-money-chart title="Evolução do patrimônio" kind="line" [fitContainer]="true" [compact]="compact()" [showDataTable]="false" [currency]="currency()" [points]="evolutionPoints()" />
      } @else { <p>Sem fotografias no período selecionado.</p> }
      @if (outdated()) { <p class="notice">{{ outdated() }} fechamentos precisam de revisão. <a routerLink="/history">Conferir fechamentos →</a></p> }
      <div class="analysis-actions"><button mat-button type="button" [disabled]="!periods().length" (click)="detailsOpen.set(true)">Ver detalhes da evolução</button><a mat-button routerLink="/history">Histórico completo</a></div>
      @if (detailsOpen()) {
      <app-edit-panel [editing]="true" [wide]="true" title="Detalhes da evolução" (closed)="detailsOpen.set(false)">
      <div class="evolution-details">
      <p class="hint">Fotografias na moeda-base. Lacunas, outra moeda e fechamentos desatualizados ou reabertos interrompem a linha.</p>
      <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Detalhar fechamento</mat-label><select matNativeControl [value]="selectedPeriod()" (change)="period.set($any($event.target).value)">@for (r of periods(); track r.period) { <option [value]="r.period" [selected]="r.period === selectedPeriod()">{{ r.period }}</option> }</select></mat-form-field>
      @if (selected(); as row) {
        <p class="hint">Comparação desde {{ row.comparisonStart || 'referência indisponível' }} até {{ row.snapshotDate || 'data indisponível' }}.</p>
        @if (validPeriod()) {
          <div class="metric-grid">
            <div>Aportes líquidos no intervalo<strong>{{ row.netFlowsBetweenSnapshots == null ? 'Indisponível' : (row.netFlowsBetweenSnapshots | currency:currency():'code') }}</strong></div>
            <div>Valorização/outros efeitos<strong>{{ row.valuationAndOtherChanges == null ? 'Indisponível' : (row.valuationAndOtherChanges | currency:currency():'code') }}</strong></div>
            <div>Dietz estimado<strong>{{ row.returns?.modifiedDietz == null ? 'Indisponível' : (row.returns!.modifiedDietz | percent:'1.2-2') }}</strong></div>
            <div>XIRR anualizada<strong>{{ row.returns?.xirr == null ? 'Indisponível' : (row.returns!.xirr | percent:'1.2-2') }}</strong></div>
          </div>
          @if (row.returns?.note) { <p class="hint">{{ row.returns!.note }}</p> }
        } @else { <p class="notice">Fechamento ausente, desatualizado, reaberto ou em outra moeda. Comparação indisponível.</p> }
        @if (row.comparisonNote) { <p class="hint">{{ row.comparisonNote }}</p> }
      }
      <details><summary>Ver dados da evolução</summary>
        <div class="table-scroll" tabindex="0" role="region" aria-label="Dados da evolução patrimonial"><table>
          <thead><tr><th scope="col">Referência</th><th scope="col">Patrimônio · {{ currency() }}</th></tr></thead>
          <tbody>@for (point of evolutionPoints(); track point.label) { <tr><th scope="row">{{ point.label }}</th><td>{{ point.value === null ? 'Indisponível' : (point.value | currency:currency():'code') }}</td></tr> }</tbody>
        </table></div>
      </details>
      <a routerLink="/history">Ver evolução e métodos de cálculo →</a>
      <div class="analysis-actions detail-actions"><button mat-stroked-button type="button" (click)="detailsOpen.set(false)">Fechar detalhes</button></div>
      </div>
      </app-edit-panel>
      }
    }
    @if (kind() === 'income' && income()) {
      <div class="analysis-options">
        <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Moeda dos proventos</mat-label><select matNativeControl [value]="incomeChoice()" (change)="selectIncome($any($event.target).value)">
          @for (code of originalCurrencies(); track code) { <option [value]="'original:' + code" [selected]="incomeChoice() === 'original:' + code">{{ code }} · original</option> }
          <option [value]="'base:' + currency()" [selected]="incomeMode() === 'base'">{{ currency() }} · moeda-base</option>
        </select></mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Períodos no gráfico</mat-label><select matNativeControl [value]="windowSize()" (change)="windowSize.set(+$any($event.target).value)"><option [value]="5">5 períodos</option><option [value]="12">12 períodos</option></select></mat-form-field>
      </div>
      @if (selectedIncomeGroup(); as group) {
        <div class="window-controls"><button mat-button type="button" [disabled]="incomeStart() === 0" (click)="shiftIncomeWindow(1)" aria-label="Ver períodos anteriores de proventos">← Anteriores</button><span class="hint">{{ mode() === 'months' ? 'Mensal' : 'Anual' }} · {{ incomeStart() + 1 }}–{{ incomeEnd() }} de {{ group.points.length }}</span><button mat-button type="button" [disabled]="incomeEnd() === group.points.length" (click)="shiftIncomeWindow(-1)" aria-label="Ver períodos seguintes de proventos">Seguintes →</button></div>
        <app-money-chart title="Proventos líquidos de estornos" kind="columns" [fitContainer]="true" [compact]="compact()" [showDataTable]="false" [currency]="group.currency" [points]="incomeWindow()" />
      } @else { <p>Sem recebimentos disponíveis para esta seleção.</p> }
      @if (pendingConversions()) { <p class="notice">Há conversões pendentes. Totais incompletos aparecem como indisponíveis.</p> }
      <div class="analysis-actions"><button mat-button type="button" (click)="detailsOpen.set(true)">Ver detalhes dos proventos</button><a mat-button routerLink="/income">Histórico de proventos</a></div>
      @if (detailsOpen()) {
        <app-edit-panel [editing]="true" [wide]="true" title="Detalhes dos proventos" (closed)="detailsOpen.set(false)"><div class="evolution-details">
          <p>Recebimentos líquidos de estornos por período, em {{ selectedIncomeCurrency() }}{{ incomeMode() === 'base' ? ' (moeda-base)' : ' (moeda original)' }}. Moedas originais não são somadas entre si. Meses sem registros não são preenchidos com valores estimados.</p>
          <div class="analysis-options">
            <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Período</mat-label><select matNativeControl [value]="mode()" (change)="mode.set($any($event.target).value)"><option value="months">Mensal</option><option value="years">Anual</option></select></mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Ano</mat-label><select matNativeControl [value]="year()" (change)="year.set($any($event.target).value)"><option value="">Todos</option>@for (y of years(); track y) { <option [value]="y" [selected]="year() === y">{{ y }}</option> }</select></mat-form-field>
          </div>
          <p class="hint">Tabela completa da moeda e dos filtros selecionados, incluindo períodos fora da janela do cartão.</p>
          @if (pendingConversions()) { <p class="notice">Há conversões pendentes. Totais incompletos aparecem como indisponíveis.</p> }
          <div class="table-scroll" tabindex="0" role="region" aria-label="Dados completos dos proventos"><table><thead><tr><th scope="col">Referência</th><th scope="col">Valor · {{ selectedIncomeCurrency() }}</th></tr></thead><tbody>
            @for (point of selectedIncomeGroup()?.points ?? []; track point.label) { <tr><th scope="row">{{ point.label }}</th><td>{{ point.value === null ? 'Indisponível' : (point.value | currency:selectedIncomeCurrency():'code') }}</td></tr> } @empty { <tr><td colspan="2">Sem recebimentos disponíveis para esta seleção.</td></tr> }
          </tbody></table></div>
          <a routerLink="/income">Detalhar por ativo ou registrar recebimento →</a>
          <div class="analysis-actions detail-actions"><button mat-stroked-button type="button" (click)="detailsOpen.set(false)">Fechar detalhes</button></div>
        </div></app-edit-panel>
      }
    }
    @if (kind() === 'highlights') {
      <p class="hint">Sobre o preço médio, na moeda original. Não é a variação do mês.</p>
      @for (group of leaders(); track group.title) {
        <h3>{{ group.title }}</h3>
        @for (p of group.rows.slice(0, 3); track p.id) {
          <div class="leader"><strong>{{ p.ticker }}</strong><span [class.gain]="number(p.pricePerformance!.variation) > 0" [class.loss]="number(p.pricePerformance!.variation) < 0">{{ number(p.pricePerformance!.variation) > 0 ? '▲ +' : '▼ ' }}{{ p.pricePerformance!.variation | percent:'1.2-2' }}</span><div class="leader-track" aria-hidden="true"><i [class.negative]="number(p.pricePerformance!.variation) < 0" [style.width.%]="leaderWidth(p)" [style.left.%]="number(p.pricePerformance!.variation) > 0 ? 50 : 50 - leaderWidth(p)"></i></div></div>
        } @empty { <p class="hint">Nenhum ativo nesta condição.</p> }
      }
      <div class="analysis-actions"><button mat-button type="button" (click)="detailsOpen.set(true)">Ver ranking completo</button></div>
      @if (detailsOpen()) {
        <app-edit-panel [editing]="true" [wide]="true" title="Detalhes dos destaques" (closed)="detailsOpen.set(false)"><div class="evolution-details">
          <p>Variação acumulada sobre o preço médio cadastrado, na moeda original. Não é a variação do mês nem retorno com proventos. Posições sem quantidade ou sem custo válido não entram no ranking. O cartão mostra até três altas e três quedas; as barras usam a mesma escala para os dois grupos.</p>
          @for (group of leaders(); track group.title) {
            <h3>{{ group.title }} · {{ group.rows.length }}</h3>
            <div class="table-scroll" tabindex="0" role="region" [attr.aria-label]="group.title"><table><thead><tr><th scope="col">Ativo</th><th scope="col">Cotação unitária</th><th scope="col">Variação</th></tr></thead><tbody>
              @for (p of group.rows; track p.id) { <tr><th scope="row">{{ p.ticker }}</th><td>{{ p.pricePerformance!.unitPrice == null ? 'Indisponível' : (p.pricePerformance!.unitPrice | currency:p.currencyCode:'code':'1.2-4') }}</td><td>{{ p.pricePerformance!.variation | percent:'1.2-2' }}</td></tr> } @empty { <tr><td colspan="3">Nenhum ativo nesta condição.</td></tr> }
            </tbody></table></div>
          }
          <a routerLink="/portfolio">Conferir posições e atualizar cotações →</a>
          <div class="analysis-actions detail-actions"><button mat-stroked-button type="button" (click)="detailsOpen.set(false)">Fechar detalhes</button></div>
        </div></app-edit-panel>
      }
    }
  `,
  styles: [`
    mat-form-field { min-width: 0; width: 100%; margin: 4px 0 12px; }
    .heading mat-form-field { width: 150px; }
    .analysis-options, .metric-grid { display: grid; grid-template-columns: repeat(2,minmax(0,1fr)); gap: 12px; }
    label { display: block; font-size: 12px; } select { width: 100%; }
    .metric-grid > div { padding: 12px; background: var(--surface-soft); border-radius: 8px; font-size: 12px; }
    strong { display: block; margin-top: 5px; overflow-wrap: anywhere; }
    .leader { display: grid; grid-template-columns: minmax(0,1fr) auto; gap: 3px 10px; padding: 4px 0; }
    .leader strong { margin: 0; } .gain { color: var(--gain); } .loss { color: var(--loss); }
    small { color: var(--muted); } h3 { font-size: 14px; }
    .analysis-actions { display: flex; flex-wrap: wrap; gap: 8px; }
    .evolution-details { padding: 16px; }
    .evolution-details details { margin: 16px 0; }
    .evolution-details td { white-space: nowrap; }
    .detail-actions { justify-content: flex-end; margin-top: 16px; }
    .window-controls { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 4px; }
    .leader-track { grid-column: 1/-1; height: 6px; position: relative; background: var(--border); }
    .leader-track::after { content: ''; position: absolute; left: 50%; height: 100%; border-left: 1px solid var(--muted); }
    .leader-track i { position: absolute; height: 100%; background: var(--gain); }
    .leader-track i.negative { background: var(--loss); }
    @media(max-width: 500px) { .evolution-details .metric-grid { grid-template-columns: 1fr; } }
  `]
})
export class DashboardAnalysis {
  private readonly http = inject(HttpClient);
  readonly kind = input.required<'evolution' | 'income' | 'highlights'>();
  readonly compact = input(false);
  readonly portfolioId = input.required<number>();
  readonly currency = input.required<string>();
  readonly positions = input<Position[]>([]);
  readonly historyLoaded = output<PortfolioHistory | null>();
  readonly incomeLoaded = output<IncomeAnalysis | null>();
  readonly loadFailed = output<string>();
  readonly history = signal<PortfolioHistory | null>(null);
  readonly income = signal<IncomeAnalysis | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly retry = signal(0);
  readonly increment = (value: number) => value + 1;
  readonly mode = signal<'months' | 'years'>('months');
  readonly year = signal('');
  readonly period = signal('');
  readonly detailsOpen = signal(false);
  readonly incomeMode = signal<'original' | 'base'>('original');
  readonly incomeCurrency = signal('');
  readonly windowSize = signal(5);
  readonly windowOffset = signal(0);
  readonly number = (value: string | null | undefined) => Number(value);
  constructor() {
    effect(onCleanup => {
      const id = this.portfolioId(), kind = this.kind(); this.retry();
      this.detailsOpen.set(false);
      this.history.set(null); this.income.set(null); this.error.set(''); this.loading.set(false);
      this.loadFailed.emit('');
      if (kind === 'evolution') this.historyLoaded.emit(null);
      if (kind === 'income') this.incomeLoaded.emit(null);
      if (kind === 'highlights') return;
      this.loading.set(true);
      const path = kind === 'evolution' ? 'history' : 'income/analysis';
      const request = this.http.get<PortfolioHistory | IncomeAnalysis>(`/api/portfolios/${id}/${path}`).subscribe({
        next: data => { if (kind === 'evolution') { this.history.set(data as PortfolioHistory); this.historyLoaded.emit(data as PortfolioHistory); } else { this.income.set(data as IncomeAnalysis); this.incomeLoaded.emit(data as IncomeAnalysis); } this.loading.set(false); },
        error: e => { this.loadFailed.emit(apiError(e)); this.error.set(apiError(e)); this.loading.set(false); }
      });
      onCleanup(() => request.unsubscribe());
    });
    effect(() => { this.incomeGroups(); this.incomeCurrency(); this.windowSize(); this.windowOffset.set(0); });
  }
  readonly years = computed(() => [...new Set((this.kind() === 'evolution' ? this.history()?.months : this.income()?.months)?.map(r => r.period.slice(0,4)) ?? [])].sort().reverse());
  readonly periods = computed(() => (this.history()?.[this.mode()] ?? []).filter(r => !this.year() || r.period.startsWith(this.year())).slice().sort((a,b) => a.period.localeCompare(b.period)));
  readonly selectedPeriod = computed(() => this.periods().some(r => r.period === this.period()) ? this.period() : this.periods().at(-1)?.period ?? '');
  readonly selected = computed(() => this.periods().find(r => r.period === this.selectedPeriod()));
  readonly validPeriod = computed(() => { const r = this.selected(); return !!r && r.snapshotId !== null && !r.isOutdated && !r.isReopened && r.currencyCode === this.currency(); });
  readonly outdated = computed(() => this.periods().filter(r => r.isOutdated || r.isReopened).length);
  readonly evolutionPoints = computed(() => this.periods().map(r => ({ label: r.period, value: r.snapshotId === null || r.totalWealth === null || r.isOutdated || r.isReopened || r.currencyCode !== this.currency() ? null : Number(r.totalWealth) })));
  readonly incomeRows = computed(() => {
    const data = this.income();
    return (this.incomeMode() === 'original' ? data?.[this.mode()] : this.mode() === 'months' ? data?.convertedMonths : data?.convertedYears)?.filter(r => (!this.year() || r.period.startsWith(this.year())) && (this.incomeMode() !== 'base' || r.currencyCode === this.currency())) ?? [];
  });
  readonly pendingConversions = computed(() => this.incomeRows().some(r => r.net === null || (r.missingConversions ?? 0) > 0));
  readonly incomeGroups = computed(() => {
    const groups = new Map<string, Map<string, MoneyPoint>>();
    for (const row of this.incomeRows()) {
      const points = groups.get(row.currencyCode) ?? new Map<string, MoneyPoint>();
      const previous = points.get(row.period);
      points.set(row.period, { label: row.period, value: row.net === null || (row.missingConversions ?? 0) > 0 || previous?.value === null ? null : (previous?.value ?? 0) + Number(row.net) });
      groups.set(row.currencyCode, points);
    }
    return [...groups].sort(([a],[b]) => a.localeCompare(b)).map(([currency, points]) => ({ currency, points: [...points.values()].sort((a,b) => a.label.localeCompare(b.label)) }));
  });
  readonly originalCurrencies = computed(() => {
    const codes = [...new Set((this.income()?.[this.mode()] ?? []).map(row => row.currencyCode))].sort();
    return codes.length ? codes : [this.currency()];
  });
  readonly selectedIncomeCurrency = computed(() => this.incomeMode() === 'base' ? this.currency() : this.originalCurrencies().includes(this.incomeCurrency()) ? this.incomeCurrency() : this.originalCurrencies().includes(this.currency()) ? this.currency() : this.originalCurrencies()[0] ?? this.currency());
  readonly incomeChoice = computed(() => `${this.incomeMode()}:${this.selectedIncomeCurrency()}`);
  readonly selectedIncomeGroup = computed(() => this.incomeGroups().find(group => group.currency === this.selectedIncomeCurrency()));
  readonly incomeEnd = computed(() => Math.max(0, (this.selectedIncomeGroup()?.points.length ?? 0) - this.windowOffset()));
  readonly incomeStart = computed(() => Math.max(0, this.incomeEnd() - this.windowSize()));
  readonly incomeWindow = computed(() => this.selectedIncomeGroup()?.points.slice(this.incomeStart(), this.incomeEnd()) ?? []);
  selectIncome(choice: string) {
    const [mode, code] = choice.split(':');
    if ((mode !== 'original' || !this.originalCurrencies().includes(code)) && (mode !== 'base' || code !== this.currency())) return;
    this.incomeMode.set(mode); this.incomeCurrency.set(code); this.windowOffset.set(0);
  }
  shiftIncomeWindow(direction: number) {
    const count = this.selectedIncomeGroup()?.points.length ?? 0;
    this.windowOffset.update(offset => Math.max(0, Math.min(Math.max(0, count - 1), offset + direction * this.windowSize())));
  }
  readonly leaderScale = computed(() => Math.max(0.000001, ...this.leaders().flatMap(group => group.rows.map(p => Math.abs(Number(p.pricePerformance!.variation))))));
  leaderWidth(position: Pick<Position, 'pricePerformance'>) { return Math.abs(Number(position.pricePerformance!.variation)) / this.leaderScale() * 50; }
  readonly leaders = computed(() => {
    const eligible = this.positions().filter(p => Number(p.quantity) > 0 && p.pricePerformance?.variation != null && Number.isFinite(Number(p.pricePerformance.variation)));
    return [{ title: 'Maiores altas', rows: eligible.filter(p => Number(p.pricePerformance!.variation) > 0).sort((a,b) => Number(b.pricePerformance!.variation) - Number(a.pricePerformance!.variation) || a.ticker.localeCompare(b.ticker)) },
      { title: 'Maiores quedas', rows: eligible.filter(p => Number(p.pricePerformance!.variation) < 0).sort((a,b) => Number(a.pricePerformance!.variation) - Number(b.pricePerformance!.variation) || a.ticker.localeCompare(b.ticker)) }];
  });
}
