import { SectionLink } from '../../shared/section-link';
import { ActivatedRoute } from '@angular/router';
import { rememberContext } from '../../shared/page-context';
import { UnsavedForm } from '../../shared/unsaved-changes';
import { UnsavedChanges } from '../../shared/unsaved-registry';
import { PrivateCurrencyPipe as CurrencyPipe } from '../../core/value-privacy';
import { EditPanel } from '../../shared/edit-panel';
import { QuoteBatchPanel } from './quote-batch-panel';
import { PositionPerformance } from './position-performance';
import {
  brazilianNumberValidator,
  formatBrazilianNumber,
  parseBrazilianNumber,
} from '../../core/brazilian-number';
import { PortfolioSelection } from '../../shared/portfolio-picker';
import { Component, DestroyRef, computed, inject, signal, linkedSignal, OnInit } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';
import { MatMenuModule } from '@angular/material/menu';
import { AppIcon } from '../../shared/app-icon';
import { ListPaginator } from '../../shared/list-paginator';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin, finalize, Subscription, Observable } from 'rxjs';
import { Portfolio, PortfolioService, PortfolioSummary, Position } from './portfolio.service';
import { CatalogService } from '../catalogs/catalog.service';
import { CatalogItem } from '../catalogs/catalog.models';
import { Asset, AssetService } from '../assets/asset.service';
import { apiError } from '../../core/services/api-error';

@Component({
  standalone: true,
  selector: 'app-portfolio-page',
  imports: [SectionLink, UnsavedForm, CurrencyPipe, EditPanel, QuoteBatchPanel, PositionPerformance, ReactiveFormsModule, DatePipe, RouterLink,
    MatButtonModule, MatFormFieldModule, MatInputModule, MatTableModule, MatMenuModule, AppIcon, ListPaginator],
  templateUrl: './portfolio-page.html',
  styleUrl: './portfolio-page.css',
})
export class PortfolioPage implements OnInit {
  private readonly context = rememberContext('portfolio', () => ({ query: this.query(), category: this.category(), sector: this.sector(), currency: this.currency(), updatedBefore: this.updatedBefore(), sort: this.sort(), view: this.view(), pageSize: this.pageSize(), pageIndex: this.pageIndex() }), state => { this.query.set(state.query); this.category.set(state.category); this.sector.set(state.sector); this.currency.set(state.currency); this.updatedBefore.set(state.updatedBefore); this.sort.set(state.sort); this.view.set(state.view); this.pageSize.set(state.pageSize); this.pageIndex.set(state.pageIndex); }, () => !this.loading());

