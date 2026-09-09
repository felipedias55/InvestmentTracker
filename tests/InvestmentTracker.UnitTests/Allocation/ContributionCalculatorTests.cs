using InvestmentTracker.Application.Allocation.Dtos;
using InvestmentTracker.Application.Allocation.Services;
using InvestmentTracker.Application.Common.Exceptions;

namespace InvestmentTracker.UnitTests.Allocation
{
    public class ContributionCalculatorTests
    {
        private static DashboardDto Dashboard(params AllocationRowDto[] rows) => new(
            new(new(1, "Principal", null, 1, "BRL"), [], [], 1000m, 1000m, true, false, false),
            new(true, true, rows, [new(99, "Setor", 1000m, 1m, 1m, 0m)], []));

        [Fact]
        public void ShouldReproduceSpreadsheetFormulaAndIgnoreOverweightGroups()
        {
            var result = ContributionCalculator.Calculate(Dashboard(
                new(1, "A", 200m, 0.2m, 0.4m, 0.2m), new(2, "B", 300m, 0.3m, 0.4m, 0.1m), new(3, "C", 500m, 0.5m, 0.2m, -0.3m)), new("category", 300m));
            Assert.Equal(0.3m, result.SumOfWeights);
            Assert.Equal(200m, result.Rows.Single(r => r.GroupId == 1).SuggestedContribution);
            Assert.Equal(100m, result.Rows.Single(r => r.GroupId == 2).SuggestedContribution);
            Assert.Equal(0m, result.Rows.Single(r => r.GroupId == 3).SuggestedContribution);
            Assert.Equal(0m, result.UnallocatedAmount);
        }

        [Theory]
        [InlineData("0.01")]
        [InlineData("0.02")]
        [InlineData("100")]
        [InlineData("999999999999999.99")]
        public void Rounding_ShouldConserveEveryCentWithDeterministicTies(string value)
        {
            var amount = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
            var result = ContributionCalculator.Calculate(Dashboard(
                new(3, "C", 0m, 0m, 0.25m, 0.25m), new(2, "B", 0m, 0m, 0.25m, 0.25m), new(1, "A", 0m, 0m, 0.25m, 0.25m),
                new(4, "D", 100m, 1m, 0.25m, -0.75m)), new("category", amount));
            Assert.Equal(amount, result.Rows.Sum(r => r.SuggestedContribution));
            Assert.Equal(0m, result.UnallocatedAmount);
            Assert.All(result.Rows, r => Assert.Equal(decimal.Round(r.SuggestedContribution, 2), r.SuggestedContribution));
            Assert.True(result.Rows.Single(r => r.GroupId == 1).SuggestedContribution >= result.Rows.Single(r => r.GroupId == 3).SuggestedContribution);
        }

        [Fact]
        public void EqualTargets_ShouldLeaveContributionUnallocatedAndSeparateSectorAnalysis()
        {
            var dashboard = Dashboard(new AllocationRowDto(1, "A", 1000m, 1m, 1m, 0m));
            var result = ContributionCalculator.Calculate(dashboard, new("category", 100m));
            Assert.Equal(100m, result.UnallocatedAmount);
            Assert.Equal(0m, result.SumOfWeights);
            Assert.Equal(0m, Assert.Single(result.Rows).SuggestedContribution);
            Assert.Equal(99, Assert.Single(ContributionCalculator.Calculate(dashboard, new("sector", 100m)).Rows).GroupId);
            Assert.Equal(0m, ContributionCalculator.Calculate(dashboard, new("category", 0m)).UnallocatedAmount);
        }

        [Theory]
        [InlineData("-0.01")]
        [InlineData("0.001")]
        [InlineData("1000000000000000")]
        public void ShouldRejectInvalidContributionAmounts(string value)
        {
            Assert.Throws<InputValidationException>(() => ContributionCalculator.Calculate(Dashboard(),
                new("category", decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture))));
        }

        [Fact]
        public void ShouldRequireValidDimensionAndConfiguredTargets()
        {
            Assert.Throws<InputValidationException>(() => ContributionCalculator.Calculate(Dashboard(), new("country", 100m)));
            var dashboard = Dashboard();
            Assert.Throws<InputValidationException>(() => ContributionCalculator.Calculate(
                dashboard with { Allocation = dashboard.Allocation with { CategoryTargetsConfigured = false } }, new("category", 100m)));
        }
    }
}

