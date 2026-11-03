using System.Text.Json.Serialization;

namespace InvestmentTracker.Application.Allocation.Dtos
{
    public sealed record ContributionPlanRequestDto(
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Amount) { public InvestmentTracker.Application.Strategies.StrategyConfig? Strategy { get; init; } }
    public sealed record ContributionCurrencyDto(string Currency,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? Amount,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? Rate,
        string Dates, bool Stale)
    {
        public IReadOnlyList<string> Tickers { get; init; } = [];
    }
    public sealed record ContributionPlanRowDto(string Key, string Category, string Sector,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Amount,
        IReadOnlyList<ContributionCurrencyDto> Currencies)
    {
        public int CategoryId { get; init; }
        public int SectorId { get; init; }
        public string Stage { get; init; } = "remainder";
        public int RulePriority { get; init; } = 100;
        public int GroupPriority { get; init; } = 100;
        public string? SuggestedCurrency { get; init; }
        public string SuggestionReason { get; init; } = "";
    }
    public sealed record ConcentrationStatusDto(string Dimension, int GroupId, string Name,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal MaximumPercentage,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal? CurrentPercentage,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal? ProjectedPercentage,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal RequestedContribution,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal SuggestedContribution,
        bool InitiallyAboveLimit, bool AboveLimit);
    public sealed record ContributionJointPlanDto(IReadOnlyList<ContributionPlanRowDto> Rows,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Unallocated);
    public sealed record RuleFundingDto(int CategoryId, string Category, int Priority,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal Requested,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal Budget,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal Suggested);
    public sealed record ContributionPlanDto(DashboardDto Dashboard, ContributionAnalysisDto? Category,
        ContributionAnalysisDto? Sector, ContributionJointPlanDto? Plan)
    {
        public string? PlanToken { get; init; }
        public IReadOnlyList<InvestmentTracker.Application.Strategies.RebalancingBandStatusDto> Bands { get; init; } = [];
        public IReadOnlyList<string> StrategyNotes { get; init; } = [];
        public IReadOnlyList<ConcentrationStatusDto> Concentration { get; init; } = [];
        public bool PrioritiesApplied { get; init; }
        public IReadOnlyList<RuleFundingDto> RuleFunding { get; init; } = [];
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
        public decimal? EligibleBase { get; init; }
    }
}
