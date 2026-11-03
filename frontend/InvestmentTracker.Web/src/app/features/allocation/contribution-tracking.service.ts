import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { JointPlan, RebalancingBandStatus } from './allocation.service';
import { StrategyConfig } from './strategy-editor';
export interface TrackingSimulation { simulation: { amount: string; strategy?: StrategyConfig }; expectedToken: string; }
export interface TrackingSummary { id: number; name: string; from: string; to: string; createdAtUtc: string; }
export interface TrackingPage<T> { items: T[]; total: number; pageIndex: number; pageSize: number; }
export interface PurchaseCandidate { tradeId: number; date: string; ticker: string; currency: string; categoryId: number; sectorId: number; amount: string; baseAmount: string | null; active: boolean; }
export interface TrackedPurchase { id: number; tradeId: number; rowKey: string; date: string; ticker: string; currency: string; amount: string; baseAmount: string; conversionSource: string; createdAtUtc: string; removedAtUtc: string | null; active: boolean; }
export interface TrackingDetail { plan: TrackingSummary; snapshot: { currency: string; simulation: { amount: string; strategy?: StrategyConfig }; plan: JointPlan; notes: string[]; bands?: RebalancingBandStatus[] };
  rows: { planned: JointPlan['rows'][number]; realized: string; difference: string; status: string }[]; purchases: TrackedPurchase[]; realized: string; difference: string; status: string; }
export interface SaveTrackingPlan extends TrackingSimulation { requestId: string; name: string; from: string; to: string; }
@Injectable({ providedIn: 'root' })
export class ContributionTrackingService {
  private readonly http = inject(HttpClient);
  private path(p: number) { return `/api/portfolios/${p}/contribution-plans`; }
  list(p: number, page = 0, size = 10) { return this.http.get<TrackingPage<TrackingSummary>>(this.path(p), { params: { pageIndex: page, pageSize: size } }); }
  read(p: number, id: number) { return this.http.get<TrackingDetail>(`${this.path(p)}/${id}`); }
  save(p: number, request: SaveTrackingPlan) { return this.http.post<TrackingDetail>(this.path(p), request); }
  candidates(p: number, id: number, page = 0, size = 10) { return this.http.get<TrackingPage<PurchaseCandidate>>(`${this.path(p)}/${id}/purchases`, { params: { pageIndex: page, pageSize: size } }); }
  link(p: number, id: number, request: { tradeId: number; rowKey: string; baseAmount: string | null }) { return this.http.post<void>(`${this.path(p)}/${id}/purchases`, request); }
  unlink(p: number, id: number, link: number) { return this.http.delete<void>(`${this.path(p)}/${id}/purchases/${link}`); }
}
