import { FilterSummary } from '../../shared/filter-summary';
import { rememberContext } from '../../shared/page-context';
import { UnsavedForm } from '../../shared/unsaved-changes';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { EditPanel } from '../../shared/edit-panel';
import { ListPaginator } from '../../shared/list-paginator';
import { PrivateCurrencyPipe as CurrencyPipe } from '../../core/value-privacy';
import { createRequestId } from '../../core/request-id';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin, Subscription } from 'rxjs';
import { PortfolioPicker } from '../../shared/portfolio-picker';
import { PortfolioService, PortfolioSummary } from '../portfolio/portfolio.service';
import { AssetService, Asset } from '../assets/asset.service';
import { CatalogService } from '../catalogs/catalog.service';
import { CatalogItem } from '../catalogs/catalog.models';
import { ExternalAssetsService, ExternalAsset } from '../external-assets/external-assets.service';
import { brazilianNumberValidator, parseBrazilianNumber, formatBrazilianNumber } from '../../core/brazilian-number';
import { apiError } from '../../core/services/api-error';

export interface Trade {
  movementId?: number | null; reversedById?: number | null;
  id: number; date: string; kind: string; ticker: string; currencyCode: string;
  quantity: string; unitPrice: string; amount: string; fees?: string; cashAssetName: string | null;
}
interface TradePage { items: { trade: Trade; movementId: number | null; reversedById: number | null }[]; total: number; pageIndex: number; pageSize: number; }
@Component({
  standalone: true, selector: 'app-trades-page',
  imports: [FilterSummary, UnsavedForm, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, EditPanel, ListPaginator, CurrencyPipe, PortfolioPicker, ReactiveFormsModule, DatePipe, RouterLink],
  templateUrl: './trades-page.html',
  styleUrl: './trades-page.css',
})
export class TradesPage {
  private readonly context = rememberContext('trades', () => ({ pageSize: this.pageSize(), pageIndex: this.pageIndex(), applied: this.appliedFilters, filterDraft: this.filters.getRawValue() }), state => { this.pageSize.set(state.pageSize); this.pageIndex.set(state.pageIndex); this.appliedFilters = state.applied; this.filters.reset(state.filterDraft); }, () => !this.loading() && !this.listLoading());

