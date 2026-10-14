using System.Text.Json.Serialization;

namespace InvestmentTracker.Application.Allocation.Dtos
{
    public sealed record ContributionPlanRequestDto(
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Amount);
    public sealed record ContributionCurrencyDto(string Currency,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? Amount,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? Rate,
        string Dates, bool Stale);
    public sealed record ContributionPlanRowDto(string Key, string Category, string Sector,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Amount,
        IReadOnlyList<ContributionCurrencyDto> Currencies);
    public sealed record ContributionJointPlanDto(IReadOnlyList<ContributionPlanRowDto> Rows,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Unallocated);
    public sealed record ContributionPlanDto(DashboardDto Dashboard, ContributionAnalysisDto? Category,
        ContributionAnalysisDto? Sector, ContributionJointPlanDto? Plan);
}
