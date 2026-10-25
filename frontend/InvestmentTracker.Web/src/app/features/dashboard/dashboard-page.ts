import { SectionLink } from '../../shared/section-link';
import { rememberContext } from '../../shared/page-context';
import { AppIcon } from '../../shared/app-icon';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { DashboardSummary } from './dashboard-summary';
import { DashboardMovements } from './dashboard-movements';
import type { PortfolioHistory } from '../history/history.service';
import type { IncomeAnalysis } from '../income/income-page';
import { DashboardAnalysis } from './dashboard-analysis';
import { PrivateCurrencyPipe as CurrencyPipe } from '../../core/value-privacy';
import { Component, DestroyRef, ElementRef, HostListener, Injector, afterNextRender, computed, inject, signal } from '@angular/core';
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
  imports: [SectionLink, AppIcon, MatButtonModule, MatFormFieldModule, MatInputModule, DashboardSummary, DashboardMovements, DashboardAnalysis, CurrencyPipe, PortfolioPicker, AllocationTable, DatePipe, RouterLink],
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.css',

})
export class DashboardPage {
  private readonly context = rememberContext('dashboard', () => ({ query: this.query(), status: this.status(), sort: this.sort() }), state => { this.query.set(state.query); this.status.set(state.status); this.sort.set(state.sort); }, () => !this.loading());

