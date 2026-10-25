import { rememberContext } from '../../shared/page-context';
import { UnsavedForm } from '../../shared/unsaved-changes';
import { AppIcon } from '../../shared/app-icon';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ListPaginator } from '../../shared/list-paginator';
import { EditPanel } from '../../shared/edit-panel';
import { Component, computed, DestroyRef, inject, signal, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize, Subscription } from 'rxjs';
import { CatalogItem, CatalogKey, catalogs } from './catalog.models';
import { CatalogService } from './catalog.service';
import { apiError } from '../../core/services/api-error';

@Component({
  standalone: true,
  selector: 'app-catalog-page',
  imports: [UnsavedForm, AppIcon, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, ListPaginator, EditPanel, ReactiveFormsModule],
  templateUrl: './catalog-page.html',
  styleUrl: '../../shared/registration-list.css',
})
export class CatalogPage implements OnInit {
  private readonly context = rememberContext('catalogs', () => ({ query: this.query(), pageSize: this.pageSize(), pageIndex: this.pageIndex() }), state => { this.query.set(state.query); this.pageSize.set(state.pageSize); this.pageIndex.set(state.pageIndex); }, () => !this.loading());

  private readonly service = inject(CatalogService);
  private readonly destroyRef = inject(DestroyRef);
  readonly key = inject(ActivatedRoute).snapshot.data['catalog'] as CatalogKey;
  readonly config = catalogs.find((item) => item.key === this.key)!;
  readonly isCurrency = this.key === 'currencies';
  private request?: Subscription;
  readonly items = signal<CatalogItem[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly success = signal('');
  readonly lastSaved = signal<number | null>(null);
  readonly editing = signal<number | null>(null);
  readonly formOpen = signal(false);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(10);
  readonly currentPage = computed(() => Math.min(this.pageIndex(), Math.max(0, Math.ceil(this.filteredItems().length / this.pageSize()) - 1)));
  readonly pageItems = computed(() => this.filteredItems().slice(this.currentPage() * this.pageSize(), (this.currentPage() + 1) * this.pageSize()));
  search(value: string) { this.query.set(value); this.pageIndex.set(0); }
  changePage(event: { pageIndex: number; pageSize: number }) { this.pageIndex.set(event.pageIndex); this.pageSize.set(event.pageSize); }
  newItem() { this.cancel(); this.error.set(''); this.success.set(''); this.formOpen.set(true); }
  readonly pendingDelete = signal<CatalogItem | null>(null);
  readonly query = signal('');
  readonly filteredItems = computed(() => {
    const normalize = (value: string) => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('pt-BR');
    const query = normalize(this.query().trim());
    return this.items().filter(item => normalize([item.name, item.code ?? '', item.symbol ?? ''].join(' ')).includes(query)).slice().sort((a, b) => a.name.localeCompare(b.name, 'pt-BR') || a.id - b.id);
  });
  readonly form = inject(FormBuilder).nonNullable.group({
    name: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(100)]],
    code: ['', this.isCurrency ? [Validators.required, Validators.pattern(/^[a-zA-Z]{3}$/)] : []],
    symbol: ['', Validators.maxLength(10)],
  });
  ngOnInit() {
    this.context.restore(this.key);
    this.load();
  }
  load() {
    this.request?.unsubscribe();
    this.loading.set(true);
    this.error.set('');
    this.request = this.service
      .list(this.key)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false)),
      )
      .subscribe({
        next: (items) => { this.items.set(items); this.pageIndex.set(this.currentPage()); },
        error: (error) => this.error.set(apiError(error)),
      });
  }
  edit(item: CatalogItem) {
    this.formOpen.set(true);
    this.editing.set(item.id);
    this.pendingDelete.set(null);
    this.error.set('');
    this.success.set('');
    this.form.reset({ name: item.name, code: item.code ?? '', symbol: item.symbol ?? '' });
  }
  cancel() {
    this.formOpen.set(false);
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
          this.lastSaved.set(id);
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
