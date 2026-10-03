using System.Text.Json.Serialization;

namespace InvestmentTracker.Application.History.Dtos
{
    public sealed record ReturnMetricsDto(
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal? ModifiedDietz,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal? Xirr,
        string? Note);
}
