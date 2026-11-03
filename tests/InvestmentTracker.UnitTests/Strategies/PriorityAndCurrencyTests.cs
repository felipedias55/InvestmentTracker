using System.Text.Json;
using InvestmentTracker.Application.Allocation.Dtos;
using InvestmentTracker.Application.Allocation.Services;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Portfolios.Dtos;
using InvestmentTracker.Application.Strategies;

namespace InvestmentTracker.UnitTests.Strategies
{
    public sealed class PriorityAndCurrencyTests
    {
        private static DashboardDto Dashboard()
        {
            var positions = new[] { (1, 1, "BRL", 1m), (2, 1, "USD", 5m), (2, 2, "BRL", 1m) }.Select((p, i) =>
                new PositionDto(i + 1, i + 1, "TEST" + i, "Test", p.Item3, 1, 0, 0, 0, 0, p.Item4, new(2026, 10, 30), false, false)
                { AssetCategoryId = p.Item1, SectorId = p.Item2, AssetCategoryName = "C" + p.Item1, SectorName = "S" + p.Item2 }).ToArray();
            return new(new(new(1, "Test", null, 1, "BRL"), positions, [], 0, 0, true, false, false),
                new(true, true, [new(1, "C1", 0, 0, .5m, .5m), new(2, "C2", 0, 0, .5m, .5m)], [new(1, "S1", 0, 0, .5m, .5m), new(2, "S2", 0, 0, .5m, .5m)], []));
        }

        [Fact]
        public void ExplicitPrioritiesShouldServeTheHigherReservationFirstAndPreserveLegacyOverspendingError()
        {
            var config = new StrategyConfig("Test", "custom", "reserve", [], [new(1, "fixed", 100, 100), new(2, "fixed", 100, 1)]);
            Assert.Throws<InputValidationException>(() => StrategyCalculator.Calculate(Dashboard(), 120, config));
            var result = StrategyCalculator.Calculate(Dashboard(), 120, config with { UsePriorities = true });
            Assert.True(result.PrioritiesApplied); Assert.Equal(120, result.Plan!.Rows.Sum(r => r.Amount));
            Assert.Equal(100, result.RuleFunding.Single(r => r.CategoryId == 2).Budget);
            Assert.Equal(20, result.RuleFunding.Single(r => r.CategoryId == 1).Budget);
            Assert.Equal(2, result.Plan.Rows[0].CategoryId);
        }

