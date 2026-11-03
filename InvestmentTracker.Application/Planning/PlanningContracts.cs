using System.Text.Json.Serialization;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.Planning
{
    public sealed record PlanningGoalConfig(string Name, string Kind, string Currency,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Target,
        DateOnly Deadline,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal MonthlyContribution = 0,
        bool Archived = false);
    public sealed record SavePlanningGoal(Guid GoalId, int ExpectedRevision, PlanningGoalConfig Config);
    public sealed record SavedPlanningGoal(Guid GoalId, int Revision, DateTime CreatedAtUtc, PlanningGoalConfig Config);
    public sealed record PlanningProgress(SavedPlanningGoal Goal, string Status, string Reason, int MonthsRemaining,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? Current,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? Gap,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? Percentage,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? RequiredMonthlyContribution,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? ScenarioValue,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? ScenarioGap);
    public sealed record PlanningGoalQuery(int PageIndex = 0, int PageSize = 10, string? Search = null, string? Kind = null, string Status = "active");
    public sealed record PlanningGoalPage(IReadOnlyList<PlanningProgress> Items, int Total, int PageIndex, int PageSize,
        string Currency, DateOnly Today, DateOnly IncomeFrom, DateOnly IncomeTo, int MissingIncomeConversions, bool HasStaleRates, bool HasFallbackRates);
    public sealed record PlanningGoalPageData(IReadOnlyList<PlanningGoalRevision> Items, int Total, int PageIndex, int PageSize, string Currency);
    public interface IPlanningGoalRepository
    {
        Task<PlanningGoalPageData?> PageAsync(int portfolioId, PlanningGoalQuery query, CancellationToken ct);
        Task<SavedPlanningGoal?> SaveAsync(int portfolioId, SavePlanningGoal request, CancellationToken ct);
        Task<IReadOnlyList<SavedPlanningGoal>?> HistoryAsync(int portfolioId, Guid goalId, CancellationToken ct);
    }
}
