import { rememberContext } from '../../shared/page-context';
import { UnsavedForm } from '../../shared/unsaved-changes';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ListPaginator } from '../../shared/list-paginator';
import { EditPanel } from '../../shared/edit-panel';
import { PrivateCurrencyPipe as CurrencyPipe } from '../../core/value-privacy';
import { createRequestId } from '../../core/request-id';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { DatePipe, PercentPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin, Observable, Subscription } from 'rxjs';
import { PortfolioPicker } from '../../shared/portfolio-picker';
import { CatalogService } from '../catalogs/catalog.service';
import { CatalogItem } from '../catalogs/catalog.models';
import { AllocationTable } from '../allocation/allocation-table';
import { HistoryChart } from './history-chart';
import { MoneyChart, MoneyPoint } from '../../shared/money-chart';
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
  imports: [UnsavedForm, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, ListPaginator, EditPanel, CurrencyPipe,
    PortfolioPicker,
    HistoryChart,
    MoneyChart,
    AllocationTable,
    ReactiveFormsModule,
    PercentPipe,
    DatePipe,
    RouterLink,
  ],
  templateUrl: './history-page.html',
  styleUrl: './history-page.css',
})
export class HistoryPage {
  private readonly context = rememberContext('history', () => ({ section: this.section(), mode: this.mode(), selectedYear: this.selectedYear(), chartCurrency: this.chartCurrency(), breakdownPeriod: this.breakdownPeriod(), flowYear: this.flowYear(), flowKind: this.flowKind(), flowSize: this.flowSize(), flowIndex: this.flowIndex(), photoSize: this.photoSize(), photoIndex: this.photoIndex() }), state => { this.section.set(state.section); this.mode.set(state.mode); this.selectedYear.set(state.selectedYear); this.chartCurrency.set(state.chartCurrency); this.breakdownPeriod.set(state.breakdownPeriod); this.flowYear.set(state.flowYear); this.flowKind.set(state.flowKind); this.flowSize.set(state.flowSize); this.flowIndex.set(state.flowIndex); this.photoSize.set(state.photoSize); this.photoIndex.set(state.photoIndex); }, () => !this.loading() && !this.flowLoading());

