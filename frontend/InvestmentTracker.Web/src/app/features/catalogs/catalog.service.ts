import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CatalogInput, CatalogItem, CatalogKey } from './catalog.models';

@Injectable({ providedIn: 'root' })
export class CatalogService {
  private readonly http = inject(HttpClient);
  list(key: CatalogKey) {
    return this.http.get<CatalogItem[]>(`/api/${key}`);
  }
  create(key: CatalogKey, input: CatalogInput) {
    return this.http.post<CatalogItem>(`/api/${key}`, input);
  }
  update(key: CatalogKey, id: number, input: CatalogInput) {
    return this.http.put<CatalogItem>(`/api/${key}/${id}`, input);
  }
  delete(key: CatalogKey, id: number) {
    return this.http.delete<void>(`/api/${key}/${id}`);
  }
}
