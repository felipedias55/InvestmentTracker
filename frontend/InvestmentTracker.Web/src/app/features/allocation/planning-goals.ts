import { Component, DestroyRef, effect, inject, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { PrivateCurrencyPipe } from '../../core/value-privacy';
import { apiError } from '../../core/services/api-error';
import { createRequestId } from '../../core/request-id';
import { brazilianNumberValidator, formatBrazilianNumber, parseBrazilianNumber } from '../../core/brazilian-number';
import { EditPanel } from '../../shared/edit-panel';
import { DraftState } from '../../shared/draft-state';
import { ListPaginator } from '../../shared/list-paginator';
import { PlanningGoalsService, PlanningGoalPage, SavedPlanningGoal, SavePlanningGoal } from './planning-goals.service';

@Component({ selector: 'app-planning-goals', standalone: true,
  imports: [DatePipe, ReactiveFormsModule, MatButtonModule, MatCheckboxModule, MatFormFieldModule, MatInputModule, MatSelectModule, RouterLink, PrivateCurrencyPipe, EditPanel, DraftState, ListPaginator],
  templateUrl: './planning-goals.html', styleUrl: './planning-goals.css' })
export class PlanningGoals {
  readonly portfolioId = input.required<number>();
  private readonly api = inject(PlanningGoalsService); private readonly destroyRef = inject(DestroyRef); private readonly fb = inject(FormBuilder).nonNullable;
  private readRequest?: Subscription; private mutation?: Subscription; private historyRequest?: Subscription;
  private goalId = ''; private revision = 0;
  readonly open = signal(false); readonly editing = signal(false); readonly loading = signal(false); readonly saving = signal(false);
  readonly error = signal(''); readonly notice = signal(''); readonly page = signal<PlanningGoalPage | null>(null);
  readonly search = signal(''); readonly kind = signal(''); readonly status = signal('active');
  readonly history = signal<SavedPlanningGoal[] | null>(null); readonly historyOpen = signal(false); readonly historyPage = signal(0);
  readonly format = formatBrazilianNumber; readonly number = Number;
  readonly labels: Record<string, string> = { 'in-progress': 'Em andamento', achieved: 'Atingido', overdue: 'Prazo encerrado', unavailable: 'Base indisponível', archived: 'Arquivado', 'currency-changed': 'Moeda alterada' };
  readonly form = this.fb.group({ name: ['', [Validators.required, Validators.maxLength(100)]], kind: ['wealth', Validators.required], currency: [''],
    target: ['', [Validators.required, brazilianNumberValidator(15, 2)]], deadline: ['', Validators.required], monthlyContribution: ['0', brazilianNumberValidator(15, 2)], archived: [false] });
  constructor() {
    effect(() => { this.portfolioId(); this.readRequest?.unsubscribe(); this.mutation?.unsubscribe(); this.historyRequest?.unsubscribe();
      this.page.set(null); this.open.set(false); this.editing.set(false); this.historyOpen.set(false); this.history.set(null); this.loading.set(false); this.saving.set(false); this.error.set(''); this.notice.set(''); this.search.set(''); this.kind.set(''); this.status.set('active'); });
  }
  show() { this.open.set(true); this.load(); }
  load(pageIndex = 0, pageSize = this.page()?.pageSize ?? 10) {
    if (this.saving()) return;
    this.readRequest?.unsubscribe(); this.loading.set(true); this.error.set('');
    this.readRequest = this.api.page(this.portfolioId(), pageIndex, pageSize, this.search(), this.kind(), this.status()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: value => { this.page.set(value); this.loading.set(false); }, error: e => { this.error.set(apiError(e)); this.loading.set(false); } });
  }
  start(goal?: SavedPlanningGoal) {
    const page = this.page(); if (!page || this.saving()) return;
    this.goalId = goal?.goalId ?? createRequestId(); this.revision = goal?.revision ?? 0; this.error.set(''); this.notice.set('');
    const c = goal?.config;
    this.form.reset({ name: c?.name ?? '', kind: c?.kind ?? 'wealth', currency: c?.currency ?? page.currency, target: c ? formatBrazilianNumber(c.target) : '',
      deadline: c?.deadline ?? `${page.today.slice(0, 4)}-12-31`, monthlyContribution: c ? formatBrazilianNumber(c.monthlyContribution) : '0', archived: c?.archived ?? false });
    this.editing.set(true);
  }
  changeKind() { if (this.form.controls.kind.value === 'monthly-income') this.form.controls.monthlyContribution.setValue('0'); }
  save() {
    if (this.saving() || this.form.invalid || !this.form.controls.name.value.trim()) { this.form.markAllAsTouched(); return; }
    const value = this.form.getRawValue();
    const request: SavePlanningGoal = { goalId: this.goalId, expectedRevision: this.revision,
      config: { ...value, name: value.name.trim(), target: parseBrazilianNumber(value.target), monthlyContribution: value.kind === 'wealth' ? parseBrazilianNumber(value.monthlyContribution) : '0' } };
    this.submit(request);
  }
  archive(goal: SavedPlanningGoal) { if (!this.saving()) this.submit({ goalId: goal.goalId, expectedRevision: goal.revision, config: { ...goal.config, archived: true } }); }
  private submit(request: SavePlanningGoal) {
    this.saving.set(true); this.error.set('');
    this.mutation = this.api.save(this.portfolioId(), request).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.saving.set(false); this.editing.set(false); this.notice.set('Objetivo salvo com uma nova revisão. Nenhuma operação financeira foi registrada.'); this.load(this.page()?.pageIndex ?? 0); },
      error: e => { this.saving.set(false); this.error.set(apiError(e)); } });
  }
  revisions(goal: SavedPlanningGoal) {
    this.historyRequest?.unsubscribe(); this.history.set(null); this.historyPage.set(0); this.historyOpen.set(true); this.error.set('');
    this.historyRequest = this.api.history(this.portfolioId(), goal.goalId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: value => this.history.set(value), error: e => this.error.set(apiError(e)) });
  }
}
