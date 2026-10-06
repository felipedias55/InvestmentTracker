import { PrivateCurrencyPipe as CurrencyPipe } from '../../core/value-privacy';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { PortfolioPicker } from '../../shared/portfolio-picker';
import { AllocationTable } from '../allocation/allocation-table';
import { AllocationRow, AllocationService, Dashboard } from '../allocation/allocation.service';
import { apiError } from '../../core/services/api-error';

@Component({
  standalone: true,
  selector: 'app-dashboard-page',
  imports: [CurrencyPipe, PortfolioPicker, AllocationTable, DatePipe, RouterLink],
  templateUrl: './dashboard-page.html',
  styles: [
    `
      .totals {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
        gap: 16px;
        margin-top: 24px;
      }
      .totals strong {
        font-size: 22px;
        overflow-wrap: anywhere;
      }
      .totals .wealth-card { background: var(--gold-metal); color: #30270f; grid-column: span 2; }
      .wealth-card h2, .wealth-card .hint { color: #514017; }
      .wealth-card strong { font-size: clamp(28px, 4vw, 38px); }
      .overview-actions { display: flex; gap: 10px; flex-wrap: wrap; margin: 20px 0; }
      .overview-actions a { text-decoration: none; padding: 10px 14px; border: 1px solid var(--border); border-radius: 8px; background: white; }
      .quality-strip { display: grid; grid-template-columns: repeat(auto-fit, minmax(170px, 1fr)); gap: 12px; margin-top: 18px; }
      .quality-strip div { padding: 16px; background: #fff; border: 1px solid var(--border); border-radius: 12px; }
      .quality-strip strong { display: block; margin-bottom: 6px; } .quality-strip span { color: var(--muted); font-size: 12px; }
      .chart-heading select { width: auto; max-width: 180px; } .chart-heading label { margin: 0; }
      @media(max-width: 600px) { .totals { grid-template-columns: 1fr; } .totals .wealth-card { grid-column: auto; } }
    `,
  ],
})
export class DashboardPage {
  private readonly api = inject(AllocationService);
  private readonly destroyRef = inject(DestroyRef);
  private request?: Subscription;
  readonly portfolioId = signal<number | null>(null);
  readonly data = signal<Dashboard | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly query = signal('');
  readonly status = signal('all');
  readonly sort = signal('value');
  readonly order = signal<ChartId[]>(['categories', 'sectors', 'countries']);
  readonly collapsed = signal<ChartId[]>([]);
  readonly announcement = signal('');
  readonly chartTypes = signal<Record<ChartId, 'bars' | 'donut'>>({ categories: 'donut', sectors: 'bars', countries: 'donut' });
  readonly oldestUpdate = computed(() => this.data()?.summary.positions.map(p => p.updatedOn).filter((x): x is string => !!x).sort()[0] ?? null);
  readonly missingDates = computed(() => this.data()?.summary.positions.filter(p => !p.updatedOn).length ?? 0);
  setChartType(id: ChartId, type: string) {
    if (type !== 'bars' && type !== 'donut') return;
    this.chartTypes.update(types => ({ ...types, [id]: type })); this.saveLayout();
  }
  readonly titles = { categories: 'Distribuição por categoria', sectors: 'Distribuição por setor', countries: 'Distribuição por país' };
  private storageKey() { return `investment-tracker.dashboard.v1.${this.portfolioId()}`; }
  private saveLayout() {
    try { localStorage.setItem(this.storageKey(), JSON.stringify({ order: this.order(), collapsed: this.collapsed(), types: this.chartTypes() })); } catch { /* Preferences remain available for this session. */ }
  }
  resetLayout() {
    this.chartTypes.set({ categories: 'donut', sectors: 'bars', countries: 'donut' });
    this.order.set(['categories', 'sectors', 'countries']);
    this.collapsed.set([]);
    this.saveLayout();
  }
  toggleChart(id: ChartId) {
    this.collapsed.update(items => items.includes(id) ? items.filter(item => item !== id) : [...items, id]);
    this.saveLayout();
  }
  moveChart(id: ChartId, direction: number) {
    const items = [...this.order()];
    const index = items.indexOf(id);
    const destination = index + direction;
    if (destination < 0 || destination >= items.length) return;
    [items[index], items[destination]] = [items[destination], items[index]];
    this.order.set(items);
    this.saveLayout();
    this.announcement.set(`${this.titles[id]} na posição ${destination + 1} de 3.`);
  }
  clearFilters() { this.query.set(''); this.status.set('all'); this.sort.set('value'); }
  filteredRows(id: ChartId): AllocationRow[] {
    const normalize = (value: string) => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('pt-BR');
    const query = normalize(this.query().trim());
    return (this.data()?.allocation[id] ?? []).filter(row => normalize(row.name).includes(query))
      .filter(row => id === 'countries' || this.status() === 'all' ||
        (this.status() === 'missing' ? row.targetPercentage === null : row.difference !== null &&
          (this.status() === 'below' ? Number(row.difference) > 0 : Number(row.difference) < 0)))
      .sort((a, b) => this.sort() === 'name' ? a.name.localeCompare(b.name, 'pt-BR') :
        a.currentValue === null ? (b.currentValue === null ? 0 : 1) : b.currentValue === null ? -1 : Number(b.currentValue) - Number(a.currentValue));
  }
  select(id: number) {
    this.portfolioId.set(id);
    this.clearFilters();
    const defaults: ChartId[] = ['categories', 'sectors', 'countries'];
    this.order.set(defaults);
    this.collapsed.set([]);
    try {
      this.chartTypes.set({ categories: 'donut', sectors: 'bars', countries: 'donut' });
      const stored = JSON.parse(localStorage.getItem(this.storageKey()) ?? 'null');
      if (stored && Array.isArray(stored.order) && Array.isArray(stored.collapsed)) {
        for (const id of defaults) {
          if (stored.types?.[id] === 'bars' || stored.types?.[id] === 'donut') this.chartTypes.update(types => ({ ...types, [id]: stored.types[id] }));
        }
        const valid = (value: unknown): value is ChartId => defaults.includes(value as ChartId);
        const order: ChartId[] = [...new Set<ChartId>(stored.order.filter(valid))];
        this.order.set([...order, ...defaults.filter(id => !order.includes(id))]);
        this.collapsed.set([...new Set<ChartId>(stored.collapsed.filter(valid))]);
      }
    } catch { /* Invalid or unavailable storage uses the default layout. */ }
    this.load();
  }
  load() {
    const id = this.portfolioId();
    if (id === null) return;
    this.request?.unsubscribe();
    this.loading.set(true);
    this.data.set(null);
    this.error.set('');
    this.request = this.api
      .dashboard(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (data) => {
          this.data.set(data);
          this.loading.set(false);
        },
        error: (e) => {
          this.error.set(apiError(e));
          this.loading.set(false);
        },
      });
  }
}

type ChartId = 'categories' | 'sectors' | 'countries';
