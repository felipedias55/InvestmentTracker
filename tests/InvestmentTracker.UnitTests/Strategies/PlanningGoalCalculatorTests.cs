using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Income;
using InvestmentTracker.Application.Planning;

namespace InvestmentTracker.UnitTests.Strategies
{
    public class PlanningGoalCalculatorTests
    {
        private static readonly DateOnly Today = new(2026, 11, 3);
        private static SavedPlanningGoal Goal(string kind = "wealth", decimal target = 1000, DateOnly? deadline = null)
            => new(Guid.NewGuid(), 1, DateTime.UtcNow, new("Objetivo", kind, "BRL", target, deadline ?? new DateOnly(2027, 1, 31), 0));
        [Fact]
        public void WealthScenarioUsesInclusiveMonthlyInstallmentsAndRoundsRequiredAmountUp()
        {
            var goal = Goal() with { Config = Goal().Config with { MonthlyContribution = 250 } };
            var result = PlanningGoalCalculator.Calculate(goal, "BRL", Today, 100);
            Assert.Equal(3, result.MonthsRemaining); Assert.Equal(300, result.RequiredMonthlyContribution);
            Assert.Equal(850, result.ScenarioValue); Assert.Equal(150, result.ScenarioGap); Assert.Equal(10, result.Percentage);
            var exact = PlanningGoalCalculator.Calculate(Goal(target: 100), "BRL", Today, 0);
            Assert.Equal(33.34m, exact.RequiredMonthlyContribution); Assert.Equal("in-progress", exact.Status);
        }
        [Fact]
        public void DeadlineTodayHasOneInstallmentAndPastDeadlineDoesNotProjectIntoThePast()
        {
            var today = PlanningGoalCalculator.Calculate(Goal(deadline: Today), "BRL", Today, 900);
            Assert.Equal(1, today.MonthsRemaining); Assert.Equal(100, today.RequiredMonthlyContribution);
            var late = PlanningGoalCalculator.Calculate(Goal(deadline: Today.AddDays(-1)), "BRL", Today, 900);
            Assert.Equal("overdue", late.Status); Assert.Equal(0, late.MonthsRemaining); Assert.Null(late.RequiredMonthlyContribution); Assert.Null(late.ScenarioValue);
            Assert.Equal("achieved", PlanningGoalCalculator.Calculate(Goal(deadline: Today.AddDays(-1)), "BRL", Today, 1000).Status);
        }
        [Fact]
        public void MissingSourceCurrencyChangeAndArchiveNeverProduceFabricatedProgress()
        {
            Assert.Equal("unavailable", PlanningGoalCalculator.Calculate(Goal(), "BRL", Today, null).Status);
            var changed = PlanningGoalCalculator.Calculate(Goal(), "USD", Today, 100000);
            Assert.Equal("currency-changed", changed.Status); Assert.Null(changed.Current); Assert.Null(changed.RequiredMonthlyContribution);
            var goal = Goal();
            Assert.Equal("archived", PlanningGoalCalculator.Calculate(goal with { Config = goal.Config with { Archived = true } }, "USD", Today, 100000).Status);
        }
        [Fact]
        public void IncomeHasNoCapitalOrContributionForecastAndAllowsNegativeNetReference()
        {
            var result = PlanningGoalCalculator.Calculate(Goal("monthly-income", 500), "BRL", Today, -10);
            Assert.Equal(-10, result.Current); Assert.Equal(510, result.Gap); Assert.Equal(0, result.Percentage);
            Assert.Null(result.RequiredMonthlyContribution); Assert.Null(result.ScenarioValue);
        }
        [Fact]
        public void ProgressCapsAt100WithoutOverflowOrInventingExtraRequiredContribution()
        {
            var result = PlanningGoalCalculator.Calculate(Goal(target: .01m), "BRL", Today, decimal.MaxValue);
            Assert.Equal("achieved", result.Status); Assert.Equal(100, result.Percentage); Assert.Equal(0, result.RequiredMonthlyContribution);
        }
        [Fact]
        public void IncomeUsesExactlyTwelveClosedMonthsWithHistoricalEquivalentsAndReversals()
        {
            var foreign = new IncomeEvent(new(2026, 10, 31), "USD", 1, "USD", 100, false, false)
                { Conversions = [new(1, 1, "BRL", 400, 4, new(2026, 10, 31), "manual", "Old", DateTime.UtcNow), new(2, 2, "BRL", 600, 6, new(2026, 10, 31), "manual", "Corrected", DateTime.UtcNow)] };
            var events = new IncomeEvent[] { new(new(2025, 11, 1), "BR", 2, "BRL", 120, false, false), foreign,
                new(new(2026, 10, 31), "BR", 2, "BRL", 120, false, true),
                new(new(2025, 10, 31), "IGNORED", 3, "EUR", 999, false, false),
                new(new(2026, 11, 1), "IGNORED", 3, "EUR", 999, false, false) };
            var result = PlanningGoalCalculator.IncomeAverage(events, "BRL", Today);
            Assert.Equal(50, result.Average); Assert.Equal(0, result.Missing);
            Assert.Equal(0, PlanningGoalCalculator.IncomeAverage([], "BRL", Today).Average);
            Assert.Equal((new DateOnly(2027, 3, 1), new DateOnly(2028, 2, 29)), PlanningGoalCalculator.IncomeWindow(new(2028, 3, 15)));
        }
        [Fact]
        public void MissingHistoricalConversionBlocksAverageInsteadOfReportingPartialIncome()
        {
            var result = PlanningGoalCalculator.IncomeAverage([new(new(2026, 10, 1), "BR", 1, "BRL", 1200, false, false), new(new(2026, 10, 1), "US", 2, "USD", 100, false, false)], "BRL", Today);
            Assert.Null(result.Average); Assert.Equal(1, result.Missing);
        }
        [Fact]
        public void RejectsInvalidGoalValuesAndAmbiguousIncomeProjection()
        {
            var goal = Goal().Config;
            foreach (var config in new[] { goal with { Name = " " }, goal with { Kind = "yield" }, goal with { Target = 0 }, goal with { Target = 1.001m },
                goal with { Target = 1000000000000000m }, goal with { Currency = "brl" }, goal with { MonthlyContribution = -1 }, goal with { MonthlyContribution = .001m },
                goal with { Kind = "monthly-income", MonthlyContribution = 1 }, goal with { Deadline = new(2101, 1, 1) } })
                Assert.Throws<InputValidationException>(() => PlanningGoalCalculator.Validate(new(Guid.NewGuid(), 0, config)));
            PlanningGoalCalculator.Validate(new(Guid.NewGuid(), 0, goal with { Target = 999999999999999.99m }));
        }
    }
}
