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
        bool HasFallbackRates)
    {
        public ReturnMetricsDto? Returns { get; init; }
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] public decimal? ReceivedIncome { get; init; }
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] public decimal? RetainedIncome { get; init; }
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] public decimal? DistributedIncome { get; init; }
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] public decimal? ValuationAndOtherChanges { get; init; }
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] public decimal? EconomicResult { get; init; }
        public bool IsOutdated { get; init; }
        public bool IsReopened { get; init; }
        public int? Revision { get; init; }
    }
}
