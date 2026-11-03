import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
export interface PlanningGoalConfig { name: string; kind: string; currency: string; target: string; deadline: string; monthlyContribution: string; archived: boolean; }
export interface SavedPlanningGoal { goalId: string; revision: number; createdAtUtc: string; config: PlanningGoalConfig; }
export interface PlanningProgress { goal: SavedPlanningGoal; status: string; reason: string; monthsRemaining: number; current: string | null; gap: string | null; percentage: string | null;
  requiredMonthlyContribution: string | null; scenarioValue: string | null; scenarioGap: string | null; }
export interface PlanningGoalPage { items: PlanningProgress[]; total: number; pageIndex: number; pageSize: number; currency: string; today: string; incomeFrom: string; incomeTo: string; missingIncomeConversions: number; hasStaleRates: boolean; hasFallbackRates: boolean; }
export interface SavePlanningGoal { goalId: string; expectedRevision: number; config: PlanningGoalConfig; }
@Injectable({ providedIn: 'root' })
export class PlanningGoalsService {
  private readonly http = inject(HttpClient);
  private path(portfolioId: number) { return `/api/portfolios/${portfolioId}/planning-goals`; }
  page(portfolio: number, pageIndex: number, pageSize: number, search: string, kind: string, status: string) {
    return this.http.get<PlanningGoalPage>(this.path(portfolio), { params: { pageIndex, pageSize, search, kind, status } });
  }
  save(portfolio: number, request: SavePlanningGoal) { return this.http.post<SavedPlanningGoal>(this.path(portfolio), request); }
  history(portfolio: number, id: string) { return this.http.get<SavedPlanningGoal[]>(`${this.path(portfolio)}/${id}/revisions`); }
}
