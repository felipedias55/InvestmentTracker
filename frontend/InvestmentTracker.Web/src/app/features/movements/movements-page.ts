import { EditPanel } from '../../shared/edit-panel';
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
import { CatalogService } from '../catalogs/catalog.service';
import { CatalogItem } from '../catalogs/catalog.models';
import { ExternalAssetsService, ExternalAsset } from '../external-assets/external-assets.service';
import { brazilianNumberValidator, parseBrazilianNumber, formatBrazilianNumber } from '../../core/brazilian-number';
import { apiError } from '../../core/services/api-error';

export interface Movement {
  id: number; date: string; kind: string; description: string; currencyCode: string; amount: string;
  tradeId: number | null; incomeReceiptId: number | null; reversalOfId: number | null; reversedById: number | null;
  canReverse: boolean; reversalBlockedReason: string | null; createdAtUtc: string;
  effects: { name: string; currencyCode: string; beforeValue: string; afterValue: string; isPosition: boolean; beforeQuantity: string; afterQuantity: string; beforeCost: string; afterCost: string; beforeIncome: string; afterIncome: string }[];
}
@Component({
  standalone: true, selector: 'app-movements-page',
  imports: [EditPanel, PortfolioPicker, ReactiveFormsModule, CurrencyPipe, DatePipe, RouterLink],
  templateUrl: './movements-page.html',
})
export class MovementsPage {
  private readonly http = inject(HttpClient);
  private readonly portfolioApi = inject(PortfolioService);
  private readonly catalogs = inject(CatalogService);
  private readonly externalApi = inject(ExternalAssetsService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);
  private request?: Subscription;
  private requestId = createRequestId();
  readonly id = signal<number | null>(null);
  readonly summary = signal<PortfolioSummary | null>(null);
  readonly currencies = signal<CatalogItem[]>([]);
  readonly balances = signal<ExternalAsset[]>([]);
  readonly movements = signal<Movement[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly success = signal('');
  readonly query = signal('');
  readonly formatNumber = formatBrazilianNumber;
  readonly today = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Sao_Paulo', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
  readonly pending = signal<Movement | null>(null);
  readonly reverseReason = signal('');
  private reverseId = createRequestId();
  readonly labels: Record<string, string> = { buy: 'Compra', sell: 'Venda', income: 'Provento', deposit: 'Depósito',
    withdrawal: 'Retirada', transfer: 'Transferência', adjustment: 'Ajuste de saldo', reversal: 'Estorno',
    historical: 'Registro histórico', 'position-adjustment': 'Ajuste de posição', opening: 'Saldo inicial', reopen: 'Reabertura de período', split: 'Desdobramento', 'reverse-split': 'Grupamento', bonus: 'Bonificação' };
  readonly form = this.fb.nonNullable.group({
    date: [this.today, Validators.required], kind: ['deposit'], cashAssetId: [0, Validators.min(1)],
    amount: ['', [Validators.required, brazilianNumberValidator(15, 4)]], destinationId: [0],
    reason: ['', Validators.maxLength(400)], baseAmount: [''],
  });
  select(id: number) {
    if (this.saving()) return;
    this.id.set(id); this.requestId = createRequestId(); this.pending.set(null);
    this.form.reset({ date: this.today, kind: 'deposit', cashAssetId: 0, amount: '', destinationId: 0, reason: '', baseAmount: '' });
    this.success.set(''); this.load();
  }
  load() {
    const id = this.id(); if (id === null) return;
    this.request?.unsubscribe(); this.loading.set(true); this.summary.set(null); this.error.set('');
    this.request = forkJoin({ summary: this.portfolioApi.summary(id),
      currencies: this.catalogs.list('currencies'), balances: this.externalApi.summary(id),
      movements: this.http.get<Movement[]>(`/api/portfolios/${id}/movements`) })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: data => { this.summary.set(data.summary); this.currencies.set(data.currencies);
          this.balances.set(data.balances.items); this.movements.set(data.movements); this.loading.set(false); },
        error: error => { this.error.set(apiError(error)); this.loading.set(false); },
      });
  }
  currency() { return this.balances().find(b => b.id === Number(this.form.controls.cashAssetId.value))?.currencyCode ?? ''; }
  destinations() { return this.balances().filter(b => b.id !== Number(this.form.controls.cashAssetId.value) && b.currencyCode === this.currency()); }
  filtered() {
    const query = this.query().trim().toLocaleLowerCase('pt-BR');
    return this.movements().filter(m => `${m.id} ${m.description} ${this.labels[m.kind]}`.toLocaleLowerCase('pt-BR').includes(query));
  }
  formatInput(field: 'amount' | 'baseAmount') {
    const control = this.form.controls[field];
    if (control.value.trim() && !brazilianNumberValidator(15, 4)(control)) control.setValue(formatBrazilianNumber(parseBrazilianNumber(control.value)));
  }
  save() {
    if (this.form.invalid || this.saving() || this.loading() || !this.summary()) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    if ((v.kind === 'adjustment' && !v.reason.trim()) || (v.kind === 'transfer' && !v.destinationId)) {
      this.error.set('Informe o motivo do ajuste ou o saldo de destino da transferência.'); return;
    }
    if (v.baseAmount.trim() && brazilianNumberValidator(15, 4)(this.form.controls.baseAmount)) { this.error.set('Equivalente inválido. Use o padrão brasileiro.'); return; }
    this.saving.set(true); this.error.set(''); this.success.set('');
    this.http.post<Movement>(`/api/portfolios/${this.id()}/movements`, { requestId: this.requestId,
      date: v.date, kind: v.kind, cashAssetId: Number(v.cashAssetId), amount: parseBrazilianNumber(v.amount),
      destinationId: v.kind === 'transfer' ? Number(v.destinationId) : null, reason: v.reason.trim() || null,
      baseAmount: ['deposit', 'withdrawal'].includes(v.kind) && v.baseAmount.trim() ? parseBrazilianNumber(v.baseAmount) : null,
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.saving.set(false); this.requestId = createRequestId();
        this.success.set('Movimentação registrada. Saldo e histórico atualizados juntos.');
        this.form.patchValue({ amount: '', reason: '', baseAmount: '' }); this.form.markAsUntouched(); this.load(); },
      error: e => { this.saving.set(false); this.error.set(apiError(e)); },
    });
  }
  confirm(m: Movement) { this.pending.set(m); this.reverseReason.set(''); this.reverseId = createRequestId(); this.error.set(''); }
  reverse() {
    if (this.saving() || !this.pending()) return;
    if (!this.reverseReason().trim()) { this.error.set('Informe o motivo do estorno.'); return; }
    this.saving.set(true); this.error.set(''); this.success.set('');
    this.http.post<Movement>(`/api/portfolios/${this.id()}/movements/${this.pending()!.id}/reversal`, {
      requestId: this.reverseId, reason: this.reverseReason().trim(),
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.saving.set(false); this.pending.set(null); this.success.set('Estorno registrado. O original e as fotografias foram preservados.'); this.load(); },
      error: e => { this.saving.set(false); this.error.set(apiError(e)); },
    });
  }
}
