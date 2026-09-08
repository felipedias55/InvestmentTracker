import { brazilianNumberValidator, formatBrazilianNumber, parseBrazilianNumber } from '../../core/brazilian-number';
import { Component, DestroyRef, inject, signal, OnInit } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
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
  imports: [ReactiveFormsModule, CurrencyPipe, DatePipe, RouterLink],
  templateUrl: './portfolio-page.html',
  styleUrl: './portfolio-page.css',
})
export class PortfolioPage implements OnInit {
  private readonly api = inject(PortfolioService);
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
  });

  readonly formatNumber = formatBrazilianNumber;

  formatInput(field: 'quantity' | 'investedAmount' | 'currentValue') {
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
            data.portfolios.find((p) => p.id === this.selectedId()) ?? data.portfolios[0];
          this.select(selected.id);
        },
        error: (error) => {
          this.loading.set(false);
          this.error.set(apiError(error));
        },
      });
  }
  select(id: number) {
    this.summaryRequest?.unsubscribe();
    this.selectedId.set(id);
    this.summary.set(null);
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
    this.editingPosition.set(position.id);
    this.pendingDelete.set(null);
    this.success.set('');
    this.error.set('');
    this.positionForm.reset({
      assetId: position.assetId,
      quantity: formatBrazilianNumber(String(position.quantity)),
      investedAmount: formatBrazilianNumber(String(position.investedAmount)),
      currentValue: formatBrazilianNumber(String(position.currentValue)),
    });
    this.positionForm.controls.assetId.disable();
  }
  cancelPosition() {
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
