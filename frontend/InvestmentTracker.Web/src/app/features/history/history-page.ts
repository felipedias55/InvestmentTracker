import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin, Observable, Subscription } from 'rxjs';
import { PortfolioPicker } from '../../shared/portfolio-picker';
import { CatalogService } from '../catalogs/catalog.service';
import { CatalogItem } from '../catalogs/catalog.models';
import { AllocationTable } from '../allocation/allocation-table';
import { HistoryChart } from './history-chart';
import { CashFlow, HistoryService, PortfolioHistory, SnapshotDetail } from './history.service';
import { apiError } from '../../core/services/api-error';
import {
  brazilianNumberValidator,
  formatBrazilianNumber,
  parseBrazilianNumber,
} from '../../core/brazilian-number';

@Component({
  standalone: true,
  selector: 'app-history-page',
  imports: [
    PortfolioPicker,
    HistoryChart,
    AllocationTable,
    ReactiveFormsModule,
    CurrencyPipe,
    DatePipe,
    RouterLink,
  ],
  templateUrl: './history-page.html',
  styleUrl: './history-page.css',
})
export class HistoryPage {
  private readonly api = inject(HistoryService);
  private readonly catalogs = inject(CatalogService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(FormBuilder).nonNullable;
  private request?: Subscription;
  private detailRequest?: Subscription;
  readonly portfolioId = signal<number | null>(null);
  readonly data = signal<PortfolioHistory | null>(null);
  readonly currencies = signal<CatalogItem[]>([]);
  readonly loading = signal(false);
  readonly detailLoading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly success = signal('');
  readonly detail = signal<SnapshotDetail | null>(null);
  readonly mode = signal<'months' | 'years'>('months');
  readonly selectedYear = signal<string | null>(null);
  readonly chartCurrency = signal('');
  readonly captureConfirmation = signal(false);
  readonly editingFlow = signal<CashFlow | null>(null);
  readonly pendingDelete = signal<CashFlow | null>(null);
  readonly formatNumber = formatBrazilianNumber;
  readonly years = computed(
    () =>
      this.data()
        ?.years.map((y) => y.period)
        .reverse() ?? [],
  );
  readonly rows = computed(() =>
    this.mode() === 'years'
      ? (this.data()?.years ?? [])
      : (this.data()?.months ?? []).filter(
          (m) => !this.selectedYear() || m.period.startsWith(this.selectedYear() ?? ''),
        ),
  );
  readonly flows = computed(() =>
    (this.data()?.cashFlows ?? []).filter(
      (f) => !this.selectedYear() || f.date.startsWith(this.selectedYear() ?? ''),
    ),
  );
  readonly chartCurrencies = computed(() =>
    [
      ...new Set([
        this.data()?.portfolio.baseCurrencyCode ?? '',
        ...(this.data()?.months ?? [])
          .filter((m) => m.snapshotId !== null)
          .map((m) => m.currencyCode),
      ]),
    ].filter(Boolean),
  );
  readonly currentSnapshot = computed(
    () =>
      this.data()?.months.find((m) => m.period === this.data()?.today.slice(0, 7))?.snapshotId ??
      null,
  );
  readonly flowBaseCode = computed(
    () => this.editingFlow()?.baseCurrencyCode ?? this.data()?.portfolio.baseCurrencyCode ?? '',
  );
  readonly form = this.fb.group({
    date: ['', Validators.required],
    kind: ['contribution' as 'contribution' | 'withdrawal'],
    currencyId: [0, Validators.min(1)],
    amount: ['', [Validators.required, brazilianNumberValidator(15, 4)]],
    baseAmount: [
      '',
      (c: import('@angular/forms').AbstractControl) =>
        c.value === '' ? null : brazilianNumberValidator(15, 4)(c),
    ],
    notes: ['', Validators.maxLength(500)],
  });

  select(id: number) {
    this.portfolioId.set(id);
    this.selectedYear.set(null);
    this.success.set('');
    this.load();
  }
  load() {
    const id = this.portfolioId();
    if (id === null) return;
    this.request?.unsubscribe();
    this.detailRequest?.unsubscribe();
    this.detailLoading.set(false);
    this.detail.set(null);
    this.loading.set(true);
    this.data.set(null);
    this.error.set('');
    this.captureConfirmation.set(false);
    this.pendingDelete.set(null);
    this.request = forkJoin({
      history: this.api.get(id),
      currencies: this.catalogs.list('currencies'),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ history, currencies }) => {
          this.data.set(history);
          this.currencies.set(currencies);
          this.loading.set(false);
          if (this.selectedYear() === null) this.selectedYear.set(history.today.slice(0, 4));
          this.chartCurrency.set(history.portfolio.baseCurrencyCode);
          this.cancelFlow();
        },
        error: (e) => {
          this.error.set(apiError(e));
          this.loading.set(false);
        },
      });
  }
  openSnapshot(id: number) {
    this.detailRequest?.unsubscribe();
    this.detail.set(null);
    this.detailLoading.set(true);
    this.error.set('');
    this.detailRequest = this.api
      .snapshot(this.portfolioId()!, id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (detail) => {
          this.detail.set(detail);
          this.detailLoading.set(false);
        },
        error: (e) => {
          this.error.set(apiError(e));
          this.detailLoading.set(false);
        },
      });
  }
  capture() {
    if (!this.data() || this.saving()) return;
    this.saving.set(true);
    this.error.set('');
    this.success.set('');
    this.api
      .capture(this.portfolioId()!, this.currentSnapshot() !== null)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.success.set('Fotografia registrada com os valores e referências de hoje.');
          this.load();
        },
        error: (e) => {
          this.saving.set(false);
          this.error.set(apiError(e));
        },
      });
  }
  cancelFlow() {
    this.editingFlow.set(null);
    this.form.reset({
      date: this.data()?.today ?? '',
      kind: 'contribution',
      currencyId: this.data()?.portfolio.baseCurrencyId ?? 0,
      amount: '',
      baseAmount: '',
      notes: '',
    });
  }
  editFlow(flow: CashFlow) {
    this.editingFlow.set(flow);
    this.pendingDelete.set(null);
    this.error.set('');
    this.success.set('');
    this.form.reset({
      date: flow.date,
      kind: flow.kind,
      currencyId: flow.currencyId,
      amount: formatBrazilianNumber(flow.amount),
      baseAmount: flow.baseAmount === null ? '' : formatBrazilianNumber(flow.baseAmount),
      notes: flow.notes ?? '',
    });
  }
  foreignCurrency() {
    return (
      this.currencies().find((c) => c.id === Number(this.form.controls.currencyId.value))?.code !==
      this.flowBaseCode()
    );
  }
  formatInput(field: 'amount' | 'baseAmount') {
    const control = this.form.controls[field];
    if (control.valid && control.value)
      control.setValue(formatBrazilianNumber(parseBrazilianNumber(control.value)));
  }
  saveFlow() {
    if (!this.data() || this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.getRawValue();
    const input = {
      ...raw,
      amount: parseBrazilianNumber(raw.amount),
      baseAmount:
        this.foreignCurrency() && raw.baseAmount !== ''
          ? parseBrazilianNumber(raw.baseAmount)
          : null,
    };
    const request: Observable<unknown> =
      this.editingFlow() === null
        ? this.api.createFlow(this.portfolioId()!, input)
        : this.api.updateFlow(this.portfolioId()!, this.editingFlow()!.id, input);
    this.saving.set(true);
    this.error.set('');
    this.success.set('');
    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.saving.set(false);
        this.success.set('Movimento registrado. Os saldos das posições não foram alterados.');
        this.load();
      },
      error: (e) => {
        this.error.set(apiError(e));
        this.saving.set(false);
      },
    });
  }
  deleteFlow() {
    const flow = this.pendingDelete();
    if (!flow || this.saving()) return;
    this.saving.set(true);
    this.error.set('');
    this.success.set('');
    this.api
      .deleteFlow(this.portfolioId()!, flow.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.success.set('Movimento removido. As comparações foram recalculadas.');
          this.load();
        },
        error: (e) => {
          this.error.set(apiError(e));
          this.saving.set(false);
        },
      });
  }
}
