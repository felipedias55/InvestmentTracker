import { FilterSummary } from '../../shared/filter-summary';
import { rememberContext } from '../../shared/page-context';
import { UnsavedForm } from '../../shared/unsaved-changes';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ListPaginator } from '../../shared/list-paginator';
import { PrivateCurrencyPipe as CurrencyPipe } from '../../core/value-privacy';
import { EditPanel } from '../../shared/edit-panel';
import { createRequestId } from '../../core/request-id';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { MoneyChart, MoneyPoint } from '../../shared/money-chart';
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

export interface IncomeTotal { period: string; ticker: string; assetId: number; currencyCode: string; received: string | null; reversed: string | null; net: string | null; missingConversions?: number; }
export interface IncomeAnalysis { months: IncomeTotal[]; years: IncomeTotal[]; assets: IncomeTotal[]; baseCurrencyCode?: string; convertedMonths?: IncomeTotal[]; convertedYears?: IncomeTotal[]; convertedAssets?: IncomeTotal[]; }
export interface IncomeConversion { id: number; revision: number; baseCurrencyCode: string; baseAmount: string; rate: string | null; rateDate: string; source: string; reason: string; createdAtUtc: string; }
export interface IncomeReceipt {
  reversedById?: number | null;
  id: number; date: string; ticker: string; currencyCode: string;
  baseCurrencyCode?: string | null; conversions?: IncomeConversion[];
  amount: string; cashAssetName: string | null; notes: string | null;
}
interface IncomeReceiptPage { items: IncomeReceipt[]; total: number; pageIndex: number; pageSize: number; }
@Component({
  standalone: true, selector: 'app-income-page',
  imports: [FilterSummary, UnsavedForm, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, ListPaginator, CurrencyPipe, EditPanel, PortfolioPicker, ReactiveFormsModule, DatePipe, RouterLink, MoneyChart],
  templateUrl: './income-page.html',
  styleUrl: './income-page.css',
})
export class IncomePage {
  private readonly context = rememberContext('income', () => ({ query: this.query(), section: this.section(), analysisYear: this.analysisYear(), analysisCurrency: this.analysisCurrency(), pageSize: this.pageSize(), pageIndex: this.pageIndex(), applied: this.appliedFilters, filterDraft: this.filters.getRawValue() }), state => { this.query.set(state.query); this.section.set(state.section); this.analysisYear.set(state.analysisYear); this.analysisCurrency.set(state.analysisCurrency); this.pageSize.set(state.pageSize); this.pageIndex.set(state.pageIndex); this.appliedFilters = state.applied; this.filters.reset(state.filterDraft); }, () => !this.loading() && !this.listLoading());

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
  readonly receipts = signal<IncomeReceipt[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly success = signal('');
  readonly query = signal('');
  readonly section = signal<'receipts' | 'analysis'>('receipts');
  readonly formOpen = signal(false);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly total = signal(0);
  readonly listLoading = signal(false);
  readonly listError = signal('');
  readonly filters = this.fb.nonNullable.group({ search: [''], pendingOnly: [''], from: [''], to: [''] });
  private appliedFilters = this.filters.getRawValue();
  private pageRequest?: Subscription;
  openReceipt() { this.error.set(''); this.formOpen.set(true); }

  activeFilters(): Record<string, string> { const v = this.appliedFilters; return { Ativo: v.search, Conversão: v.pendingOnly === 'true' ? 'Somente pendentes' : '', De: v.from, Até: v.to }; }
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
    this.listLoading.set(true); this.receipts.set([]);
    const params: Record<string, string | number> = { pageIndex: this.pageIndex(), pageSize: this.pageSize() };
    for (const [key, value] of Object.entries(v)) if (value.trim()) params[key] = value.trim();
    this.pageRequest = this.http.get<IncomeReceiptPage>(`/api/portfolios/${this.id()}/income/page`, { params })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: page => { this.receipts.set(page.items); this.total.set(page.total); this.pageIndex.set(page.pageIndex); this.listLoading.set(false); },
        error: e => { this.listError.set(apiError(e)); this.listLoading.set(false); },
      });
  }
  readonly analysis = signal<IncomeAnalysis>({ months: [], years: [], assets: [] });
  readonly analysisMode = signal<'months' | 'years' | 'assets'>('months');
  readonly analysisYear = signal('');
  readonly analysisCurrency = signal('');
  readonly valueMode = signal<'original' | 'base'>('original');
  readonly chartGroups = computed(() => {
    const currencies = new Map<string, Map<string, MoneyPoint>>();
    for (const row of this.analysisRows()) {
      const groups = currencies.get(row.currencyCode) ?? new Map<string, MoneyPoint>();
      const key = this.analysisMode() === 'assets' ? String(row.assetId) : row.period;
      const previous = groups.get(key);
      groups.set(key, { label: this.analysisMode() === 'assets' ? row.ticker : row.period,
        value: row.net === null || previous?.value === null ? null : (previous?.value ?? 0) + Number(row.net) });
      currencies.set(row.currencyCode, groups);
    }
    return [...currencies].sort(([a], [b]) => a.localeCompare(b)).map(([currency, groups]) => {
      const points = [...groups.values()].sort((a, b) => this.analysisMode() === 'assets'
        ? (b.value ?? -Infinity) - (a.value ?? -Infinity) || a.label.localeCompare(b.label)
        : a.label.localeCompare(b.label));
      return { currency, points, total: points.some(p => p.value === null) ? null : points.reduce((sum, p) => sum + p.value!, 0) };
    });
  });
  analysisRows() {
    const data = this.analysis();
    const rows = this.valueMode() === 'original' ? data[this.analysisMode()] :
      (this.analysisMode() === 'months' ? data.convertedMonths : this.analysisMode() === 'years' ? data.convertedYears : data.convertedAssets) ?? [];
    return rows.filter(r => r.ticker.toLocaleLowerCase('pt-BR').includes(this.query().trim().toLocaleLowerCase('pt-BR'))
      && (this.valueMode() === 'base' || !this.analysisCurrency() || r.currencyCode === this.analysisCurrency())
      && (this.analysisMode() === 'assets' || !this.analysisYear() || r.period.startsWith(this.analysisYear())));
  }
  analysisYears() { return [...new Set(this.analysis().years.map(x => x.period))]; }
  analysisCurrencies() { return [...new Set(this.analysis().assets.map(x => x.currencyCode))]; }
  barWidth(row: IncomeTotal) {
    const max = Math.max(...this.analysisRows().filter(x => x.currencyCode === row.currencyCode).map(x => Math.abs(Number(x.net))), 1);
    return Math.abs(Number(row.net)) / max * 100;
  }
  readonly formatNumber = formatBrazilianNumber;
  readonly today = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Sao_Paulo', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
  readonly form = this.fb.nonNullable.group({
    date: [this.today, Validators.required], assetId: [0, Validators.min(1)],
    amount: ['', [Validators.required, brazilianNumberValidator(15, 4)]],
    baseAmount: ['', (c: import('@angular/forms').AbstractControl) => c.value ? brazilianNumberValidator(15, 4)(c) : null],
    cashAssetId: [0], notes: ['', Validators.maxLength(500)],
  });
  readonly pendingConversion = signal<IncomeReceipt | null>(null);
  private conversionRequestId = createRequestId();
  readonly conversionForm = this.fb.nonNullable.group({ baseCurrencyCode: ['', Validators.required],
    baseAmount: ['', (c: import('@angular/forms').AbstractControl) => c.value ? brazilianNumberValidator(15, 4)(c) : null],
    reason: ['', [Validators.required, Validators.maxLength(400)]] });
  conversionFor(receipt: IncomeReceipt, code = this.summary()?.portfolio.baseCurrencyCode) {
    return receipt.conversions?.filter(c => c.baseCurrencyCode === code).sort((a, b) => b.revision - a.revision)[0];
  }
  equivalent(receipt: IncomeReceipt) { return receipt.currencyCode === this.summary()?.portfolio.baseCurrencyCode ? receipt.amount : this.conversionFor(receipt)?.baseAmount ?? null; }
  sourceLabel(source: string) { return source === 'same-currency' ? 'Mesma moeda' : source === 'manual' ? 'Informado' : source; }
  openConversion(receipt: IncomeReceipt) {
    this.pendingConversion.set(receipt); this.conversionRequestId = createRequestId(); this.error.set('');
    this.conversionForm.reset({ baseCurrencyCode: this.summary()?.portfolio.baseCurrencyCode ?? '', baseAmount: '', reason: '' });
  }
  saveConversion() {
    const receipt = this.pendingConversion();
    if (!receipt || this.saving() || this.conversionForm.invalid) { this.conversionForm.markAllAsTouched(); return; }
    const v = this.conversionForm.getRawValue(); this.saving.set(true); this.error.set('');
    this.http.post<IncomeReceipt>(`/api/portfolios/${this.id()}/income/${receipt.id}/conversions`, {
      requestId: this.conversionRequestId, baseCurrencyCode: v.baseCurrencyCode,
      expectedRevision: this.conversionFor(receipt, v.baseCurrencyCode)?.revision ?? 0,
      baseAmount: v.baseAmount.trim() ? parseBrazilianNumber(v.baseAmount) : null, reason: v.reason.trim(),
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.saving.set(false); this.pendingConversion.set(null); this.conversionRequestId = createRequestId();
        this.success.set('Conversão registrada com auditoria. Os saldos e as fotografias foram preservados.'); this.load(); },
      error: e => { this.saving.set(false); this.error.set(apiError(e)); },
    });
  }
  select(id: number) {
    this.context.save();
    if (this.saving()) return;
    const contextualAsset = this.contextualAsset(id);
    this.pendingConversion.set(null); this.formOpen.set(false); this.pageIndex.set(0); this.filters.reset(); this.appliedFilters = this.filters.getRawValue(); this.query.set('');
    this.analysisYear.set(''); this.analysisCurrency.set('');
    this.id.set(id); this.requestId = createRequestId();
    this.form.reset({ date: this.today, assetId: contextualAsset,
      amount: '', baseAmount: '', cashAssetId: 0, notes: '' });
    if (contextualAsset) this.formOpen.set(true);
    this.success.set(''); this.context.restore(id); this.load();
  }
  load() {
    const id = this.id(); if (id === null) return;
    this.loadPage();
    this.request?.unsubscribe(); this.loading.set(true); this.summary.set(null); this.error.set('');
    this.request = forkJoin({ summary: this.portfolioApi.summary(id), assets: this.assetApi.list(),
      currencies: this.catalogs.list('currencies'), balances: this.externalApi.summary(id),
      analysis: this.http.get<IncomeAnalysis>(`/api/portfolios/${id}/income/analysis`) })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: data => { this.summary.set(data.summary); this.assets.set(data.assets); this.currencies.set(data.currencies);
          if (this.form.controls.assetId.value && !data.assets.some(a => a.id === Number(this.form.controls.assetId.value))) {
            this.form.controls.assetId.setValue(0); this.error.set('O ativo do link não está disponível. Selecione outro ativo.');
          }
          this.balances.set(data.balances.items); this.analysis.set(data.analysis); this.loading.set(false); },
        error: error => { this.error.set(apiError(error)); this.loading.set(false); },
      });
  }
  asset() { return this.assets().find(a => a.id === Number(this.form.controls.assetId.value)); }
  currency() { return this.currencies().find(c => c.id === this.asset()?.currencyId)?.code ?? ''; }
  cashOptions() { return this.balances().filter(b => b.currencyId === this.asset()?.currencyId); }
  positions() { return this.summary()?.positions ?? []; }
  formatInput() {
    const control = this.form.controls.amount;
    if (control.valid && control.value.trim()) control.setValue(formatBrazilianNumber(parseBrazilianNumber(control.value)));
  }
  save() {
    if (this.form.invalid || this.saving() || this.loading() || !this.summary()) { this.form.markAllAsTouched(); return; }
    const value = this.form.getRawValue();
    const amount = parseBrazilianNumber(value.amount);
    if (Number(amount) <= 0) { this.error.set('O valor deve ser maior que zero.'); return; }
    const input = { requestId: this.requestId, date: value.date, assetId: Number(value.assetId),
      amount, baseAmount: this.currency() !== this.summary()?.portfolio.baseCurrencyCode && value.baseAmount.trim() ? parseBrazilianNumber(value.baseAmount) : null, cashAssetId: Number(value.cashAssetId) || null, notes: value.notes.trim() || null };
    this.saving.set(true); this.error.set(''); this.success.set('');
    this.http.post<IncomeReceipt>(`/api/portfolios/${this.id()}/income`, input).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.saving.set(false); this.requestId = createRequestId();
        this.success.set('Recebimento registrado. Proventos acumulados e saldo de destino atualizados. Nenhum aporte foi gerado.');
        this.formOpen.set(false); this.form.patchValue({ amount: '', baseAmount: '', notes: '' }); this.form.markAsUntouched(); this.load();
      },
      error: error => { this.saving.set(false); this.error.set(apiError(error)); },
    });
  }
}
