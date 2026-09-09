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

export interface Trade {
  id: number; date: string; kind: string; ticker: string; currencyCode: string;
  quantity: string; unitPrice: string; amount: string; cashAssetName: string | null;
}
@Component({
  standalone: true, selector: 'app-trades-page',
  imports: [PortfolioPicker, ReactiveFormsModule, CurrencyPipe, DatePipe, RouterLink],
  templateUrl: './trades-page.html',
})
export class TradesPage {
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
  readonly trades = signal<Trade[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly success = signal('');
  readonly query = signal('');
  readonly formatNumber = formatBrazilianNumber;
  readonly today = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Sao_Paulo', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
  readonly form = this.fb.nonNullable.group({
    date: [this.today, Validators.required],
    kind: ['buy', Validators.required],
    assetId: [0, Validators.min(1)],
    quantity: ['', [Validators.required, brazilianNumberValidator(13, 6)]],
    unitPrice: ['', [Validators.required, brazilianNumberValidator(15, 4)]],
    cashAssetId: [0],
    baseAmount: ['', (control: import('@angular/forms').AbstractControl) => control.value ? brazilianNumberValidator(15, 4)(control) : null],
  });
  select(id: number) {
    if (this.saving()) return;
    this.id.set(id); this.requestId = createRequestId();
    this.form.reset({ date: this.today, kind: this.route.snapshot.queryParamMap.get('kind') === 'sell' ? 'sell' : 'buy',
      assetId: Number(this.route.snapshot.queryParamMap.get('assetId')) || 0, quantity: '', unitPrice: '', cashAssetId: 0, baseAmount: '' });
    this.success.set(''); this.load();
  }
  load() {
    const id = this.id(); if (id === null) return;
    this.request?.unsubscribe(); this.loading.set(true); this.summary.set(null); this.error.set('');
    this.request = forkJoin({ summary: this.portfolioApi.summary(id), assets: this.assetApi.list(),
      currencies: this.catalogs.list('currencies'), balances: this.externalApi.summary(id),
      trades: this.http.get<Trade[]>(`/api/portfolios/${id}/trades`) })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: data => { this.summary.set(data.summary); this.assets.set(data.assets); this.currencies.set(data.currencies);
          this.balances.set(data.balances.items); this.trades.set(data.trades); this.loading.set(false); },
        error: error => { this.error.set(apiError(error)); this.loading.set(false); },
      });
  }
  asset() { return this.assets().find(a => a.id === Number(this.form.controls.assetId.value)); }
  currency() { return this.currencies().find(c => c.id === this.asset()?.currencyId)?.code ?? ''; }
  cashOptions() { return this.balances().filter(b => b.currencyId === this.asset()?.currencyId); }
  availableQuantity() { return this.summary()?.positions.find(p => p.assetId === Number(this.form.controls.assetId.value))?.quantity ?? '0'; }
  estimatedTotal() { return Number(parseBrazilianNumber(this.form.controls.quantity.value)) * Number(parseBrazilianNumber(this.form.controls.unitPrice.value)); }
  filteredTrades() {
    const query = this.query().trim().toLocaleLowerCase('pt-BR');
    return this.trades().filter(t => t.ticker.toLocaleLowerCase('pt-BR').includes(query));
  }
  formatInput(field: 'quantity' | 'unitPrice' | 'baseAmount') {
    const control = this.form.controls[field];
    if (control.valid && control.value.trim()) control.setValue(formatBrazilianNumber(parseBrazilianNumber(control.value)));
  }
  save() {
    if (this.form.invalid || this.saving() || this.loading() || !this.summary()) { this.form.markAllAsTouched(); return; }
    const value = this.form.getRawValue();
    const quantity = parseBrazilianNumber(value.quantity); const price = parseBrazilianNumber(value.unitPrice);
    if (Number(quantity) <= 0 || Number(price) <= 0) { this.error.set('Quantidade e preço devem ser maiores que zero.'); return; }
    const input = { requestId: this.requestId, date: value.date, kind: value.kind, assetId: Number(value.assetId),
      quantity, unitPrice: price, cashAssetId: Number(value.cashAssetId) || null,
      baseAmount: !Number(value.cashAssetId) && value.baseAmount.trim() ? parseBrazilianNumber(value.baseAmount) : null };
    this.saving.set(true); this.error.set(''); this.success.set('');
    this.http.post<Trade>(`/api/portfolios/${this.id()}/trades`, input).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.saving.set(false); this.requestId = createRequestId();
        this.success.set('Operação registrada. Posição e movimentação do dinheiro atualizadas automaticamente. Não registre este aporte ou retirada novamente.');
        this.form.patchValue({ quantity: '', unitPrice: '', baseAmount: '' }); this.form.markAsUntouched(); this.load();
      },
      error: error => { this.saving.set(false); this.error.set(apiError(error)); },
    });
  }
}
