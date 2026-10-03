using System.Text.Json.Serialization;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.Movements
{
    public sealed record CorrectionOperation(string Kind, DateOnly Date, int? AssetId = null, int? CashAssetId = null,
        int? DestinationId = null,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Quantity = 0,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal UnitPrice = 0,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Fees = 0,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Amount = 0,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? BaseAmount = null);
    public sealed record CorrectionInput(int? MovementId, string Reason, CorrectionOperation Operation);
    public sealed record ApplyCorrectionDto(Guid RequestId, string Token, CorrectionInput Input);
    public sealed record CorrectionBalanceDto(string Name, string CurrencyCode, bool IsPosition,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal BeforeValue,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal AfterValue,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal BeforeQuantity,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal AfterQuantity,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal BeforeCost,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal AfterCost,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal BeforeIncome,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal AfterIncome);
    public sealed record CorrectionSimulationDto(bool CanApply, string Token, DateOnly FromDate,
        IReadOnlyList<int> ReplacedMovementIds, IReadOnlyList<CorrectionBalanceDto> Balances,
        IReadOnlyList<CorrectionSnapshotDto> Snapshots, IReadOnlyList<string> Warnings, IReadOnlyList<string> Blockers,
        int? CorrectionId = null);
    public interface ICorrectionService
    {
        Task<CorrectionOperation?> DraftAsync(int portfolioId, int movementId, CancellationToken ct);
        Task<CorrectionSimulationDto?> SimulateAsync(int portfolioId, CorrectionInput input, CancellationToken ct);
        Task<CorrectionSimulationDto?> ApplyAsync(int portfolioId, ApplyCorrectionDto dto, CancellationToken ct);
    }
    public interface ICorrectionRepository
    {
        Task<CorrectionOperation?> DraftAsync(int portfolioId, int movementId, CancellationToken ct);
        Task<CorrectionSimulationDto?> RunAsync(int portfolioId, CorrectionInput input, Guid? requestId, string? token,
            Func<CorrectionState, CorrectionPlan> calculate, CancellationToken ct);
    }
    public sealed record CorrectionState(Portfolio Portfolio, IReadOnlyList<PortfolioAsset> Positions,
        IReadOnlyList<ExternalAsset> Cash, IReadOnlyList<Asset> Assets, IReadOnlyList<Currency> Currencies,
        IReadOnlyList<FinancialMovement> Movements, IReadOnlyList<PortfolioCashFlow> Flows,
        IReadOnlyList<PortfolioSnapshot> Snapshots, string Fingerprint);
    public sealed record CorrectionStep(FinancialMovement Movement, IReadOnlyList<PortfolioCashFlow> Flows);
    public sealed record CorrectionPlan(CorrectionSimulationDto Preview, IReadOnlyList<PortfolioAsset> Positions,
        IReadOnlyList<ExternalAsset> Cash, IReadOnlyList<CorrectionStep> Steps);
}
