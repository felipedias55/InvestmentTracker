import { Component, computed, input, signal } from '@angular/core';
import { DatePipe, PercentPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PrivateCurrencyPipe } from '../../core/value-privacy';
import type { PortfolioHistory } from '../history/history.service';
import type { IncomeAnalysis, IncomeTotal } from '../income/income-page';

// Presentation arithmetic preserves the API's decimal amounts (six decimal places).
export function decimalTotal(values: (string | null | undefined)[]): string | null {
  if (values.some(v => v == null || !/^-?\d+(\.\d{1,6})?$/.test(v))) return null;
  const total = values.reduce((sum, value) => {
    const [whole, fraction = ''] = value!.replace('-', '').split('.');
    return sum + (BigInt(whole) * 1000000n + BigInt(fraction.padEnd(6, '0'))) * (value!.startsWith('-') ? -1n : 1n);
  }, 0n);
  const absolute = total < 0 ? -total : total;
  return (total < 0 ? '-' : '') + String(absolute / 1000000n) + '.' + String(absolute % 1000000n).padStart(6, '0');
}
export function previousPeriod(period: string): string {
  if (/^\d{4}$/.test(period)) return String(Number(period) - 1);
  const [year, month] = period.split('-').map(Number);
  return month === 1 ? (year - 1) + '-12' : year + '-' + String(month - 1).padStart(2, '0');
}
export function incomeTotal(rows: IncomeTotal[] | undefined, period: string, currency: string): string | null {
  const selected = rows?.filter(r => r.period === period && r.currencyCode === currency);
  // No row means absence of a comparable reference, not an invented zero.
  if (!selected?.length || selected.some(r => (r.missingConversions ?? 0) > 0)) return null;
  return decimalTotal(selected.map(r => r.net));
}
@Component({
  selector: 'app-dashboard-summary', standalone: true,
  imports: [PrivateCurrencyPipe, DatePipe, PercentPipe, RouterLink],
  template: `
    <section class="panel period-summary" aria-label="Resumo por período">
      <div class="heading"><h2>Resumo por período</h2>
        <label>Frequência<select [value]="mode()" (change)="mode.set($any($event.target).value); period.set('')"><option value="months">Mensal</option><option value="years">Anual</option></select></label>
        <label>Período<select [value]="selectedPeriod()" (change)="period.set($any($event.target).value)">
          @for (p of options(); track p) { <option [value]="p">{{ p }}</option> }
        </select></label>
      </div>
      @if (!history() && !historyError()) { <p role="status">Carregando resumo histórico…</p> }
      @if (historyError()) { <p class="notice error">{{ historyError() }} <a routerLink="/history">Consultar histórico →</a></p> }
      @if (incomeError()) { <p class="notice error">{{ incomeError() }} <a routerLink="/income">Consultar proventos →</a></p> }
      @if (selectedPeriod()) {
        <p class="hint">{{ currency() }} · {{ selectedPeriod() }}{{ partial() ? ' · período em andamento: dados parciais' : '' }}. Patrimônio e valorização dependem das fotografias; fluxos e proventos usam o período civil selecionado.</p>
        <div class="metrics">
          <div>Patrimônio na fotografia<strong>{{ wealth() === null ? 'Indisponível' : (wealth() | currency:currency():'code') }}</strong><small>{{ row()?.snapshotDate ? (row()!.snapshotDate | date:'dd/MM/yyyy':'UTC') : 'Sem fotografia' }}</small></div>
          <div>Aportes líquidos do período<strong>{{ flows() === null ? 'Indisponível' : (flows() | currency:currency():'code') }}</strong><small>Entradas menos retiradas</small></div>
          <div>Proventos líquidos do período<strong>{{ currentIncome() === null ? 'Indisponível' : (currentIncome() | currency:currency():'code') }}</strong><small>Recebimentos menos estornos</small></div>
          <div>Valorização/outros efeitos<strong>{{ valuation() === null ? 'Indisponível' : (valuation() | currency:currency():'code') }}</strong><small>Entre {{ row()?.comparisonStart || '—' }} e {{ row()?.snapshotDate || '—' }}</small></div>
        </div>
        <details><summary>Comparar proventos com {{ previous() }}</summary>
          <p>Anterior: {{ priorIncome() === null ? 'Indisponível' : (priorIncome() | currency:currency():'code') }} · Atual: {{ currentIncome() === null ? 'Indisponível' : (currentIncome() | currency:currency():'code') }}</p>
          <p>Diferença: {{ incomeChange() === null ? 'Indisponível' : (incomeChange() | currency:currency():'code') }} · Variação: {{ incomeRatio() === null ? 'Sem base percentual positiva' : (incomeRatio() | percent:'1.2-2') }}</p>
          @if (partial()) { <p class="hint">O período atual ainda não terminou; esta comparação não é entre intervalos de mesma duração.</p> }
          <p class="hint">Sem registros ou com conversão incompleta, a referência permanece indisponível. O comparativo usa o período civil imediatamente anterior.</p>
        </details>
        @if (row()?.comparisonNote) { <p class="hint">{{ row()!.comparisonNote }}</p> }
      }
      @if (outdated()) { <p class="notice">{{ outdated() }} fotografias precisam de revisão. <a routerLink="/history">Revisar fechamentos →</a></p> }
    </section>
  `,
  styles: [`
    .period-summary { margin: 16px 0; padding: 16px; }
    .heading { display: flex; align-items: end; gap: 12px; flex-wrap: wrap; }
    h2 { flex: 1; font-size: 18px; margin: 0 0 10px; }
    label { font-size: 12px; } select { min-width: 100px; }
    .metrics { display: grid; grid-template-columns: repeat(4,minmax(0,1fr)); gap: 12px; margin: 12px 0; }
    .metrics > div { background: #f5f6f8; padding: 12px; border-radius: 8px; font-size: 12px; }
    strong { display: block; margin: 8px 0; font-size: 18px; overflow-wrap: anywhere; }
    small { color: var(--muted); } summary { cursor: pointer; }
    @media(max-width: 1000px) { .metrics { grid-template-columns: repeat(2,minmax(0,1fr)); } }
    @media(max-width: 380px) { .metrics { grid-template-columns: 1fr; } }
  `]
})
export class DashboardSummary {
  readonly history = input<PortfolioHistory | null>(null);
  readonly income = input<IncomeAnalysis | null>(null);
  readonly historyError = input('');
  readonly incomeError = input('');
  readonly currency = input.required<string>();
  readonly mode = signal<'months' | 'years'>('months');
  readonly period = signal('');
  readonly options = computed(() => [...new Set([
    ...(this.history()?.[this.mode()] ?? []).map(r => r.period),
    ...(this.income()?.[this.mode()] ?? []).map(r => r.period)
  ])].sort().reverse());
  readonly selectedPeriod = computed(() => this.options().includes(this.period()) ? this.period() : this.options()[0] ?? '');
  readonly row = computed(() => this.history()?.[this.mode()].find(r => r.period === this.selectedPeriod()));
  readonly valid = computed(() => { const r = this.row(); return !!r && r.snapshotId !== null && !r.isOutdated && !r.isReopened && r.currencyCode === this.currency(); });
  readonly wealth = computed(() => this.valid() ? this.row()!.totalWealth : null);
  readonly valuation = computed(() => this.valid() ? this.row()!.valuationAndOtherChanges ?? null : null);
  readonly flows = computed(() => {
    const r = this.row();
    if (!r || r.currencyCode !== this.currency() || r.contributions == null || r.withdrawals == null) return null;
    return decimalTotal([r.contributions, r.withdrawals.startsWith('-') ? r.withdrawals.slice(1) : '-' + r.withdrawals]);
  });
  readonly conversionRows = computed(() => this.mode() === 'months' ? this.income()?.convertedMonths : this.income()?.convertedYears);
  readonly currentIncome = computed(() => incomeTotal(this.conversionRows(), this.selectedPeriod(), this.currency()));
  readonly previous = computed(() => this.selectedPeriod() ? previousPeriod(this.selectedPeriod()) : '');
  readonly priorIncome = computed(() => incomeTotal(this.conversionRows(), this.previous(), this.currency()));
  readonly incomeChange = computed(() => {
    const before = this.priorIncome();
    return before === null ? null : decimalTotal([this.currentIncome(), before.startsWith('-') ? before.slice(1) : '-' + before]);
  });
  readonly incomeRatio = computed(() => this.incomeChange() !== null && Number(this.priorIncome()) > 0 ? Number(this.incomeChange()) / Number(this.priorIncome()) : null);
  readonly partial = computed(() => !!this.history()?.today && this.selectedPeriod() === this.history()!.today.slice(0,this.mode() === 'months' ? 7 : 4));
  readonly outdated = computed(() => this.history()?.months.filter(r => r.isOutdated || r.isReopened).length ?? 0);
}
