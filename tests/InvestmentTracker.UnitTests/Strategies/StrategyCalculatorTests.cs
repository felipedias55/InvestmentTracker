using InvestmentTracker.Application.Allocation.Dtos;
using InvestmentTracker.Application.Portfolios.Dtos;
using InvestmentTracker.Application.Strategies;
using InvestmentTracker.Application.Common.Exceptions;

namespace InvestmentTracker.UnitTests.Strategies
{
    public class StrategyCalculatorTests
    {
        private static DashboardDto Dashboard()
        {
            var positions = new[] { 500m, 100m, 300m }.Select((v,i) =>
                new PositionDto(i+1, i+1, "TEST"+i, "Test", "BRL", 1, v, v, v, v, 1, null, false, false)
                { AssetCategoryId = i+1, SectorId = i+1, AssetCategoryName = "Category"+(i+1), SectorName = "Sector"+(i+1) }).ToList();
            var rows = positions.Select(p => new AllocationRowDto(p.AssetCategoryId, p.AssetCategoryName, p.BaseCurrentValue, p.BaseCurrentValue/900m,
                p.AssetCategoryId == 1 ? .2m : .4m, 0)).ToList();
            return new(new(new(1,"Test",null,1,"BRL"), positions, [], 900,900,true,false,false),
                new(true,true,rows,rows,[]));
        }
        private static StrategyConfig Config(string model = "custom", string remainder = "equal") => new("Test",model,remainder,[],[]);
        [Fact]
        public void FixedRealEstateReservesAndExcludesFromRemainingTargetsWithoutChangingPortfolio()
        {
            var original = Dashboard();
            var config = new StrategyConfig("Imobiliário","real-estate","targets",[1],[new(1,"fixed",500)]);
            var result = StrategyCalculator.Calculate(original,2000,config);
            Assert.Equal(400,result.EligibleBase);
            Assert.Equal(2000,result.Plan!.Rows.Sum(r => r.Amount));
            Assert.Equal(500,result.Plan.Rows.Single(r => r.Category == "Category1").Amount);
            Assert.Equal(1500,result.Plan.Rows.Single(r => r.Category == "Category2").Amount);
            Assert.Equal(0,result.Plan.Unallocated);
            Assert.Equal(900,original.Summary.CurrentValue);
            Assert.Same(original,result.Dashboard);
        }
        [Fact]
        public void PercentageUsesWholeBudgetAndPreservesEveryCent()
        {
            var c = Config() with { Rules = [new(1,"percent",25),new(2,"fixed",10)], ExcludedCategoryIds = [1,2] };
            var plan = StrategyCalculator.Calculate(Dashboard(),100.01m,c).Plan!;
            Assert.Equal(100.01m,plan.Rows.Sum(r => r.Amount) + plan.Unallocated);
            Assert.Equal(25m,plan.Rows.Single(r => r.Category == "Category1").Amount);
            Assert.Equal(65.01m,plan.Rows.Single(r => r.Category == "Category3").Amount);
        }
        [Fact]
        public void EqualDistributionAndClosedEligibilityPreserveUnallocatedBudget()
        {
            var c = Config() with { ExcludedCategoryIds = [1,2,3] };
            Assert.Equal(100,StrategyCalculator.Calculate(Dashboard(),100,c).Plan!.Unallocated);
            var rows = StrategyCalculator.Calculate(Dashboard(),.02m,Config()).Plan!.Rows;
            Assert.Equal(.02m,rows.Sum(r => r.Amount));
            Assert.Equal(2,rows.Count);
        }
        [Fact]
        public void RejectsConflictingRulesOverspendingAndUnknownCategories()
        {
            Assert.Throws<InputValidationException>(() => StrategyCalculator.Calculate(Dashboard(),10,Config() with { Rules = [new(1,"fixed",11)] }));
            Assert.Throws<InputValidationException>(() => StrategyCalculator.Validate(Config() with { Rules = [new(1,"percent",60),new(2,"percent",50)] }));
            Assert.Throws<InputValidationException>(() => StrategyCalculator.Validate(Config() with { Rules = [new(1,"fixed",1),new(1,"percent",1)] }));
            Assert.Throws<InputValidationException>(() => StrategyCalculator.Calculate(Dashboard(),10,Config() with { ExcludedCategoryIds = [999] }));
        }
        [Fact]
        public void BandsLeaveMoneyUnallocatedInsideTolerance()
        {
            var c = Config("bands","targets") with { Tolerance = 1 };
            Assert.Equal(100,StrategyCalculator.Calculate(Dashboard(),100,c).Plan!.Unallocated);
        }
        [Fact]
        public void ModelsNeverGenerateSalesAndMissingFxBlocksSimulation()
        {
            foreach (var model in StrategyCalculator.Models.Except(["real-estate","core-satellite"]))
                Assert.All(StrategyCalculator.Calculate(Dashboard(),100,Config(model)).Plan!.Rows, r => Assert.True(r.Amount >= 0));
            var d = Dashboard();
            Assert.Throws<ResourceConflictException>(() => StrategyCalculator.Calculate(d with { Summary = d.Summary with { ConversionAvailable = false } },100,Config()));
        }