  private readonly api = inject(AllocationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);
  private dragPreview: HTMLElement | null = null;
  private dragFocus: HTMLElement | null = null;
  constructor() { this.destroyRef.onDestroy(() => this.removePreview()); }
  private request?: Subscription;
  readonly portfolioId = signal<number | null>(null);
  readonly data = signal<Dashboard | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly periodHistory = signal<PortfolioHistory | null>(null);
  readonly periodIncome = signal<IncomeAnalysis | null>(null);
  readonly historyError = signal('');
  readonly incomeError = signal('');
  analysisError(id: ChartId, message: string) {
    if (id === 'evolution') this.historyError.set(message);
    if (id === 'income') this.incomeError.set(message);
  }
  readonly query = signal('');
  readonly status = signal('all');
  readonly sort = signal('value');
  readonly order = signal<ChartId[]>(['categories', 'sectors', 'countries', 'evolution', 'income', 'highlights', 'movements']);
  readonly collapsed = signal<ChartId[]>(['movements']);
  readonly announcement = signal('');
  readonly organizing = signal(false);
  readonly dragged = signal<ChartId | null>(null);
  readonly dropTarget = signal<ChartId | null>(null);
  readonly sizes = signal<Record<ChartId, string>>({ categories: 'medium', sectors: 'medium', countries: 'compact', evolution: 'wide', income: 'medium', highlights: 'medium', movements: 'medium' });
  setSize(id: ChartId, size: string) {
    if (!['compact', 'medium', 'wide'].includes(size)) return;
    this.sizes.update(sizes => ({ ...sizes, [id]: size })); this.saveLayout();
  }
  toggleOrganizing() { this.organizing.update(value => !value); this.endDrag(); }
  startDrag(event: DragEvent, id: ChartId) {
    if (!this.organizing() || window.matchMedia('(max-width: 700px)').matches) { event.preventDefault(); return; }
    this.dragged.set(id);
    this.dragFocus = event.currentTarget instanceof HTMLElement ? event.currentTarget : null;
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = 'move'; event.dataTransfer.setData('text/plain', id);
      this.removePreview();
      const preview = document.createElement('div');
      preview.textContent = this.titles[id]; preview.setAttribute('aria-hidden', 'true');
      Object.assign(preview.style, { position: 'fixed', left: '0', top: '0', zIndex: '9999', pointerEvents: 'none',
        background: 'var(--surface)', color: 'var(--ink)', border: '2px solid var(--accent)', borderRadius: '8px',
        padding: '12px 16px', font: '600 14px sans-serif', width: '280px', boxSizing: 'border-box' });
      document.body.append(preview); this.dragPreview = preview;
      const handle = this.dragFocus?.getBoundingClientRect();
      const x = Math.max(0, Math.min(280, event.clientX - (handle?.left ?? event.clientX)));
      const y = Math.max(0, Math.min(preview.getBoundingClientRect().height, event.clientY - (handle?.top ?? event.clientY)));
      event.dataTransfer.setDragImage(preview, x, y);
      requestAnimationFrame(() => { if (this.dragPreview === preview) this.removePreview(); });
    }
  }
  overChart(event: DragEvent, id: ChartId) {
    if (!this.organizing() || !this.dragged()) return;
    if (this.dragged() === id) { this.dropTarget.set(null); return; }
    event.preventDefault(); this.dropTarget.set(id);
    if (event.dataTransfer) event.dataTransfer.dropEffect = 'move';
  }
  dropChart(event: DragEvent, target: ChartId) {
    event.preventDefault();
    const source = this.dragged();
    if (this.organizing() && source && source !== target) {
      const items = [...this.order()];
      const destination = items.indexOf(target);
      items.splice(items.indexOf(source), 1);
      items.splice(destination, 0, source);
      this.order.set(items); this.saveLayout();
      this.announcement.set(`${this.titles[source]} na posição ${destination + 1} de ${items.length}.`);
      this.focusChart(source, '.drag-handle');
    }
    this.endDrag();
  }
  private removePreview() { this.dragPreview?.remove(); this.dragPreview = null; }
  endDrag() {
    const focus = this.dragFocus; this.dragFocus = null;
    this.dragged.set(null); this.dropTarget.set(null); this.removePreview();
    if (focus?.isConnected) focus.focus({ preventScroll: true });
  }
  @HostListener('document:keydown.escape') cancelDrag() { this.endDrag(); }
  private focusChart(id: ChartId, selector: string) {
    afterNextRender(() => {
      let control = this.element.nativeElement.querySelector<HTMLElement>(`[data-chart-id="${id}"] ${selector}`);
      const card = control?.closest('.chart-card');
      if (!control || !(card instanceof HTMLElement)) return;
      if (control instanceof HTMLButtonElement && control.disabled) control = card.querySelector<HTMLElement>('[data-move]:not(:disabled)') ?? control;
      control.focus({ preventScroll: true });
      const rect = control.getBoundingClientRect();
      if (rect.top < 80 || rect.bottom > window.innerHeight) control.scrollIntoView({ block: 'nearest', behavior: 'instant' });
    }, { injector: this.injector });
  }
  readonly chartTypes = signal<Record<AllocationChartId, 'bars' | 'donut'>>({ categories: 'donut', sectors: 'bars', countries: 'donut' });
  readonly oldestUpdate = computed(() => this.data()?.summary.positions.map(p => p.updatedOn).filter((x): x is string => !!x).sort()[0] ?? null);
  readonly missingDates = computed(() => this.data()?.summary.positions.filter(p => !p.updatedOn).length ?? 0);
  setChartType(id: AllocationChartId, type: string) {
    if (type !== 'bars' && type !== 'donut') return;
    this.chartTypes.update(types => ({ ...types, [id]: type })); this.saveLayout();
  }
  readonly titles = { categories: 'Distribuição por categoria', sectors: 'Distribuição por setor', countries: 'Distribuição por país', evolution: 'Evolução patrimonial', income: 'Proventos por período', highlights: 'Destaques dos ativos', movements: 'Movimentações recentes' };
  private storageKey() { return `investment-tracker.dashboard.v1.${this.portfolioId()}`; }
  private saveLayout() {
    try { localStorage.setItem(this.storageKey(), JSON.stringify({ order: this.order(), collapsed: this.collapsed(), types: this.chartTypes(), sizes: this.sizes() })); } catch { /* Preferences remain available for this session. */ }
  }
  resetLayout() {
    this.endDrag();
    this.sizes.set({ categories: 'medium', sectors: 'medium', countries: 'compact', evolution: 'wide', income: 'medium', highlights: 'medium', movements: 'medium' });
    this.chartTypes.set({ categories: 'donut', sectors: 'bars', countries: 'donut' });
    this.order.set(['categories', 'sectors', 'countries', 'evolution', 'income', 'highlights', 'movements']);
    this.collapsed.set(['movements']);
    this.saveLayout();
  }
  toggleChart(id: ChartId) {
    this.collapsed.update(items => items.includes(id) ? items.filter(item => item !== id) : [...items, id]);
    this.saveLayout();
  }
  moveChart(id: ChartId, direction: number) {
    if (!this.organizing() || (direction !== -1 && direction !== 1)) return;
    const items = [...this.order()];
    const index = items.indexOf(id);
    const destination = index + direction;
    if (destination < 0 || destination >= items.length) return;
    [items[index], items[destination]] = [items[destination], items[index]];
    this.order.set(items);
    this.saveLayout();
    this.announcement.set(`${this.titles[id]} na posição ${destination + 1} de ${items.length}.`);
    this.focusChart(id, `[data-move="${direction === -1 ? 'up' : 'down'}"]`);
  }
  clearFilters() { this.query.set(''); this.status.set('all'); this.sort.set('value'); }
  isAllocation(id: ChartId): id is AllocationChartId { return id === 'categories' || id === 'sectors' || id === 'countries'; }
  filteredRows(id: AllocationChartId): AllocationRow[] {
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
    this.context.save();
    this.endDrag(); this.organizing.set(false);
    this.sizes.set({ categories: 'medium', sectors: 'medium', countries: 'compact', evolution: 'wide', income: 'medium', highlights: 'medium', movements: 'medium' });
    this.portfolioId.set(id);
    this.clearFilters();
    const defaults: ChartId[] = ['categories', 'sectors', 'countries', 'evolution', 'income', 'highlights', 'movements'];
    this.order.set(defaults);
    this.collapsed.set(['movements']);
    try {
      this.chartTypes.set({ categories: 'donut', sectors: 'bars', countries: 'donut' });
      const stored = JSON.parse(localStorage.getItem(this.storageKey()) ?? 'null');
      if (stored && Array.isArray(stored.order) && Array.isArray(stored.collapsed)) {
        for (const id of defaults) {
          if (['compact', 'medium', 'wide'].includes(stored.sizes?.[id])) this.sizes.update(sizes => ({ ...sizes, [id]: stored.sizes[id] }));
          if (this.isAllocation(id) && (stored.types?.[id] === 'bars' || stored.types?.[id] === 'donut')) this.chartTypes.update(types => ({ ...types, [id]: stored.types[id] }));
        }
        const valid = (value: unknown): value is ChartId => defaults.includes(value as ChartId);
        const order: ChartId[] = [...new Set<ChartId>(stored.order.filter(valid))];
        this.order.set([...order, ...defaults.filter(id => !order.includes(id))]);
        this.collapsed.set([...new Set<ChartId>([...stored.collapsed.filter(valid), ...(!stored.order.includes('movements') ? ['movements' as ChartId] : [])])]);
      }
    } catch { /* Invalid or unavailable storage uses the default layout. */ }
    this.context.restore(id); this.load();
  }
  load() {
    const id = this.portfolioId();
    if (id === null) return;
    this.request?.unsubscribe();
    this.loading.set(true);
    this.data.set(null);
    this.periodHistory.set(null); this.periodIncome.set(null); this.historyError.set(''); this.incomeError.set('');
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

type AllocationChartId = 'categories' | 'sectors' | 'countries';
type ChartId = AllocationChartId | 'evolution' | 'income' | 'highlights' | 'movements';