  private readonly drafts = inject(UnsavedChanges);
  private readonly api = inject(PortfolioService);
  private readonly selection = inject(PortfolioSelection);
  private readonly route = inject(ActivatedRoute);
  private readonly catalogs = inject(CatalogService);
  private readonly assetApi = inject(AssetService);
  private readonly destroyRef = inject(DestroyRef);
  private summaryRequest?: Subscription;
  private readonly fb = inject(FormBuilder);
  readonly portfolios = signal<Portfolio[]>([]);
  readonly currencies = signal<CatalogItem[]>([]);
  readonly assets = signal<Asset[]>([]);
  readonly summary = signal<PortfolioSummary | null>(null);
  readonly selectedId = signal<number | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly success = signal('');
  readonly view = signal<'base' | 'original'>('base');
  readonly showPortfolioForm = signal(false);
  readonly editingPortfolio = signal<number | null>(null);
  readonly editingPosition = signal<number | null>(null);
  readonly pendingDelete = signal<Position | null>(null);
  readonly batchPositions = signal<Position[] | null>(null);
  openQuotes() { this.batchPositions.set([...this.filteredPositions()]); }
  quotesSaved(count: number) {
    this.batchPositions.set(null); this.success.set(`${count} cotações atualizadas. Quantidades, custos e proventos preservados.`);
    this.select(this.selectedId()!);
  }
  readonly query = signal('');
  readonly category = signal('');
  readonly sector = signal('');
  readonly currency = signal('');
  readonly updatedBefore = signal('');
  readonly sort = signal('ticker');
  readonly lastSaved = signal<number | null>(null);
  readonly showPositionForm = signal(false);
  readonly detailsId = signal<number | null>(null);
  readonly detailsPosition = computed(() => this.summary()?.positions.find(p => p.id === this.detailsId()) ?? null);
  readonly columns = ['asset', 'category', 'quantity', 'invested', 'current', 'performance', 'actions'];
  readonly pageSize = signal(10);
  readonly pageIndex = linkedSignal({
    source: () => [this.selectedId(), this.query(), this.category(), this.sector(), this.currency(), this.updatedBefore(), this.sort()].join('|'),
    computation: () => 0,
  });
  readonly effectivePage = computed(() => Math.min(this.pageIndex(), Math.max(0, Math.ceil(this.filteredPositions().length / this.pageSize()) - 1)));
  readonly pagedPositions = computed(() => this.filteredPositions().slice(this.effectivePage() * this.pageSize(), (this.effectivePage() + 1) * this.pageSize()));
  startPosition() { this.cancelPosition(); this.showPositionForm.set(true); }
  readonly categories = computed(() => this.options('assetCategoryName'));
  readonly sectors = computed(() => this.options('sectorName'));
  readonly positionCurrencies = computed(() => this.options('currencyCode'));
  readonly filteredPositions = computed(() => {
    const query = this.normalize(this.query().trim());
    return (this.summary()?.positions ?? []).filter(p =>
      (!query || this.normalize(`${p.ticker} ${p.name}`).includes(query)) &&
      (!this.category() || p.assetCategoryName === this.category()) &&
      (!this.sector() || p.sectorName === this.sector()) &&
      (!this.currency() || p.currencyCode === this.currency()) &&
      (!this.updatedBefore() || !p.updatedOn || p.updatedOn < this.updatedBefore())
    ).sort((a, b) => {
      const tie = a.ticker.localeCompare(b.ticker, 'pt-BR');
      if (this.sort() === 'oldest') return (a.updatedOn ?? '').localeCompare(b.updatedOn ?? '') || tie;
      if (this.sort() === 'value') {
        if (a.baseCurrentValue == null) return b.baseCurrentValue == null ? tie : 1;
        if (b.baseCurrentValue == null) return -1;
        return Number(b.baseCurrentValue) - Number(a.baseCurrentValue) || tie;
      }
      return tie;
    });
  });
  private normalize(value: string) { return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('pt-BR'); }
  private options(key: 'assetCategoryName' | 'sectorName' | 'currencyCode') {
    return [...new Set((this.summary()?.positions ?? []).map(p => p[key]).filter((v): v is string => !!v))].sort((a, b) => a.localeCompare(b, 'pt-BR'));
  }
  clearFilters() {
    this.query.set(''); this.category.set(''); this.sector.set(''); this.currency.set('');
    this.updatedBefore.set(''); this.sort.set('ticker');
  }
  private defaultCode = 'BRL';
  readonly portfolioForm = this.fb.nonNullable.group({
    name: [
      'Carteira Principal',
      [Validators.required, Validators.pattern(/\S/), Validators.maxLength(100)],
    ],
    description: ['', Validators.maxLength(500)],
    baseCurrencyId: [0, Validators.min(1)],
  });
  readonly positionForm = this.fb.nonNullable.group({
    assetId: [0, Validators.min(1)],
    quantity: ['0', [Validators.required, brazilianNumberValidator(13, 6)]],
    investedAmount: ['0', [Validators.required, brazilianNumberValidator(15, 4)]],
    currentValue: ['0', [Validators.required, brazilianNumberValidator(15, 4)]],
    income: ['0', [Validators.required, brazilianNumberValidator(15, 4)]],
  });

  readonly formatNumber = formatBrazilianNumber;

  formatInput(field: 'quantity' | 'investedAmount' | 'currentValue' | 'income') {
    const control = this.positionForm.controls[field];
    if (control.valid) control.setValue(formatBrazilianNumber(parseBrazilianNumber(control.value)));
  }

