using System.Text.Json.Serialization;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.Income
{
    public sealed record SaveIncomeDto(Guid RequestId, DateOnly Date, int AssetId,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal Amount,
        int? CashAssetId = null, string? Notes = null);

    public sealed record IncomeDto(int Id, DateOnly Date, string Ticker, string CurrencyCode,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Amount,
        string? CashAssetName, string? Notes);

    public interface IIncomeService
    {
        Task<IReadOnlyList<IncomeDto>> ListAsync(int portfolioId, CancellationToken ct);
        Task<IncomeDto?> SaveAsync(int portfolioId, SaveIncomeDto dto, CancellationToken ct);
    }
    public interface IIncomeRepository
    {
        Task<IReadOnlyList<IncomeReceipt>> ListAsync(int portfolioId, CancellationToken ct);
        Task<IncomeReceipt?> ExecuteAsync(int portfolioId, Guid requestId,
            Func<Portfolio, IncomeReceipt?, CancellationToken, Task<IncomeReceipt>> apply, CancellationToken ct);
        Task<PortfolioAsset?> PositionAsync(int portfolioId, int assetId, CancellationToken ct);
        Task<DateOnly?> LatestDateAsync(int portfolioId, CancellationToken ct);
    }
}
