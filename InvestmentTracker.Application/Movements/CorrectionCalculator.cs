using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.Movements
{
    public static class CorrectionCalculator
    {
        private const decimal MaxMoney = 999999999999999.9999m;
        private const decimal MaxQuantity = 9999999999999.999999m;
        private static readonly string[] Supported = ["buy", "sell", "income", "deposit", "withdrawal", "transfer", "adjustment"];
        public static string Token(string state, CorrectionInput input) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state + JsonSerializer.Serialize(input))));
        private static decimal Round(decimal value) => decimal.Round(value, 4, MidpointRounding.AwayFromZero);
        private static void Require(bool valid, string message) { if (!valid) throw new InputValidationException(message); }
        private static void Money(decimal value, bool zero = false) => Require(value >= 0 && (zero || value > 0) && value <= MaxMoney && Round(value) == value, "Saldo insuficiente ou valor fora do limite de 15 inteiros e 4 decimais.");
        private static PortfolioAsset Copy(PortfolioAsset p) => new() { Id = p.Id, PortfolioId = p.PortfolioId, AssetId = p.AssetId,
            Quantity = p.Quantity, InvestedAmount = p.InvestedAmount, CurrentValue = p.CurrentValue, Income = p.Income, UpdatedOn = p.UpdatedOn };
        private static ExternalAsset Copy(ExternalAsset b) => new() { Id = b.Id, PortfolioId = b.PortfolioId, Name = b.Name,
            CurrencyId = b.CurrencyId, Value = b.Value, Description = b.Description, UpdatedOn = b.UpdatedOn };

        public static CorrectionOperation ReadOperation(FinancialMovement m, IReadOnlyList<PortfolioCashFlow> flows)
        {
            Require(Supported.Contains(m.Kind) && m.ReversalOfId is null, $"O lançamento #{m.Id} ({m.Kind}) exige conferência manual; este tipo não pode ser recalculado automaticamente.");
            var flow = flows.SingleOrDefault(x => x.MovementId == m.Id && !x.IsReversal);
            if (m.Trade is { } trade) return new(m.Kind, m.Date, trade.AssetId, trade.CashAssetId,
                Quantity: trade.Quantity, UnitPrice: trade.UnitPrice, Fees: trade.Fees, BaseAmount: flow?.BaseAmount);
            if (m.IncomeReceipt is { } income) return new("income", m.Date, income.AssetId, income.CashAssetId,
                Amount: income.Amount);
            try
            {
                var dto = JsonSerializer.Deserialize<SaveMovementDto>(m.RequestPayload ?? "");
                Require(dto is not null && dto.Kind == m.Kind, $"O lançamento #{m.Id} não contém os dados originais suficientes.");
                return new(m.Kind, m.Date, CashAssetId: dto!.CashAssetId, DestinationId: dto.DestinationId, Amount: dto.Amount, BaseAmount: flow?.BaseAmount);
            }
            catch (JsonException) { throw new InputValidationException($"O lançamento #{m.Id} não contém os dados originais suficientes."); }
        }

        public static CorrectionPlan Calculate(CorrectionState state, CorrectionInput input)
        {
            var token = Token(state.Fingerprint, input);
            var from = input.Operation.Date;
            try { return Build(state, input, token); }
            catch (InputValidationException ex) { return new(new(false, token, from, [], [], [], [], [ex.Message]), [], [], []); }
            catch (OverflowException) { return new(new(false, token, from, [], [], [], [], ["O recálculo excede os limites numéricos. Confira quantidade, preço, taxas e saldos."]), [], [], []); }
        }

        private static CorrectionPlan Build(CorrectionState s, CorrectionInput input, string token)
        {
            var op = input.Operation;
            Require(Supported.Contains(op.Kind), "Tipo de operação não disponível para correção automática.");
            var reversedIds = s.Movements.Where(m => m.ReversalOfId.HasValue).Select(m => m.ReversalOfId!.Value).ToHashSet();
            var original = input.MovementId.HasValue ? s.Movements.SingleOrDefault(m => m.Id == input.MovementId) : null;
            Require(!input.MovementId.HasValue || original is not null, "Lançamento não encontrado nesta carteira.");
            Require(original is null || (!reversedIds.Contains(original.Id) && original.ReversalOfId is null && Supported.Contains(original.Kind)), "O original já foi estornado ou seu tipo exige conferência manual.");
            var from = original is not null && original.Date < op.Date ? original.Date : op.Date;
            var positions = s.Positions.ToDictionary(p => p.Id, Copy);
            var cash = s.Cash.ToDictionary(b => b.Id, Copy);
            var asset = op.AssetId.HasValue ? s.Assets.SingleOrDefault(a => a.Id == op.AssetId) : null;
            Require(!op.AssetId.HasValue || asset is not null, "Ativo não encontrado.");
            var position = asset is null ? null : positions.Values.SingleOrDefault(p => p.AssetId == asset.Id);
            if (position is null && asset is not null && op.Kind == "buy")
            {
                position = new PortfolioAsset { Id = -asset.Id, AssetId = asset.Id, PortfolioId = s.Portfolio.Id };
                positions.Add(position.Id, position);
            }
            var pids = new HashSet<int>(); var bids = new HashSet<int>();
            void Include(FinancialMovement m)
            {
                pids.UnionWith(m.Effects.Where(e => e.PositionId.HasValue).Select(e => e.PositionId!.Value));
                bids.UnionWith(m.Effects.Where(e => e.CashAssetId.HasValue).Select(e => e.CashAssetId!.Value));
            }
            if (original is not null) Include(original);
            if (position is not null) pids.Add(position.Id);
            if (op.CashAssetId.HasValue) bids.Add(op.CashAssetId.Value);
            if (op.DestinationId.HasValue) bids.Add(op.DestinationId.Value);
            Require(pids.All(positions.ContainsKey) && bids.All(cash.ContainsKey), "Uma posição ou saldo não pertence a esta carteira ou foi removido.");
            var candidates = s.Movements.Where(m => !reversedIds.Contains(m.Id) && m.ReversalOfId is null
                && m.Kind is not ("reopen" or "income-conversion" or "correction")
                && (original is null ? m.Date >= from : m.Id >= original.Id || (op.Date < original.Date && m.Date >= from))).ToArray();
            Require(!candidates.Any(m => m.Effects.Count == 0), "Há lançamentos sem efeitos auditáveis no período. Confira o histórico antes da correção automática.");
            var affected = new Dictionary<int, FinancialMovement>();
            bool changed;
            do
            {
                changed = false;
                foreach (var m in candidates)
                {
                    if (affected.ContainsKey(m.Id)) continue;
                    if (m.Id != original?.Id && !m.Effects.Any(e => (e.PositionId.HasValue && pids.Contains(e.PositionId.Value)) || (e.CashAssetId.HasValue && bids.Contains(e.CashAssetId.Value)))) continue;
                    affected.Add(m.Id, m); Include(m); changed = true;
                }
            } while (changed);
            Require(affected.Values.All(m => Supported.Contains(m.Kind)), "A cadeia contém saldo inicial, ajuste de posição ou evento societário. É necessária conferência manual antes de automatizar esta correção.");
            Require(pids.All(positions.ContainsKey) && bids.All(cash.ContainsKey), "A cadeia possui referências removidas; confira o histórico.");
            var steps = new List<CorrectionStep>();
            var beforePositions = positions.ToDictionary(x => x.Key, x => Copy(x.Value));
            var beforeCash = cash.ToDictionary(x => x.Key, x => Copy(x.Value));
            foreach (var m in affected.Values.OrderByDescending(m => m.Id))
            {
                var reversal = new FinancialMovement { PortfolioId = s.Portfolio.Id, RequestId = Guid.NewGuid(), Kind = "reversal",
                    Date = m.Date, ReversalOfId = m.Id, Amount = m.Amount, CurrencyCode = m.CurrencyCode,
                    Description = $"Correção retroativa: estorno de #{m.Id}" };
                foreach (var e in m.Effects)
                {
                    if (e.PositionId.HasValue)
                    {
                        Require(positions.TryGetValue(e.PositionId.Value, out var p), "Posição não encontrada na cadeia.");
                        Require(s.Assets.Single(a => a.Id == p!.AssetId).CurrencyId == e.CurrencyId && p!.Quantity == e.AfterQuantity && p.InvestedAmount == e.AfterCost && p.CurrentValue == e.AfterValue && p.Income == e.AfterIncome,
                            $"A posição de #{m.Id} diverge da auditoria. Confira os ajustes antes de corrigir.");
                        p!.Quantity = e.BeforeQuantity; p.InvestedAmount = e.BeforeCost; p.CurrentValue = e.BeforeValue; p.Income = e.BeforeIncome;
                    }
                    else
                    {
                        Require(e.CashAssetId.HasValue && cash.TryGetValue(e.CashAssetId.Value, out _), "Saldo não encontrado na cadeia.");
                        var b = cash[e.CashAssetId!.Value];
                        Require(b.CurrencyId == e.CurrencyId && b.Value == e.AfterValue, $"O saldo de #{m.Id} diverge da auditoria. Confira os ajustes.");
                        b.Value = e.BeforeValue;
                    }
                    reversal.Effects.Add(new MovementEffect { PositionId = e.PositionId, CashAssetId = e.CashAssetId, CurrencyId = e.CurrencyId, Name = e.Name,
                        BeforeValue = e.AfterValue, AfterValue = e.BeforeValue, BeforeQuantity = e.AfterQuantity, AfterQuantity = e.BeforeQuantity,
                        BeforeCost = e.AfterCost, AfterCost = e.BeforeCost, BeforeIncome = e.AfterIncome, AfterIncome = e.BeforeIncome });
                }
                steps.Add(new(reversal, s.Flows.Where(f => f.MovementId == m.Id).Select(f => new PortfolioCashFlow {
                    PortfolioId = s.Portfolio.Id, Date = f.Date, Kind = f.Kind, CurrencyId = f.CurrencyId, Amount = f.Amount,
                    BaseCurrencyCode = f.BaseCurrencyCode, BaseAmount = f.BaseAmount, IsReversal = true,
                    Notes = $"Correção retroativa: anula fluxo de #{m.Id}" }).ToArray()));
            }
            var replay = affected.Values.Where(m => m.Id != original?.Id).Select(m => (Op: ReadOperation(m, s.Flows), Original: (FinancialMovement?)m)).ToList();
            replay.Add((op, original));
            foreach (var item in replay.OrderBy(x => x.Op.Date).ThenBy(x => x.Original?.Id ?? 0))
            {
                try { steps.Add(ApplyOperation(s, item.Op, item.Original, ReferenceEquals(item.Op, op), positions, cash, pids, bids)); }
                catch (InputValidationException ex) { throw new InputValidationException($"{(ReferenceEquals(item.Op, op) ? "Operação corrigida" : $"Relançamento de #{item.Original!.Id}")}: {ex.Message}"); }
            }
            var impactFrom = affected.Values.Select(m => m.Date).Append(from).Min();
            var photos = s.Snapshots.Where(x => x.SnapshotDate >= impactFrom).OrderBy(x => x.SnapshotDate)
                .Select(x => new CorrectionSnapshotDto(x.Id, x.Month, x.SnapshotDate, x.Revision, x.IsOutdated, x.IsReopened, !x.IsReopened)).ToArray();
            var balances = pids.Order().Select(id => {
                var p = positions[id]; var old = beforePositions[id]; var a = s.Assets.Single(a => a.Id == p.AssetId);
                return new CorrectionBalanceDto(a.Ticker, s.Currencies.Single(c => c.Id == a.CurrencyId).Code, true,
                    old.CurrentValue, p.CurrentValue, old.Quantity, p.Quantity, old.InvestedAmount, p.InvestedAmount, old.Income, p.Income);
            }).Concat(bids.Order().Select(id => new CorrectionBalanceDto(cash[id].Name, s.Currencies.Single(c => c.Id == cash[id].CurrencyId).Code,
                false, beforeCash[id].Value, cash[id].Value, 0, 0, 0, 0, 0, 0))).ToArray();
            var warnings = new List<string> {
                "A confirmação estorna e relança a cadeia em uma transação. Os registros originais continuam disponíveis.",
                "Estornos desta correção têm a data efetiva original para anular o histórico naquele período; a data de gravação registra quando a correção foi feita.",
                "Na mesma data, um lançamento novo entra antes dos existentes; correções mantêm a ordem do registro original."
            };
            if (photos.Length > 0) warnings.Add("Os períodos listados serão reabertos e suas fotografias marcadas como desatualizadas, sem alterar os valores ou versões congelados.");
            if (replay.Any(x => x.Op.Kind == "adjustment")) warnings.Add("A cadeia contém ajuste absoluto: o saldo informado nesse ajuste será mantido como total, mesmo com operações anteriores corrigidas.");
            return new(new(true, token, impactFrom, affected.Keys.Order().ToArray(), balances, photos, warnings, []),
                pids.Select(id => positions[id]).ToArray(), bids.Select(id => cash[id]).ToArray(), steps);
        }

        private static CorrectionStep ApplyOperation(CorrectionState s, CorrectionOperation op, FinancialMovement? old, bool corrected,
            Dictionary<int, PortfolioAsset> positions, Dictionary<int, ExternalAsset> cash, HashSet<int> pids, HashSet<int> bids)
        {
            Require(Supported.Contains(op.Kind), "Tipo não suportado.");
            var isTrade = op.Kind is "buy" or "sell";
            var usesAsset = isTrade || op.Kind == "income";
            Require(usesAsset ? op.AssetId.HasValue : !op.AssetId.HasValue, "Selecione um ativo apenas para compra, venda ou provento.");
            Require(op.Kind == "transfer" ? op.DestinationId.HasValue : !op.DestinationId.HasValue, "Destino adicional é permitido somente em transferências.");
            Require(isTrade || (op.Quantity == 0 && op.UnitPrice == 0 && op.Fees == 0), "Quantidade, preço e taxas são exclusivos de compras e vendas.");
            Require(!isTrade || op.Amount == 0, "Informe quantidade e preço para compras e vendas, sem valor separado.");
            var a = usesAsset ? s.Assets.SingleOrDefault(a => a.Id == op.AssetId) : null;
            Require(!usesAsset || a is not null, "Ativo não encontrado.");
            var p = a is null ? null : positions.Values.SingleOrDefault(x => x.AssetId == a.Id);
            Require(!usesAsset || p is not null, "A operação exige uma posição na carteira.");
            var b = op.CashAssetId.HasValue ? cash.GetValueOrDefault(op.CashAssetId.Value) : null;
            Require(!op.CashAssetId.HasValue || b is not null, "Saldo não encontrado nesta carteira.");
            Require(usesAsset || b is not null, "Selecione o saldo da operação.");
            var currencyId = a?.CurrencyId ?? b!.CurrencyId;
            var currency = s.Currencies.Single(c => c.Id == currencyId).Code;
            Require(b is null || b.CurrencyId == currencyId, "O saldo e o ativo precisam usar a mesma moeda.");
            var oldFlow = old is null ? null : s.Flows.SingleOrDefault(f => f.MovementId == old.Id && !f.IsReversal);
            var targetCurrency = !corrected && oldFlow is not null ? oldFlow.BaseCurrencyCode : s.Portfolio.BaseCurrency.Code;
            var beforeP = p is null ? null : Copy(p); var beforeB = b is null ? null : Copy(b);
            var requestId = Guid.NewGuid();
            var m = new FinancialMovement { PortfolioId = s.Portfolio.Id, RequestId = requestId, Date = op.Date, Kind = op.Kind,
                CurrencyCode = currency, ReplacesMovementId = old?.Id, Description = a?.Ticker ?? b!.Name };
            var flows = new List<PortfolioCashFlow>();
            decimal amount;
            if (isTrade)
            {
                Require(op.Quantity > 0 && op.Quantity <= MaxQuantity && decimal.Round(op.Quantity, 6) == op.Quantity, "Quantidade deve ser positiva e ter até seis decimais.");
                Money(op.UnitPrice); Money(op.Fees, true);
                amount = Round(op.Quantity * op.UnitPrice) + (op.Kind == "buy" ? op.Fees : -op.Fees); Money(amount);
                var q = p!.Quantity + (op.Kind == "buy" ? op.Quantity : -op.Quantity);
                Require(q >= 0 && q <= MaxQuantity, "Quantidade vendida excede a disponível ou a posição excede o limite.");
                var cost = op.Kind == "buy" ? p.InvestedAmount + amount : q == 0 ? 0 : Round(p.InvestedAmount * (q / p.Quantity));
                Money(cost, true); var value = Round(q * op.UnitPrice); Money(value, true);
                p.Quantity = q; p.InvestedAmount = cost; p.CurrentValue = value;
                if (b is not null) b.Value += op.Kind == "buy" ? -amount : amount;
                m.Trade = new PortfolioTrade { PortfolioId = s.Portfolio.Id, RequestId = requestId, AssetId = a!.Id, Date = op.Date,
                    Kind = op.Kind, Ticker = a.Ticker, CurrencyCode = currency, Quantity = op.Quantity, UnitPrice = op.UnitPrice, Fees = op.Fees,
                    Amount = amount, RemainingQuantity = q, RemainingCost = cost, CashAssetId = b?.Id, CashAssetName = b?.Name, RequestedBaseAmount = op.BaseAmount };
            }
            else
            {
                amount = op.Amount; Money(amount, op.Kind == "adjustment");
                if (op.Kind == "income")
                {
                    p!.Income += amount; Money(p.Income, true); if (b is not null) b.Value += amount;
                    var receipt = new IncomeReceipt { PortfolioId = s.Portfolio.Id, PositionId = p.Id, AssetId = a!.Id, RequestId = requestId,
                        Date = op.Date, Ticker = a.Ticker, CurrencyCode = currency, Amount = amount, CashAssetId = b?.Id, CashAssetName = b?.Name,
                        BaseCurrencyCode = corrected ? s.Portfolio.BaseCurrency.Code : old!.IncomeReceipt!.BaseCurrencyCode,
                        RequestedBaseAmount = op.BaseAmount, Notes = corrected ? "Recebimento corrigido" : old!.IncomeReceipt!.Notes };
                    if (!corrected)
                        receipt.Conversions = old!.IncomeReceipt!.Conversions.Select(c => new IncomeConversion { Revision = c.Revision, RequestId = Guid.NewGuid(),
                            BaseCurrencyCode = c.BaseCurrencyCode, BaseAmount = c.BaseAmount, Rate = c.Rate, RateDate = c.RateDate, Source = c.Source,
                            Reason = $"Preservada no relançamento: {c.Reason}".Substring(0, Math.Min(400, $"Preservada no relançamento: {c.Reason}".Length)), CreatedAtUtc = c.CreatedAtUtc }).ToList();
                    else
                    {
                        var equivalent = Equivalent(amount, currency, s.Portfolio.BaseCurrency.Code, op.BaseAmount);
                        receipt.Conversions.Add(new IncomeConversion { Revision = 1, RequestId = Guid.NewGuid(), BaseCurrencyCode = s.Portfolio.BaseCurrency.Code,
                            BaseAmount = equivalent, Rate = decimal.Round(equivalent / amount, 18) is var rate && rate > 0 ? rate : null,
                            RateDate = op.Date, Source = currency == s.Portfolio.BaseCurrency.Code ? "same-currency" : "manual", Reason = "Equivalente confirmado na correção" });
                    }
                    m.IncomeReceipt = receipt;
                }
                else if (op.Kind == "adjustment") { b!.Value = amount; m.Amount = Math.Abs(beforeB!.Value - amount); }
                else
                {
                    b!.Value += op.Kind == "deposit" ? amount : -amount;
                    if (op.Kind == "transfer")
                    {
                        var destination = cash.GetValueOrDefault(op.DestinationId!.Value);
                        Require(destination is not null && destination.Id != b.Id && destination.CurrencyId == b.CurrencyId, "Selecione outro saldo da mesma carteira e moeda.");
                        var before = destination!.Value; destination.Value += amount; Money(destination.Value, true); bids.Add(destination.Id);
                        m.Effects.Add(new MovementEffect { CashAssetId = destination.Id, CurrencyId = currencyId, Name = destination.Name, BeforeValue = before, AfterValue = destination.Value });
                    }
                }
            }
            if (b is not null) { Money(b.Value, true); bids.Add(b.Id); m.Effects.Add(new MovementEffect { CashAssetId = b.Id, CurrencyId = b.CurrencyId, Name = b.Name, BeforeValue = beforeB!.Value, AfterValue = b.Value }); }
            if (p is not null)
            {
                pids.Add(p.Id); m.Effects.Add(new MovementEffect { PositionId = p.Id, CurrencyId = currencyId, Name = a!.Ticker,
                    BeforeQuantity = beforeP!.Quantity, AfterQuantity = p.Quantity, BeforeCost = beforeP.InvestedAmount, AfterCost = p.InvestedAmount,
                    BeforeValue = beforeP.CurrentValue, AfterValue = p.CurrentValue, BeforeIncome = beforeP.Income, AfterIncome = p.Income });
            }
            if (op.Kind != "adjustment") m.Amount = amount;
            if ((isTrade && b is null) || op.Kind is "deposit" or "withdrawal")
            {
                Require(corrected || oldFlow is not null, "Fluxo original sem equivalente auditável. Confira o histórico.");
                var equivalent = Equivalent(amount, currency, targetCurrency, op.BaseAmount);
                flows.Add(new PortfolioCashFlow { PortfolioId = s.Portfolio.Id, Date = op.Date, CurrencyId = currencyId, Amount = amount,
                    Kind = op.Kind is "buy" or "deposit" ? "contribution" : "withdrawal", BaseCurrencyCode = targetCurrency, BaseAmount = equivalent,
                    Notes = "Relançamento auditável de correção" });
            }
            m.RequestPayload = JsonSerializer.Serialize(new SaveMovementDto(requestId, op.Date, op.Kind, op.CashAssetId ?? 0, op.Amount,
                op.DestinationId, "Relançamento auditável", op.BaseAmount));
            return new(m, flows);
        }
        private static decimal Equivalent(decimal amount, string currency, string target, decimal? equivalent)
        {
            if (currency == target) { Require(!equivalent.HasValue || equivalent == amount, "Na mesma moeda, o equivalente deve ser igual ao total."); return amount; }
            Require(equivalent.HasValue, $"Informe o equivalente histórico em {target}; a correção não usa câmbio atual.");
            Money(equivalent!.Value); return equivalent.Value;
        }
    }
}
