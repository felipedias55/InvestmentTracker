using InvestmentTracker.Application.Allocation.Dtos;
using InvestmentTracker.Application.Allocation.Services;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Portfolios.Dtos;
using InvestmentTracker.Application.Strategies;

namespace InvestmentTracker.UnitTests.Strategies
{
    public sealed class ConcentrationCalculatorTests
    {
        private static PositionDto Position(int category, int sector, decimal value, string currency = "BRL", decimal rate = 1)
            => new(category * 10 + sector, category * 10 + sector, "TEST", "Synthetic", currency, 1, value / rate, value / rate, value, value, rate, null, false, false)
            { AssetCategoryId = category, SectorId = sector, AssetCategoryName = "C" + category, SectorName = "S" + sector };
        private static DashboardDto Dashboard(params PositionDto[] positions)
        {
            var total = positions.Sum(p => p.BaseCurrentValue!.Value);
            var categories = positions.GroupBy(p => p.AssetCategoryId).Select(g => new AllocationRowDto(g.Key, "C" + g.Key, g.Sum(p => p.BaseCurrentValue), 0, .5m, .5m)).ToArray();
            var sectors = positions.GroupBy(p => p.SectorId).Select(g => new AllocationRowDto(g.Key, "S" + g.Key, g.Sum(p => p.BaseCurrentValue), 0, .5m, .5m)).ToArray();
            return new(new(new(1, "Test", null, 1, "BRL"), positions, [], total, total, true, false, false), new(true, true, categories, sectors, []));
        }
        private static ContributionPlanRowDto Row(int category, int sector, decimal value)
            => new($"{category}-{sector}", "C" + category, "S" + sector, value, []) { CategoryId = category, SectorId = sector };
        private static ConcentrationResult Apply(DashboardDto d, IReadOnlyList<ContributionPlanRowDto> rows, params ConcentrationLimit[] limits)
            => ConcentrationCalculator.Apply(d, new(rows, 0), limits, d.Summary.Positions);

        [Fact]
        public void UnallocatedMoneyMustNotDiluteTheLimitAndCurrencyOptionsMustFollowTheFinalAmount()
        {
            var d = Dashboard(Position(1, 1, 50, "USD", 5), Position(2, 2, 50));
            var result = Apply(d, [Row(1, 1, 100)], new ConcentrationLimit("category", 1, 60));
            var row = Assert.Single(result.Plan.Rows);
            Assert.Equal(25, row.Amount); Assert.Equal(75, result.Plan.Unallocated);
            Assert.Equal(5, row.Currencies.Single().Amount);
            Assert.Equal(60, result.Statuses.Single().ProjectedPercentage);
            Assert.Equal(50, d.Summary.Positions[0].BaseCurrentValue);
        }

        [Fact]
        public void CategoryAndSectorCapsMustBothHoldWithoutIncreasingAnyOriginalSuggestion()
        {
            var d = Dashboard(Position(1, 1, 40), Position(1, 2, 10), Position(2, 1, 20), Position(2, 2, 30));
            var rows = new[] { Row(1, 1, 25), Row(1, 2, 25), Row(2, 1, 25), Row(2, 2, 25) };
            var limits = new[] { new ConcentrationLimit("category", 1, 40), new ConcentrationLimit("sector", 1, 50) };
            var result = Apply(d, rows, limits);
            AssertSafe(d, new(rows, 0), result, limits);
            Assert.True(result.Plan.Unallocated > 0);
            Assert.All(result.Plan.Rows, row => Assert.InRange(row.Amount, .01m, 25m));
        }

        [Fact]
        public void AlreadyOverweightGroupsMayRemainOverweightButMustNotReceiveAnUnsafeAddition()
        {
            var d = Dashboard(Position(1, 1, 60), Position(2, 2, 40));
            var result = Apply(d, [Row(1, 1, 100)], new ConcentrationLimit("sector", 1, 50));
            Assert.Empty(result.Plan.Rows); Assert.Equal(100, result.Plan.Unallocated);
            Assert.True(result.Statuses.Single().InitiallyAboveLimit); Assert.True(result.Statuses.Single().AboveLimit);
            Assert.Equal(0, result.Statuses.Single().SuggestedContribution);
            var diluted = Apply(d, [Row(1, 1, 10), Row(2, 2, 100)], new ConcentrationLimit("sector", 1, 50));
            Assert.False(diluted.Statuses.Single().AboveLimit); Assert.Equal(10, diluted.Statuses.Single().SuggestedContribution);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(100, 100)]
        public void ZeroAndHundredPercentHaveExplicitMeanings(decimal maximum, decimal expected)
        {
            var d = Dashboard(Position(1, 1, 50), Position(2, 2, 50));
            var result = Apply(d, [Row(1, 1, 100)], new ConcentrationLimit("category", 1, maximum));
            Assert.Equal(expected, result.Plan.Rows.Sum(r => r.Amount)); Assert.Equal(100 - expected, result.Plan.Unallocated);
        }

