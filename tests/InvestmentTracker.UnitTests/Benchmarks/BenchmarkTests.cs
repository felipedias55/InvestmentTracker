using InvestmentTracker.Application.Benchmarks;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.History.Dtos;
using InvestmentTracker.Application.History.Interfaces;
using Moq;

namespace InvestmentTracker.UnitTests.Benchmarks
{
    public class BenchmarkTests
    {
        private static DateOnly Day(int month, int day) => new(2026, month, day);
        [Fact]
        public void DailyRatesCompoundAndExcludeInitialDate()
        {
            var result = BenchmarkCalculator.Measure("CDI", Day(2, 1), Day(2, 3), [new(Day(2, 1), 50), new(Day(2, 2), 10), new(Day(2, 3), 10)]);
            Assert.Equal(1.21m, result.Factor);
        }
        [Fact]
        public void InflationCompoundsPublishedMonthsIncludingDeflationButNeverProrates()
        {
            BenchmarkPoint[] points = [new(Day(2, 1), 0.5m), new(Day(3, 1), -0.1m)];
            Assert.Equal(1.003995m, BenchmarkCalculator.Measure("IPCA", Day(1, 31), Day(3, 31), points).Factor);
            Assert.Null(BenchmarkCalculator.Measure("IPCA", Day(1, 30), Day(3, 31), points).Factor);
            Assert.Null(BenchmarkCalculator.Measure("IPCA", Day(1, 31), Day(3, 31), points[..1]).Factor);
        }
        [Fact]
        public void PriceIndexUsesOnlyPastClosesWithLimitedLookbackAndRejectsMissingOrDuplicateData()
        {
            BenchmarkPoint[] points = [new(Day(1, 30), 100), new(Day(2, 27), 90), new(Day(3, 2), 500)];
            var value = BenchmarkCalculator.Measure("SP500", Day(1, 31), Day(2, 28), points);
            Assert.Equal(0.9m, value.Factor); Assert.Equal(Day(2, 27), value.Last);
            Assert.Null(BenchmarkCalculator.Measure("SP500", Day(2, 10), Day(2, 28), points).Factor);
            Assert.Null(BenchmarkCalculator.Measure("SP500", Day(1, 31), Day(2, 28), [..points, points[0]]).Factor);
            Assert.Null(BenchmarkCalculator.Measure("SP500", Day(1, 31), Day(2, 28), [new(Day(1, 30), 0), points[1]]).Factor);
        }
        [Fact]
        public void DailySeriesDoesNotBridgeLongGapsOrTreatAnEmptySeriesAsZero()
        {
            Assert.Null(BenchmarkCalculator.Measure("CDI", Day(1, 31), Day(2, 28), [new(Day(2, 2), 1), new(Day(2, 27), 1)]).Factor);
            Assert.Null(BenchmarkCalculator.Measure("CDI", Day(1, 31), Day(2, 28), []).Factor);
        }
        private static HistoryDto History(string currency = "BRL", bool outdated = false, bool partial = false)
        {
            var row = new HistoryPeriodDto("2026-02", 2, Day(2, partial ? 27 : 28), currency, 110, 0, 110, 0, 0, 0, 10, 0, 10, Day(1, 31), null, false, false)
                { Returns = new(0.10m, 9m, null), IsOutdated = outdated };
            return new(new(1, "Carteira", null, 1, currency), Day(3, 1), [row], [], []);
        }
        private static (BenchmarkService Service, Mock<IHistoryService> History, Mock<IBenchmarkProvider> Provider) Create(HistoryDto? data)
        {
            var history = new Mock<IHistoryService>(MockBehavior.Strict);
            history.Setup(h => h.GetAsync(1, It.IsAny<CancellationToken>(), false)).ReturnsAsync(data);
            var provider = new Mock<IBenchmarkProvider>(MockBehavior.Strict);
            provider.Setup(p => p.FetchAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string code, DateOnly start, DateOnly end, CancellationToken _) => new BenchmarkSeries(code, "Fonte sintética", code switch
                {
                    "CDI" => Enumerable.Range(1, 28).Select(d => new BenchmarkPoint(Day(2, d), 0.01m)).ToArray(),
                    "IPCA" => [new(Day(2, 1), 0.5m)],
                    "IBOV" => [new(Day(1, 30), 100), new(Day(2, 27), 105)],
                    "SP500" => [new(Day(1, 30), 100), new(Day(2, 27), 110)],
                    _ => [new(Day(1, 30), 5), new(Day(2, 27), 6)]
                }));
            return (new(history.Object, provider.Object), history, provider);
        }
        [Fact]
        public async Task UsesBackendDietzInsteadOfXirrAndHistoricalFxForDollarReference()
        {
            var (service, _, provider) = Create(History()); var result = (await service.CompareAsync(1, "months", "2026-02", default))!;
            Assert.Equal(0.10m, result.PortfolioReturn); Assert.Equal(4, result.Items.Count);
            var ibov = result.Items.Single(r => r.Code == "IBOV"); Assert.Equal(.05m, ibov.Return); Assert.Equal(.05m, ibov.Difference);
            var sp = result.Items.Single(r => r.Code == "SP500"); Assert.Equal(.32m, sp.Return); Assert.Equal(-.22m, sp.Difference);
            provider.Verify(p => p.FetchAsync("USDBRL", Day(1, 31), Day(2, 28), It.IsAny<CancellationToken>()), Times.Once);
        }
        [Fact]
        public async Task ConvertsBrazilianIndexToUsdButDoesNotPresentIpcaAsForeignInflation()
        {
            var (service, _, provider) = Create(History("USD")); var result = (await service.CompareAsync(1, "months", "2026-02", default))!;
            Assert.Equal(-.125m, result.Items.Single(r => r.Code == "IBOV").Return);
            Assert.Equal(.10m, result.Items.Single(r => r.Code == "SP500").Return);
            Assert.Null(result.Items.Single(r => r.Code == "IPCA").Return);
            provider.Verify(p => p.FetchAsync("IPCA", It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
            provider.Verify(p => p.FetchAsync("USDBRL", It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        [Theory]
        [InlineData("BRL", true)]
        [InlineData("EUR", false)]
        public async Task InvalidHistoryAndUnsupportedCurrenciesDoNotConsumeProviderRequests(string currency, bool outdated)
        {
            var (service, _, provider) = Create(History(currency, outdated)); var result = (await service.CompareAsync(1, "months", "2026-02", default))!;
            Assert.All(result.Items, row => { Assert.Null(row.Return); Assert.Null(row.Difference); }); provider.VerifyNoOtherCalls();
        }
        [Fact]
        public async Task PartialCalendarMonthsNeverTriggerInflationRequests()
        {
            var (service, _, provider) = Create(History(partial: true)); var result = (await service.CompareAsync(1, "months", "2026-02", default))!;
            Assert.Null(result.Items.Single(r => r.Code == "IPCA").Return);
            provider.Verify(p => p.FetchAsync("IPCA", It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        [Fact]
        public async Task MissingHistoricalFxBlocksOnlyConvertedReferences()
        {
            var (service, _, provider) = Create(History());
            provider.Setup(p => p.FetchAsync("USDBRL", It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ReturnsAsync(new BenchmarkSeries("USDBRL", "Synthetic", []));
            var result = (await service.CompareAsync(1, "months", "2026-02", default))!;
            Assert.Null(result.Items.Single(r => r.Code == "SP500").Return); Assert.NotNull(result.Items.Single(r => r.Code == "IBOV").Return);
        }
        [Fact]
        public async Task RejectsArbitraryPeriodsAndReturnsNotFoundWithoutProviderCalls()
        {
            var (service, _, provider) = Create(History());
            await Assert.ThrowsAsync<InputValidationException>(() => service.CompareAsync(1, "months", "2030-01", default)); provider.VerifyNoOtherCalls();
            var missing = Create(null); Assert.Null(await missing.Service.CompareAsync(1, "months", "2026-02", default)); missing.Provider.VerifyNoOtherCalls();
        }
        [Fact]
        public async Task ExtremeConversionDoesNotOverflowTheWholeComparison()
        {
            var (service, _, provider) = Create(History());
            provider.Setup(p => p.FetchAsync("SP500", It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ReturnsAsync(new BenchmarkSeries("SP500", "Synthetic", [new(Day(1, 30), 1), new(Day(2, 27), 2)]));
            provider.Setup(p => p.FetchAsync("USDBRL", It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ReturnsAsync(new BenchmarkSeries("USDBRL", "Synthetic", [new(Day(1, 30), 1), new(Day(2, 27), decimal.MaxValue)]));
            var result = (await service.CompareAsync(1, "months", "2026-02", default))!;
            Assert.Null(result.Items.Single(r => r.Code == "SP500").Return); Assert.NotNull(result.Items.Single(r => r.Code == "IBOV").Return);
        }
    }
}
