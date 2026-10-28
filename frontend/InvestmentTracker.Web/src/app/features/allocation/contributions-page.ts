import { SectionLink } from '../../shared/section-link';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { StrategyEditor, StrategyConfig } from './strategy-editor';
import { PrivateCurrencyPipe as CurrencyPipe } from '../../core/value-privacy';
import { Component, DestroyRef, inject, signal, computed } from '@angular/core';
import { PercentPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { AllocationTable } from './allocation-table';

import { PortfolioPicker } from '../../shared/portfolio-picker';
import { AllocationService, Contribution, Dashboard, JointPlan } from './allocation.service';
import { apiError } from '../../core/services/api-error';
import {
  brazilianNumberValidator,
  formatBrazilianNumber,
  parseBrazilianNumber,
} from '../../core/brazilian-number';

@Component({
  standalone: true,
  selector: 'app-contributions-page',
  imports: [SectionLink, MatCheckboxModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, StrategyEditor, CurrencyPipe, AllocationTable,
    PortfolioPicker,
    ReactiveFormsModule,

    PercentPipe,
    RouterLink,
  ],
  templateUrl: './contributions-page.html',
  styleUrl: './contributions-page.css',
})
export class ContributionsPage {
  private readonly api = inject(AllocationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(FormBuilder).nonNullable;
  private request?: Subscription;
  readonly portfolioId = signal<number | null>(null);
  readonly dashboard = signal<Dashboard | null>(null);
  readonly result = signal<Contribution | null>(null);
  readonly sectorResult = signal<Contribution | null>(null);
  readonly checked = signal(new Set<string>());
  readonly plan = signal<JointPlan | null>(null);
  readonly analyses = computed(() => [this.result(), this.sectorResult()].filter((r): r is Contribution => r !== null));
  private simulation?: Subscription;
  private version = 0;
  reset() {
    this.strategyNotes.set([]); this.eligibleBase.set(null);
    this.version++;
    this.simulation?.unsubscribe();
    this.saving.set(false);
    this.plan.set(null); this.result.set(null); this.sectorResult.set(null); this.checked.set(new Set());
  }
  toggle(key: string) {
    this.checked.update(current => { const next = new Set(current); if (next.has(key)) next.delete(key); else next.add(key); return next; });
  }
  readonly strategy = signal<StrategyConfig | null>(null);
  readonly strategyValid = signal(true);
  readonly strategyNotes = signal<string[]>([]);
  readonly eligibleBase = signal<string | null>(null);
  strategyChanged(value: { config: StrategyConfig | null; valid: boolean }) { this.strategy.set(value.config); this.strategyValid.set(value.valid); this.reset(); }
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly number = Number;
  readonly form = this.fb.group({
    amount: ['0', [Validators.required, brazilianNumberValidator(15, 2)]],

  });
  constructor() {
    this.form.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => this.reset());
  }
  select(id: number) {
    this.request?.unsubscribe();
    this.reset();
    this.portfolioId.set(id);
    this.dashboard.set(null);
    this.result.set(null);
    this.error.set('');
    this.loading.set(true);
    this.request = this.api
      .dashboard(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (data) => {
          this.dashboard.set(data);
          this.loading.set(false);
        },
        error: (e) => {
          this.error.set(apiError(e));
          this.loading.set(false);
        },
      });
  }
  formatAmount() {
    const control = this.form.controls.amount;
    if (control.valid)
      control.setValue(formatBrazilianNumber(parseBrazilianNumber(control.value)), {
        emitEvent: false,
      });
  }
  simulate() {
    if (this.form.invalid || !this.strategyValid() || this.saving() || !this.dashboard()) {
      this.form.markAllAsTouched();
      return;
    }
    this.reset();
    const version = this.version;
    this.saving.set(true);
    this.error.set('');
    const amount = parseBrazilianNumber(this.form.controls.amount.value);
    this.simulation = this.api.plan(this.portfolioId()!, amount, this.strategy())
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: response => {
          if (version !== this.version) return;
          this.strategyNotes.set(response.strategyNotes ?? []); this.eligibleBase.set(response.eligibleBase ?? null);
          this.dashboard.set(response.dashboard);
          this.result.set(response.category); this.sectorResult.set(response.sector);
          this.plan.set(response.plan); this.saving.set(false);
        },
        error: e => { if (version === this.version) { this.error.set(apiError(e)); this.saving.set(false); } }
      });
  }
}
