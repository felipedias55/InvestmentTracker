using System.Text.Json.Serialization;

namespace InvestmentTracker.Application.Strategies
{
    public sealed record StrategyRule(int CategoryId, string Kind,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Value);
    public sealed record StrategyConfig(string Name, string Model, string Remainder,
        IReadOnlyList<int> ExcludedCategoryIds, IReadOnlyList<StrategyRule> Rules,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Tolerance = 0m);
    public sealed record SavedStrategy(Guid ProfileId, int Revision, DateTime CreatedAtUtc, StrategyConfig Config);
    public sealed record SaveStrategy(Guid ProfileId, int ExpectedRevision, StrategyConfig Config);
    public interface IStrategyRepository
    {
        Task<IReadOnlyList<SavedStrategy>?> ListAsync(int portfolioId, CancellationToken ct);
        Task<SavedStrategy?> SaveAsync(int portfolioId, SaveStrategy request, CancellationToken ct);
    }
}
