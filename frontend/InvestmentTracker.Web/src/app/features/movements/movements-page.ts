import { FilterSummary } from '../../shared/filter-summary';
import { rememberContext } from '../../shared/page-context';
import { DraftState } from '../../shared/draft-state';
import { UnsavedForm } from '../../shared/unsaved-changes';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ListPaginator } from '../../shared/list-paginator';
import { PrivateCurrencyPipe as CurrencyPipe } from '../../core/value-privacy';
import { EditPanel } from '../../shared/edit-panel';
import { CorrectionPanel } from './correction-panel';
import { createRequestId } from '../../core/request-id';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
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
  correctionId?: number | null; replacesMovementId?: number | null;
  effects: { name: string; currencyCode: string; beforeValue: string; afterValue: string; isPosition: boolean; beforeQuantity: string; afterQuantity: string; beforeCost: string; afterCost: string; beforeIncome: string; afterIncome: string }[];
}
type MovementSummary = Pick<Movement, 'id' | 'date' | 'kind' | 'description' | 'currencyCode' | 'amount' | 'reversalOfId' | 'reversedById'>;
interface MovementPage { items: MovementSummary[]; total: number; pageIndex: number; pageSize: number; }
export interface CorrectionPreview {
  fromDate: string; movementId: number | null;
  movements: { id: number; date: string; kind: string; description: string; dependency: string; isReversed: boolean }[];
  snapshots: { id: number; month: string; snapshotDate: string; revision: number; isOutdated: boolean; isReopened: boolean; requiresReopening: boolean }[];
  reviewOrder: number[]; warnings: string[];
}
@Component({
  standalone: true, selector: 'app-movements-page',
  imports: [FilterSummary, DraftState, UnsavedForm, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, ListPaginator, CurrencyPipe, CorrectionPanel, EditPanel, PortfolioPicker, ReactiveFormsModule, DatePipe, RouterLink],
  templateUrl: './movements-page.html',
  styleUrl: './movements-page.css',
})
export class MovementsPage {
  private readonly context = rememberContext('movements', () => ({ pageSize: this.pageSize(), pageIndex: this.pageIndex(), applied: this.appliedFilters, filterDraft: this.filters.getRawValue() }), state => { this.pageSize.set(state.pageSize); this.pageIndex.set(state.pageIndex); this.appliedFilters = state.applied; this.filters.reset(state.filterDraft); }, () => !this.loading() && !this.listLoading());

