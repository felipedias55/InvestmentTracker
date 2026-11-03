using System.Text.Json.Serialization;

namespace InvestmentTracker.Application.Strategies
{
    public sealed record StrategyRule(int CategoryId, string Kind,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Value,
        int Priority = 100);
    public sealed record StrategyCombination(int CategoryId, int SectorId);
    public sealed record StrategyGroupPreference(int CategoryId, int SectorId, int Priority = 100, string? PreferredCurrency = null);
    public sealed record ConcentrationLimit(string Dimension, int GroupId,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal MaximumPercentage);
    public sealed record StrategyConfig(string Name, string Model, string Remainder,
        IReadOnlyList<int> ExcludedCategoryIds, IReadOnlyList<StrategyRule> Rules,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Tolerance = 0m,
        IReadOnlyList<StrategyCombination>? ExcludedCombinations = null,
        IReadOnlyList<ConcentrationLimit>? ConcentrationLimits = null,
        bool UsePriorities = false,
        IReadOnlyList<StrategyGroupPreference>? GroupPreferences = null);
    public sealed record SavedStrategy(Guid ProfileId, int Revision, DateTime CreatedAtUtc, StrategyConfig Config);
    public sealed record SaveStrategy(Guid ProfileId, int ExpectedRevision, StrategyConfig Config);
    public interface IStrategyRepository
    {
        Task<IReadOnlyList<SavedStrategy>?> ListAsync(int portfolioId, CancellationToken ct);
        Task<SavedStrategy?> SaveAsync(int portfolioId, SaveStrategy request, CancellationToken ct);
    }
}
