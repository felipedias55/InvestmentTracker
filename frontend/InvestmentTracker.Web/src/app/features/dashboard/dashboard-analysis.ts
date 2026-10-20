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

@Component({
  selector: 'app-dashboard-analysis', standalone: true,
  imports: [MoneyChart, PrivateCurrencyPipe, PercentPipe, RouterLink],
  template: `
    @if (loading()) { <p role="status">Carregando análise…</p> }
    @if (error()) { <p class="notice error" role="alert">{{ error() }}</p><button (click)="retry.update(increment)">Tentar novamente</button> }
    @if (kind() !== 'highlights' && !loading() && !error()) {
      <div class="analysis-options">
        <label>Período<select [value]="mode()" (change)="mode.set($any($event.target).value)"><option value="months">Mensal</option><option value="years">Anual</option></select></label>
        <label>Ano<select [value]="year()" (change)="year.set($any($event.target).value)"><option value="">Todos</option>@for (y of years(); track y) { <option [value]="y">{{ y }}</option> }</select></label>
      </div>
    }
    @if (kind() === 'evolution' && history(); as h) {
      <app-money-chart title="Evolução do patrimônio" kind="line" [currency]="currency()" [points]="evolutionPoints()" />
      <p class="hint">Fotografias na moeda-base. Lacunas, outra moeda e fechamentos desatualizados ou reabertos interrompem a linha.</p>
      @if (outdated()) { <p class="notice">{{ outdated() }} fechamentos precisam de revisão. <a routerLink="/history">Conferir fechamentos →</a></p> }
      <label>Detalhar fechamento<select [value]="selectedPeriod()" (change)="period.set($any($event.target).value)">@for (r of periods(); track r.period) { <option [value]="r.period">{{ r.period }}</option> }</select></label>
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
      @if (!periods().length) { <p>Sem fotografias no período selecionado.</p> }
      <a routerLink="/history">Ver evolução e métodos de cálculo →</a>
    }
    @if (kind() === 'income' && income()) {
      <label>Exibição<select [value]="incomeMode()" (change)="incomeMode.set($any($event.target).value)"><option value="original">Moedas originais</option><option value="base">Moeda-base</option></select></label>
      @for (group of incomeGroups(); track group.currency) {
        <app-money-chart title="Proventos líquidos de estornos" kind="columns" [currency]="group.currency" [points]="group.points" />
      } @empty { <p>Sem recebimentos disponíveis para esta seleção.</p> }
      @if (pendingConversions()) { <p class="notice">Há conversões pendentes. Totais incompletos aparecem como indisponíveis.</p> }
      <p class="hint">Recebimentos líquidos de estornos por período. Moedas originais não são somadas entre si. Meses sem registros não são preenchidos com valores estimados.</p>
      <a routerLink="/income">Detalhar por ativo ou registrar recebimento →</a>
    }
    @if (kind() === 'highlights') {
      <p class="hint">Variação acumulada sobre o preço médio cadastrado, na moeda original. Não é a variação do mês nem retorno com proventos.</p>
      @for (group of leaders(); track group.title) {
        <h3>{{ group.title }}</h3>
        @for (p of group.rows; track p.id) {
          <div class="leader"><strong>{{ p.ticker }}</strong><span [class.gain]="number(p.pricePerformance!.variation) > 0" [class.loss]="number(p.pricePerformance!.variation) < 0">{{ number(p.pricePerformance!.variation) > 0 ? '▲ +' : '▼ ' }}{{ p.pricePerformance!.variation | percent:'1.2-2' }}</span><small>{{ p.pricePerformance!.unitPrice | currency:p.currencyCode:'code':'1.2-4' }}</small></div>
        } @empty { <p class="hint">Nenhum ativo nesta condição.</p> }
      }
      <p class="hint">Posições sem quantidade ou sem custo válido não entram no ranking.</p>
      <a routerLink="/portfolio">Conferir posições e atualizar cotações →</a>
    }
  `,
  styles: [`
    .analysis-options, .metric-grid { display: grid; grid-template-columns: repeat(2,minmax(0,1fr)); gap: 12px; }
    label { display: block; font-size: 12px; } select { width: 100%; }
    .metric-grid > div { padding: 12px; background: #f5f6f8; border-radius: 8px; font-size: 12px; }
    strong { display: block; margin-top: 5px; overflow-wrap: anywhere; }
    .leader { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 10px; padding: 10px 0; border-bottom: 1px solid var(--border); }
    .leader strong { margin: 0; } .gain { color: #17623a; } .loss { color: #a12e36; }
    small { color: var(--muted); } h3 { font-size: 14px; }
  `]
})
export class DashboardAnalysis {
  private readonly http = inject(HttpClient);
  readonly kind = input.required<'evolution' | 'income' | 'highlights'>();
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
  readonly incomeMode = signal<'original' | 'base'>('original');
  readonly number = (value: string | null | undefined) => Number(value);
  constructor() {
    effect(onCleanup => {
      const id = this.portfolioId(), kind = this.kind(); this.retry();
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
  readonly leaders = computed(() => {
    const eligible = this.positions().filter(p => Number(p.quantity) > 0 && p.pricePerformance?.variation != null && Number.isFinite(Number(p.pricePerformance.variation)));
    return [{ title: 'Maiores altas', rows: eligible.filter(p => Number(p.pricePerformance!.variation) > 0).sort((a,b) => Number(b.pricePerformance!.variation) - Number(a.pricePerformance!.variation) || a.ticker.localeCompare(b.ticker)).slice(0,5) },
      { title: 'Maiores quedas', rows: eligible.filter(p => Number(p.pricePerformance!.variation) < 0).sort((a,b) => Number(a.pricePerformance!.variation) - Number(b.pricePerformance!.variation) || a.ticker.localeCompare(b.ticker)).slice(0,5) }];
  });
}
