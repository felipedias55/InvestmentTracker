import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Portfolio } from '../portfolio/portfolio.service';

export interface ExternalAssetInput {
  name: string;
  currencyId: number;
  value: string;
  description: string | null;
}
export interface ExternalAsset extends ExternalAssetInput {
  id: number;
  currencyCode: string;
  baseValue: string | null;
  exchangeRate: string | null;
  updatedOn?: string | null;
  rateDate: string | null;
  isStale: boolean;
  isFallback: boolean;
}
export interface ExternalAssetSummary {
  portfolio: Portfolio;
  items: ExternalAsset[];
  totalValue: string | null;
  conversionAvailable: boolean;
  hasStaleRates: boolean;
  hasFallbackRates: boolean;
}
@Injectable({ providedIn: 'root' })
export class ExternalAssetsService {
  private readonly http = inject(HttpClient);
  summary(id: number) {
    return this.http.get<ExternalAssetSummary>(`/api/portfolios/${id}/external-assets`);
  }
  create(id: number, input: ExternalAssetInput) {
    return this.http.post<{ id: number }>(`/api/portfolios/${id}/external-assets`, input);
  }
  update(id: number, itemId: number, input: ExternalAssetInput) {
    return this.http.put<void>(`/api/portfolios/${id}/external-assets/${itemId}`, input);
  }
  delete(id: number, itemId: number) {
    return this.http.delete<void>(`/api/portfolios/${id}/external-assets/${itemId}`);
  }
}
