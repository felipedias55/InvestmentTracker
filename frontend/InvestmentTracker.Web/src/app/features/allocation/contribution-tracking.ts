import { Component, DestroyRef, effect, inject, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { AbstractControl, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { PrivateCurrencyPipe } from '../../core/value-privacy';
import { apiError } from '../../core/services/api-error';
import { createRequestId } from '../../core/request-id';
import { brazilianNumberValidator, parseBrazilianNumber } from '../../core/brazilian-number';
import { EditPanel } from '../../shared/edit-panel';
import { DraftState } from '../../shared/draft-state';
import { ListPaginator } from '../../shared/list-paginator';
import { ContributionTrackingService, PurchaseCandidate, SaveTrackingPlan, TrackingDetail, TrackingPage, TrackingSimulation, TrackingSummary } from './contribution-tracking.service';
@Component({ selector: 'app-contribution-tracking', standalone: true,
  imports: [DatePipe, ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, RouterLink, PrivateCurrencyPipe, EditPanel, DraftState, ListPaginator],
  templateUrl: './contribution-tracking.html', styleUrl: './contribution-tracking.css' })
export class ContributionTracking {
  readonly portfolioId = input.required<number>(); readonly simulation = input<TrackingSimulation | null>(null);
  private readonly api = inject(ContributionTrackingService); private readonly destroyRef = inject(DestroyRef); private readonly fb = inject(FormBuilder).nonNullable;
  private listRequest?: Subscription; private detailRequest?: Subscription; private candidateRequest?: Subscription; private mutation?: Subscription;
  private saveRequest?: SaveTrackingPlan;
  readonly open = signal(false); readonly saving = signal(false); readonly loading = signal(false); readonly error = signal(''); readonly notice = signal('');
  readonly saveOpen = signal(false); readonly linking = signal<PurchaseCandidate | null>(null);
  readonly page = signal<TrackingPage<TrackingSummary> | null>(null); readonly detail = signal<TrackingDetail | null>(null); readonly candidates = signal<TrackingPage<PurchaseCandidate> | null>(null);
  readonly rowPage = signal(0); readonly historyPage = signal(0);
  readonly today = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Sao_Paulo', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
  readonly saveForm = this.fb.group({ name: ['', [Validators.required, Validators.maxLength(100)]], from: [this.today, Validators.required], to: [this.today, Validators.required] });
  readonly linkForm = this.fb.group({ rowKey: ['', Validators.required], baseAmount: ['', (control: AbstractControl) => !control.value ? null : brazilianNumberValidator(15, 4)(control)] });
  readonly labels: Record<string, string> = { pending: 'Pendente', partial: 'Parcial', completed: 'Concluído', exceeded: 'Acima do planejado', unallocated: 'Sem distribuição' };
  constructor() { effect(() => { this.portfolioId(); this.cancel(); this.open.set(false); this.page.set(null); this.detail.set(null); this.candidates.set(null); this.error.set(''); this.notice.set(''); this.saveOpen.set(false); this.linking.set(null); this.saving.set(false); this.loading.set(false); }); }
  private cancel() { this.listRequest?.unsubscribe(); this.detailRequest?.unsubscribe(); this.candidateRequest?.unsubscribe(); this.mutation?.unsubscribe(); }
  show() { this.open.set(true); this.load(); }
  load(page = 0, size = 10) {
    this.listRequest?.unsubscribe(); this.loading.set(true); this.error.set('');
    this.listRequest = this.api.list(this.portfolioId(), page, size).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: value => { this.page.set(value); this.loading.set(false); }, error: e => { this.error.set(apiError(e)); this.loading.set(false); } });
  }
  read(id: number) {
    if (this.saving()) return;
    this.detailRequest?.unsubscribe(); this.candidateRequest?.unsubscribe(); this.detail.set(null); this.candidates.set(null); this.loading.set(true); this.error.set('');
    this.detailRequest = this.api.read(this.portfolioId(), id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: value => { this.detail.set(value); this.rowPage.set(0); this.historyPage.set(0); this.loading.set(false); this.loadCandidates(); }, error: e => { this.error.set(apiError(e)); this.loading.set(false); } });
  }
  loadCandidates(page = 0, size = 10) {
    const detail = this.detail(); if (!detail) return;
    this.candidateRequest?.unsubscribe(); this.candidates.set(null);
    this.candidateRequest = this.api.candidates(this.portfolioId(), detail.plan.id, page, size).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: value => this.candidates.set(value), error: e => this.error.set(apiError(e)) });
  }
  startSave() { this.error.set(''); this.notice.set(''); this.saveForm.reset({ name: 'Plano de aporte', from: this.today, to: this.today }); this.saveRequest = undefined; this.saveOpen.set(true); }
  save() {
    const simulation = this.simulation();
    if (!simulation || this.saving() || this.saveForm.invalid || this.saveForm.controls.from.value > this.saveForm.controls.to.value) { this.saveForm.markAllAsTouched(); return; }
    const values = { ...simulation, ...this.saveForm.getRawValue() };
    if (!this.saveRequest || JSON.stringify({ ...this.saveRequest, requestId: undefined }) !== JSON.stringify({ ...values, requestId: undefined })) this.saveRequest = { ...values, requestId: createRequestId() };
    this.saving.set(true); this.error.set('');
    this.mutation = this.api.save(this.portfolioId(), this.saveRequest).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: value => { this.saving.set(false); this.notice.set('Plano salvo. Vincule as compras registradas para acompanhar a execução.'); this.saveOpen.set(false); this.open.set(true); this.detail.set(value); this.rowPage.set(0); this.historyPage.set(0); this.load(); this.loadCandidates(); }, error: e => { this.saving.set(false); this.error.set(apiError(e)); } });
  }
  matching(trade: PurchaseCandidate) { return this.detail()?.rows.filter(r => r.planned.categoryId === trade.categoryId && r.planned.sectorId === trade.sectorId && r.planned.currencies.some(c => c.currency === trade.currency)) ?? []; }
  beginLink(trade: PurchaseCandidate) { this.error.set(''); this.notice.set(''); this.linking.set(trade); this.linkForm.reset({ rowKey: this.matching(trade)[0]?.planned.key ?? '', baseAmount: '' }); }
  needsEquivalent() { const t = this.linking(); return !!t && t.currency !== this.detail()?.snapshot.currency && t.baseAmount === null; }
  link() {
    const trade = this.linking(), detail = this.detail();
    if (!trade || !detail || this.saving() || this.linkForm.invalid || (this.needsEquivalent() && !this.linkForm.controls.baseAmount.value)) { this.linkForm.markAllAsTouched(); return; }
    this.saving.set(true); this.error.set('');
    this.mutation = this.api.link(this.portfolioId(), detail.plan.id, { tradeId: trade.tradeId, rowKey: this.linkForm.controls.rowKey.value, baseAmount: this.needsEquivalent() ? parseBrazilianNumber(this.linkForm.controls.baseAmount.value) : null }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.saving.set(false); this.linking.set(null); this.notice.set('Compra vinculada. Os valores da carteira foram preservados.'); this.read(detail.plan.id); }, error: e => { this.saving.set(false); this.error.set(apiError(e)); } });
  }
  unlink(id: number) {
    const detail = this.detail(); if (!detail || this.saving()) return;
    this.saving.set(true); this.error.set('');
    this.mutation = this.api.unlink(this.portfolioId(), detail.plan.id, id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => { this.saving.set(false); this.notice.set('Vínculo removido; a compra continua registrada.'); this.read(detail.plan.id); }, error: e => { this.saving.set(false); this.error.set(apiError(e)); } });
  }
}
