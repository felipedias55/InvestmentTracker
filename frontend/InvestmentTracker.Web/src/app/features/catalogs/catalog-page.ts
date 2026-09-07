import { Component, DestroyRef, inject, signal, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { CatalogItem, CatalogKey, catalogs } from './catalog.models';
import { CatalogService } from './catalog.service';
import { apiError } from '../../core/services/api-error';

@Component({
  standalone: true,
  selector: 'app-catalog-page',
  imports: [ReactiveFormsModule],
  templateUrl: './catalog-page.html',
})
export class CatalogPage implements OnInit {
  private readonly service = inject(CatalogService);
  private readonly destroyRef = inject(DestroyRef);
  readonly key = inject(ActivatedRoute).snapshot.data['catalog'] as CatalogKey;
  readonly config = catalogs.find((item) => item.key === this.key)!;
  readonly isCurrency = this.key === 'currencies';
  readonly items = signal<CatalogItem[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly success = signal('');
  readonly editing = signal<number | null>(null);
  readonly pendingDelete = signal<CatalogItem | null>(null);
  readonly form = inject(FormBuilder).nonNullable.group({
    name: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(100)]],
    code: ['', this.isCurrency ? [Validators.required, Validators.pattern(/^[a-zA-Z]{3}$/)] : []],
    symbol: ['', Validators.maxLength(10)],
  });
  ngOnInit() {
    this.load();
  }
  load() {
    this.loading.set(true);
    this.error.set('');
    this.service
      .list(this.key)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false)),
      )
      .subscribe({
        next: (items) => this.items.set(items),
        error: (error) => this.error.set(apiError(error)),
      });
  }
  edit(item: CatalogItem) {
    this.editing.set(item.id);
    this.pendingDelete.set(null);
    this.error.set('');
    this.success.set('');
    this.form.reset({ name: item.name, code: item.code ?? '', symbol: item.symbol ?? '' });
  }
  cancel() {
    this.editing.set(null);
    this.form.reset();
  }
  save() {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.error.set('');
    this.success.set('');
    const value = this.form.getRawValue();
    const input = this.isCurrency ? value : { name: value.name };
    const id = this.editing();
    const request =
      id === null ? this.service.create(this.key, input) : this.service.update(this.key, id, input);
    request
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.saving.set(false)),
      )
      .subscribe({
        next: () => {
          this.cancel();
          this.success.set('Cadastro salvo.');
          this.load();
        },
        error: (error) => this.error.set(apiError(error)),
      });
  }
  remove() {
    const item = this.pendingDelete();
    if (!item || this.saving()) return;
    this.saving.set(true);
    this.error.set('');
    this.success.set('');
    this.service
      .delete(this.key, item.id)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.saving.set(false)),
      )
      .subscribe({
        next: () => {
          this.pendingDelete.set(null);
          if (this.editing() === item.id) this.cancel();
          this.success.set('Cadastro excluído.');
          this.load();
        },
        error: (error) => this.error.set(apiError(error)),
      });
  }
}
