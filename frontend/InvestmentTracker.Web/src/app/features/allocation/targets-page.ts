import { Component, DestroyRef, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin, Subscription } from 'rxjs';
import { PortfolioPicker } from '../../shared/portfolio-picker';
import { CatalogService } from '../catalogs/catalog.service';
import { AllocationService, Dimension } from './allocation.service';
import { brazilianNumberValidator } from '../../core/brazilian-number';
import { apiError } from '../../core/services/api-error';
import { fractionToPercentage, percentageToFraction, percentageUnits } from './percentage-input';

@Component({
  standalone: true,
  selector: 'app-targets-page',
  imports: [PortfolioPicker, ReactiveFormsModule],
  templateUrl: './targets-page.html',
})
export class TargetsPage {
  private readonly api = inject(AllocationService);
  private readonly catalogs = inject(CatalogService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(FormBuilder).nonNullable;
  private request?: Subscription;
  readonly portfolioId = signal<number | null>(null);
  readonly dimension = signal<Dimension>('category');
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly success = signal('');
  readonly ready = signal(false);
  readonly form = this.fb.group({ rows: this.fb.array<ReturnType<TargetsPage['row']>>([]) });
  get rows() {
    return this.form.controls.rows;
  }
  private row(id: number, name: string, percentage: string) {
    return this.fb.group({
      groupId: [id],
      name: [name],
      percentage: [
        percentage,
        (control: AbstractControl) =>
          control.value === '' ? null : brazilianNumberValidator(3, 4)(control),
      ],
    });
  }
  select(id: number) {
    this.portfolioId.set(id);
    this.load();
  }
  changeDimension(value: Dimension) {
    this.dimension.set(value);
    this.load();
  }
  load() {
    const id = this.portfolioId();
    if (id === null) return;
    this.request?.unsubscribe();
    this.loading.set(true);
    this.ready.set(false);
    this.error.set('');
    this.success.set('');
    this.rows.clear();
    this.request = forkJoin({
      options: this.catalogs.list(this.dimension() === 'category' ? 'asset-categories' : 'sectors'),
      targets: this.api.targets(id, this.dimension()),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ options, targets }) => {
          for (const item of options) {
            const target = targets.find((t) => t.groupId === item.id);
            this.rows.push(
              this.row(
                item.id,
                item.name,
                target ? fractionToPercentage(target.targetPercentage) : '',
              ),
            );
          }
          this.loading.set(false);
          this.ready.set(true);
        },
        error: (e) => {
          this.loading.set(false);
          this.error.set(apiError(e));
        },
      });
  }
  total() {
    if (this.form.invalid) return '—';
    const units = this.rows.controls.reduce(
      (sum, row) => sum + percentageUnits(row.controls.percentage.value),
      0n,
    );
    return `${units / 10000n},${(units % 10000n).toString().padStart(4, '0')}`;
  }
  save() {
    this.success.set('');
    if (this.form.invalid || !this.ready() || this.saving()) {
      this.form.markAllAsTouched();
      this.error.set('Informe percentuais no padrão brasileiro, com até quatro casas decimais.');
      return;
    }
    const targets = this.rows
      .getRawValue()
      .filter((r) => r.percentage !== '')
      .map((r) => ({
        groupId: r.groupId,
        targetPercentage: percentageToFraction(r.percentage),
      }));
    this.saving.set(true);
    this.error.set('');
    this.api
      .saveTargets(this.portfolioId()!, this.dimension(), targets)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.success.set('Metas salvas. A soma do conjunto é 100%.');
        },
        error: (e) => {
          this.saving.set(false);
          this.error.set(apiError(e));
        },
      });
  }
}
