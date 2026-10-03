using InvestmentTracker.Application.History.Services;
using InvestmentTracker.Application.Income;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.UnitTests.History
{
    public class ReturnCalculatorTests
    {
        private static readonly DateOnly Start = new(2025, 1, 1);
        private static PortfolioSnapshot Photo(DateOnly date, decimal value) => new()
        {
            Month = new(date.Year, date.Month, 1), SnapshotDate = date,
            BaseCurrencyCode = "BRL", TotalWealth = value
        };
        private static PortfolioCashFlow Flow(int day, decimal amount, string kind = "contribution") => new()
        {
            Date = Start.AddDays(day), Amount = amount, Kind = kind, Currency = new() { Code = "BRL" }
        };

        [Theory]
        [InlineData(1100, 0.1)]
        [InlineData(900, -0.1)]
        [InlineData(1000, 0)]
        public void FullYearWithoutFlows_ReturnsKnownRate(decimal final, decimal expected)
        {
            var result = ReturnCalculator.Calculate(Photo(Start, 1000), Photo(Start.AddDays(365), final), [], []);
            Assert.Equal(expected, result.ModifiedDietz);
            Assert.InRange(result.Xirr!.Value, expected - 0.00000001m, expected + 0.00000001m);
        }

        [Fact]
        public void Dietz_WeightsContributionsByDaysAndExcludesOutsideBoundaries()
        {
            var result = ReturnCalculator.Calculate(Photo(Start, 1000), Photo(Start.AddDays(30), 1600),
                [Flow(0, 999), Flow(15, 500), Flow(31, 999)], []);
            Assert.Equal(0.08m, result.ModifiedDietz); // 100 / (1000 + 500 * 15/30).
            Assert.Contains("inferior a um ano", result.Note);
        }

        [Fact]
        public void FinalDayContributionHasZeroWeightAndNoReturn()
        {
            var result = ReturnCalculator.Calculate(Photo(Start, 1000), Photo(Start.AddDays(30), 1500), [Flow(30, 500)], []);
            Assert.Equal(0m, result.ModifiedDietz);
            Assert.InRange(result.Xirr!.Value, -0.00000001m, 0.00000001m);
        }

        [Fact]
        public void IncomeRetainedAndDistributed_ProducesSameTotalReturnWithoutDoubleCounting()
        {
            var retained = new IncomeEvent(Start.AddDays(365), "TEST", 1, "BRL", 100, true, false);
            var a = ReturnCalculator.Calculate(Photo(Start, 1000), Photo(Start.AddDays(365), 1100), [], [retained]);
            var b = ReturnCalculator.Calculate(Photo(Start, 1000), Photo(Start.AddDays(365), 1000), [], [retained with { Retained = false }]);
            Assert.Equal(0.1m, a.ModifiedDietz);
            Assert.Equal(a.ModifiedDietz, b.ModifiedDietz);
            Assert.Equal(a.Xirr, b.Xirr);
        }

        [Fact]
        public void ReversalsCancelFlowsAndDistributionsOnSameDate()
        {
            var original = Flow(15, 100);
            var reversal = Flow(15, 100); reversal.IsReversal = true;
            var income = new IncomeEvent(Start.AddDays(15), "TEST", 1, "BRL", 20, false, false);
            var result = ReturnCalculator.Calculate(Photo(Start, 1000), Photo(Start.AddDays(365), 1100),
                [original, reversal], [income, income with { IsReversal = true }]);
            Assert.Equal(0.1m, result.ModifiedDietz);
            Assert.InRange(result.Xirr!.Value, 0.09999999m, 0.10000001m);
        }

        [Fact]
        public void ForeignFlow_UsesStoredEquivalentAndBlocksMissingConversion()
        {
            var flow = Flow(365, 10); flow.Currency.Code = "USD"; flow.BaseCurrencyCode = "BRL"; flow.BaseAmount = 50;
            var start = Photo(Start, 1000); var end = Photo(Start.AddDays(365), 1150);
            Assert.Equal(0.1m, ReturnCalculator.Calculate(start, end, [flow], []).ModifiedDietz);
            flow.BaseAmount = null;
            var result = ReturnCalculator.Calculate(start, end, [flow], []);
            Assert.Null(result.ModifiedDietz); Assert.Null(result.Xirr);
        }

        [Fact]
        public void NonConventionalFlows_DoNotSelectArbitraryXirrRoot()
        {
            var result = ReturnCalculator.Calculate(Photo(Start, 1000), Photo(Start.AddDays(365), 1000),
                [Flow(100, 100, "withdrawal"), Flow(200, 100)], []);
            Assert.NotNull(result.ModifiedDietz); Assert.Null(result.Xirr);
            Assert.Contains("raiz única", result.Note);
        }

        [Fact]
        public void NonPositiveWeightedCapital_BlocksDietz()
        {
            var result = ReturnCalculator.Calculate(Photo(Start, 100), Photo(Start.AddDays(30), 0), [Flow(1, 200, "withdrawal")], []);
            Assert.Null(result.ModifiedDietz);
        }

        [Theory]
        [InlineData("missing")]
        [InlineData("outdated")]
        [InlineData("reopened")]
        [InlineData("currency")]
        [InlineData("zero")]
        [InlineData("dates")]
        [InlineData("income")]
        public void UnreliableInputs_BlockBothMetrics(string reason)
        {
            var start = Photo(Start, 1000); var end = Photo(Start.AddDays(30), 1100);
            if (reason == "outdated") start.IsOutdated = true;
            if (reason == "reopened") end.IsReopened = true;
            if (reason == "currency") end.BaseCurrencyCode = "USD";
            if (reason == "zero") start.TotalWealth = 0;
            if (reason == "dates") end.SnapshotDate = Start;
            IncomeEvent[] income = reason == "income" ? [new(Start.AddDays(1), "TEST", 1, "USD", 10, true, false)] : [];
            var result = ReturnCalculator.Calculate(reason == "missing" ? null : start, end, [], income);
            Assert.Null(result.ModifiedDietz); Assert.Null(result.Xirr); Assert.NotEmpty(result.Note!);
        }

        [Fact]
        public void Xirr_UsesDatesOfIntermediateContributions()
        {
            // 1000 * 1.1^2 + 500 * 1.1 = 1760, with an additional investment after one year.
            var result = ReturnCalculator.Calculate(Photo(Start, 1000), Photo(Start.AddDays(730), 1760),
                [Flow(365, 500)], []);
            Assert.InRange(result.Xirr!.Value, 0.09999999m, 0.10000001m);
            Assert.Equal(0.208m, result.ModifiedDietz);
        }

        [Fact]
        public void Xirr_UsesDatesOfIntermediateWithdrawals()
        {
            // Withdraw 100 of the 1100 after one year; the remaining 1000 reaches 1100 next year.
            var result = ReturnCalculator.Calculate(Photo(Start, 1000), Photo(Start.AddDays(730), 1100),
                [Flow(365, 100, "withdrawal")], []);
            Assert.InRange(result.Xirr!.Value, 0.09999999m, 0.10000001m);
        }

        [Fact]
        public void LongIntervals_AvoidNumericalOverflow()
        {
            var result = ReturnCalculator.Calculate(Photo(new(1900, 1, 1), 1000), Photo(new(2026, 1, 1), 2000), [], []);
            Assert.InRange(result.Xirr!.Value, 0.005m, 0.006m);
        }
    }
}