  ngOnInit() {
    this.load();
  }
  load() {
    this.loading.set(true);
    this.error.set('');
    forkJoin({
      portfolios: this.api.list(),
      currencies: this.catalogs.list('currencies'),
      assets: this.assetApi.list(),
      defaults: this.api.defaults(),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (data) => {
          this.portfolios.set(data.portfolios);
          this.currencies.set(data.currencies);
          this.assets.set(data.assets);
          this.defaultCode = data.defaults.currencyCode;
          if (!data.portfolios.length) {
            this.loading.set(false);
            this.newPortfolio();
            this.summary.set(null);
            this.selectedId.set(null);
            return;
          }
          const selected =
            data.portfolios.find(p => p.id === (this.selectedId() ?? (Number(this.route.snapshot.queryParamMap.get('portfolioId')) || this.selection.id()))) ??
            data.portfolios[0];
          this.select(selected.id);
        },
        error: (error) => {
          this.loading.set(false);
          this.error.set(apiError(error));
        },
      });
  }
  select(id: number) {
    this.context.save();
    if (id !== this.selectedId() && !this.drafts.confirmWithin()) return;
    this.summaryRequest?.unsubscribe();
    const changed = this.summary()?.portfolio.id !== id;
    this.selectedId.set(id);
    this.selection.id.set(id);
    if (changed) { this.summary.set(null); this.clearFilters(); this.lastSaved.set(null); }
    this.context.restore(id);
    this.cancelPosition();
    this.pendingDelete.set(null);
    this.showPortfolioForm.set(false);
    this.loading.set(true);
    this.error.set('');
    this.summaryRequest = this.api
      .summary(id)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false)),
      )
      .subscribe({
        next: (summary) => this.summary.set(summary),
        error: (error) => this.error.set(apiError(error)),
      });
  }
  newPortfolio() {
    this.editingPortfolio.set(null);
    this.showPortfolioForm.set(true);
    this.error.set('');
    this.portfolioForm.reset({
      name: this.portfolios().length ? '' : 'Carteira Principal',
      description: '',
      baseCurrencyId: this.currencies().find((c) => c.code === this.defaultCode)?.id ?? 0,
    });
  }
  editPortfolio() {
    const portfolio = this.summary()?.portfolio;
    if (!portfolio) return;
    this.editingPortfolio.set(portfolio.id);
    this.portfolioForm.reset({ ...portfolio, description: portfolio.description ?? '' });
    this.showPortfolioForm.set(true);
  }
  savePortfolio() {
    if (this.portfolioForm.invalid || this.saving()) {
      this.portfolioForm.markAllAsTouched();
      return;
    }
    const id = this.editingPortfolio();
    const value = this.portfolioForm.getRawValue();
    this.saving.set(true);
    this.error.set('');
    this.success.set('');
    const request = id === null ? this.api.create(value) : this.api.update(id, value);
    request
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.saving.set(false)),
      )
      .subscribe({
        next: (portfolio) => {
          this.selectedId.set(portfolio.id);
          this.success.set('Carteira salva. Os valores originais foram preservados.');
          this.load();
        },
        error: (error) => this.error.set(apiError(error)),
      });
  }
  originalCurrency() {
    const asset = this.assets().find(
      (a) => a.id === Number(this.positionForm.controls.assetId.value),
    );
    return this.currencies().find((c) => c.id === asset?.currencyId)?.code ?? 'moeda do ativo';
  }
  availableAssets() {
    return this.assets().filter(
      (a) =>
        this.editingPosition() !== null ||
        !this.summary()?.positions.some((p) => p.assetId === a.id),
    );
  }
  editPosition(position: Position) {
    this.detailsId.set(null);
    this.editingPosition.set(position.id);
    this.pendingDelete.set(null);
    this.success.set('');
    this.error.set('');
    this.positionForm.reset({
      assetId: position.assetId,
      quantity: formatBrazilianNumber(String(position.quantity)),
      investedAmount: formatBrazilianNumber(String(position.investedAmount)),
      currentValue: formatBrazilianNumber(String(position.currentValue)),
      income: formatBrazilianNumber(String(position.income)),
    });
    this.positionForm.controls.assetId.disable();
  }
  cancelPosition() {
    this.showPositionForm.set(false);
    this.detailsId.set(null);
    this.editingPosition.set(null);
    this.positionForm.reset();
    this.positionForm.controls.assetId.enable();
  }
  savePosition() {
    if (this.positionForm.invalid || this.saving() || !this.summary()) {
      this.positionForm.markAllAsTouched();
      return;
    }
    const id = this.selectedId()!;
    const value = this.positionForm.getRawValue();
    const input = {
      ...value,
      quantity: parseBrazilianNumber(value.quantity),
      investedAmount: parseBrazilianNumber(value.investedAmount),
      currentValue: parseBrazilianNumber(value.currentValue),
      income: parseBrazilianNumber(value.income),
    };
    const positionId = this.editingPosition();
    this.saving.set(true);
    this.error.set('');
    this.success.set('');
    const request: Observable<unknown> =
      positionId === null
        ? this.api.addPosition(id, input)
        : this.api.updatePosition(id, positionId, input);
    request
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.saving.set(false)),
      )
      .subscribe({
        next: () => {
          this.success.set('Posição salva na moeda original do ativo.');
          this.lastSaved.set(positionId);
          this.select(id);
        },
        error: (error) => this.error.set(apiError(error)),
      });
  }
  remove() {
    const position = this.pendingDelete();
    const id = this.selectedId();
    if (!position || id === null || this.saving()) return;
    this.saving.set(true);
    this.error.set('');
    this.success.set('');
    this.api
      .deletePosition(id, position.id)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.saving.set(false)),
      )
      .subscribe({
        next: () => {
          this.success.set('Posição removida.');
          this.select(id);
        },
        error: (error) => this.error.set(apiError(error)),
      });
  }
}
