import type { StrategyConfig } from './strategy-editor';
import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ExternalAssetSummary } from '../external-assets/external-assets.service';
import { PortfolioSummary } from '../portfolio/portfolio.service';

export type Dimension = 'category' | 'sector';
export interface Target {
  groupId: number;
  name: string;
  targetPercentage: string;
}
export interface AllocationRow {
  groupId: number;
  name: string;
  currentValue: string | null;
  currentPercentage: string | null;
  targetPercentage: string | null;
  difference: string | null;
}
export interface Dashboard {
  externalAssets: ExternalAssetSummary | null;
  totalWealth: string | null;
  summary: PortfolioSummary;
  allocation: {
    categoryTargetsConfigured: boolean;
    sectorTargetsConfigured: boolean;
    categories: AllocationRow[];
    sectors: AllocationRow[];
    countries: AllocationRow[];
  };
}
export interface Contribution {
  currencyCode: string;
  dimension: Dimension;
  amount: string;
  sumOfWeights: string;
  unallocatedAmount: string;
  hasStaleRates: boolean;
  hasFallbackRates: boolean;
  rows: {
    groupId: number;
    name: string;
    currentPercentage: string;
    targetPercentage: string;
    difference: string;
    adjustedWeight: string;
    suggestedContribution: string;
    currencies?: ContributionCurrency[];
  }[];
}
export interface ContributionCurrency { currency: string; amount: string | null; rate: string | null; dates: string; stale: boolean; tickers?: string[]; }
export interface JointPlan { rows: { key: string; category: string; sector: string; amount: string; currencies: ContributionCurrency[];
  categoryId?: number; sectorId?: number;
  stage?: string; rulePriority?: number; groupPriority?: number; suggestedCurrency?: string | null; suggestionReason?: string }[]; unallocated: string; }
export interface RuleFunding { categoryId: number; category: string; priority: number; requested: string; budget: string; suggested: string; }
export interface ConcentrationStatus { dimension: string; groupId: number; name: string; maximumPercentage: string; currentPercentage: string | null;
  projectedPercentage: string | null; requestedContribution: string; suggestedContribution: string; initiallyAboveLimit: boolean; aboveLimit: boolean; }
export interface ContributionPlan { dashboard: Dashboard; category: Contribution | null; sector: Contribution | null; plan: JointPlan | null; strategyNotes?: string[]; eligibleBase?: string | null;
  bands?: RebalancingBandStatus[];
  planToken?: string;
  concentration?: ConcentrationStatus[]; prioritiesApplied?: boolean; ruleFunding?: RuleFunding[]; }
export interface RebalancingBandStatus { dimension: string; groupId: number; name: string; state: string; hasOverride: boolean; tolerancePoints: string;
  currentPercentage: string | null; targetPercentage: string | null; lowerPercentage: string | null; upperPercentage: string | null;
  requestedRemainder: string; suggestedRemainder: string; reservedContribution: string; reason: string; }
@Injectable({ providedIn: 'root' })
export class AllocationService {
  private readonly http = inject(HttpClient);
  targets(id: number, dimension: Dimension) {
    return this.http.get<Target[]>(`/api/portfolios/${id}/${dimension}-targets`);
  }
  saveTargets(
    id: number,
    dimension: Dimension,
    targets: { groupId: number; targetPercentage: string }[],
  ) {
    return this.http.put<void>(`/api/portfolios/${id}/${dimension}-targets`, { targets });
  }
  dashboard(id: number) {
    return this.http.get<Dashboard>(`/api/portfolios/${id}/dashboard`);
  }
  plan(id: number, amount: string, strategy: StrategyConfig | null = null) {
    return this.http.post<ContributionPlan>('/api/portfolios/' + id + '/contribution-plan', strategy ? { amount, strategy } : { amount });
  }
  simulate(id: number, dimension: Dimension, amount: string) {
    return this.http.post<Contribution>(`/api/portfolios/${id}/contribution-analysis`, {
      dimension,
      amount,
    });
  }
}
