using InvestmentTracker.Application.Portfolios;

namespace InvestmentTracker.UnitTests.Portfolios
{
    public class PositionPricePerformanceTests
    {
        [Theory]
        [InlineData(10, 200, 230, 20, 23, .15)]
        [InlineData(10, 200, 180, 20, 18, -.1)]
        [InlineData(10, 200, 200, 20, 20, 0)]
        [InlineData(10, 200, 0, 20, 0, -1)]
        [InlineData(.5, 10, 11.5, 20, 23, .15)]
        public void ShouldCompareUnitPricesWithoutRoundingBeforeVariation(decimal quantity, decimal cost, decimal value,
            decimal average, decimal quote, decimal variation)
        {
            var result = PositionPricePerformance.Calculate(quantity, cost, value);
            Assert.Equal(average, result.AveragePrice);
            Assert.Equal(quote, result.UnitPrice);
            Assert.Equal(variation, result.Variation);
        }
        [Fact]
        public void ShouldNotInventReturnsForPositionsWithoutCostOrQuantity()
        {
            Assert.Equal(new PositionPricePerformance(null, null, null), PositionPricePerformance.Calculate(0, 200, 230));
            var free = PositionPricePerformance.Calculate(2, 0, 100);
            Assert.Null(free.AveragePrice); Assert.Null(free.Variation); Assert.Equal(50, free.UnitPrice);
        }
        [Fact]
        public void ShouldSupportLargestValidAmountsAndFractionalQuantity()
        {
            var result = PositionPricePerformance.Calculate(.000001m, .0001m, 999999999999999.9999m);
            Assert.Equal(100, result.AveragePrice);
            Assert.Equal(9999999999999999999m - 1m, result.Variation);
        }
    }
}
