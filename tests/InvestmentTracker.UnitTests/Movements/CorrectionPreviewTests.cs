using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Movements;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.UnitTests.Movements
{
    public class CorrectionPreviewTests
    {
        private static readonly DateOnly Day = new(2026, 9, 1);
        private static FinancialMovement Move(int id, int day, int? position = null, int? cash = null, string kind = "buy") => new()
        {
            Id = id, Date = Day.AddDays(day), Kind = kind,
            Effects = position.HasValue || cash.HasValue ? [new MovementEffect { PositionId = position, CashAssetId = cash }] : []
        };

        [Fact]
        public void Preview_ShouldFollowIndirectDependenciesRegardlessOfInputOrderAndSeparateIdentifierSpaces()
        {
            var source = Move(1, 0, position: 10);
            var bridge = Move(2, 1, position: 10, cash: 20);
            var indirect = Move(3, 2, position: 30, cash: 20);
            var unrelated = Move(4, 3, cash: 10);
            var result = CorrectionPreviewService.Calculate(new(Day, MovementId: 1), [indirect, unrelated, bridge, source], []);
            Assert.Equal([1, 2, 3], result.Movements.Select(x => x.Id));
            Assert.Equal("indirect", result.Movements.Single(x => x.Id == 3).Dependency);
            Assert.Equal([3, 2, 1], result.ReviewOrder);
            Assert.Equal(3, indirect.Id); Assert.Empty(result.Snapshots);
        }

        [Fact]
        public void NewLateOperation_ShouldIncludeSameDayAndBothTransferBalancesButNotEarlierUnrelatedMovements()
        {
            var result = CorrectionPreviewService.Calculate(new(Day, CashAssetId: 1, DestinationId: 2),
                [Move(1, -1, cash: 1), Move(2, 0, cash: 1), Move(3, 0, cash: 2), Move(4, 1, cash: 9)], []);
            Assert.Equal([2, 3], result.Movements.Select(x => x.Id));
            Assert.Null(result.MovementId);
        }

        [Fact]
        public void Preview_ShouldKeepReversalsForAuditAndWarnAboutUnknownAndAbsoluteBalances()
        {
            var source = Move(1, 0, cash: 1, kind: "adjustment");
            var reversal = Move(2, 1, cash: 1, kind: "reversal"); reversal.ReversalOfId = 1;
            var unknown = Move(3, 2, kind: "historical");
            var result = CorrectionPreviewService.Calculate(new(Day, MovementId: 1),
                [source, reversal, unknown, Move(4, 3, kind: "income-conversion"), Move(5, 4, kind: "reopen")], []);
            Assert.Equal(3, result.Movements.Count);
            Assert.True(result.Movements[0].IsReversed); Assert.Empty(result.ReviewOrder);
            Assert.Contains(result.Warnings, x => x.Contains("sem efeitos auditáveis"));
            Assert.Contains(result.Warnings, x => x.Contains("estornos na cadeia"));
            Assert.Contains(result.Warnings, x => x.Contains("ajustes absolutos"));
        }

        [Fact]
        public void Preview_ShouldUseEarlierDateAndPreserveSnapshotContentsAndFlags()
        {
            var photos = new[] {
                new PortfolioSnapshot { Id = 1, SnapshotDate = Day.AddDays(-1) },
                new PortfolioSnapshot { Id = 2, SnapshotDate = Day, Revision = 2, DashboardJson = "frozen", PreviousVersionsJson = "old" },
                new PortfolioSnapshot { Id = 3, SnapshotDate = Day.AddDays(2) },
                new PortfolioSnapshot { Id = 4, SnapshotDate = Day.AddDays(3), IsReopened = true }
            };
            var result = CorrectionPreviewService.Calculate(new(Day, MovementId: 1), [Move(1, 1, cash: 1)], photos);
            Assert.Equal(Day, result.FromDate); Assert.Equal(3, result.Snapshots.Count);
            Assert.False(result.Snapshots[0].RequiresReopening); Assert.True(result.Snapshots[1].RequiresReopening);
            Assert.False(result.Snapshots[2].RequiresReopening);
            Assert.Equal("frozen", photos[1].DashboardJson); Assert.Equal("old", photos[1].PreviousVersionsJson);
            Assert.False(photos[1].IsOutdated); Assert.False(photos[1].IsReopened);
            Assert.Equal(Day.AddDays(1), CorrectionPreviewService.Calculate(new(Day.AddDays(5), MovementId: 1), [Move(1, 1, cash: 1)], []).FromDate);
        }

        [Theory]
        [InlineData("income-conversion")]
        [InlineData("reopen")]
        public void Preview_ShouldRejectMetadataCorrections(string kind)
        {
            Assert.Throws<InputValidationException>(() => CorrectionPreviewService.Calculate(new(Day, MovementId: 1), [Move(1, 0, kind: kind)], []));
        }

        [Fact]
        public void LegacySourceWithoutEffects_ShouldConservativelyIncludeAllLaterFinancialMovements()
        {
            var result = CorrectionPreviewService.Calculate(new(Day, MovementId: 1), [Move(1, 0, kind: "historical"), Move(2, 1, cash: 9)], []);
            Assert.Equal(2, result.Movements.Count);
            Assert.Contains(result.Warnings, x => x.Contains("cadeia pode estar incompleta"));
        }
    }
}
