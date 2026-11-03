using System.Text.Json;
using InvestmentTracker.Application.Allocation.Dtos;
using InvestmentTracker.Application.Allocation.Tracking;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Portfolios.Dtos;
using InvestmentTracker.Application.Strategies;

namespace InvestmentTracker.UnitTests.Strategies
{
    public class RebalancingBandTests
    {
        private static DashboardDto Dashboard(decimal first = 40m)
        {
            var positions = new[] { first, 100m - first }.Select((v, i) => new PositionDto(i + 1, i + 1, "TEST" + i, "Asset", "BRL", 1, v, v, v, v, 1, null, false, false)
                { AssetCategoryId = i + 1, SectorId = i + 1, AssetCategoryName = "Category" + i, SectorName = "Sector" + i }).ToArray();
            var rows = positions.Select(p => new AllocationRowDto(p.AssetCategoryId, p.AssetCategoryName, p.BaseCurrentValue, p.BaseCurrentValue / 100, .5m, 0)).ToArray();
            return new(new(new(1, "Test", null, 1, "BRL"), positions, [], 100, 100, true, false, false), new(true, true, rows, rows, []));
        }
        private static StrategyConfig Config(params RebalancingBand[] bands) => new("Faixas", "bands", "targets", [], [], .2m, Bands: bands);
        [Fact]
        public void SpecificBandsOverrideGeneralAndCategoryAndSectorRemainIndependent()
        {
            var blockedBySector = StrategyCalculator.Calculate(Dashboard(), 100, Config(new RebalancingBand("category", 1, .03m)));
            Assert.Equal(100, blockedBySector.Plan!.Unallocated);
            Assert.Equal("below", blockedBySector.Bands.Single(b => b.Dimension == "category" && b.GroupId == 1).State);
            Assert.Equal("within", blockedBySector.Bands.Single(b => b.Dimension == "sector" && b.GroupId == 1).State);
            Assert.Contains("outra dimensão", blockedBySector.Bands.Single(b => b.Dimension == "category" && b.GroupId == 1).Reason);
            var plan = StrategyCalculator.Calculate(Dashboard(), 100, Config(new("category", 1, .03m), new("sector", 1, .05m)));
            Assert.Equal(100, Assert.Single(plan.Plan!.Rows).Amount); Assert.Equal(0, plan.Plan.Unallocated);
            var specific = plan.Bands.Single(b => b.Dimension == "category" && b.GroupId == 1);
            Assert.True(specific.HasOverride); Assert.Equal(3, specific.TolerancePoints); Assert.Equal(47, specific.LowerPercentage); Assert.Equal(53, specific.UpperPercentage);
            Assert.False(plan.Bands.Single(b => b.Dimension == "category" && b.GroupId == 2).HasOverride);
        }
        [Theory]
        [InlineData(47, "within")]
        [InlineData(53, "within")]
        [InlineData(46.9999, "below")]
        [InlineData(53.0001, "above")]
        public void BandEdgesAreInclusiveAndDecisionsUseUnroundedValues(double value, string state)
        {
            var result = StrategyCalculator.Calculate(Dashboard((decimal)value), 100, Config(new("category", 1, .03m), new("sector", 1, .03m)));
            Assert.Equal(state, result.Bands.Single(b => b.Dimension == "category" && b.GroupId == 1).State);
        }
        [Fact]
        public void ExplicitZeroIsNotReplacedByGeneralAndChangingModelDoesNotApplyStoredBands()
        {
            var c = Config(new("category", 1, 0), new("sector", 1, 0));
            Assert.Equal(100, Assert.Single(StrategyCalculator.Calculate(Dashboard(49), 100, c).Plan!.Rows).Amount);
            var other = StrategyCalculator.Calculate(Dashboard(49), 100, c with { Model = "custom" });
            Assert.Empty(other.Bands); Assert.Equal(100, Assert.Single(other.Plan!.Rows).Amount);
            var equal = StrategyCalculator.Calculate(Dashboard(), 100, c with { Remainder = "equal" });
            Assert.Empty(equal.Bands); Assert.Equal(2, equal.Plan!.Rows.Count); Assert.Contains(equal.StrategyNotes, n => n.Contains("não foram aplicadas"));
        }
        [Fact]
        public void WeightsUseEntireDeficitNotOnlyExcessBeyondTolerance()
        {
            var d = Dashboard(20); var third = d.Summary.Positions[1] with { Id = 3, AssetId = 3, AssetCategoryId = 3, SectorId = 3, CurrentValue = 60, BaseCurrentValue = 60 };
            var positions = new[] { d.Summary.Positions[0], d.Summary.Positions[1] with { CurrentValue = 20, BaseCurrentValue = 20 }, third };
            var rows = positions.Select(p => new AllocationRowDto(p.AssetCategoryId, p.AssetCategoryName, p.BaseCurrentValue, p.BaseCurrentValue / 100m, p.Id == 1 ? .5m : p.Id == 2 ? .3m : .2m, 0)).ToArray();
            d = d with { Summary = d.Summary with { Positions = positions }, Allocation = d.Allocation with { Categories = rows, Sectors = rows } };
            var plan = StrategyCalculator.Calculate(d, 100, Config(new("category", 1, .2m), new("sector", 1, .2m), new("category", 2, .05m), new("sector", 2, .05m))).Plan!;
            Assert.Equal(75, plan.Rows.Single(r => r.CategoryId == 1).Amount); Assert.Equal(25, plan.Rows.Single(r => r.CategoryId == 2).Amount);
        }
        [Fact]
        public void ReservationsAreIndependentAndExclusionsRebaseTargetsWithoutMutatingPortfolio()
        {
            var original = Dashboard(); var config = Config() with { Rules = [new(1, "fixed", 40)], ExcludedCategoryIds = [1] };
            var result = StrategyCalculator.Calculate(original, 100, config);
            var excluded = result.Bands.Single(b => b.Dimension == "category" && b.GroupId == 1);
            Assert.Equal("excluded", excluded.State); Assert.Null(excluded.CurrentPercentage); Assert.Equal(40, excluded.ReservedContribution);
            Assert.Equal("no-eligible", result.Bands.Single(b => b.Dimension == "sector" && b.GroupId == 1).State);
            Assert.Equal(100, result.Bands.Single(b => b.Dimension == "category" && b.GroupId == 2).TargetPercentage);
            Assert.Equal(60, result.Plan!.Unallocated); Assert.Equal(100, original.Summary.CurrentValue); Assert.Same(original, result.Dashboard);
        }
        [Fact]
        public void ConcentrationAndPrioritiesStillLimitReservationsAndBandTriggeredRemaining()
        {
            var result = StrategyCalculator.Calculate(Dashboard(), 100, Config(new("category", 1, 0), new("sector", 1, 0)) with {
                Rules = [new(1, "fixed", 30, 1)], UsePriorities = true, ConcentrationLimits = [new("category", 1, 45)] });
            var band = result.Bands.Single(b => b.Dimension == "category" && b.GroupId == 1);
            Assert.Equal(70, band.RequestedRemainder); Assert.Equal(0, band.SuggestedRemainder); Assert.Equal(9.09m, band.ReservedContribution);
            Assert.Contains("limites", band.Reason); Assert.Equal(100, result.Plan!.Rows.Sum(r => r.Amount) + result.Plan.Unallocated);
        }
        [Fact]
        public void NoBudgetAndNoEligibleOrInvalidTargetsHaveExplicitDiagnostics()
        {
            var noBudget = StrategyCalculator.Calculate(Dashboard(), 100, Config(new("category", 1, 0), new("sector", 1, 0)) with { Rules = [new(2, "fixed", 100)] });
            Assert.Contains("orçamento restante", noBudget.Bands.Single(b => b.Dimension == "category" && b.GroupId == 1).Reason);
            var blocked = StrategyCalculator.Calculate(Dashboard(), 100, Config() with { ExcludedCombinations = [new(1, 1), new(2, 2)] });
            Assert.All(blocked.Bands, b => Assert.Equal("no-eligible", b.State)); Assert.Equal(100, blocked.Plan!.Unallocated);
            var d = Dashboard(); var missing = StrategyCalculator.Calculate(d with { Allocation = d.Allocation with { CategoryTargetsConfigured = false } }, 0, Config());
            Assert.All(missing.Bands.Where(b => b.Dimension == "category"), b => Assert.Equal("no-target", b.State));
        }
        [Fact]
        public void InvalidBandsAndUnknownReferencesAreRejectedAndLegacyJsonUsesGeneralTolerance()
        {
            foreach (var invalid in new[] { new RebalancingBand("country", 1, 0), new("category", 0, 0), new("category", 1, -1), new("category", 1, 1.0001m), new("category", 1, .00001m) })
                Assert.Throws<InputValidationException>(() => StrategyCalculator.Validate(Config(invalid)));
            Assert.Throws<InputValidationException>(() => StrategyCalculator.Validate(Config(new("category", 1, 0), new("category", 1, .01m))));
            Assert.Throws<InputValidationException>(() => StrategyCalculator.Validate(Config(Enumerable.Range(1, 201).Select(i => new RebalancingBand("category", i, 0)).ToArray())));
            Assert.Throws<InputValidationException>(() => StrategyCalculator.Calculate(Dashboard(), 100, Config(new RebalancingBand("category", 999, 0))));
            var legacy = JsonSerializer.Deserialize<StrategyConfig>("""{"Name":"Antiga","Model":"bands","Remainder":"targets","ExcludedCategoryIds":[],"Rules":[],"Tolerance":"0.2"}""")!;
            Assert.Null(legacy.Bands); Assert.Equal(100, StrategyCalculator.Calculate(Dashboard(), 100, legacy).Plan!.Unallocated);
            var snapshot = JsonSerializer.Deserialize<ContributionPlanSnapshot>("""{"Currency":"BRL","Simulation":{"Amount":"0"},"Plan":{"Rows":[],"Unallocated":"0"},"Notes":[]}""")!;
            Assert.Empty(snapshot.Bands);
        }
    }
}
