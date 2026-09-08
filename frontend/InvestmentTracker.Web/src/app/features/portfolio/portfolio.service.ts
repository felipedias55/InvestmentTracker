import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
export interface Portfolio {
  id: number;
  name: string;
  description: string | null;
  baseCurrencyId: number;
  baseCurrencyCode: string;
}
export interface PortfolioInput {
  name: string;
  description: string | null;
  baseCurrencyId: number;
}
export interface PositionInput {
  assetId: number;
  quantity: string;
  investedAmount: string;
  currentValue: string;
}
export interface Position extends PositionInput {
  id: number;
  ticker: string;
  name: string;
  currencyCode: string;
  baseInvestedAmount: string | null;
  baseCurrentValue: string | null;
  exchangeRate: string | null;
  rateDate: string | null;
  isStale: boolean;
  isFallback: boolean;
}
export interface PortfolioSummary {
  portfolio: Portfolio;
  positions: Position[];
  originalSubtotals: { currencyCode: string; investedAmount: string; currentValue: string }[];
  totalInvested: string | null;
  currentValue: string | null;
  conversionAvailable: boolean;
  hasStaleRates: boolean;
  hasFallbackRates: boolean;
}
@Injectable({ providedIn: 'root' })
export class PortfolioService {
  private readonly http = inject(HttpClient);
  list() {
    return this.http.get<Portfolio[]>('/api/portfolios');
  }
  defaults() {
    return this.http.get<{ currencyCode: string }>('/api/portfolios/defaults');
  }
  summary(id: number) {
    return this.http.get<PortfolioSummary>(`/api/portfolios/${id}`);
  }
  create(input: PortfolioInput) {
    return this.http.post<Portfolio>('/api/portfolios', input);
  }
  update(id: number, input: PortfolioInput) {
    return this.http.put<Portfolio>(`/api/portfolios/${id}`, input);
  }
  addPosition(id: number, input: PositionInput) {
    return this.http.post<{ id: number }>(`/api/portfolios/${id}/assets`, input);
  }
  updatePosition(id: number, positionId: number, input: PositionInput) {
    return this.http.put<void>(`/api/portfolios/${id}/assets/${positionId}`, input);
  }
  deletePosition(id: number, positionId: number) {
    return this.http.delete<void>(`/api/portfolios/${id}/assets/${positionId}`);
  }
}
