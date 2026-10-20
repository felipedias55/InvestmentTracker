using InvestmentTracker.Application.Quotes;
using InvestmentTracker.Application.Portfolios.Interfaces;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Domain.Entities;
using Moq;

namespace InvestmentTracker.UnitTests.Quotes
{
    public class AssetQuoteServiceTests
    {
        private sealed class Clock : TimeProvider
        { public override DateTimeOffset GetUtcNow() => new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero); }
        private static PortfolioAsset Position(int id, string country = "Estados Unidos", string currency = "USD")
            => new() { Id = id, Quantity = 2, Asset = new() { Ticker = "MSFT", Country = new() { Name = country }, Currency = new() { Code = currency } } };

        [Theory]
        [InlineData("MSFT", "USD", 6, 100, true)]
        [InlineData("OTHER", "USD", 6, 100, false)]
        [InlineData("MSFT", "BRL", 6, 100, false)]
        [InlineData("MSFT", "USD", 8, 100, false)]
        [InlineData("MSFT", "USD", 1, 0, false)]
        public async Task ShouldValidateReturnedIdentityDateAndPrice(string ticker, string currency, int day, int price, bool valid)
        {
            var repo = new Mock<IPortfolioRepository>();
            repo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(new Portfolio());
            repo.Setup(r => r.GetPositionsAsync(1, default)).ReturnsAsync([Position(7)]);
            var provider = new Mock<IAssetQuoteProvider>();
            provider.Setup(p => p.FetchAsync("US", "MSFT", default)).ReturnsAsync(new AssetQuote("Alpha Vantage", ticker, currency, price, new(2026, 10, day), "available", ""));
            var result = await new AssetQuoteService(repo.Object, provider.Object, new Clock()).PreviewAsync(1, new([7]), default);
            Assert.Equal(valid, Assert.Single(result!).Quote.Price.HasValue);
            repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
        [Fact]
        public async Task ShouldFetchOnlySelectedPositivePositions()
        {
            var repo = new Mock<IPortfolioRepository>();
            repo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(new Portfolio());
            var zero = Position(9); zero.Quantity = 0;
            repo.Setup(r => r.GetPositionsAsync(1, default)).ReturnsAsync([Position(7), Position(8), zero]);
            var provider = new Mock<IAssetQuoteProvider>(MockBehavior.Strict);
            provider.Setup(p => p.FetchAsync("US", "MSFT", default))
                .ReturnsAsync(new AssetQuote("Alpha Vantage", "MSFT", "USD", 100, new(2026, 10, 6), "available", ""));
            var result = await new AssetQuoteService(repo.Object, provider.Object, new Clock()).PreviewAsync(1, new([8, 9]), default);
            Assert.Equal(new[] { 8, 9 }, result!.Select(r => r.PositionId));
            provider.Verify(p => p.FetchAsync("US", "MSFT", default), Times.Once);
            provider.VerifyNoOtherCalls();
        }
        [Fact]
        public async Task UnsupportedCountriesShouldNotBeGuessedFromCurrency()
        {
            var repo = new Mock<IPortfolioRepository>();
            repo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(new Portfolio());
            repo.Setup(r => r.GetPositionsAsync(1, default)).ReturnsAsync([Position(7, "Canadá")]);
            var provider = new Mock<IAssetQuoteProvider>(MockBehavior.Strict);
            var service = new AssetQuoteService(repo.Object, provider.Object, new Clock());
            Assert.Equal("unsupported", Assert.Single((await service.PreviewAsync(1, new([7]), default))!).Quote.Status);
            await Assert.ThrowsAsync<ResourceConflictException>(() => service.PreviewAsync(1, new([99]), default));
            provider.VerifyNoOtherCalls();
        }
    }
}
