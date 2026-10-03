using InvestmentTracker.Application.Movements;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.UnitTests.Movements
{
    public class CorrectionCalculatorTests
    {
        private static readonly DateOnly Day = new(2026, 9, 1);
        private static CorrectionState State()
        {
            var currency = new Currency { Id = 1, Code = "BRL" };
            return new(new Portfolio { Id = 1, BaseCurrencyId = 1, BaseCurrency = currency },
                [new PortfolioAsset { Id = 1, PortfolioId = 1, AssetId = 1, Quantity = 6, InvestedAmount = 60, CurrentValue = 120 }],
                [new ExternalAsset { Id = 1, PortfolioId = 1, CurrencyId = 1, Name = "Caixa", Value = 980 }],
                [new Asset { Id = 1, CurrencyId = 1, Ticker = "TEST" }, new Asset { Id = 2, CurrencyId = 1, Ticker = "NEW" }], [currency],
                [new FinancialMovement { Id = 1, Date = Day, Kind = "buy", Amount = 100, CurrencyCode = "BRL",
                    Trade = new PortfolioTrade { AssetId = 1, Quantity = 10, UnitPrice = 10, Amount = 100, CashAssetId = 1 },
                    Effects = [new MovementEffect { PositionId = 1, CurrencyId = 1, AfterQuantity = 10, AfterCost = 100, AfterValue = 100 },
                        new MovementEffect { CashAssetId = 1, CurrencyId = 1, BeforeValue = 1000, AfterValue = 900 }] },
                 new FinancialMovement { Id = 2, Date = Day.AddDays(1), Kind = "sell", Amount = 80, CurrencyCode = "BRL",
                    Trade = new PortfolioTrade { AssetId = 1, Quantity = 4, UnitPrice = 20, Amount = 80, CashAssetId = 1 },
                    Effects = [new MovementEffect { PositionId = 1, CurrencyId = 1, BeforeQuantity = 10, AfterQuantity = 6, BeforeCost = 100, AfterCost = 60, BeforeValue = 100, AfterValue = 120 },
                        new MovementEffect { CashAssetId = 1, CurrencyId = 1, BeforeValue = 900, AfterValue = 980 }] }], [],
                [new PortfolioSnapshot { Id = 1, SnapshotDate = Day.AddDays(2), DashboardJson = "frozen" }], "state-v1");
        }
        private static CorrectionInput Input(decimal quantity = 12) => new(1, "Corrigir quantidade", new("buy", Day, AssetId: 1, CashAssetId: 1, Quantity: quantity, UnitPrice: 10));
        [Fact]
        public void CorrectPurchase_ShouldRecalculateLaterSaleAndKeepOriginalStateImmutable()
        {
            var state = State(); var plan = CorrectionCalculator.Calculate(state, Input());
            Assert.True(plan.Preview.CanApply, string.Join(";", plan.Preview.Blockers));
            var p = Assert.Single(plan.Positions); Assert.Equal(8, p.Quantity); Assert.Equal(80, p.InvestedAmount); Assert.Equal(160, p.CurrentValue);
            Assert.Equal(960, Assert.Single(plan.Cash).Value); Assert.Equal(6, state.Positions[0].Quantity); Assert.Equal(980, state.Cash[0].Value);
            Assert.Equal([2, 1], plan.Steps.Take(2).Select(x => x.Movement.ReversalOfId!.Value));
            Assert.Equal([1, 2], plan.Steps.Skip(2).Select(x => x.Movement.ReplacesMovementId!.Value));
            Assert.Equal(Day, plan.Steps[1].Movement.Date); Assert.Equal("frozen", state.Snapshots[0].DashboardJson); Assert.False(state.Snapshots[0].IsOutdated);
            Assert.Equal(1, Assert.Single(plan.Preview.Snapshots).Id);
        }
        [Theory]
        [InlineData(2)]
        [InlineData(200)]
        public void InvalidReplay_ShouldReturnBlockerWithoutAnyPartialPlan(decimal quantity)
        {
            var state = State(); var plan = CorrectionCalculator.Calculate(state, Input(quantity));
            Assert.False(plan.Preview.CanApply); Assert.NotEmpty(plan.Preview.Blockers); Assert.Empty(plan.Steps); Assert.Empty(plan.Positions);
            Assert.Equal(6, state.Positions[0].Quantity); Assert.Equal(980, state.Cash[0].Value);
        }
        [Fact]
        public void NewLatePurchase_ShouldReplayAffectedChainAndSupportANewPosition()
        {
            var state = State(); var plan = CorrectionCalculator.Calculate(state, new(null, "Compra esquecida", new("buy", Day.AddDays(-1), AssetId: 1, CashAssetId: 1, Quantity: 2, UnitPrice: 10)));
            Assert.True(plan.Preview.CanApply); Assert.Equal(8, Assert.Single(plan.Positions).Quantity);
            var another = CorrectionCalculator.Calculate(state, new(null, "Novo ativo", new("buy", Day.AddDays(3), AssetId: 2, Quantity: 2, UnitPrice: 10)));
            Assert.True(another.Preview.CanApply); Assert.Equal(-2, Assert.Single(another.Positions).Id);
            Assert.Equal(20, Assert.Single(Assert.Single(another.Steps).Flows).BaseAmount);
        }
        [Fact]
        public void DivergentAuditAndCorporateEvents_ShouldBlockReplay()
        {
            var state = State(); state.Positions[0].CurrentValue = 999;
            Assert.False(CorrectionCalculator.Calculate(state, Input()).Preview.CanApply);
            state = State(); state.Movements[1].Kind = "split";
            Assert.Contains(CorrectionCalculator.Calculate(state, Input()).Preview.Blockers, x => x.Contains("evento societário"));
        }
        [Fact]
        public void ForeignNewFlow_ShouldRequireHistoricalEquivalentAndNeverInventOne()
        {
            var state = State(); state.Assets[1].CurrencyId = 2;
            state = state with { Currencies = [..state.Currencies, new Currency { Id = 2, Code = "USD" }] };
            var input = new CorrectionInput(null, "Aporte estrangeiro", new("buy", Day.AddDays(3), AssetId: 2, Quantity: 2, UnitPrice: 10));
            Assert.False(CorrectionCalculator.Calculate(state, input).Preview.CanApply);
            var plan = CorrectionCalculator.Calculate(state, input with { Operation = input.Operation with { BaseAmount = 123.4567m } });
            Assert.True(plan.Preview.CanApply); Assert.Equal(123.4567m, Assert.Single(Assert.Single(plan.Steps).Flows).BaseAmount);
            Assert.NotEqual(plan.Preview.Token, CorrectionCalculator.Calculate(state with { Fingerprint = "changed" }, input).Preview.Token);
        }
        [Fact]
        public void AbsoluteAdjustment_ShouldKeepTargetAndWarnRatherThanAddItsOldDifference()
        {
            var state = State();
            var input = new CorrectionInput(null, "Extrato", new("adjustment", Day.AddDays(3), CashAssetId: 1, Amount: 1000));
            var plan = CorrectionCalculator.Calculate(state, input);
            Assert.True(plan.Preview.CanApply); Assert.Equal(1000, Assert.Single(plan.Cash).Value);
            Assert.Contains(plan.Preview.Warnings, x => x.Contains("ajuste absoluto"));
        }
    }
}
