using System.Text.Json.Serialization;

namespace InvestmentTracker.Application.ExternalAssets.Dtos
{
    public sealed record SaveExternalAssetDto(
        string Name,
        int CurrencyId,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Value,
        string? Description = null);
}
