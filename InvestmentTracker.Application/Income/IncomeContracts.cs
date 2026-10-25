using System.Text.Json.Serialization;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.Income
{
    public sealed record SaveIncomeDto(Guid RequestId, DateOnly Date, int AssetId,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal Amount,
        int? CashAssetId = null, string? Notes = null,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal? BaseAmount = null);

    public sealed record IncomeDto(int Id, DateOnly Date, string Ticker, string CurrencyCode,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Amount,
        string? CashAssetName, string? Notes)
    {
        public int? ReversedById { get; init; }
        public string? BaseCurrencyCode { get; init; }
        public IReadOnlyList<IncomeConversionDto> Conversions { get; init; } = [];
    }
    public sealed record IncomeConversionDto(int Id, int Revision, string BaseCurrencyCode,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal BaseAmount,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? Rate,
        DateOnly RateDate, string Source, string Reason, DateTime CreatedAtUtc);
    public sealed record SaveIncomeConversionDto(Guid RequestId, string BaseCurrencyCode, int ExpectedRevision, string Reason,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal? BaseAmount = null);

    public sealed record IncomePageQuery(int PageIndex = 0, int PageSize = 10, string? Search = null,
        bool PendingOnly = false, DateOnly? From = null, DateOnly? To = null);
    public sealed record IncomePageDto(IReadOnlyList<IncomeDto> Items, int Total, int PageIndex, int PageSize);

    public interface IIncomeService
    {
        Task<IncomePageDto> PageAsync(int portfolioId, IncomePageQuery query, CancellationToken ct);
        Task<IReadOnlyList<IncomeDto>> ListAsync(int portfolioId, CancellationToken ct);
        Task<IncomeDto?> SaveAsync(int portfolioId, SaveIncomeDto dto, CancellationToken ct);
    }
    public interface IIncomeRepository
    {
        Task<IncomePageDto> PageAsync(int portfolioId, IncomePageQuery query, CancellationToken ct);
        Task<IReadOnlyList<IncomeReceipt>> ListAsync(int portfolioId, CancellationToken ct);
        Task<IncomeReceipt?> GetAsync(int portfolioId, int id, CancellationToken ct);
        Task<IncomeReceipt?> ExecuteAsync(int portfolioId, Guid requestId,
            Func<Portfolio, IncomeReceipt?, CancellationToken, Task<IncomeReceipt>> apply, CancellationToken ct);
        Task<PortfolioAsset?> PositionAsync(int portfolioId, int assetId, CancellationToken ct);
        Task<DateOnly?> LatestDateAsync(int portfolioId, CancellationToken ct);
    }
}
