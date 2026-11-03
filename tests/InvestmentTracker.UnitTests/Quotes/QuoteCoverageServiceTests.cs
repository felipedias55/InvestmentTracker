using InvestmentTracker.Application.Assets.Interfaces;
using InvestmentTracker.Application.Quotes;
using InvestmentTracker.Domain.Entities;
using Moq;

namespace InvestmentTracker.UnitTests.Quotes
{
    public sealed class QuoteCoverageServiceTests
    {
        private sealed class Clock : TimeProvider
        { public override DateTimeOffset GetUtcNow() => new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero); }

        [Theory]
        [InlineData("MSFT", "USD", 27, 100, "available")]
        [InlineData("OTHER", "USD", 27, 100, "invalid")]
        [InlineData("MSFT", "BRL", 27, 100, "invalid")]
        [InlineData("MSFT", "USD", 29, 100, "invalid")]
        [InlineData("MSFT", "USD", 1, 100, "invalid")]
        [InlineData("MSFT", "USD", 27, 0, "invalid")]
        public async Task CoverageShouldValidateQuotesWithoutWritingOrGuessingMarkets(
            string symbol, string currency, int day, int price, string expected)
        {
            var assets = new Mock<IAssetRepository>(MockBehavior.Strict);
            assets.Setup(repo => repo.GetAllAsync(default)).ReturnsAsync([
                new Asset { Id = 1, Ticker = "MSFT", Country = new() { Name = "Estados Unidos" }, Currency = new() { Code = "USD" } },
                new Asset { Id = 2, Ticker = "OTHER", Country = new() { Name = "Canadá" }, Currency = new() { Code = "USD" } }
            ]);
            var provider = new Mock<IAssetQuoteProvider>(MockBehavior.Strict);
            provider.Setup(p => p.FetchAsync("US", "MSFT", default)).ReturnsAsync(
                new AssetQuote("Alpha Vantage", symbol, currency, price, new(2026, 10, day), "available", "") { Requested = true });
            var rows = await new QuoteCoverageService(assets.Object, provider.Object, new Clock()).CheckAsync(default);
            Assert.Equal(expected, rows.Single(row => row.AssetId == 1).Status);
            Assert.Equal("unsupported", rows.Single(row => row.AssetId == 2).Status);
            assets.Verify(repo => repo.GetAllAsync(default), Times.Once);
            assets.VerifyNoOtherCalls();
            provider.Verify(p => p.FetchAsync("US", "MSFT", default), Times.Once);
            provider.VerifyNoOtherCalls();
        }
    }
}
