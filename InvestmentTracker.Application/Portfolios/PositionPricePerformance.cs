using System.Text.Json.Serialization;

namespace InvestmentTracker.Application.Portfolios
{
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public sealed record PositionPricePerformance(decimal? AveragePrice, decimal? UnitPrice, decimal? Variation)
    {
        public static PositionPricePerformance Calculate(decimal quantity, decimal investedAmount, decimal currentValue)
        {
            if (quantity <= 0) return new(null, null, null);
            return new(investedAmount > 0 ? investedAmount / quantity : null, currentValue / quantity,
                investedAmount > 0 ? currentValue / investedAmount - 1m : null);
        }
    }
}
