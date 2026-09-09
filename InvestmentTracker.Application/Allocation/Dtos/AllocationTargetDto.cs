using System.Text.Json.Serialization;

namespace InvestmentTracker.Application.Allocation.Dtos
{
    public sealed record AllocationTargetDto(
        int GroupId,
        string Name,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal TargetPercentage);
}