  private readonly http = inject(HttpClient);
  private readonly portfolioApi = inject(PortfolioService);
  private readonly catalogs = inject(CatalogService);
  private readonly externalApi = inject(ExternalAssetsService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(FormBuilder);
  private request?: Subscription;
  private previewRequest?: Subscription;
  private requestId = createRequestId();
  readonly id = signal<number | null>(null);
  readonly summary = signal<PortfolioSummary | null>(null);
  readonly currencies = signal<CatalogItem[]>([]);
  readonly balances = signal<ExternalAsset[]>([]);
  readonly movements = signal<MovementSummary[]>([]);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly total = signal(0);
  readonly listLoading = signal(false);
  readonly listError = signal('');
  readonly detail = signal<Movement | null>(null);
  readonly detailOpen = signal(false);
  readonly detailLoading = signal(false);
  readonly detailError = signal('');
  readonly formOpen = signal(false);
  private pageRequest?: Subscription;
  private detailRequest?: Subscription;
  readonly filters = this.fb.nonNullable.group({ search: [''], kind: [''], status: [''], from: [''], to: [''] });
  private appliedFilters = this.filters.getRawValue();

  activeFilters(): Record<string, string> { const v = this.appliedFilters; return { Busca: v.search, Tipo: this.labels[v.kind] || v.kind, Status: ({active:'Não estornado',reversed:'Estornado',reversal:'Registro de estorno'} as Record<string,string>)[v.status] || v.status, De: v.from, Até: v.to }; }
  hasAppliedFilters() { return Object.values(this.appliedFilters).some(value => !!value); }
  filtersPending() { return JSON.stringify(this.filters.getRawValue()) !== JSON.stringify(this.appliedFilters); }
  applyFilters() { this.appliedFilters = this.filters.getRawValue(); this.pageIndex.set(0); this.loadPage(); }
  clearFilters() { this.filters.reset(); this.applyFilters(); }
  changePage(event: { pageIndex: number; pageSize: number }) { this.pageIndex.set(event.pageIndex); this.pageSize.set(event.pageSize); this.loadPage(); }
  loadPage() {
    this.pageRequest?.unsubscribe(); this.listError.set('');
    const v = this.appliedFilters;
    if (v.from && v.to && v.from > v.to) { this.listLoading.set(false); this.listError.set('A data inicial deve ser anterior ou igual à final.'); return; }
    this.listLoading.set(true); this.movements.set([]);
    const params: Record<string, string | number> = { pageIndex: this.pageIndex(), pageSize: this.pageSize() };
    for (const [key, value] of Object.entries(v)) if (value.trim()) params[key] = value.trim();
    this.pageRequest = this.http.get<MovementPage>(`/api/portfolios/${this.id()}/movements/page`, { params })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: page => { this.movements.set(page.items); this.total.set(page.total); this.pageIndex.set(page.pageIndex); this.listLoading.set(false); },
        error: e => { this.listError.set(apiError(e)); this.listLoading.set(false); },
      });
  }
  closeDetail() { this.detailRequest?.unsubscribe(); this.detailOpen.set(false); this.detail.set(null); this.detailLoading.set(false); }
  showDetail(id: number) {
    this.closeDetail(); this.detailOpen.set(true); this.detailLoading.set(true); this.detailError.set('');
    this.detailRequest = this.http.get<Movement>(`/api/portfolios/${this.id()}/movements/${id}`)
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: m => { this.detail.set(m); this.detailLoading.set(false); },
        error: e => { this.detailError.set(apiError(e)); this.detailLoading.set(false); },
      });
  }
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly success = signal('');
  readonly formatNumber = formatBrazilianNumber;
  readonly today = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Sao_Paulo', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
  readonly pending = signal<Movement | null>(null);
  readonly executionOpen = signal(false);
  readonly executionSourceId = signal<number | null>(null);
  openExecution(m: Movement | null = null) { this.closeDetail(); this.executionSourceId.set(m?.id ?? null); this.executionOpen.set(true); }
  corrected(id: number) { this.executionOpen.set(false); this.success.set(`Correção #${id} aplicada. Saldos e histórico atualizados; confira as fotografias sinalizadas.`); this.load(); }
  canCorrect(m: Movement) { return !m.reversedById && !m.reversalOfId && ['buy', 'sell', 'income', 'deposit', 'withdrawal', 'transfer', 'adjustment'].includes(m.kind); }
  readonly previewOpen = signal(false);
  readonly previewSource = signal<Movement | null>(null);
  readonly preview = signal<CorrectionPreview | null>(null);
  readonly previewLoading = signal(false);
  readonly previewError = signal('');
  readonly dependencyLabels: Record<string, string> = { original: 'Registro a corrigir', direct: 'Envolve a posição ou saldo selecionado', indirect: 'Dependência indireta', unknown: 'Efeitos históricos insuficientes' };
  readonly reverseReason = signal('');
  private reverseId = createRequestId();
  readonly labels: Record<string, string> = { buy: 'Compra', sell: 'Venda', income: 'Provento', deposit: 'Depósito',
    correction: 'Lote de correção retroativa',
    withdrawal: 'Retirada', transfer: 'Transferência', adjustment: 'Ajuste de saldo', reversal: 'Estorno',
    historical: 'Registro histórico', 'position-adjustment': 'Ajuste de posição', 'income-conversion': 'Conversão de provento', opening: 'Saldo inicial', reopen: 'Reabertura de período', split: 'Desdobramento', 'reverse-split': 'Grupamento', bonus: 'Bonificação' };
  readonly kinds = Object.entries(this.labels);
  readonly form = this.fb.nonNullable.group({
    date: [this.today, Validators.required], kind: ['deposit'], cashAssetId: [0, Validators.min(1)],
    amount: ['', [Validators.required, brazilianNumberValidator(15, 4)]], destinationId: [0],
    reason: ['', Validators.maxLength(400)], baseAmount: [''],
  });
  readonly previewForm = this.fb.nonNullable.group({ date: [this.today, Validators.required], positionId: [0], cashAssetId: [0], destinationId: [0] });
  constructor() {
    this.previewForm.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.clearPreview());
  }
  private clearPreview() {
    this.previewRequest?.unsubscribe(); this.previewLoading.set(false); this.preview.set(null); this.previewError.set('');
  }
  closePreview() { this.clearPreview(); this.previewOpen.set(false); this.previewSource.set(null); }
  openPreview(source: Movement | null = null) {
    this.closeDetail(); this.clearPreview(); this.previewSource.set(source);
    this.previewForm.reset({ date: source?.date ?? this.today, positionId: 0, cashAssetId: 0, destinationId: 0 });
    this.previewOpen.set(true);
  }
  analyzeCorrection() {
    if (this.previewLoading() || this.previewForm.invalid || !this.id()) { this.previewForm.markAllAsTouched(); return; }
    const v = this.previewForm.getRawValue();
    if (!this.previewSource() && !v.positionId && !v.cashAssetId && !v.destinationId) {
      this.previewError.set('Selecione a posição e/ou os saldos envolvidos.'); return;
    }
    this.clearPreview(); this.previewLoading.set(true);
    this.previewRequest = this.http.post<CorrectionPreview>(`/api/portfolios/${this.id()}/movements/correction-preview`, {
      date: v.date, movementId: this.previewSource()?.id ?? null, positionId: Number(v.positionId) || null,
      cashAssetId: Number(v.cashAssetId) || null, destinationId: Number(v.destinationId) || null,
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: result => { this.preview.set(result); this.previewLoading.set(false); },
      error: e => { this.previewError.set(apiError(e)); this.previewLoading.set(false); },
    });
  }
  select(id: number) {
    this.context.save();
    if (this.saving()) return;
    this.closeDetail(); this.formOpen.set(false); this.pageIndex.set(0); this.filters.reset(); this.appliedFilters = this.filters.getRawValue();
    this.id.set(id); this.requestId = createRequestId(); this.pending.set(null);
    this.form.reset({ date: this.today, kind: 'deposit', cashAssetId: 0, amount: '', destinationId: 0, reason: '', baseAmount: '' });
    this.success.set(''); this.context.restore(id); this.load();
  }
  load() {
    const id = this.id(); if (id === null) return;
    this.closePreview(); this.closeDetail(); this.loadPage();
    this.request?.unsubscribe(); this.loading.set(true); this.summary.set(null); this.error.set('');
    this.request = forkJoin({ summary: this.portfolioApi.summary(id),
      currencies: this.catalogs.list('currencies'), balances: this.externalApi.summary(id) })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: data => { this.summary.set(data.summary); this.currencies.set(data.currencies);
          this.balances.set(data.balances.items); this.loading.set(false); },
        error: error => { this.error.set(apiError(error)); this.loading.set(false); },
      });
  }
  currency() { return this.balances().find(b => b.id === Number(this.form.controls.cashAssetId.value))?.currencyCode ?? ''; }
  destinations() { return this.balances().filter(b => b.id !== Number(this.form.controls.cashAssetId.value) && b.currencyCode === this.currency()); }
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
        this.formOpen.set(false); this.form.patchValue({ amount: '', reason: '', baseAmount: '' }); this.form.markAsUntouched(); this.load(); },
      error: e => { this.saving.set(false); this.error.set(apiError(e)); },
    });
  }
  confirm(m: Movement) { this.closeDetail(); this.pending.set(m); this.reverseReason.set(''); this.reverseId = createRequestId(); this.error.set(''); }
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
