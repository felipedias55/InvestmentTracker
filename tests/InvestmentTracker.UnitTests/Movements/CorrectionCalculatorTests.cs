using InvestmentTracker.Application.Movements;
using InvestmentTracker.Domain.Entities;
using System.Text.Json;
using InvestmentTracker.Application.Portfolios;

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

        private static CorrectionState WithSplit()
        {
            var s = State();
            var sale = s.Movements[1]; sale.Id = 3;
            var effect = sale.Effects.Single(e => e.PositionId.HasValue);
            effect.BeforeQuantity = 20; effect.AfterQuantity = 16; effect.AfterCost = 80; effect.AfterValue = 320;
            s.Positions[0].Quantity = 16; s.Positions[0].InvestedAmount = 80; s.Positions[0].CurrentValue = 320;
            var split = new FinancialMovement { Id = 2, Kind = "split", Date = Day, CurrencyCode = "BRL",
                RequestPayload = JsonSerializer.Serialize(new SaveCorporateEventDto(Guid.NewGuid(), Day, "split", 1, 20, 0, "2 para 1")),
                Effects = [new MovementEffect { PositionId = 1, CurrencyId = 1, BeforeQuantity = 10, AfterQuantity = 20,
                    BeforeCost = 100, AfterCost = 100, BeforeValue = 100, AfterValue = 100 }] };
            return s with { Movements = [s.Movements[0], split, sale] };
        }

        [Fact]
        public void CorrectPurchase_ShouldReplayAuditedSplitAndSaleUsingProportion()
        {
            var state = WithSplit();
            var plan = CorrectionCalculator.Calculate(state, Input());
            Assert.True(plan.Preview.CanApply, string.Join(";", plan.Preview.Blockers));
            var p = Assert.Single(plan.Positions);
            Assert.Equal(20, p.Quantity); Assert.Equal(100, p.InvestedAmount); Assert.Equal(400, p.CurrentValue);
            Assert.Equal(960, Assert.Single(plan.Cash).Value);
            var replay = plan.Steps.Single(step => step.Movement.Kind == "split").Movement;
            Assert.Equal(24, replay.Effects.Single().AfterQuantity);
            Assert.Equal(120, replay.Effects.Single().AfterValue);
            var operation = CorrectionCalculator.ReadOperation(replay, [], state.Positions);
            Assert.Equal(20, operation.RatioNumerator); Assert.Equal(10, operation.RatioDenominator);
            Assert.Contains(plan.Preview.Warnings, text => text.Contains("proporção"));
            Assert.Equal(16, state.Positions[0].Quantity);
        }

        [Theory]
        [InlineData("split", 2, 1, 16)]
        [InlineData("reverse-split", 1, 2, 1)]
        public void LateRatioEvent_ShouldReplayLaterSale(string kind, decimal numerator, decimal denominator, decimal expected)
        {
            var state = State();
            var plan = CorrectionCalculator.Calculate(state, new(null, "Comunicado", new(kind, Day.AddDays(1), AssetId: 1,
                RatioNumerator: numerator, RatioDenominator: denominator)));
            Assert.True(plan.Preview.CanApply, string.Join(";", plan.Preview.Blockers));
            Assert.Equal(expected, Assert.Single(plan.Positions).Quantity);
            Assert.Equal(980, Assert.Single(plan.Cash).Value);
            Assert.Empty(plan.Steps.SelectMany(step => step.Flows));
        }

        [Fact]
        public void Bonus_ShouldPreserveMarketValueAndAddOnlyDeclaredQuantityAndCost()
        {
            var state = State();
            var plan = CorrectionCalculator.Calculate(state, new(null, "Bonificação", new("bonus", Day.AddDays(3), AssetId: 1, Quantity: 2, Cost: 5)));
            Assert.True(plan.Preview.CanApply);
            var p = Assert.Single(plan.Positions);
            Assert.Equal(8, p.Quantity); Assert.Equal(65, p.InvestedAmount); Assert.Equal(120, p.CurrentValue); Assert.Equal(0, p.Income);
            Assert.Empty(plan.Cash); Assert.Empty(plan.Steps.Single().Flows);
            var operation = CorrectionCalculator.ReadOperation(plan.Steps.Single().Movement, [], state.Positions);
            Assert.Equal(2, operation.Quantity); Assert.Equal(5, operation.Cost);
        }

        [Theory]
        [InlineData("split", 1, 2)]
        [InlineData("reverse-split", 2, 1)]
        [InlineData("split", 0, 1)]
        [InlineData("split", 2, 0)]
        [InlineData("split", 4, 7)]
        public void InvalidRatioOrResidualFractions_ShouldBlockWithoutPartialState(string kind, decimal numerator, decimal denominator)
        {
            var state = State();
            var plan = CorrectionCalculator.Calculate(state, new(null, "Conferir", new(kind, Day.AddDays(3), AssetId: 1,
                RatioNumerator: numerator, RatioDenominator: denominator)));
            Assert.False(plan.Preview.CanApply); Assert.Empty(plan.Steps); Assert.Equal(6, state.Positions[0].Quantity);
        }

        [Fact]
        public void InconsistentEventPayload_ShouldRemainBlocked()
        {
            var state = WithSplit(); state.Movements[1].RequestPayload = "{}";
            var plan = CorrectionCalculator.Calculate(state, Input());
            Assert.False(plan.Preview.CanApply); Assert.Contains(plan.Preview.Blockers, text => text.Contains("evento societário"));
        }

        private static CorrectionState WithPositionAdjustment(bool quote)
        {
            var state = State(); state.Positions[0].CurrentValue = 180;
            var adjustment = new FinancialMovement { Id = 3, Date = Day.AddDays(2), Kind = "position-adjustment", CurrencyCode = "BRL",
                RequestPayload = quote ? JsonSerializer.Serialize(new[] { new QuoteUpdate(1, "BRL", 30, 6, 120, Day.AddDays(1)) }) : null,
                Effects = [new MovementEffect { PositionId = 1, CurrencyId = 1, BeforeQuantity = 6, AfterQuantity = 6,
                    BeforeCost = 60, AfterCost = 60, BeforeValue = 120, AfterValue = 180 }] };
            return state with { Movements = [..state.Movements, adjustment] };
        }

        [Theory]
        [InlineData(false, 180)]
        [InlineData(true, 240)]
        public void PositionAdjustment_ShouldKeepManualTargetOrConfirmedUnitPrice(bool quote, decimal expectedValue)
        {
            var state = WithPositionAdjustment(quote);
            var plan = CorrectionCalculator.Calculate(state, Input());
            Assert.True(plan.Preview.CanApply, string.Join(";", plan.Preview.Blockers));
            var p = Assert.Single(plan.Positions);
            Assert.Equal(8, p.Quantity); Assert.Equal(80, p.InvestedAmount); Assert.Equal(expectedValue, p.CurrentValue);
            Assert.Contains(plan.Preview.Warnings, text => text.Contains("preço unitário"));
            Assert.Equal(180, state.Positions[0].CurrentValue);
            // Replaying a correction again must retain the original adjustment's intent.
            var replayed = plan.Steps.Where(step => step.Movement.ReversalOfId is null).Select(step => step.Movement).ToArray();
            for (var i = 0; i < replayed.Length; i++) replayed[i].Id = i + 10;
            var nextState = state with { Positions = plan.Positions, Cash = plan.Cash, Movements = replayed };
            var next = CorrectionCalculator.Calculate(nextState, new(10, "Nova conferência", Input().Operation with { Quantity = 14 }));
            Assert.True(next.Preview.CanApply, string.Join(";", next.Preview.Blockers));
            Assert.Equal(quote ? 300 : 180, Assert.Single(next.Positions).CurrentValue);
        }

        [Fact]
        public void IncompatibleQuotePayload_ShouldNotBeReinterpretedAsManualAdjustment()
        {
            var state = WithPositionAdjustment(true);
            state.Movements[2].RequestPayload = JsonSerializer.Serialize(new[] { new QuoteUpdate(1, "BRL", 50, 6, 120, Day) });
            var plan = CorrectionCalculator.Calculate(state, Input());
            Assert.False(plan.Preview.CanApply); Assert.Empty(plan.Steps);
        }
    }
}
