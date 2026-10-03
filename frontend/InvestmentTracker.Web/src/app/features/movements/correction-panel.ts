import { Component, DestroyRef, EventEmitter, Input, OnInit, Output, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin, of, Observable, Subscription } from 'rxjs';
import { EditPanel } from '../../shared/edit-panel';
import { createRequestId } from '../../core/request-id';
import { apiError } from '../../core/services/api-error';
import { brazilianNumberValidator, formatBrazilianNumber, parseBrazilianNumber } from '../../core/brazilian-number';
import { Asset } from '../assets/asset.service';
import { ExternalAsset } from '../external-assets/external-assets.service';

export interface CorrectionOperation {
  kind: string; date: string; assetId: number | null; cashAssetId: number | null; destinationId: number | null;
  quantity: string; unitPrice: string; fees: string; amount: string; baseAmount: string | null;
}
export interface CorrectionInput { movementId: number | null; reason: string; operation: CorrectionOperation; }
export interface CorrectionSimulation {
  canApply: boolean; token: string; fromDate: string; correctionId: number | null; replacedMovementIds: number[];
  balances: { name: string; currencyCode: string; isPosition: boolean; beforeValue: string; afterValue: string;
    beforeQuantity: string; afterQuantity: string; beforeCost: string; afterCost: string; beforeIncome: string; afterIncome: string }[];
  snapshots: { id: number; month: string; revision: number }[]; warnings: string[]; blockers: string[];
}
@Component({
  standalone: true, selector: 'app-correction-panel', imports: [ReactiveFormsModule, CurrencyPipe, DatePipe, EditPanel],
  templateUrl: './correction-panel.html',
})
export class CorrectionPanel implements OnInit {
  @Input({ required: true }) portfolioId!: number;
  @Input() movementId: number | null = null;
  @Input() balances: ExternalAsset[] = [];
  @Input() baseCurrency = '';
  @Output() closed = new EventEmitter<void>();
  @Output() completed = new EventEmitter<number>();
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly destroyRef = inject(DestroyRef);
  private pending?: Subscription;
  private requestId = createRequestId();
  private simulatedInput: CorrectionInput | null = null;
  readonly assets = signal<Asset[]>([]);
  readonly loading = signal(true);
  readonly simulating = signal(false);
  readonly applying = signal(false);
  readonly error = signal('');
  readonly result = signal<CorrectionSimulation | null>(null);
  readonly accepted = signal(false);
  readonly format = formatBrazilianNumber;
  readonly today = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Sao_Paulo', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
  readonly form = this.fb.group({ kind: ['buy'], date: [this.today, Validators.required], assetId: [0], cashAssetId: [0], destinationId: [0],
    quantity: ['0'], unitPrice: ['0'], fees: ['0'], amount: ['0'], baseAmount: [''], reason: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(400)]] });
  constructor() {
    for (const field of ['kind', 'date', 'assetId', 'quantity', 'unitPrice', 'fees', 'amount'] as const) {
      (this.form.controls[field].valueChanges as Observable<unknown>).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.form.controls.baseAmount.setValue('', { emitEvent: false }));
    }
    this.form.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.pending?.unsubscribe(); this.simulating.set(false); this.result.set(null); this.accepted.set(false);
      this.simulatedInput = null; this.requestId = createRequestId(); this.error.set('');
    });
  }
  ngOnInit() { this.load(); }
  load() {
    this.loading.set(true); this.error.set('');
    forkJoin({ assets: this.http.get<Asset[]>('/api/assets'), draft: this.movementId === null ? of(null) :
      this.http.get<CorrectionOperation>(`/api/portfolios/${this.portfolioId}/movements/${this.movementId}/correction-draft`) })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: ({ assets, draft }) => {
          this.assets.set(assets);
          if (draft) this.form.patchValue({ ...draft, assetId: draft.assetId ?? 0, cashAssetId: draft.cashAssetId ?? 0, destinationId: draft.destinationId ?? 0,
            quantity: this.format(draft.quantity), unitPrice: this.format(draft.unitPrice), fees: this.format(draft.fees), amount: this.format(draft.amount), baseAmount: draft.baseAmount === null ? '' : this.format(draft.baseAmount) });
          this.loading.set(false);
        }, error: e => { this.loading.set(false); this.error.set(apiError(e)); },
      });
  }
  isTrade() { return ['buy', 'sell'].includes(this.form.controls.kind.value); }
  usesAsset() { return this.isTrade() || this.form.controls.kind.value === 'income'; }
  needsEquivalent() { return this.form.controls.kind.value === 'income' || (this.isTrade() && !this.form.controls.cashAssetId.value) || ['deposit', 'withdrawal'].includes(this.form.controls.kind.value); }
  simulate() {
    if (this.loading() || this.applying() || this.simulating() || this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    const fields: ('quantity' | 'unitPrice' | 'fees' | 'amount' | 'baseAmount')[] = this.isTrade() ? ['quantity', 'unitPrice', 'fees'] : ['amount'];
    if (this.needsEquivalent() && v.baseAmount.trim()) fields.push('baseAmount');
    if (fields.some(f => !!brazilianNumberValidator(f === 'quantity' ? 13 : 15, f === 'quantity' ? 6 : 4)(this.form.controls[f]))) {
      this.error.set('Use valores no padrão brasileiro; quantidade com até 6 decimais e valores com até 4.'); return;
    }
    if ((this.usesAsset() && !v.assetId) || (!this.usesAsset() && !v.cashAssetId) || (v.kind === 'transfer' && !v.destinationId)) {
      this.error.set('Selecione o ativo e os saldos envolvidos na operação.'); return;
    }
    const input: CorrectionInput = { movementId: this.movementId, reason: v.reason.trim(), operation: {
      kind: v.kind, date: v.date, assetId: this.usesAsset() ? Number(v.assetId) : null, cashAssetId: Number(v.cashAssetId) || null,
      destinationId: v.kind === 'transfer' ? Number(v.destinationId) : null,
      quantity: this.isTrade() ? parseBrazilianNumber(v.quantity) : '0', unitPrice: this.isTrade() ? parseBrazilianNumber(v.unitPrice) : '0',
      fees: this.isTrade() ? parseBrazilianNumber(v.fees) : '0', amount: this.isTrade() ? '0' : parseBrazilianNumber(v.amount),
      baseAmount: this.needsEquivalent() && v.baseAmount.trim() ? parseBrazilianNumber(v.baseAmount) : null,
    } };
    this.error.set(''); this.result.set(null); this.accepted.set(false); this.simulating.set(true);
    this.pending = this.http.post<CorrectionSimulation>(`/api/portfolios/${this.portfolioId}/movements/correction-simulation`, input)
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: result => { this.simulatedInput = input; this.result.set(result); this.simulating.set(false); this.requestId = createRequestId(); },
        error: e => { this.error.set(apiError(e)); this.simulating.set(false); },
      });
  }
  apply() {
    const result = this.result();
    if (this.applying() || !this.accepted() || !result?.canApply || !this.simulatedInput) return;
    this.applying.set(true); this.error.set('');
    this.http.post<CorrectionSimulation>(`/api/portfolios/${this.portfolioId}/movements/corrections`, {
      requestId: this.requestId, token: result.token, input: this.simulatedInput,
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: applied => { this.applying.set(false); this.completed.emit(applied.correctionId!); },
      error: e => {
        this.applying.set(false); this.error.set(apiError(e));
        if (e.status === 409) { this.result.set(null); this.accepted.set(false); this.simulatedInput = null; }
      },
    });
  }
}