        [Fact]
        public void ZeroValuePortfolioCanReceiveBalancedCentsWithoutInventingAConcentrationForAnEmptyPlan()
        {
            var d = Dashboard(Position(1, 1, 0), Position(2, 2, 0));
            var limits = new[] { new ConcentrationLimit("category", 1, 50), new ConcentrationLimit("category", 2, 50) };
            var balanced = Apply(d, [Row(1, 1, .01m), Row(2, 2, .01m)], limits);
            Assert.Equal(.02m, balanced.Plan.Rows.Sum(r => r.Amount)); Assert.All(balanced.Statuses, s => Assert.Null(s.CurrentPercentage));
            var odd = Apply(d, [Row(1, 1, .01m)], limits);
            Assert.Empty(odd.Plan.Rows); Assert.Equal(.01m, odd.Plan.Unallocated); Assert.All(odd.Statuses, s => Assert.Null(s.ProjectedPercentage));
        }

        [Fact]
        public void ExtremeLimitMustTerminateConservativelyAndMaximumMoneyMustRemainExact()
        {
            const decimal maximum = 999999999999999.99m;
            var d = Dashboard(Position(1, 1, 0));
            var constrained = Apply(d, [Row(1, 1, maximum)], new ConcentrationLimit("category", 1, 99.99m));
            Assert.Empty(constrained.Plan.Rows); Assert.Equal(maximum, constrained.Plan.Unallocated); Assert.True(constrained.Conservative);
            var unrestricted = Apply(d, [Row(1, 1, maximum)], new ConcentrationLimit("category", 1, 100));
            Assert.Equal(maximum, unrestricted.Plan.Rows.Single().Amount);
        }

        [Fact]
        public void SubCentConvertedHoldingsMustNotBeRoundedAwayToCreateAnExtraCentOfHeadroom()
        {
            var d = Dashboard(Position(1, 1, .00000000000000000001m), Position(2, 2, .99999999999999999999m));
            var result = Apply(d, [Row(1, 1, 1)], new ConcentrationLimit("category", 1, 50));
            Assert.Equal(.99m, result.Plan.Rows.Single().Amount); Assert.Equal(.01m, result.Plan.Unallocated);
        }

        [Fact]
        public void ConservativeFallbackMustKeepUnrestrictedSuggestionsAndOnlySafeAdditionalHeadroom()
        {
            var d = Dashboard(Position(1, 1, 99), Position(2, 2, 1));
            var rows = new[] { Row(1, 1, 100), Row(2, 2, .01m) };
            var limits = new[] { new ConcentrationLimit("category", 1, 99) };
            var result = Apply(d, rows, limits);
            Assert.True(result.Conservative); Assert.Equal(.99m, result.Plan.Rows.Single(r => r.CategoryId == 1).Amount);
            Assert.Equal(.01m, result.Plan.Rows.Single(r => r.CategoryId == 2).Amount);
            AssertSafe(d, new(rows, 0), result, limits);
        }

        [Fact]
        public void FullPortfolioIncludingExcludedPositionsMustBeTheReferenceAndCapsMustPrecedeReservations()
        {
            var d = Dashboard(Position(1, 1, 50), Position(2, 2, 50));
            var c = new StrategyConfig("Cap", "custom", "reserve", [1], [new(1, "fixed", 100)], ConcentrationLimits: [new("category", 1, 60)]);
            var result = StrategyCalculator.Calculate(d, 100, c);
            Assert.Equal(50, result.EligibleBase); Assert.Equal(25, result.Plan!.Rows.Single().Amount); Assert.Equal(75, result.Plan.Unallocated);
            Assert.Null(result.Category); Assert.Null(result.Sector); Assert.Same(d, result.Dashboard);
        }

