import { Component, DestroyRef, inject, signal } from '@angular/core';
import { CurrencyPipe, DecimalPipe, PercentPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { PortfolioPicker } from '../../shared/portfolio-picker';
import { AllocationService, Contribution, Dashboard, Dimension } from './allocation.service';
import { apiError } from '../../core/services/api-error';
import {
  brazilianNumberValidator,
  formatBrazilianNumber,
  parseBrazilianNumber,
} from '../../core/brazilian-number';

@Component({
  standalone: true,
  selector: 'app-contributions-page',
  imports: [
    PortfolioPicker,
    ReactiveFormsModule,
    CurrencyPipe,
    DecimalPipe,
    PercentPipe,
    RouterLink,
  ],
  templateUrl: './contributions-page.html',
})
export class ContributionsPage {
  private readonly api = inject(AllocationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(FormBuilder).nonNullable;
  private request?: Subscription;
  readonly portfolioId = signal<number | null>(null);
  readonly dashboard = signal<Dashboard | null>(null);
  readonly result = signal<Contribution | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly number = Number;
  readonly form = this.fb.group({
    amount: ['0', [Validators.required, brazilianNumberValidator(15, 2)]],
    dimension: ['category' as Dimension],
  });
  constructor() {
    this.form.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => this.result.set(null));
  }
  select(id: number) {
    this.request?.unsubscribe();
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
    if (this.form.invalid || this.saving() || !this.dashboard()) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.result.set(null);
    this.error.set('');
    const input = this.form.getRawValue();
    this.api
      .simulate(this.portfolioId()!, input.dimension, parseBrazilianNumber(input.amount))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.result.set(result);
          this.saving.set(false);
        },
        error: (e) => {
          this.error.set(apiError(e));
          this.saving.set(false);
        },
      });
  }
}
