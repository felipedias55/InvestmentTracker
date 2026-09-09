using InvestmentTracker.Application.History.Services;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.UnitTests.History
{
    public class HistoryCalculatorTests
    {
        private static PortfolioSnapshot Photo(int id, int year, int month, int day, decimal wealth, string currency = "BRL") => new()
        {
            Id = id, Month = new DateOnly(year, month, 1), SnapshotDate = new DateOnly(year, month, day),
            TotalWealth = wealth, PortfolioValue = wealth, BaseCurrencyCode = currency, TotalIncome = 12m
        };
        private static PortfolioCashFlow Flow(int year, int month, int day, decimal amount, string kind = "contribution",
            string currency = "BRL", decimal? baseAmount = null) => new()
        {
            Date = new DateOnly(year, month, day), Kind = kind, Currency = new Currency { Code = currency },
            Amount = amount, BaseCurrencyCode = "BRL", BaseAmount = baseAmount
        };

        [Fact]
        public void MonthlyChange_ShouldSubtractContributionsAndAddWithdrawalsBetweenPhotographDates()
        {
            var photos = new[] { Photo(1, 2026, 8, 20, 1000m), Photo(2, 2026, 9, 20, 1300m) };
            var flows = new[] { Flow(2026, 8, 20, 900m), Flow(2026, 8, 21, 100m), Flow(2026, 9, 20, 50m, "withdrawal"), Flow(2026, 9, 21, 800m) };
            var rows = HistoryCalculator.Months(photos, flows, new DateOnly(2026, 9, 30), "BRL");
            var september = rows.Single(r => r.Period == "2026-09");
            Assert.Equal(300m, september.Change);
            Assert.Equal(50m, september.NetFlowsBetweenSnapshots);
            Assert.Equal(250m, september.ChangeExcludingFlows);
            Assert.Equal(800m, september.Contributions);
            Assert.Equal(50m, september.Withdrawals);
            Assert.Equal(new DateOnly(2026, 8, 20), september.ComparisonStart);
            Assert.Null(rows.First().Change);
        }

        [Fact]
        public void MissingMonths_ShouldRemainGapsAndNotUseZeroAsABaseline()
        {
            var rows = HistoryCalculator.Months([Photo(1, 2026, 7, 31, 1000m), Photo(2, 2026, 9, 8, 2000m)], [], new DateOnly(2026, 9, 8), "BRL");
            Assert.Equal(3, rows.Count);
            Assert.Null(rows[1].SnapshotId); Assert.Null(rows[1].TotalWealth);
            Assert.Null(rows[2].Change); Assert.Null(rows[2].ChangeExcludingFlows);
            Assert.Equal("Sem fotografia no período anterior para comparar.", rows[2].ComparisonNote);
        }

        [Fact]
        public void YearlyView_ShouldUseLastSnapshotAndActualComparisonDatesInsteadOfSummingBalances()
        {
            var photos = new[] { Photo(1, 2025, 10, 15, 1000m), Photo(2, 2026, 1, 15, 1100m), Photo(3, 2026, 12, 20, 1800m) };
            var flows = new[] { Flow(2025, 11, 1, 100m), Flow(2026, 2, 1, 200m), Flow(2026, 12, 25, 400m) };
            var row = HistoryCalculator.Years(photos, flows, new DateOnly(2026, 12, 31), "BRL").Last();
            Assert.Equal(1800m, row.TotalWealth); Assert.Equal(3, row.SnapshotId);
            Assert.Equal(600m, row.Contributions); Assert.Equal(300m, row.NetFlowsBetweenSnapshots);
            Assert.Equal(500m, row.ChangeExcludingFlows);
            Assert.Equal(new DateOnly(2025, 10, 15), row.ComparisonStart);
        }

        [Fact]
        public void ChangedBaseCurrency_ShouldNotCompareAmountsInDifferentCurrencies()
        {
            var row = HistoryCalculator.Months([Photo(1, 2026, 8, 31, 1000m), Photo(2, 2026, 9, 30, 200m, "USD")], [], new DateOnly(2026, 9, 30), "USD").Last();
            Assert.Null(row.Change); Assert.Null(row.ChangeExcludingFlows);
            Assert.Equal("Moedas-base diferentes entre as fotografias.", row.ComparisonNote);
        }

        [Fact]
        public void ForeignFlows_ShouldUseSavedEquivalentAndNeverTodaysExchangeRate()
        {
            var photos = new[] { Photo(1, 2026, 8, 31, 1000m), Photo(2, 2026, 9, 30, 1600m) };
            var rows = HistoryCalculator.Months(photos, [Flow(2026, 9, 1, 100m, currency: "USD", baseAmount: 450m)], new DateOnly(2026, 9, 30), "BRL");
            Assert.Equal(450m, rows.Last().Contributions); Assert.Equal(150m, rows.Last().ChangeExcludingFlows);
            rows = HistoryCalculator.Months(photos, [Flow(2026, 9, 1, 100m, currency: "USD")], new DateOnly(2026, 9, 30), "BRL");
            Assert.Equal(600m, rows.Last().Change); Assert.Null(rows.Last().Contributions);
            Assert.Null(rows.Last().NetFlowsBetweenSnapshots); Assert.Null(rows.Last().ChangeExcludingFlows);
        }

        [Fact]
        public void EmptyHistory_ShouldExposeCurrentPeriodWithoutInventingHistoricalWealth()
        {
            var month = Assert.Single(HistoryCalculator.Months([], [], new DateOnly(2026, 9, 8), "BRL"));
            Assert.Equal("2026-09", month.Period); Assert.Null(month.TotalWealth); Assert.Equal(0m, month.Contributions);
            Assert.Null(month.Change); Assert.Null(month.SnapshotId);
            Assert.Single(HistoryCalculator.Years([], [], new DateOnly(2026, 9, 8), "BRL"));
        }
    }
}
