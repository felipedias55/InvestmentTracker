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
import { RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin, finalize, Subscription } from 'rxjs';
import { Asset, AssetService } from './asset.service';
import { CatalogItem, CatalogKey } from '../catalogs/catalog.models';
import { CatalogService } from '../catalogs/catalog.service';
import { apiError } from '../../core/services/api-error';
type ReferenceField = 'assetTypeId' | 'countryId' | 'currencyId' | 'assetCategoryId' | 'sectorId';

@Component({
  standalone: true,
  selector: 'app-assets-page',
  imports: [UnsavedForm, AppIcon, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, ListPaginator, EditPanel, ReactiveFormsModule, RouterLink, DatePipe],
  templateUrl: './assets-page.html',
  styleUrl: '../../shared/registration-list.css',
})
export class AssetsPage implements OnInit {
  private readonly context = rememberContext('assets', () => ({ query: this.query(), categoryFilter: this.categoryFilter(), sectorFilter: this.sectorFilter(), pageSize: this.pageSize(), pageIndex: this.pageIndex() }), state => { this.query.set(state.query); this.categoryFilter.set(state.categoryFilter); this.sectorFilter.set(state.sectorFilter); this.pageSize.set(state.pageSize); this.pageIndex.set(state.pageIndex); }, () => !this.loading());

  private readonly service = inject(AssetService);
  private readonly catalogs = inject(CatalogService);
  private readonly destroyRef = inject(DestroyRef);
  private request?: Subscription;
  readonly items = signal<Asset[]>([]);
  readonly references = signal<Record<string, CatalogItem[]>>({});
  readonly ready = signal(false);
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
  readonly pendingDelete = signal<Asset | null>(null);
  readonly query = signal('');
  readonly categoryFilter = signal('');
  readonly sectorFilter = signal('');
  readonly filteredItems = computed(() => {
    const normalize = (value: string) => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('pt-BR');
    const query = normalize(this.query().trim());
    return this.items().filter(asset => (!this.categoryFilter() || asset.assetCategoryId === Number(this.categoryFilter())) && (!this.sectorFilter() || asset.sectorId === Number(this.sectorFilter())) && normalize([asset.ticker, asset.name, ...this.fields.map(field => this.label(field.key, asset[field.control]))].join(' ')).includes(query)).slice().sort((a, b) => a.ticker.localeCompare(b.ticker, 'pt-BR') || a.id - b.id);
  });
  readonly fields: { control: ReferenceField; key: CatalogKey; label: string }[] = [
    { control: 'assetTypeId', key: 'asset-types', label: 'Tipo de ativo' },
    { control: 'countryId', key: 'countries', label: 'País' },
    { control: 'currencyId', key: 'currencies', label: 'Moeda' },
    { control: 'assetCategoryId', key: 'asset-categories', label: 'Categoria' },
    { control: 'sectorId', key: 'sectors', label: 'Setor' },
  ];
  readonly form = inject(FormBuilder).nonNullable.group({
    ticker: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(20)]],
    name: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(200)]],
    assetTypeId: [0, Validators.min(1)],
    countryId: [0, Validators.min(1)],
    currencyId: [0, Validators.min(1)],
    assetCategoryId: [0, Validators.min(1)],
    sectorId: [0, Validators.min(1)],
  });
  ngOnInit() {
    this.context.restore('global');
    this.load();
  }
  load() {
    this.request?.unsubscribe();
    this.loading.set(true);
    this.error.set('');
    this.request = forkJoin({
      assets: this.service.list(),
      'asset-types': this.catalogs.list('asset-types'),
      countries: this.catalogs.list('countries'),
      currencies: this.catalogs.list('currencies'),
      'asset-categories': this.catalogs.list('asset-categories'),
      sectors: this.catalogs.list('sectors'),
    })
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false)),
      )
      .subscribe({
        next: ({ assets, ...references }) => {
          this.items.set(assets);
          this.references.set(references);
          this.ready.set(true); this.pageIndex.set(this.currentPage());
        },
        error: (error) => this.error.set(apiError(error)),
      });
  }
  label(key: string, id: number) {
    const item = this.references()[key]?.find((item) => item.id === id);
    return item?.code ?? item?.name ?? 'Indisponível';
  }
  edit(asset: Asset) {
    this.formOpen.set(true);
    this.editing.set(asset.id);
    this.pendingDelete.set(null);
    this.error.set('');
    this.success.set('');
    this.form.reset(asset);
  }
  cancel() {
    this.formOpen.set(false);
    this.editing.set(null);
    this.form.reset();
  }
  save() {
    if (this.form.invalid || this.saving() || !this.ready()) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.error.set('');
    this.success.set('');
    const id = this.editing();
    const input = this.form.getRawValue();
    const request = id === null ? this.service.create(input) : this.service.update(id, input);
    request
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.saving.set(false)),
      )
      .subscribe({
        next: () => {
          this.lastSaved.set(id);
          this.cancel();
          this.success.set('Ativo salvo.');
          this.load();
        },
        error: (error) => this.error.set(apiError(error)),
      });
  }
  remove() {
    const asset = this.pendingDelete();
    if (!asset || this.saving()) return;
    this.saving.set(true);
    this.error.set('');
    this.success.set('');
    this.service
      .delete(asset.id)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.saving.set(false)),
      )
      .subscribe({
        next: () => {
          this.pendingDelete.set(null);
          if (this.editing() === asset.id) this.cancel();
          this.success.set('Ativo excluído.');
          this.load();
        },
        error: (error) => this.error.set(apiError(error)),
      });
  }
}
