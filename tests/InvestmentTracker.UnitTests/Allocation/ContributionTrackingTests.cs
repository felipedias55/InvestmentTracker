using System.Text.Json;
using InvestmentTracker.Application.Allocation.Dtos;
using InvestmentTracker.Application.Allocation.Tracking;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.UnitTests.Allocation
{
    public class ContributionTrackingTests
    {
        private static readonly DateOnly Day = new(2026, 11, 3);
        private static ContributionPlanRowDto Row(string key, decimal amount = 100m) => new(key, "Categoria", "Setor", amount,
            [new("BRL", amount, 1, "", false), new("USD", amount / 5m, 5, "", false)]) { CategoryId = 1, SectorId = 2 };
        private static ContributionPlanSnapshot Snapshot() => new("BRL", new(250), new([Row("a"), Row("b")], 50), []);
        private static TrackedPurchase Link(int id, string key, decimal amount, bool active = true, bool removed = false)
            => new(id, id, key, Day, "TEST", "BRL", amount, amount, "same-currency", DateTime.UtcNow, removed ? DateTime.UtcNow : null, active);
        private static TrackingPlanDetail Detail(params TrackedPurchase[] purchases) => ContributionTrackingCalculator.Detail(new(
            new SavedContributionPlan { Id = 1, Name = "Plano", From = Day, To = Day, SnapshotJson = JsonSerializer.Serialize(Snapshot()) }, purchases));

        [Fact]
        public void CompletionRequiresEveryRowAndDoesNotCountUnallocatedMoney()
        {
            var wrongGroup = Detail(Link(1, "a", 200));
            Assert.Equal(0m, wrongGroup.Difference); Assert.Equal("partial", wrongGroup.Status);
            Assert.Equal("exceeded", wrongGroup.Rows[0].Status); Assert.Equal("pending", wrongGroup.Rows[1].Status);
            var complete = Detail(Link(1, "a", 100), Link(2, "b", 100));
            Assert.Equal("completed", complete.Status); Assert.Equal(200m, complete.Realized); Assert.Equal(50m, complete.Snapshot.Plan.Unallocated);
            Assert.Equal("exceeded", Detail(Link(1, "a", 100), Link(2, "b", 101)).Status);
        }
        [Fact]
        public void ReversedAndRemovedPurchasesRemainVisibleButDoNotCount()
        {
            var detail = Detail(Link(1, "a", 100, false), Link(2, "b", 100, removed: true));
            Assert.Equal(0m, detail.Realized); Assert.Equal("pending", detail.Status); Assert.Equal(2, detail.Purchases.Count);
        }
        [Theory]
        [InlineData("BRL", null, null, 20, "same-currency")]
        [InlineData("USD", 110d, null, 110d, "historical")]
        [InlineData("USD", null, 109.1234, 109.1234, "manual")]
        public void UsesOriginalCostAndHistoricalOrExplicitEquivalent(string currency, double? historical, double? manual, double expected, string source)
        {
            var trade = new PurchaseCandidate(1, Day, "TEST", currency, 1, 2, 20, (decimal?)historical, true);
            var result = ContributionTrackingCalculator.ValidatePurchase(Snapshot(), Day, Day, trade, new(1, "a", (decimal?)manual));
            Assert.Equal((decimal)expected, result.Amount); Assert.Equal(source, result.Source);
        }
        [Fact]
        public void RejectsMissingOrConflictingEquivalentAndIncompatiblePurchases()
        {
            var trade = new PurchaseCandidate(1, Day, "TEST", "USD", 1, 2, 20, null, true);
            var invalid = new[] { trade, trade with { Active = false }, trade with { Date = Day.AddDays(-1) }, trade with { CategoryId = 9 }, trade with { SectorId = 9 }, trade with { Currency = "EUR" } };
            foreach (var t in invalid) Assert.Throws<InputValidationException>(() => ContributionTrackingCalculator.ValidatePurchase(Snapshot(), Day, Day, t, new(1, "a")));
            Assert.Throws<InputValidationException>(() => ContributionTrackingCalculator.ValidatePurchase(Snapshot(), Day, Day, trade with { BaseAmount = 110 }, new(1, "a", 109)));
            foreach (var amount in new[] { 0m, -1m, .00001m, 1000000000000000m })
                Assert.Throws<InputValidationException>(() => ContributionTrackingCalculator.ValidatePurchase(Snapshot(), Day, Day, trade, new(1, "a", amount)));
        }
        [Fact]
        public void HandlesEntirelyUnallocatedPlans()
        {
            var snapshot = Snapshot() with { Plan = new([], 250) };
            var result = ContributionTrackingCalculator.Detail(new(new SavedContributionPlan { SnapshotJson = JsonSerializer.Serialize(snapshot) }, []));
            Assert.Equal("unallocated", result.Status); Assert.Equal(0, result.Realized);
        }
    }
}