  private readonly api = inject(HistoryService);
  private readonly catalogs = inject(CatalogService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(FormBuilder).nonNullable;
  private request?: Subscription;
  private detailRequest?: Subscription;
  private flowRequest?: Subscription;
  readonly section = signal<'evolution' | 'photos' | 'flows'>('evolution');
  readonly flowOpen = signal(false);
  readonly reopenOpen = signal(false);
  readonly snapshotOpen = signal(false);
  readonly rootSnapshot = signal<SnapshotDetail | null>(null);
  readonly flowYear = signal('');
  readonly flowKind = signal('');
  readonly flowIndex = signal(0);
  readonly flowSize = signal(10);
  readonly flowTotal = signal(0);
  readonly flowLoading = signal(false);
  readonly flowError = signal('');
  readonly flows = signal<CashFlow[]>([]);
  readonly photoIndex = signal(0);
  readonly photoSize = signal(10);
  readonly photos = computed(() => (this.data()?.months ?? []).filter(r => !this.selectedYear() || r.period.startsWith(this.selectedYear()!)).slice().reverse());
  readonly photoPage = computed(() => this.photos().slice(this.photoIndex() * this.photoSize(), (this.photoIndex() + 1) * this.photoSize()));
  setYear(year: string) { this.selectedYear.set(year); this.photoIndex.set(0); }
  showSection(section: 'evolution' | 'photos' | 'flows') {
    this.section.set(section);
    if (section === 'flows') this.loadFlows();
  }
  filterFlows() { this.flowIndex.set(0); this.loadFlows(); }
  changeFlowPage(event: { pageIndex: number; pageSize: number }) {
    this.flowIndex.set(event.pageIndex); this.flowSize.set(event.pageSize); this.loadFlows();
  }
  loadFlows() {
    if (!this.portfolioId()) return;
    this.flowRequest?.unsubscribe(); this.flowLoading.set(true); this.flowError.set(''); this.flows.set([]);
    this.flowRequest = this.api.flows(this.portfolioId()!, this.flowIndex(), this.flowSize(), this.flowYear(), this.flowKind())
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: page => { this.flows.set(page.items); this.flowTotal.set(page.total); this.flowIndex.set(page.pageIndex); this.flowSize.set(page.pageSize); this.flowLoading.set(false); },
        error: e => { this.flowError.set(apiError(e)); this.flowLoading.set(false); },
      });
  }
  newFlow() { this.cancelFlow(); this.error.set(''); this.flowOpen.set(true); }
  openCapture() { this.error.set(''); this.captureConfirmation.set(true); }
  openReopen() { this.error.set(''); this.reopenOpen.set(true); }
  selectRevision(revision: string) {
    const current = this.rootSnapshot();
    if (current) this.detail.set(current.revision === Number(revision) ? current : current.previousVersions?.find(v => v.revision === Number(revision)) ?? current);
  }
  closeSnapshot() { this.detailRequest?.unsubscribe(); this.snapshotOpen.set(false); this.detailLoading.set(false); this.detail.set(null); this.rootSnapshot.set(null); }

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
  readonly breakdownPeriod = signal('');
  readonly breakdownRow = computed(() => {
    const rows = this.rows().filter(r => r.currencyCode === this.chartCurrency());
    return rows.find(r => r.period === this.breakdownPeriod()) ?? rows.at(-1);
  });
  readonly breakdown = computed<MoneyPoint[]>(() => {
    const r = this.breakdownRow();
    if (!r || r.isOutdated || r.isReopened || r.totalWealth == null || r.change == null || r.netFlowsBetweenSnapshots == null || r.retainedIncome == null || r.valuationAndOtherChanges == null) return [];
    return [
      { label: 'Patrimônio inicial', value: Number(r.totalWealth) - Number(r.change), total: true },
      { label: 'Aportes líquidos', value: Number(r.netFlowsBetweenSnapshots) },
      { label: 'Proventos retidos', value: Number(r.retainedIncome) },
      { label: 'Valorização/outros', value: Number(r.valuationAndOtherChanges) },
      { label: 'Patrimônio final', value: Number(r.totalWealth), total: true },
    ];
  });
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

  readonly reopenForm = this.fb.group({ date: ['', Validators.required], reason: ['', [Validators.required, Validators.maxLength(400)]] });
  private reopenId = createRequestId();
  reopen() {
    if (this.saving() || this.reopenForm.invalid || !this.portfolioId()) { this.reopenForm.markAllAsTouched(); return; }
    this.saving.set(true); this.error.set('');
    const v = this.reopenForm.getRawValue();
    this.api.reopen(this.portfolioId()!, this.reopenId, v.date, v.reason.trim()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.saving.set(false); this.reopenId = createRequestId(); this.reopenForm.reset(); this.reopenOpen.set(false);
        this.success.set('Período reaberto. As fotografias afetadas foram preservadas e marcadas como desatualizadas.'); this.load(); },
      error: e => { this.saving.set(false); this.error.set(apiError(e)); },
    });
  }
  select(id: number) {
    this.context.save();
    if (this.saving()) return;
    this.flowIndex.set(0); this.photoIndex.set(0); this.flowYear.set(''); this.flowKind.set(''); this.chartCurrency.set('');
    this.portfolioId.set(id);
    this.selectedYear.set(null);
    this.success.set('');
    this.context.restore(id); this.load();
  }
  load() {
    const id = this.portfolioId();
    if (id === null || this.saving()) return;
    this.request?.unsubscribe();
    this.flowRequest?.unsubscribe(); this.closeSnapshot(); this.flowLoading.set(false); this.flows.set([]); this.flowTotal.set(0);
    this.detailLoading.set(false);
    this.detail.set(null);
    this.loading.set(true);
    this.data.set(null);
    this.error.set('');
    this.captureConfirmation.set(false);
    this.pendingDelete.set(null);
    this.request = forkJoin({
      history: this.api.get(id, false),
      currencies: this.catalogs.list('currencies'),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ history, currencies }) => {
          this.data.set(history);
          this.currencies.set(currencies);
          this.loading.set(false);
          if (this.selectedYear() === null) this.selectedYear.set(history.today.slice(0, 4));
          if (!this.chartCurrencies().includes(this.chartCurrency())) this.chartCurrency.set(history.portfolio.baseCurrencyCode);
          this.photoIndex.set(Math.min(this.photoIndex(), Math.max(0, Math.ceil(this.photos().length / this.photoSize()) - 1)));
          if (this.section() === 'flows') this.loadFlows();
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
    this.snapshotOpen.set(true); this.rootSnapshot.set(null);
    this.detailLoading.set(true);
    this.error.set('');
    this.detailRequest = this.api
      .snapshot(this.portfolioId()!, id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (detail) => {
          this.detail.set(detail); this.rootSnapshot.set(detail);
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
    this.flowOpen.set(false);
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
    this.editingFlow.set(flow); this.flowOpen.set(true);
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