  private readonly http = inject(HttpClient);
  private readonly portfolioApi = inject(PortfolioService);
  private readonly assetApi = inject(AssetService);
  private readonly catalogs = inject(CatalogService);
  private readonly externalApi = inject(ExternalAssetsService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private contextualOpened = false;
  private contextualAsset(id: number) {
    if (this.contextualOpened) return 0;
    this.contextualOpened = true;
    const source = Number(this.route.snapshot.queryParamMap.get('portfolioId'));
    return source && source !== id ? 0 : Number(this.route.snapshot.queryParamMap.get('assetId')) || 0;
  }
  private readonly fb = inject(FormBuilder);
  private request?: Subscription;
  private requestId = createRequestId();
  readonly id = signal<number | null>(null);
  readonly summary = signal<PortfolioSummary | null>(null);
  readonly assets = signal<Asset[]>([]);
  readonly currencies = signal<CatalogItem[]>([]);
  readonly balances = signal<ExternalAsset[]>([]);
  readonly trades = signal<Trade[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly success = signal('');
  readonly panel = signal<'trade' | 'event' | null>(null);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly total = signal(0);
  readonly listLoading = signal(false);
  readonly listError = signal('');
  readonly assetSearch = signal('');
  readonly filters = this.fb.nonNullable.group({ search: [''], kind: [''], from: [''], to: [''] });
  private appliedFilters = this.filters.getRawValue();
  private pageRequest?: Subscription;
  openTrade(kind: 'buy' | 'sell') { this.form.controls.kind.setValue(kind); this.error.set(''); this.panel.set('trade'); }
  openEvent() { this.error.set(''); this.panel.set('event'); }
  selectableAssets() {
    const search = this.assetSearch().trim().toLocaleLowerCase('pt-BR');
    const selected = Number(this.form.controls.assetId.value);
    return this.assets().filter(a => a.id === selected || `${a.ticker} ${a.name}`.toLocaleLowerCase('pt-BR').includes(search));
  }

  activeFilters(): Record<string, string> { const v = this.appliedFilters; return { Busca: v.search, Tipo: ({buy:'Compra',sell:'Venda'} as Record<string,string>)[v.kind] || v.kind, De: v.from, Até: v.to }; }
  hasAppliedFilters() { return Object.values(this.appliedFilters).some(value => !!value); }
  filtersPending() { return JSON.stringify(this.filters.getRawValue()) !== JSON.stringify(this.appliedFilters); }
  applyFilters() { this.appliedFilters = this.filters.getRawValue(); this.pageIndex.set(0); this.loadPage(); }
  clearFilters() { this.filters.reset(); this.applyFilters(); }
  changePage(event: { pageIndex: number; pageSize: number }) { this.pageIndex.set(event.pageIndex); this.pageSize.set(event.pageSize); this.loadPage(); }
  loadPage() {
    if (!this.id()) return;
    this.pageRequest?.unsubscribe(); this.listError.set('');
    const v = this.appliedFilters;
    if (v.from && v.to && v.from > v.to) { this.listLoading.set(false); this.listError.set('A data inicial deve ser anterior ou igual à final.'); return; }
    this.listLoading.set(true); this.trades.set([]);
    const params: Record<string, string | number> = { pageIndex: this.pageIndex(), pageSize: this.pageSize() };
    for (const [key, value] of Object.entries(v)) if (value.trim()) params[key] = value.trim();
    this.pageRequest = this.http.get<TradePage>(`/api/portfolios/${this.id()}/trades/page`, { params })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: page => { this.trades.set(page.items.map(x => ({ ...x.trade, movementId: x.movementId, reversedById: x.reversedById }))); this.total.set(page.total); this.pageIndex.set(page.pageIndex); this.listLoading.set(false); },
        error: e => { this.listError.set(apiError(e)); this.listLoading.set(false); },
      });
  }
  readonly formatNumber = formatBrazilianNumber;
  readonly today = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Sao_Paulo', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
  readonly form = this.fb.nonNullable.group({
    date: [this.today, Validators.required],
    kind: ['buy', Validators.required],
    assetId: [0, Validators.min(1)],
    quantity: ['', [Validators.required, brazilianNumberValidator(13, 6)]],
    unitPrice: ['', [Validators.required, brazilianNumberValidator(15, 4)]],
    cashAssetId: [0],
    fees: ['0', [Validators.required, brazilianNumberValidator(15, 4)]],
    baseAmount: ['', (control: import('@angular/forms').AbstractControl) => control.value ? brazilianNumberValidator(15, 4)(control) : null],
  });
  private eventRequestId = createRequestId();
  readonly eventForm = this.fb.nonNullable.group({ date: [this.today, Validators.required], kind: ['split'],
    positionId: [0, Validators.min(1)], quantity: ['', [Validators.required, brazilianNumberValidator(13, 6)]],
    cost: ['0', [Validators.required, brazilianNumberValidator(15, 4)]], reason: ['', [Validators.required, Validators.maxLength(400)]] });
  saveEvent() {
    if (this.saving() || this.loading() || !this.summary() || this.eventForm.invalid) { this.eventForm.markAllAsTouched(); return; }
    const v = this.eventForm.getRawValue(); this.saving.set(true); this.error.set(''); this.success.set('');
    this.http.post(`/api/portfolios/${this.id()}/movements/corporate-events`, { ...v, requestId: this.eventRequestId,
      positionId: Number(v.positionId), quantity: parseBrazilianNumber(v.quantity), cost: v.kind === 'bonus' ? parseBrazilianNumber(v.cost) : '0',
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.saving.set(false); this.eventRequestId = createRequestId();
        this.panel.set(null); this.eventForm.patchValue({ quantity: '', cost: '0', reason: '' }); this.success.set('Evento registrado com auditoria. Atualize a cotação da posição quando disponível.'); this.load(); },
      error: e => { this.saving.set(false); this.error.set(apiError(e)); },
    });
  }
  select(id: number) {
    this.context.save();
    if (this.saving()) return;
    const contextualAsset = this.contextualAsset(id);
    this.panel.set(null); this.assetSearch.set(''); this.pageIndex.set(0); this.filters.reset(); this.appliedFilters = this.filters.getRawValue();
    this.id.set(id); this.requestId = createRequestId(); this.eventRequestId = createRequestId();
    this.eventForm.reset({ date: this.today, kind: 'split', positionId: 0, quantity: '', cost: '0', reason: '' });
    this.form.reset({ date: this.today, kind: this.route.snapshot.queryParamMap.get('kind') === 'sell' ? 'sell' : 'buy',
      assetId: contextualAsset, quantity: '', unitPrice: '', fees: '0', cashAssetId: 0, baseAmount: '' });
    if (contextualAsset) this.panel.set('trade');
    this.success.set(''); this.context.restore(id); this.load();
  }
  load() {
    const id = this.id(); if (id === null) return;
    this.loadPage();
    this.request?.unsubscribe(); this.loading.set(true); this.summary.set(null); this.error.set('');
    this.request = forkJoin({ summary: this.portfolioApi.summary(id), assets: this.assetApi.list(),
      currencies: this.catalogs.list('currencies'), balances: this.externalApi.summary(id) })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: data => { this.summary.set(data.summary); this.assets.set(data.assets); this.currencies.set(data.currencies);
          if (this.form.controls.assetId.value && !data.assets.some(a => a.id === Number(this.form.controls.assetId.value))) {
            this.form.controls.assetId.setValue(0); this.error.set('O ativo do link não está disponível. Selecione outro ativo.');
          }
          this.balances.set(data.balances.items); this.loading.set(false); },
        error: error => { this.error.set(apiError(error)); this.loading.set(false); },
      });
  }
  asset() { return this.assets().find(a => a.id === Number(this.form.controls.assetId.value)); }
  currency() { return this.currencies().find(c => c.id === this.asset()?.currencyId)?.code ?? ''; }
  cashOptions() { return this.balances().filter(b => b.currencyId === this.asset()?.currencyId); }
  availableQuantity() { return this.summary()?.positions.find(p => p.assetId === Number(this.form.controls.assetId.value))?.quantity ?? '0'; }
  estimatedTotal() { return Number(parseBrazilianNumber(this.form.controls.quantity.value)) * Number(parseBrazilianNumber(this.form.controls.unitPrice.value)) + (this.form.controls.kind.value === 'buy' ? 1 : -1) * Number(parseBrazilianNumber(this.form.controls.fees.value)); }
  formatInput(field: 'quantity' | 'unitPrice' | 'baseAmount' | 'fees') {
    const control = this.form.controls[field];
    if (control.valid && control.value.trim()) control.setValue(formatBrazilianNumber(parseBrazilianNumber(control.value)));
  }
  save() {
    if (this.form.invalid || this.saving() || this.loading() || !this.summary()) { this.form.markAllAsTouched(); return; }
    const value = this.form.getRawValue();
    const quantity = parseBrazilianNumber(value.quantity); const price = parseBrazilianNumber(value.unitPrice);
    if (Number(quantity) <= 0 || Number(price) <= 0) { this.error.set('Quantidade e preço devem ser maiores que zero.'); return; }
    const input = { requestId: this.requestId, date: value.date, kind: value.kind, assetId: Number(value.assetId),
      quantity, unitPrice: price, fees: parseBrazilianNumber(value.fees), cashAssetId: Number(value.cashAssetId) || null,
      baseAmount: !Number(value.cashAssetId) && value.baseAmount.trim() ? parseBrazilianNumber(value.baseAmount) : null };
    this.saving.set(true); this.error.set(''); this.success.set('');
    this.http.post<Trade>(`/api/portfolios/${this.id()}/trades`, input).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.saving.set(false); this.requestId = createRequestId();
        this.success.set('Operação registrada. Posição e movimentação do dinheiro atualizadas automaticamente. Não registre este aporte ou retirada novamente.');
        this.panel.set(null); this.form.patchValue({ quantity: '', unitPrice: '', fees: '0', baseAmount: '' }); this.form.markAsUntouched(); this.load();
      },
      error: error => { this.saving.set(false); this.error.set(apiError(error)); },
    });
  }
}
