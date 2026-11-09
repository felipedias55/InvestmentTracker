import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CatalogService } from './catalog.service';
import { apiError } from '../../core/services/api-error';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { catalogs } from './catalog.models';

@Component({ standalone: true, selector: 'app-catalog-hub', imports: [RouterLink, MatButtonModule],
  template: `<header class="page-header"><p class="eyebrow">CADASTROS</p><h1>{{ references ? 'Referências' : 'Classificações' }}</h1>
    <p>{{ references ? 'Países e moedas apoiam o cadastro dos investimentos. Mantenha estas referências quando precisar adicionar opções.' : 'Organize os instrumentos, os grupos da sua estratégia e os setores. Cada classificação mantém sua função no planejamento.' }}</p>
    <a routerLink="/assets">Voltar para Ativos →</a></header>
    <div class="catalog-grid">@for (item of items; track item.key) { <section class="panel"><h2>{{ item.label }}</h2><p>{{ item.description }}</p><a mat-stroked-button [routerLink]="'/' + item.key">Gerenciar {{ item.label.toLowerCase() }}</a></section> }</div>
    @if (!references) { <p class="hint">Tipo de ativo identifica o instrumento. Categoria define o agrupamento da estratégia. Setor identifica a atividade econômica. Uma classificação não substitui a outra.</p> }
    @else { <p class="hint">O país classifica a geografia; a moeda identifica a unidade monetária. São escolhas independentes. O cadastro de um ativo permite criar uma referência faltante sem sair do editor.</p>
      <section class="panel"><h2>Referências prontas para Brasil e EUA</h2><p>Disponibilize Brasil, Estados Unidos, BRL e USD. Apenas opções ausentes são adicionadas; nomes, símbolos e vínculos existentes são preservados.</p><button mat-stroked-button type="button" (click)="prepare()" [disabled]="busy()">Disponibilizar países e moedas BR/US</button>
      @if (message()) { <p role="status">{{ message() }}</p> } @if (error()) { <p class="notice error" role="alert">{{ error() }}</p> }</section> }`,
  styles: ['.catalog-grid { display:grid; grid-template-columns:repeat(auto-fit,minmax(min(100%,260px),1fr)); gap:16px; }']
})
export class CatalogHub {
  private readonly service = inject(CatalogService); private readonly destroyRef = inject(DestroyRef);
  readonly busy = signal(false); readonly message = signal(''); readonly error = signal('');
  readonly references = inject(ActivatedRoute).snapshot.data['catalogGroup'] === 'references';
  readonly items = catalogs.filter(c => this.references ? ['countries', 'currencies'].includes(c.key) : ['asset-types', 'asset-categories', 'sectors'].includes(c.key));
  prepare() { if (this.busy()) return; this.busy.set(true); this.error.set(''); this.message.set(''); this.service.prepareDefaults().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: result => { this.busy.set(false); this.message.set(result.message); }, error: e => { this.busy.set(false); this.error.set(apiError(e)); } }); }
}
