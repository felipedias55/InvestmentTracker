using System.Text.Json.Serialization;

namespace InvestmentTracker.Application.History.Dtos
{
    public sealed record SaveCashFlowDto(
        DateOnly Date,
        string Kind,
        int CurrencyId,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Amount,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? BaseAmount = null,
        string? Notes = null);
}
