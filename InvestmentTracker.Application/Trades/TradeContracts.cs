using System.Text.Json.Serialization;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.Trades
{
    public sealed record SaveTradeDto(Guid RequestId, DateOnly Date, string Kind, int AssetId,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal Quantity,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal UnitPrice,
        int? CashAssetId = null,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal? BaseAmount = null);

    public sealed record TradeDto(int Id, DateOnly Date, string Kind, string Ticker, string CurrencyCode,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal Quantity,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal UnitPrice,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal Amount,
        string? CashAssetName);

    public interface ITradeService
    {
        Task<IReadOnlyList<TradeDto>> ListAsync(int portfolioId, CancellationToken ct);
        Task<TradeDto?> SaveAsync(int portfolioId, SaveTradeDto dto, CancellationToken ct);
    }

    public interface ITradeRepository
    {
        Task<IReadOnlyList<PortfolioTrade>> ListAsync(int portfolioId, CancellationToken ct);
        Task<PortfolioTrade?> ExecuteAsync(int portfolioId, Guid requestId,
            Func<Portfolio, PortfolioTrade?, CancellationToken, Task<PortfolioTrade>> apply, CancellationToken ct);
        Task<PortfolioAsset?> PositionAsync(int portfolioId, int assetId, CancellationToken ct);
        Task<DateOnly?> LatestDateAsync(int portfolioId, CancellationToken ct);
        void AddPosition(PortfolioAsset position);
        void AddFlow(PortfolioCashFlow flow, PortfolioTrade trade);
    }
}
