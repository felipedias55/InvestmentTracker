using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Currencies.Interfaces;
using InvestmentTracker.Application.ExchangeRates.Interfaces;
using InvestmentTracker.Application.Income;
using InvestmentTracker.Application.Movements;
using InvestmentTracker.Domain.Entities;
using Moq;

namespace InvestmentTracker.UnitTests.Income
{
    public class IncomeConversionServiceTests
    {
        private static readonly DateOnly Date = new(2026, 9, 6);
        private static IncomeReceipt Receipt(string code = "USD") => new() { Date = Date, CurrencyCode = code, Amount = 10 };
        private static IncomeConversionService Service(IHistoricalExchangeRateProvider provider) => new(provider,
            Mock.Of<IIncomeRepository>(), Mock.Of<IMovementRepository>(), Mock.Of<ICurrencyRepository>(), TimeProvider.System);
        [Fact]
        public async Task ManualAndSameCurrency_ShouldNeverFetchAndShouldPreserveExactEquivalent()
        {
            var provider = new Mock<IHistoricalExchangeRateProvider>(MockBehavior.Strict);
            var service = Service(provider.Object);
            var manual = await service.BuildAsync(Receipt(), "BRL", 51.2345m, Guid.NewGuid(), 1, "Extrato", default);
            Assert.Equal(51.2345m, manual!.BaseAmount); Assert.Equal(5.12345m, manual.Rate); Assert.Equal("manual", manual.Source);
            var same = await service.BuildAsync(Receipt("BRL"), "BRL", null, Guid.NewGuid(), 1, "Mesmo código", default);
            Assert.Equal(10m, same!.BaseAmount); Assert.Equal(1m, same.Rate);
            await Assert.ThrowsAsync<InputValidationException>(() => service.BuildAsync(Receipt("BRL"), "BRL", 11, Guid.NewGuid(), 1, "Inválido", default));
        }
        [Theory]
        [InlineData(0, true)]
        [InlineData(-2, true)]
        [InlineData(-7, true)]
        [InlineData(-8, false)]
        [InlineData(1, false)]
        public async Task Automatic_ShouldUseRequestedDateAndBoundPreviousReferences(int offset, bool valid)
        {
            var provider = new Mock<IHistoricalExchangeRateProvider>();
            provider.Setup(x => x.FetchAsync("USD", "BRL", Date, It.IsAny<CancellationToken>())).ReturnsAsync(new ExchangeRate {
                BaseCode = "USD", QuoteCode = "BRL", Rate = 5.123456m, RateDate = Date.AddDays(offset) });
            var result = await Service(provider.Object).BuildAsync(Receipt(), "BRL", null, Guid.NewGuid(), 1, "Automático", default);
            if (valid) { Assert.NotNull(result); Assert.Equal(51.2346m, result.BaseAmount); Assert.Equal(Date.AddDays(offset), result.RateDate); }
            else Assert.Null(result);
            provider.VerifyAll();
        }
        [Fact]
        public async Task Outage_ShouldLeaveConversionPendingButCancellationShouldPropagate()
        {
            var provider = new Mock<IHistoricalExchangeRateProvider>();
            provider.Setup(x => x.FetchAsync("USD", "BRL", Date, It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("offline"));
            Assert.Null(await Service(provider.Object).BuildAsync(Receipt(), "BRL", null, Guid.NewGuid(), 1, "Automático", default));
            using var cts = new CancellationTokenSource(); cts.Cancel();
            provider.Setup(x => x.FetchAsync("USD", "BRL", Date, cts.Token)).ThrowsAsync(new TaskCanceledException());
            await Assert.ThrowsAsync<TaskCanceledException>(() => Service(provider.Object).BuildAsync(Receipt(), "BRL", null, Guid.NewGuid(), 1, "Automático", cts.Token));
        }
        [Fact]
        public void Analysis_ShouldUseLatestRevisionForBothOriginalAndReversalAndNeverSumPartialAmounts()
        {
            var entry = new IncomeEvent(Date, "TEST", 1, "USD", 10, true, false) { Conversions = [
                new(1, 1, "BRL", 50, 5, Date, "manual", "Primeira", DateTime.UtcNow),
                new(2, 2, "BRL", 52, 5.2m, Date, "manual", "Correção", DateTime.UtcNow)] };
            var report = IncomeAnalysisService.Calculate([entry, entry with { IsReversal = true, Date = Date.AddMonths(1) }], "BRL");
            Assert.Equal(52m, report.ConvertedMonths.Single(x => x.Period == "2026-09").Net);
            Assert.Equal(-52m, report.ConvertedMonths.Single(x => x.Period == "2026-10").Net);
            Assert.Equal(0m, report.ConvertedAssets.Single().Net);
            var partial = IncomeAnalysisService.Calculate([entry, entry with { Conversions = [] }], "BRL");
            Assert.Null(partial.ConvertedAssets.Single().Net); Assert.Equal(1, partial.ConvertedAssets.Single().MissingConversions);
            Assert.Equal(20m, partial.Assets.Single().Net);
            Assert.Null(IncomeAnalysisService.Calculate([entry], "EUR").ConvertedAssets.Single().Net);
        }
    }
}
