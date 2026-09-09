using System.Text.Json.Serialization;

namespace InvestmentTracker.Application.Allocation.Dtos
{
    public sealed record ContributionAnalysisDto(
        string CurrencyCode,
        string Dimension,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Amount,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal SumOfWeights,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal UnallocatedAmount,
        bool HasStaleRates,
        bool HasFallbackRates,
        IReadOnlyList<ContributionRowDto> Rows);
}
