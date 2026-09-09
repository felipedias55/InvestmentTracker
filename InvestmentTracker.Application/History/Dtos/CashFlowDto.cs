using System.Text.Json.Serialization;

namespace InvestmentTracker.Application.History.Dtos
{
    public sealed record CashFlowDto(
        int Id,
        DateOnly Date,
        string Kind,
        int CurrencyId,
        string CurrencyCode,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Amount,
        string BaseCurrencyCode,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? BaseAmount,
        string? Notes)
    {
        public int? TradeId { get; init; }
    }
}
