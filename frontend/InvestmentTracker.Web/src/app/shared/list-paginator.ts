import { Component, Injectable, input, output } from '@angular/core';
import { MatPaginatorIntl, MatPaginatorModule, PageEvent } from '@angular/material/paginator';

@Injectable()
export class PortuguesePaginator extends MatPaginatorIntl {
  override itemsPerPageLabel = 'Itens por página';
  override nextPageLabel = 'Próxima página';
  override previousPageLabel = 'Página anterior';
  override firstPageLabel = 'Primeira página';
  override lastPageLabel = 'Última página';
  override getRangeLabel = (page: number, size: number, length: number) =>
    length === 0 ? '0 registros' : `${page * size + 1}–${Math.min((page + 1) * size, length)} de ${length}`;
}

@Component({
  selector: 'app-list-paginator', standalone: true, imports: [MatPaginatorModule],
  providers: [{ provide: MatPaginatorIntl, useClass: PortuguesePaginator }],
  template: `<mat-paginator [hidePageSize]="hidePageSize()" [length]="length()" [pageIndex]="index()" [pageSize]="size()" [pageSizeOptions]="[10,25,50]" showFirstLastButtons aria-label="Paginação da lista" (page)="changed.emit($event)" />`,
})
export class ListPaginator {
  readonly hidePageSize = input(false);
  readonly length = input.required<number>();
  readonly index = input(0);
  readonly size = input(25);
  readonly changed = output<PageEvent>();
}
