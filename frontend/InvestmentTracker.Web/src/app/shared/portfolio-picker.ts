import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import {
  Component,
  DestroyRef,
  Injectable,
  inject,
  input,
  output,
  signal,
  OnInit,
} from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Portfolio, PortfolioService } from '../features/portfolio/portfolio.service';
import { UnsavedChanges } from './unsaved-registry';
import { apiError } from '../core/services/api-error';

@Injectable({ providedIn: 'root' })
export class PortfolioSelection {
  readonly id = signal<number | null>(null);
}

@Component({
  selector: 'app-portfolio-picker',
  standalone: true,
  imports: [RouterLink, MatButtonModule, MatFormFieldModule, MatInputModule],
  template: `
    @if (error()) {
      <p class="notice error" role="alert">
        {{ error() }} <button mat-button (click)="load()">Tentar novamente</button>
      </p>
    }
    @if (loading()) {
      <p role="status">Carregando carteiras…</p>
    }
    @if (portfolios().length) {
      <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Carteira</mat-label>
      <select matNativeControl
        id="analysis-portfolio"
        #selection
        [value]="selected()"
        [disabled]="disabled()"
        (change)="select(+selection.value); selection.value = '' + selected()"
      >
        @for (p of portfolios(); track p.id) {
          <option [value]="p.id">{{ p.name }} · {{ p.baseCurrencyCode }}</option>
        }
      </select></mat-form-field>
    } @else if (!loading() && !error()) {
      <p>Crie uma <a routerLink="/portfolio">carteira</a> para começar.</p>
    }
  `,
  styles: [
    `
      :host {
        display: block;
        max-width: 500px;
        margin-bottom: 24px;
      }
    `,
  ],
})
export class PortfolioPicker implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly drafts = inject(UnsavedChanges);
  private readonly api = inject(PortfolioService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly selection = inject(PortfolioSelection);
  readonly disabled = input(false);
  readonly selectedId = output<number>();
  readonly portfolios = signal<Portfolio[]>([]);
  readonly selected = this.selection.id;
  readonly error = signal('');
  readonly loading = signal(false);
  ngOnInit() {
    this.load();
  }
  load() {
    this.loading.set(true);
    this.error.set('');
    this.api
      .list()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (items) => {
          this.portfolios.set(items);
          this.loading.set(false);
          if (items.length)
            this.select(items.find(p => p.id === Number(this.route.snapshot.queryParamMap.get('portfolioId')))?.id ?? items.find((p) => p.id === this.selected())?.id ?? items[0].id);
        },
        error: (e) => {
          this.loading.set(false);
          this.error.set(apiError(e));
        },
      });
  }
  select(id: number) {
    if (id !== this.selected() && !this.drafts.confirmWithin()) return;
    this.selection.id.set(id);
    this.selectedId.emit(id);
  }
}