        [Fact]
        public void BlockedCombinationShouldAffectReservesAndRemainingBaseWithoutChangingPortfolio()
        {
            var original = Dashboard();
            var config = Config() with { Rules = [new(1, "fixed", 50)], ExcludedCombinations = [new(1, 1)] };
            var result = StrategyCalculator.Calculate(original, 100.01m, config);
            Assert.Equal(400, result.EligibleBase);
            Assert.Equal(50, result.Plan!.Unallocated);
            Assert.Equal(50.01m, result.Plan.Rows.Sum(row => row.Amount));
            Assert.DoesNotContain(result.Plan.Rows, row => row.Category == "Category1");
            Assert.Equal(900, original.Summary.CurrentValue);
            Assert.Equal(3, original.Summary.Positions.Count);
            Assert.Same(original, result.Dashboard);
        }

        [Fact]
        public void AllBlockedOrUnclassifiedShouldLeaveMoneyUnallocatedEvenInTargetMode()
        {
            var original = Dashboard();
            var config = Config(remainder: "targets") with { ExcludedCombinations = [new(1, 1), new(2, 2), new(3, 3)] };
            var result = StrategyCalculator.Calculate(original, 100, config);
            Assert.Equal(100, result.Plan!.Unallocated); Assert.Empty(result.Plan.Rows); Assert.Equal(0, result.EligibleBase);
            var unclassified = original with { Summary = original.Summary with { Positions = original.Summary.Positions.Select(p => p with { InstrumentClass = "unclassified" }).ToList() } };
            Assert.Equal(100, StrategyCalculator.Calculate(unclassified, 100, Config(remainder: "targets")).Plan!.Unallocated);
        }

        [Fact]
        public void PairBlockShouldLeaveOtherSectorsOfTheSameCategoryAvailable()
        {
            var d = Dashboard();
            d = d with { Summary = d.Summary with { Positions = [..d.Summary.Positions, d.Summary.Positions[0] with { Id = 4, SectorId = 2, SectorName = "Sector2" }] } };
            var result = StrategyCalculator.Calculate(d, 100, Config() with { Rules = [new(1, "fixed", 40)], ExcludedCombinations = [new(1, 1)] });
            Assert.Equal(100, result.Plan!.Rows.Sum(row => row.Amount));
            Assert.Contains(result.Plan.Rows, row => row.Category == "Category1" && row.Sector == "Sector2" && row.Key.StartsWith("rule"));
            Assert.DoesNotContain(result.Plan.Rows, row => row.Key == "rule-1-1");
        }

        [Fact]
        public void InvalidOrDuplicatePairsShouldFailInsteadOfBeingSilentlyIgnored()
        {
            Assert.Throws<InputValidationException>(() => StrategyCalculator.Validate(Config() with { ExcludedCombinations = [new(1, 0)] }));
            Assert.Throws<InputValidationException>(() => StrategyCalculator.Validate(Config() with { ExcludedCombinations = [new(1, 1), new(1, 1)] }));
            Assert.Throws<InputValidationException>(() => StrategyCalculator.Calculate(Dashboard(), 100, Config() with { ExcludedCombinations = [new(1, 999)] }));
            Assert.Throws<InputValidationException>(() => StrategyCalculator.Validate(Config() with { ExcludedCombinations = Enumerable.Range(1, 501).Select(i => new StrategyCombination(1, i)).ToArray() }));
        }
    }
}
