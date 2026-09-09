import { EditPanel } from '../../shared/edit-panel';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
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
  imports: [EditPanel, PortfolioPicker, ReactiveFormsModule, CurrencyPipe, DatePipe, RouterLink],
  templateUrl: './external-assets-page.html',
})
export class ExternalAssetsPage {
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
  readonly pendingDelete = signal<ExternalAsset | null>(null);
  readonly view = signal<'base' | 'original'>('base');
  readonly form = this.fb.group({
    name: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(200)]],
    currencyId: [0, Validators.min(1)],
    value: ['0', [Validators.required, brazilianNumberValidator(15, 4)]],
    description: ['', Validators.maxLength(500)],
  });
  select(id: number) {
    this.portfolioId.set(id);
    this.success.set('');
    this.load();
  }
  load() {
    const id = this.portfolioId();
    if (id === null) return;
    this.request?.unsubscribe();
    this.summary.set(null);
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
          this.summary.set(summary);
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
    this.editingId.set(null);
    this.form.reset({
      name: '',
      description: '',
      value: '0',
      currencyId: this.summary()?.portfolio.baseCurrencyId ?? 0,
    });
  }
  edit(item: ExternalAsset) {
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
