using InvestmentTracker.Application.Allocation.Dtos;
using InvestmentTracker.Application.Allocation.Services;
using InvestmentTracker.Application.Portfolios.Dtos;

namespace InvestmentTracker.UnitTests.Allocation
{
    public class ContributionPlanCalculatorTests
    {
        private static ContributionAnalysisDto Analysis(string dimension, params decimal[] values)
            => new("BRL", dimension, values.Sum(), 1, 0, false, false,
                values.Select((v, i) => new ContributionRowDto(i + 1, $"Group{i}", 0, .5m, .5m, .5m, v)).ToList());
        private static PositionDto Position(int category, int sector, decimal quantity = 1, decimal? rate = 5,
            string name = "Ações", string sectorName = "Tecnologia")
            => new(1, 1, "TEST", "Test", "USD", quantity, 0, 0, 0, 0, rate, new(2026, 10, 7), false, false)
            { AssetCategoryId = category, SectorId = sector, AssetCategoryName = name, SectorName = sectorName };

        [Fact]
        public void ResidualPathsShouldReassignEarlierChoicesAndPreserveEveryCent()
        {
            var plan = ContributionPlanCalculator.Build(Analysis("category", 50, 50.01m), Analysis("sector", 50.01m, 50),
                [Position(1, 1), Position(1, 2), Position(2, 1)]);
            Assert.Equal(0, plan.Unallocated);
            Assert.Equal(new[] { "1-2", "2-1" }, plan.Rows.Select(r => r.Key));
            Assert.Equal(100.01m, plan.Rows.Sum(r => r.Amount));
            Assert.Equal(10, plan.Rows[0].Currencies[0].Amount);
        }

        [Fact]
        public void ClosedPositionsAndIncompatibleBudgetsShouldLeaveRemainder()
        {
            var plan = ContributionPlanCalculator.Build(Analysis("category", 50, 50.01m), Analysis("sector", 50.01m, 50),
                [Position(1, 1), Position(2, 2, 0)]);
            Assert.Single(plan.Rows);
            Assert.Equal(50.01m, plan.Unallocated);
        }

        [Theory]
        [InlineData("REITs", "Tecnologia", 100)]
        [InlineData("FII", "Consumo", 100)]
        [InlineData("FIIs", "Imobiliário", 0)]
        [InlineData("REIT", "Real Estate", 0)]
        public void RealEstateRestrictionShouldMatchExistingBehavior(string category, string sector, int remaining)
        {
            var result = ContributionPlanCalculator.Build(Analysis("category", 100), Analysis("sector", 100),
                [Position(1, 1, name: category, sectorName: sector)]);
            Assert.Equal(remaining, result.Unallocated);
        }

        [Fact]
        public void CurrencyConversionShouldRespectDirectionRoundingAndUnavailableRates()
        {
            Assert.Equal(20, ContributionPlanCalculator.CurrencyOptions(100.01m, [Position(1, 1)], "BRL")[0].Amount);
            Assert.Equal(5, ContributionPlanCalculator.CurrencyOptions(1, [Position(1, 1, rate: .2m)], "BRL")[0].Amount);
            Assert.Equal(.01m, ContributionPlanCalculator.CurrencyOptions(.01m, [Position(1, 1, rate: 2)], "BRL")[0].Amount);
            Assert.Null(ContributionPlanCalculator.CurrencyOptions(100, [Position(1, 1), Position(1, 1, rate: 6)], "BRL")[0].Amount);
            Assert.Null(ContributionPlanCalculator.CurrencyOptions(100, [Position(1, 1, rate: null)], "BRL")[0].Amount);
            Assert.Null(ContributionPlanCalculator.CurrencyOptions(100, [Position(1, 1, rate: 0)], "BRL")[0].Amount);
            const decimal maximum = 999999999999999.99m;
            Assert.Equal(maximum, ContributionPlanCalculator.CurrencyOptions(maximum, [Position(1, 1)], "USD")[0].Amount);
        }

        [Fact]
        public void SingleDimensionShouldRemainAvailableWithoutJointPlan()
        {
            var dashboard = new DashboardDto(new(new(1, "Test", null, 1, "BRL"), [], [], 0, 0, true, false, false),
                new(true, false, [new(1, "A", 0, 0, 1, 1)], [], []));
            var result = ContributionPlanCalculator.Calculate(dashboard, 100);
            Assert.NotNull(result.Category);
            Assert.Null(result.Sector);
            Assert.Null(result.Plan);
        }

        [Fact]
        public void MaximumContributionAndTieOrderShouldRemainExact()
        {
            const decimal amount = 999999999999999.99m;
            var result = ContributionPlanCalculator.Build(Analysis("category", amount), Analysis("sector", amount), [Position(1, 1)]);
            Assert.Equal(amount, Assert.Single(result.Rows).Amount);
            Assert.Equal(0, result.Unallocated);
        }
    }
}