        [Fact]
        public void EqualPrioritiesShouldSplitInsufficientBudgetProportionallyWithExactCents()
        {
            var result = PriorityBudgetCalculator.Allocate([(1, 100, 1m), (2, 100, 2m), (3, 200, 10m)], .05m);
            Assert.Equal(.02m, result[1]); Assert.Equal(.03m, result[2]); Assert.Equal(0, result[3]);
            Assert.Equal(.05m, result.Values.Sum());
            const decimal maximum = 999999999999999.99m;
            var large = PriorityBudgetCalculator.Allocate([(1, 1, maximum), (2, 1, maximum)], maximum);
            Assert.Equal(maximum, large.Values.Sum()); Assert.Equal(.01m, large[1] - large[2]);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void PrioritiesMustBreakScarceSectorCapacityTiesInsteadOfFollowingCategoryIds(bool rulePriority)
        {
            var d = Dashboard();
            var rows = new[] {
                new ContributionPlanRowDto("low", "C1", "S1", 50, []) { CategoryId = 1, SectorId = 1, Stage = "reservation" },
                new ContributionPlanRowDto("high", "C2", "S1", 50, []) { CategoryId = 2, SectorId = 1, Stage = "reservation",
                    RulePriority = rulePriority ? 1 : 100, GroupPriority = rulePriority ? 100 : 1 },
                new ContributionPlanRowDto("other", "C2", "S2", 20, []) { CategoryId = 2, SectorId = 2 }
            };
            var result = ConcentrationCalculator.Apply(d, new(rows, 0), [new("sector", 1, 50)], d.Summary.Positions, true);
            Assert.DoesNotContain(result.Plan.Rows, r => r.Key == "low");
            Assert.Equal(20, result.Plan.Rows.Single(r => r.Key == "high").Amount);
            Assert.Equal(20, result.Plan.Rows.Single(r => r.Key == "other").Amount);
            Assert.Equal(80, result.Plan.Unallocated); Assert.False(result.Statuses.Single().AboveLimit);
        }

        [Fact]
        public void ReservationStageMustPrecedeRemainderEvenWhenRemainderHasASmallerPriorityNumber()
        {
            var d = Dashboard();
            var rows = new[] {
                new ContributionPlanRowDto("rest", "C1", "S1", 50, []) { CategoryId = 1, SectorId = 1, GroupPriority = 1 },
                new ContributionPlanRowDto("reserve", "C2", "S1", 50, []) { CategoryId = 2, SectorId = 1, Stage = "reservation" },
                new ContributionPlanRowDto("dilution", "C2", "S2", 20, []) { CategoryId = 2, SectorId = 2 }
            };
            var result = ConcentrationCalculator.Apply(d, new(rows, 0), [new("sector", 1, 50)], d.Summary.Positions, true);
            Assert.Equal(20, result.Plan.Rows.Single(r => r.Key == "reserve").Amount);
            Assert.DoesNotContain(result.Plan.Rows, r => r.Key == "rest");
        }

        [Fact]
        public void StrategyShouldIdentifyDollarAndRealBySectorAndNeverDoubleCountCurrencyAlternatives()
        {
            var d = Dashboard(); var config = new StrategyConfig("Moedas", "regular", "equal", [], [], UsePriorities: true,
                GroupPreferences: [new(2, 1, 1, "USD")]);
            var result = StrategyCalculator.Calculate(d, 90, config);
            Assert.Equal(90, result.Plan!.Rows.Sum(r => r.Amount));
            var dollar = result.Plan.Rows.Single(r => r.CategoryId == 2 && r.SectorId == 1);
            Assert.Equal("USD", dollar.SuggestedCurrency); Assert.Equal(6, dollar.Currencies.Single().Amount);
            Assert.Equal(["TEST1"], dollar.Currencies.Single().Tickers);
            Assert.Equal("BRL", result.Plan.Rows.Single(r => r.CategoryId == 1).SuggestedCurrency);
            Assert.Equal(0, d.Summary.CurrentValue);
        }

        [Fact]
        public void CurrencyChoiceMustRequireAnEligiblePositiveEquivalentAndHonorUnavailablePreferences()
        {
            var row = new ContributionPlanRowDto("test", "C1", "S1", 100,
                [new("BRL", 100, 1, "", false), new("USD", 20, 5, "", false)]) { CategoryId = 1, SectorId = 1 };
            var both = ContributionSuggestionCalculator.Enrich(new([row], 0), []).Rows.Single();
            Assert.Null(both.SuggestedCurrency); Assert.Contains("mais de uma", both.SuggestionReason);
            var chosen = ContributionSuggestionCalculator.Enrich(new([row], 0), [new(1, 1, PreferredCurrency: "USD")]).Rows.Single();
            Assert.Equal("USD", chosen.SuggestedCurrency); Assert.Equal(100, chosen.Amount);
            var unavailable = ContributionSuggestionCalculator.Enrich(new([row with { Currencies = [row.Currencies[0]] }], 0), [new(1, 1, PreferredCurrency: "USD")]).Rows.Single();
            Assert.Null(unavailable.SuggestedCurrency); Assert.Contains("indisponível", unavailable.SuggestionReason);
            var roundedZero = ContributionSuggestionCalculator.Enrich(new([row with { Amount = .01m, Currencies = [new("USD", 0, 5, "", false)] }], 0), []).Rows.Single();
            Assert.Null(roundedZero.SuggestedCurrency);
        }

        [Fact]
        public void LegacyJsonMustKeepPrioritiesDisabledAndTheDefaultPriority()
        {
            var config = JsonSerializer.Deserialize<StrategyConfig>("""{"Name":"Old","Model":"custom","Remainder":"equal","ExcludedCategoryIds":[],"Rules":[{"CategoryId":1,"Kind":"fixed","Value":"10"}],"Tolerance":"0"}""")!;
            Assert.False(config.UsePriorities); Assert.Null(config.GroupPreferences); Assert.Equal(100, config.Rules.Single().Priority);
            Assert.Equal(100, StrategyCalculator.Calculate(Dashboard(), 100, config).Plan!.Rows.Sum(r => r.Amount));
        }

        [Theory]
        [InlineData(0, "USD")]
        [InlineData(1001, "BRL")]
        [InlineData(1, "EUR")]
        public void InvalidPreferencesShouldFailInsteadOfSilentlyChangingTheChoice(int priority, string currency)
        {
            var config = new StrategyConfig("Test", "custom", "equal", [], [], GroupPreferences: [new(1, 1, priority, currency)]);
            Assert.Throws<InputValidationException>(() => StrategyCalculator.Validate(config));
        }
    }
}
