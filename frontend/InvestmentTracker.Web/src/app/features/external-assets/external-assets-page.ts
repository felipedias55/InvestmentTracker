import { rememberContext } from '../../shared/page-context';
import { UnsavedForm } from '../../shared/unsaved-changes';
import { AppIcon } from '../../shared/app-icon';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ListPaginator } from '../../shared/list-paginator';
import { PrivateCurrencyPipe as CurrencyPipe } from '../../core/value-privacy';
import { EditPanel } from '../../shared/edit-panel';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin, Observable, Subscription } from 'rxjs';
import { PortfolioPicker } from '../../shared/portfolio-picker';
import { CatalogService } from '../catalogs/catalog.service';
import { CatalogItem } from '../catalogs/catalog.models';
import {
  ExternalAsset,
  ExternalAssetsService,
  ExternalAssetSummary,
} from './external-assets.service';
import { apiError } from '../../core/services/api-error';
import {
  brazilianNumberValidator,
  formatBrazilianNumber,
  parseBrazilianNumber,
} from '../../core/brazilian-number';

@Component({
  standalone: true,
  selector: 'app-external-assets-page',
  imports: [UnsavedForm, AppIcon, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, ListPaginator, CurrencyPipe, EditPanel, PortfolioPicker, ReactiveFormsModule, DatePipe, RouterLink],
  templateUrl: './external-assets-page.html',
  styleUrl: '../../shared/registration-list.css',
})
export class ExternalAssetsPage {
  private readonly context = rememberContext('external', () => ({ query: this.query(), currencyFilter: this.currencyFilter(), view: this.view(), pageSize: this.pageSize(), pageIndex: this.pageIndex() }), state => { this.query.set(state.query); this.currencyFilter.set(state.currencyFilter); this.view.set(state.view); this.pageSize.set(state.pageSize); this.pageIndex.set(state.pageIndex); }, () => !this.loading());

  private readonly api = inject(ExternalAssetsService);
  private readonly catalogs = inject(CatalogService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(FormBuilder).nonNullable;
  private request?: Subscription;
  readonly portfolioId = signal<number | null>(null);
  readonly summary = signal<ExternalAssetSummary | null>(null);
  readonly currencies = signal<CatalogItem[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly success = signal('');
  readonly editingId = signal<number | null>(null);
  readonly formOpen = signal(false);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly currentPage = computed(() => Math.min(this.pageIndex(), Math.max(0, Math.ceil(this.filteredItems().length / this.pageSize()) - 1)));
  readonly pageItems = computed(() => this.filteredItems().slice(this.currentPage() * this.pageSize(), (this.currentPage() + 1) * this.pageSize()));
  search(value: string) { this.query.set(value); this.pageIndex.set(0); }
  changePage(event: { pageIndex: number; pageSize: number }) { this.pageIndex.set(event.pageIndex); this.pageSize.set(event.pageSize); }
  newItem() { this.cancel(); this.error.set(''); this.success.set(''); this.formOpen.set(true); }
  readonly pendingDelete = signal<ExternalAsset | null>(null);
  readonly view = signal<'base' | 'original'>('base');
  readonly query = signal('');
  readonly currencyFilter = signal('');
  readonly filteredItems = computed(() => {
    const normalize = (v: string) => v.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('pt-BR');
    const query = normalize(this.query().trim());
    return (this.summary()?.items ?? []).filter(item => (!this.currencyFilter() || item.currencyCode === this.currencyFilter())
      && normalize([item.name, item.description ?? '', item.currencyCode].join(' ')).includes(query))
      .slice().sort((a, b) => a.name.localeCompare(b.name, 'pt-BR') || a.id - b.id);
  });
  readonly form = this.fb.group({
    name: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(200)]],
    currencyId: [0, Validators.min(1)],
    value: ['0', [Validators.required, brazilianNumberValidator(15, 4)]],
    description: ['', Validators.maxLength(500)],
  });
  select(id: number) {
    this.context.save();
    if (this.saving()) return;
    if (id !== this.portfolioId()) { this.summary.set(null); this.cancel(); this.query.set(''); this.currencyFilter.set(''); this.pageIndex.set(0); }
    this.portfolioId.set(id);
    this.success.set('');
    this.context.restore(id); this.load();
  }
  load() {
    const id = this.portfolioId();
    if (id === null) return;
    this.request?.unsubscribe();
    this.loading.set(true);
    this.error.set('');
    this.pendingDelete.set(null);
    this.request = forkJoin({
      summary: this.api.summary(id),
      currencies: this.catalogs.list('currencies'),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ summary, currencies }) => {
          this.summary.set(summary); this.pageIndex.set(this.currentPage());
          this.currencies.set(currencies);
          this.loading.set(false);
          this.cancel();
        },
        error: (e) => {
          this.error.set(apiError(e));
          this.loading.set(false);
        },
      });
  }
  cancel() {
    this.formOpen.set(false);
    this.editingId.set(null);
    this.form.reset({
      name: '',
      description: '',
      value: '0',
      currencyId: this.summary()?.portfolio.baseCurrencyId ?? 0,
    });
  }
  edit(item: ExternalAsset) {
    this.formOpen.set(true);
    this.editingId.set(item.id);
    this.pendingDelete.set(null);
    this.error.set('');
    this.success.set('');
    this.form.reset({
      name: item.name,
      description: item.description ?? '',
      currencyId: item.currencyId,
      value: formatBrazilianNumber(item.value),
    });
  }
  formatValue() {
    const control = this.form.controls.value;
    if (control.valid) control.setValue(formatBrazilianNumber(parseBrazilianNumber(control.value)));
  }
  save() {
    if (this.form.invalid || this.saving() || !this.summary()) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.getRawValue();
    const input = { ...raw, value: parseBrazilianNumber(raw.value) };
    const request: Observable<unknown> =
      this.editingId() === null
        ? this.api.create(this.portfolioId()!, input)
        : this.api.update(this.portfolioId()!, this.editingId()!, input);
    this.saving.set(true);
    this.error.set('');
    this.success.set('');
    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.saving.set(false);
        this.success.set('Patrimônio externo salvo.');
        this.load();
      },
      error: (e) => {
        this.saving.set(false);
        this.error.set(apiError(e));
      },
    });
  }
  remove() {
    const item = this.pendingDelete();
    if (!item || this.saving()) return;
    this.saving.set(true);
    this.error.set('');
    this.success.set('');
    this.api
      .delete(this.portfolioId()!, item.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.success.set('Patrimônio externo removido.');
          this.load();
        },
        error: (e) => {
          this.saving.set(false);
          this.error.set(apiError(e));
        },
      });
  }
}
