using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Income;
using InvestmentTracker.Application.Portfolios.Interfaces;

namespace InvestmentTracker.Application.Planning
{
    public sealed class PlanningGoalService(IPlanningGoalRepository repository, IPortfolioService portfolios, IIncomeAnalysisService income, TimeProvider clock)
    {
        private DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);
        public async Task<PlanningGoalPage?> PageAsync(int portfolioId, PlanningGoalQuery query, CancellationToken ct)
        {
            if (query.PageIndex < 0 || query.PageSize is not (10 or 25 or 50) || query.Search?.Length > 100
                || query.Kind is not (null or "" or "wealth" or "monthly-income") || query.Status is not ("active" or "archived" or "all"))
                throw new InputValidationException("Informe filtros e paginação válidos.");
            var page = await repository.PageAsync(portfolioId, query with { Search = query.Search?.Trim() }, ct);
            if (page is null) return null;
            var today = Today;
            var active = page.Items.Where(r => !r.Archived && r.Currency == page.Currency).ToArray();
            var wealth = active.Any(r => r.Kind == "wealth") ? await portfolios.GetSummaryAsync(portfolioId, ct) : null;
            var average = active.Any(r => r.Kind == "monthly-income") ? PlanningGoalCalculator.IncomeAverage(await income.EventsAsync(portfolioId, ct), page.Currency, today) : (Average: (decimal?)null, Missing: 0);
            var window = PlanningGoalCalculator.IncomeWindow(today);
            return new(page.Items.Select(r => PlanningGoalCalculator.Calculate(PlanningGoalCalculator.Read(r), page.Currency, today,
                r.Kind == "wealth" ? wealth?.CurrentValue : average.Average)).ToArray(), page.Total, page.PageIndex, page.PageSize,
                page.Currency, today, window.From, window.To, average.Missing, wealth?.HasStaleRates ?? false, wealth?.HasFallbackRates ?? false);
        }
        public Task<SavedPlanningGoal?> SaveAsync(int portfolioId, SavePlanningGoal request, CancellationToken ct)
        {
            PlanningGoalCalculator.Validate(request);
            return repository.SaveAsync(portfolioId, request, ct);
        }
        public Task<IReadOnlyList<SavedPlanningGoal>?> HistoryAsync(int portfolioId, Guid goalId, CancellationToken ct) => repository.HistoryAsync(portfolioId, goalId, ct);
    }
}
