using System.Text.Json.Serialization;

namespace InvestmentTracker.Application.Allocation.Dtos
{
    public sealed record ContributionRequestDto(
        string Dimension,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Amount);
}