        [Fact]
        public void TargetBasedRemainderMustRespectSectorCapAndNotReturnUncappedAnalyses()
        {
            var d = Dashboard(Position(1, 1, 80), Position(2, 2, 20)) with { TotalWealth = 10000 };
            var config = new StrategyConfig("Metas", "targets", "targets", [], [], ConcentrationLimits: [new("sector", 2, 25)]);
            var result = StrategyCalculator.Calculate(d, 100, config);
            var row = result.Plan!.Rows.Single();
            Assert.Equal(2, row.CategoryId); Assert.Equal(2, row.SectorId); Assert.Equal(6.66m, row.Amount);
            Assert.Equal(93.34m, result.Plan.Unallocated);
            Assert.Null(result.Category); Assert.Null(result.Sector); Assert.Equal(10000, result.Dashboard.TotalWealth);
        }

        [Theory]
        [InlineData("country", 1, 50)]
        [InlineData("category", 0, 50)]
        [InlineData("sector", 1, -1)]
        [InlineData("sector", 1, 100.01)]
        [InlineData("sector", 1, 30.001)]
        public void InvalidLimitsMustFailValidation(string dimension, int id, decimal maximum)
        {
            var config = new StrategyConfig("Cap", "custom", "equal", [], [], ConcentrationLimits: [new(dimension, id, maximum)]);
            Assert.Throws<InputValidationException>(() => StrategyCalculator.Validate(config));
        }

        [Fact]
        public void DuplicateExcessiveAndUnknownLimitsMustNotBeSilentlyAccepted()
        {
            var config = new StrategyConfig("Cap", "custom", "equal", [], []);
            Assert.Throws<InputValidationException>(() => StrategyCalculator.Validate(config with { ConcentrationLimits = [new("category", 1, 30), new("category", 1, 40)] }));
            Assert.Throws<InputValidationException>(() => StrategyCalculator.Validate(config with { ConcentrationLimits = Enumerable.Range(1, 201).Select(i => new ConcentrationLimit("category", i, 50)).ToArray() }));
            Assert.Throws<InputValidationException>(() => StrategyCalculator.Calculate(Dashboard(Position(1, 1, 100)), 100,
                config with { ConcentrationLimits = [new("sector", 999, 50)] }));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void VariedCrossedCapsMustAlwaysPreserveBudgetAndBeDeterministic(bool priorities)
        {
            var random = new Random(938);
            for (var i = 0; i < 120; i++)
            {
                var d = Dashboard(Position(1, 1, random.Next(10000) / 100m), Position(1, 2, random.Next(10000) / 100m),
                    Position(2, 1, random.Next(10000) / 100m), Position(2, 2, random.Next(10000) / 100m));
                var rows = new[] { Row(1, 1, random.Next(10000) / 100m), Row(1, 2, random.Next(10000) / 100m), Row(2, 1, random.Next(10000) / 100m), Row(2, 2, random.Next(10000) / 100m) };
                var limits = new[] { new ConcentrationLimit("category", 1, random.Next(10001) / 100m), new ConcentrationLimit("sector", 1, random.Next(10001) / 100m) };
                var requested = new ContributionJointPlanDto(rows, 17.23m);
                if (priorities) rows = rows.Select((r, index) => r with { GroupPriority = 4 - index }).ToArray();
                requested = requested with { Rows = rows };
                var result = ConcentrationCalculator.Apply(d, requested, limits, d.Summary.Positions, priorities);
                AssertSafe(d, requested, result, limits);
                var repeated = ConcentrationCalculator.Apply(d, requested, limits, d.Summary.Positions, priorities);
                Assert.Equal(result.Plan.Rows.Select(r => (r.Key, r.Amount)), repeated.Plan.Rows.Select(r => (r.Key, r.Amount)));
            }
        }

        private static void AssertSafe(DashboardDto d, ContributionJointPlanDto original, ConcentrationResult result, IReadOnlyList<ConcentrationLimit> limits)
        {
            var allocated = result.Plan.Rows.Sum(r => r.Amount);
            Assert.Equal(original.Rows.Sum(r => r.Amount) + original.Unallocated, allocated + result.Plan.Unallocated);
            Assert.All(result.Plan.Rows, row => Assert.Equal(decimal.Round(row.Amount, 2), row.Amount));
            foreach (var limit in limits)
            {
                var added = result.Plan.Rows.Where(r => (limit.Dimension == "category" ? r.CategoryId : r.SectorId) == limit.GroupId).Sum(r => r.Amount);
                var value = d.Summary.Positions.Where(p => (limit.Dimension == "category" ? p.AssetCategoryId : p.SectorId) == limit.GroupId).Sum(p => p.BaseCurrentValue!.Value);
                if (added > 0) Assert.True((value + added) * 100 <= limit.MaximumPercentage * (d.Summary.CurrentValue!.Value + allocated));
            }
        }
    }
}
