import { createRequestId } from '../../core/request-id';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
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

export interface IncomeTotal { period: string; ticker: string; assetId: number; currencyCode: string; received: string; reversed: string; net: string; }
export interface IncomeAnalysis { months: IncomeTotal[]; years: IncomeTotal[]; assets: IncomeTotal[]; }
export interface IncomeReceipt {
  id: number; date: string; ticker: string; currencyCode: string;
  amount: string; cashAssetName: string | null; notes: string | null;
}
@Component({
  standalone: true, selector: 'app-income-page',
  imports: [PortfolioPicker, ReactiveFormsModule, CurrencyPipe, DatePipe, RouterLink],
  templateUrl: './income-page.html',
})
export class IncomePage {
  private readonly http = inject(HttpClient);
  private readonly portfolioApi = inject(PortfolioService);
  private readonly assetApi = inject(AssetService);
  private readonly catalogs = inject(CatalogService);
  private readonly externalApi = inject(ExternalAssetsService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
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
  readonly analysis = signal<IncomeAnalysis>({ months: [], years: [], assets: [] });
  readonly analysisMode = signal<'months' | 'years' | 'assets'>('months');
  readonly analysisYear = signal('');
  readonly analysisCurrency = signal('');
  analysisRows() {
    return this.analysis()[this.analysisMode()].filter(r => r.ticker.toLocaleLowerCase('pt-BR').includes(this.query().trim().toLocaleLowerCase('pt-BR'))
      && (!this.analysisCurrency() || r.currencyCode === this.analysisCurrency())
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
    cashAssetId: [0], notes: ['', Validators.maxLength(500)],
  });
  select(id: number) {
    if (this.saving()) return;
    this.analysisYear.set(''); this.analysisCurrency.set('');
    this.id.set(id); this.requestId = createRequestId();
    this.form.reset({ date: this.today, assetId: Number(this.route.snapshot.queryParamMap.get('assetId')) || 0,
      amount: '', cashAssetId: 0, notes: '' });
    this.success.set(''); this.load();
  }
  load() {
    const id = this.id(); if (id === null) return;
    this.request?.unsubscribe(); this.loading.set(true); this.summary.set(null); this.error.set('');
    this.request = forkJoin({ summary: this.portfolioApi.summary(id), assets: this.assetApi.list(),
      currencies: this.catalogs.list('currencies'), balances: this.externalApi.summary(id),
      analysis: this.http.get<IncomeAnalysis>(`/api/portfolios/${id}/income/analysis`),
      receipts: this.http.get<IncomeReceipt[]>(`/api/portfolios/${id}/income`) })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: data => { this.summary.set(data.summary); this.assets.set(data.assets); this.currencies.set(data.currencies);
          this.balances.set(data.balances.items); this.receipts.set(data.receipts); this.analysis.set(data.analysis); this.loading.set(false); },
        error: error => { this.error.set(apiError(error)); this.loading.set(false); },
      });
  }
  asset() { return this.assets().find(a => a.id === Number(this.form.controls.assetId.value)); }
  currency() { return this.currencies().find(c => c.id === this.asset()?.currencyId)?.code ?? ''; }
  cashOptions() { return this.balances().filter(b => b.currencyId === this.asset()?.currencyId); }
  positions() { return this.summary()?.positions ?? []; }
  filteredIncomeReceipts() {
    const query = this.query().trim().toLocaleLowerCase('pt-BR');
    return this.receipts().filter(t => t.ticker.toLocaleLowerCase('pt-BR').includes(query));
  }
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
      amount, cashAssetId: Number(value.cashAssetId) || null, notes: value.notes.trim() || null };
    this.saving.set(true); this.error.set(''); this.success.set('');
    this.http.post<IncomeReceipt>(`/api/portfolios/${this.id()}/income`, input).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.saving.set(false); this.requestId = createRequestId();
        this.success.set('Recebimento registrado. Proventos acumulados e saldo de destino atualizados. Nenhum aporte foi gerado.');
        this.form.patchValue({ amount: '', notes: '' }); this.form.markAsUntouched(); this.load();
      },
      error: error => { this.saving.set(false); this.error.set(apiError(error)); },
    });
  }
}
