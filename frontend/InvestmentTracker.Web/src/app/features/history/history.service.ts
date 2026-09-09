import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Portfolio } from '../portfolio/portfolio.service';
import { Dashboard } from '../allocation/allocation.service';

export interface CashFlowInput {
  date: string;
  kind: 'contribution' | 'withdrawal';
  currencyId: number;
  amount: string;
  baseAmount: string | null;
  notes: string | null;
}
export interface CashFlow extends CashFlowInput {
  tradeId?: number | null;
  id: number;
  currencyCode: string;
  baseCurrencyCode: string;
}
export interface HistoryPeriod {
  period: string;
  snapshotId: number | null;
  snapshotDate: string | null;
  currencyCode: string;
  portfolioValue: string | null;
  externalValue: string | null;
  totalWealth: string | null;
  totalIncome: string | null;
  contributions: string | null;
  withdrawals: string | null;
  change: string | null;
  netFlowsBetweenSnapshots: string | null;
  changeExcludingFlows: string | null;
  comparisonStart: string | null;
  comparisonNote: string | null;
  hasStaleRates: boolean;
  hasFallbackRates: boolean;
}
export interface PortfolioHistory {
  portfolio: Portfolio;
  today: string;
  months: HistoryPeriod[];
  years: HistoryPeriod[];
  cashFlows: CashFlow[];
}
export interface SnapshotDetail {
  id: number;
  month: string;
  snapshotDate: string;
  capturedAtUtc: string;
  payloadVersion: number;
  dashboard: Dashboard;
}
@Injectable({ providedIn: 'root' })
export class HistoryService {
  private readonly http = inject(HttpClient);
  get(id: number) {
    return this.http.get<PortfolioHistory>(`/api/portfolios/${id}/history`);
  }
  snapshot(id: number, snapshotId: number) {
    return this.http.get<SnapshotDetail>(`/api/portfolios/${id}/history/snapshots/${snapshotId}`);
  }
  capture(id: number, replace: boolean) {
    const path = `/api/portfolios/${id}/history/snapshots`;
    return replace
      ? this.http.put<SnapshotDetail>(path + '/current', {})
      : this.http.post<SnapshotDetail>(path, {});
  }
  createFlow(id: number, input: CashFlowInput) {
    return this.http.post<{ id: number }>(`/api/portfolios/${id}/history/cash-flows`, input);
  }
  updateFlow(id: number, flowId: number, input: CashFlowInput) {
    return this.http.put<void>(`/api/portfolios/${id}/history/cash-flows/${flowId}`, input);
  }
  deleteFlow(id: number, flowId: number) {
    return this.http.delete<void>(`/api/portfolios/${id}/history/cash-flows/${flowId}`);
  }
}
