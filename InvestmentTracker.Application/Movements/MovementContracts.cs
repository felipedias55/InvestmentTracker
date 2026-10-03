using System.Text.Json.Serialization;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.Movements
{
    public sealed record SaveMovementDto(Guid RequestId, DateOnly Date, string Kind, int CashAssetId,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal Amount,
        int? DestinationId = null, string? Reason = null,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal? BaseAmount = null);
    public sealed record SaveCorporateEventDto(Guid RequestId, DateOnly Date, string Kind, int PositionId,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal Quantity,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal Cost, string Reason);
    public sealed record ReverseMovementDto(Guid RequestId, string Reason);
    public sealed record MovementEffectDto(string Name, string CurrencyCode,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal BeforeValue,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal AfterValue,
        bool IsPosition,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal BeforeQuantity,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal AfterQuantity,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal BeforeCost,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal AfterCost,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal BeforeIncome,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal AfterIncome);
    public sealed record MovementDto(int Id, DateOnly Date, string Kind, string Description, string CurrencyCode,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal Amount,
        int? TradeId, int? IncomeReceiptId, int? ReversalOfId, int? ReversedById,
        bool CanReverse, string? ReversalBlockedReason, DateTime CreatedAtUtc, IReadOnlyList<MovementEffectDto> Effects)
    {
        public int? CorrectionId { get; init; }
        public int? ReplacesMovementId { get; init; }
    }
    public interface IMovementService
    {
        Task<IReadOnlyList<MovementDto>> ListAsync(int portfolioId, CancellationToken ct);
        Task<MovementDto?> SaveAsync(int portfolioId, SaveMovementDto dto, CancellationToken ct);
        Task<MovementDto?> CorporateEventAsync(int portfolioId, SaveCorporateEventDto dto, CancellationToken ct);
        Task<MovementDto?> ReverseAsync(int portfolioId, int id, ReverseMovementDto dto, CancellationToken ct);
    }
    public interface IMovementRepository
    {
        Task<IReadOnlyList<FinancialMovement>> ListAsync(int portfolioId, CancellationToken ct);
        Task<FinancialMovement?> ExecuteAsync(int portfolioId, Guid requestId,
            Func<Portfolio, FinancialMovement?, CancellationToken, Task<FinancialMovement>> apply, CancellationToken ct);
        Task<FinancialMovement?> GetAsync(int portfolioId, int id, CancellationToken ct);
        Task<bool> IsReversedAsync(int id, CancellationToken ct);
        Task<bool> HasDependentAsync(FinancialMovement movement, CancellationToken ct);
        Task<int> ReopenAsync(int portfolioId, DateOnly from, CancellationToken ct);
        Task<DateOnly?> LatestDateAsync(int portfolioId, CancellationToken ct);
        Task<PortfolioAsset?> PositionAsync(int portfolioId, int id, CancellationToken ct);
        Task<IReadOnlyList<PortfolioCashFlow>> FlowsAsync(int movementId, CancellationToken ct);
        void AddFlow(PortfolioCashFlow flow, FinancialMovement movement);
    }
}
