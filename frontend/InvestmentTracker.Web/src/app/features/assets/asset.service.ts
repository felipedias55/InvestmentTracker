import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
export interface AssetInput {
  ticker: string;
  name: string;
  assetTypeId: number;
  countryId: number;
  currencyId: number;
  assetCategoryId: number;
  sectorId: number;
}
export interface Asset extends AssetInput {
  id: number;
  createdAt: string;
}
@Injectable({ providedIn: 'root' })
export class AssetService {
  private readonly http = inject(HttpClient);
  list() {
    return this.http.get<Asset[]>('/api/assets');
  }
  create(input: AssetInput) {
    return this.http.post<Asset>('/api/assets', input);
  }
  update(id: number, input: AssetInput) {
    return this.http.put<Asset>(`/api/assets/${id}`, input);
  }
  delete(id: number) {
    return this.http.delete<void>(`/api/assets/${id}`);
  }
}
