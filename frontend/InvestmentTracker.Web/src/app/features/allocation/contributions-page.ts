import { PrivateCurrencyPipe as CurrencyPipe } from '../../core/value-privacy';
import { Component, DestroyRef, inject, signal, computed } from '@angular/core';
import { PercentPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription, forkJoin, of, catchError } from 'rxjs';
import { AllocationTable } from './allocation-table';
import { contributionPlan, currencyOptions } from './contribution-plan';
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
  imports: [CurrencyPipe, AllocationTable,
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
  readonly plan = computed(() => {
    const category = this.result(), sector = this.sectorResult();
    return category && sector ? contributionPlan(category, sector, this.dashboard()!.summary.positions) : null;
  });
  readonly analyses = computed(() => [this.result(), this.sectorResult()].filter((r): r is Contribution => r !== null));
  private simulation?: Subscription;
  private version = 0;
  reset() {
    this.version++;
    this.simulation?.unsubscribe();
    this.saving.set(false);
    this.result.set(null); this.sectorResult.set(null); this.checked.set(new Set());
  }
  toggle(key: string) {
    this.checked.update(current => { const next = new Set(current); next.has(key) ? next.delete(key) : next.add(key); return next; });
  }
  equivalents(r: Contribution, groupId: number, amount: string) {
    return currencyOptions(amount, this.dashboard()!.summary.positions.filter(p => Number(p.quantity) > 0 &&
      (r.dimension === 'category' ? p.assetCategoryId : p.sectorId) === groupId), r.currencyCode);
  }
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
    if (this.form.invalid || this.saving() || !this.dashboard()) {
      this.form.markAllAsTouched();
      return;
    }
    this.reset();
    const version = this.version;
    this.saving.set(true);
    this.error.set('');
    const amount = parseBrazilianNumber(this.form.controls.amount.value);
    const d = this.dashboard()!;
    const request = (dimension: Dimension, configured: boolean) => configured
      ? this.api.simulate(this.portfolioId()!, dimension, amount).pipe(catchError(e => {
          this.error.update(previous => [previous, (dimension === 'category' ? 'Categoria: ' : 'Setor: ') + apiError(e)].filter(Boolean).join(' '));
          return of(null);
        }))
      : of(null);
    this.simulation = forkJoin([
      request('category', d.allocation.categoryTargetsConfigured),
      request('sector', d.allocation.sectorTargetsConfigured),
    ]).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(([category, sector]) => {
      if (version !== this.version) return;
      this.result.set(category); this.sectorResult.set(sector); this.saving.set(false);
    });
  }
}