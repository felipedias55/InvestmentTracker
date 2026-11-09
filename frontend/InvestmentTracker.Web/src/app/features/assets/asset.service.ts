import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
export interface AssetInput {
  listing?: { market: string; symbol: string } | null;
  image?: AssetImageChange | null;
  ticker: string;
  name: string;
  assetTypeId: number;
  countryId: number;
  currencyId: number;
  assetCategoryId: number;
  sectorId: number;
}
export interface Asset extends AssetInput {
  imageVersion?: string | null;
  id: number;
  createdAt: string;
}
export interface AssetImageChange { data: string | null; expectedVersion: string | null; }
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
