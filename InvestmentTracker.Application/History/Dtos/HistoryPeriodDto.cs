using System.Text.Json.Serialization;

namespace InvestmentTracker.Application.History.Dtos
{
    public sealed record HistoryPeriodDto(
        string Period,
        int? SnapshotId,
        DateOnly? SnapshotDate,
        string CurrencyCode,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? PortfolioValue,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? ExternalValue,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? TotalWealth,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? TotalIncome,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? Contributions,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? Withdrawals,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? Change,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? NetFlowsBetweenSnapshots,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? ChangeExcludingFlows,
        DateOnly? ComparisonStart,
        string? ComparisonNote,
        bool HasStaleRates,
        bool HasFallbackRates);
}
