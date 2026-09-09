using System.Text.Json.Serialization;

namespace InvestmentTracker.Application.Allocation.Dtos
{
    public sealed record TargetEntryDto(
        int GroupId,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal TargetPercentage);
}
