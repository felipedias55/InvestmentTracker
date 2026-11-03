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
        private static readonly string[] Supported = ["buy", "sell", "income", "deposit", "withdrawal", "transfer", "adjustment", "split", "reverse-split", "bonus"];
        private static bool IsEvent(string kind) => kind is "split" or "reverse-split" or "bonus";
        private static bool CanReplay(string kind) => Supported.Contains(kind) || kind == "position-adjustment";
        private static void Quantity(decimal value) => Require(value > 0 && value <= MaxQuantity && decimal.Round(value, 6) == value,
            "Quantidade ou proporção deve ser positiva, dentro do limite e com até seis decimais.");
        public static string Token(string state, CorrectionInput input) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state + JsonSerializer.Serialize(input))));
        private static decimal Round(decimal value) => decimal.Round(value, 4, MidpointRounding.AwayFromZero);
        private static void Require(bool valid, string message) { if (!valid) throw new InputValidationException(message); }
        private static void Money(decimal value, bool zero = false) => Require(value >= 0 && (zero || value > 0) && value <= MaxMoney && Round(value) == value, "Saldo insuficiente ou valor fora do limite de 15 inteiros e 4 decimais.");
        private static PortfolioAsset Copy(PortfolioAsset p) => new() { Id = p.Id, PortfolioId = p.PortfolioId, AssetId = p.AssetId,
            Quantity = p.Quantity, InvestedAmount = p.InvestedAmount, CurrentValue = p.CurrentValue, Income = p.Income, UpdatedOn = p.UpdatedOn };
        private static ExternalAsset Copy(ExternalAsset b) => new() { Id = b.Id, PortfolioId = b.PortfolioId, Name = b.Name,
            CurrencyId = b.CurrencyId, Value = b.Value, Description = b.Description, UpdatedOn = b.UpdatedOn };

        public static CorrectionOperation ReadOperation(FinancialMovement m, IReadOnlyList<PortfolioCashFlow> flows,
            IReadOnlyList<PortfolioAsset>? positions = null)
        {
            Require(CanReplay(m.Kind) && m.ReversalOfId is null, $"O lançamento #{m.Id} ({m.Kind}) exige conferência manual; este tipo não pode ser recalculado automaticamente.");
            if (m.Kind == "position-adjustment") return new(m.Kind, m.Date);
            if (IsEvent(m.Kind))
            {
                Require(!flows.Any(f => f.MovementId == m.Id), $"O evento societário #{m.Id} possui fluxo financeiro incompatível.");
                return ReadEvent(m, positions ?? []);
            }
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
            Require(affected.Values.All(m => CanReplay(m.Kind)), "A cadeia contém saldo inicial ou tipo sem dados suficientes. É necessária conferência manual antes de automatizar esta correção.");
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
            var replay = affected.Values.Where(m => m.Id != original?.Id).Select(m => (Op: ReadOperation(m, s.Flows, s.Positions), Original: (FinancialMovement?)m)).ToList();
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
            if (replay.Any(x => x.Op.Kind == "position-adjustment")) warnings.Add("Ajustes manuais preservam os totais dos campos alterados; os demais campos acompanham o recálculo. Cotações em lote preservam o preço unitário confirmado e recalculam o valor pela nova quantidade.");
            if (replay.Any(x => IsEvent(x.Op.Kind))) warnings.Add("Eventos preservam valor de mercado e proventos. Desdobramento/grupamento reaplicam a proporção registrada ou recuperada dos efeitos auditados; bonificações preservam quantidade adicional e custo informados. Confira o comunicado e as frações antes de confirmar.");
            var numbers = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
            foreach (var item in replay.Where(x => IsEvent(x.Op.Kind)))
            {
                var description = item.Original is null ? "Evento informado" : $"Evento #{item.Original.Id}";
                warnings.Add(item.Op.Kind == "bonus"
                    ? $"{description}: bonificação de {item.Op.Quantity.ToString("0.######", numbers)} unidades, custo adicional {item.Op.Cost.ToString("0.####", numbers)} na moeda do ativo."
                    : $"{description}: {item.Op.RatioNumerator.ToString("0.######", numbers)} novas unidades para cada {item.Op.RatioDenominator.ToString("0.######", numbers)} antigas.");
            }
            return new(new(true, token, impactFrom, affected.Keys.Order().ToArray(), balances, photos, warnings, []),
                pids.Select(id => positions[id]).ToArray(), bids.Select(id => cash[id]).ToArray(), steps);
        }

        private static CorrectionStep ApplyOperation(CorrectionState s, CorrectionOperation op, FinancialMovement? old, bool corrected,
            Dictionary<int, PortfolioAsset> positions, Dictionary<int, ExternalAsset> cash, HashSet<int> pids, HashSet<int> bids)
        {
            if (op.Kind == "position-adjustment" && old is not null && !corrected)
                return ReplayPositionAdjustment(s, old, positions, pids);
            Require(Supported.Contains(op.Kind), "Tipo não suportado.");
            var isTrade = op.Kind is "buy" or "sell";
            var isEvent = IsEvent(op.Kind);
            var usesAsset = isTrade || op.Kind == "income" || isEvent;
            Require(usesAsset ? op.AssetId.HasValue : !op.AssetId.HasValue, "Selecione um ativo apenas para compra, venda ou provento.");
            Require(op.Kind == "transfer" ? op.DestinationId.HasValue : !op.DestinationId.HasValue, "Destino adicional é permitido somente em transferências.");
            Require(isTrade || op.Kind == "bonus" || op.Quantity == 0, "Quantidade adicional é exclusiva de bonificação; para desdobramento/grupamento informe a proporção.");
            Require(isTrade || (op.UnitPrice == 0 && op.Fees == 0), "Preço e taxas são exclusivos de compras e vendas.");
            Require(!(isTrade || isEvent) || op.Amount == 0, "Esta operação não recebe valor separado.");
            Require(isEvent || (op.RatioNumerator == 0 && op.RatioDenominator == 0 && op.Cost == 0), "Proporção e custo do evento são exclusivos de eventos societários.");
            Require(!isEvent || (!op.CashAssetId.HasValue && !op.BaseAmount.HasValue), "Evento societário não movimenta saldo nem recebe equivalente de aporte.");
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
            if (isEvent)
            {
                Require(p!.Quantity > 0, "O evento exige uma posição com quantidade positiva.");
                Money(op.Cost, true);
                decimal quantity;
                if (op.Kind == "bonus")
                {
                    Require(op.RatioNumerator == 0 && op.RatioDenominator == 0, "Bonificação recebe quantidade adicional, sem proporção.");
                    Quantity(op.Quantity);
                    quantity = p.Quantity + op.Quantity;
                }
                else
                {
                    Require(op.Cost == 0, "Desdobramento e grupamento preservam o custo total.");
                    Quantity(op.RatioNumerator); Quantity(op.RatioDenominator);
                    // Work with exact scaled integers; never round away residual units.
                    var numerator = new System.Numerics.BigInteger(op.RatioNumerator * 1000000m);
                    var denominator = new System.Numerics.BigInteger(op.RatioDenominator * 1000000m);
                    var scaled = new System.Numerics.BigInteger(p.Quantity * 1000000m) * numerator;
                    var units = System.Numerics.BigInteger.DivRem(scaled, denominator, out var remainder);
                    Require(remainder.IsZero, "A proporção produz frações além de seis decimais. Confira o tratamento de frações; nenhuma quantidade será arredondada automaticamente.");
                    Require(units <= new System.Numerics.BigInteger(MaxQuantity * 1000000m), "A quantidade final excede o limite.");
                    quantity = (decimal)units / 1000000m;
                    Require(op.Kind == "split" ? quantity > p.Quantity : quantity < p.Quantity, "A proporção deve aumentar a quantidade no desdobramento e diminuir no grupamento.");
                }
                Quantity(quantity); Money(p.InvestedAmount + op.Cost, true);
                p.Quantity = quantity; p.InvestedAmount += op.Cost; amount = op.Cost;
            }
            else if (isTrade)
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
            m.RequestPayload = isEvent ? JsonSerializer.Serialize(new EventPayload(op)) : JsonSerializer.Serialize(new SaveMovementDto(requestId, op.Date, op.Kind, op.CashAssetId ?? 0, op.Amount,
                op.DestinationId, "Relançamento auditável", op.BaseAmount));
            return new(m, flows);
        }
        private sealed record EventPayload(CorrectionOperation Operation);
        private sealed record PositionTarget(int PositionId, decimal? Quantity, decimal? Cost, decimal? Value, decimal? Income,
            decimal? UnitPrice = null, string? Source = null, DateOnly? QuoteDate = null);
        private sealed record PositionPayload(IReadOnlyList<PositionTarget> PositionTargets);

        private static CorrectionOperation ReadEvent(FinancialMovement m, IReadOnlyList<PortfolioAsset> positions)
        {
            var message = $"O evento societário #{m.Id} não possui proporção/dados e efeitos auditáveis consistentes. Confira o comunicado e o histórico.";
            Require(m.Effects.Count == 1 && m.Effects.All(e => e.PositionId.HasValue && !e.CashAssetId.HasValue)
                && m.Trade is null && m.IncomeReceipt is null, message);
            var effect = m.Effects.Single();
            var position = positions.SingleOrDefault(p => p.Id == effect.PositionId);
            Require(position is not null && effect.BeforeValue == effect.AfterValue && effect.BeforeIncome == effect.AfterIncome, message);
            Quantity(effect.BeforeQuantity); Quantity(effect.AfterQuantity);
            CorrectionOperation op;
            try
            {
                using var json = JsonDocument.Parse(m.RequestPayload ?? "");
                if (json.RootElement.TryGetProperty("Operation", out _))
                {
                    op = JsonSerializer.Deserialize<EventPayload>(m.RequestPayload!)!.Operation;
                    Require(op is not null && op.Kind == m.Kind && op.Date == m.Date && op.AssetId == position!.AssetId, message);
                }
                else
                {
                    var dto = JsonSerializer.Deserialize<SaveCorporateEventDto>(m.RequestPayload!);
                    Require(dto is not null && dto.Kind == m.Kind && dto.Date == m.Date && dto.PositionId == effect.PositionId, message);
                    Require(dto!.Quantity == (m.Kind == "bonus" ? effect.AfterQuantity - effect.BeforeQuantity : effect.AfterQuantity), message);
                    op = new(m.Kind, m.Date, AssetId: position!.AssetId, Quantity: m.Kind == "bonus" ? dto.Quantity : 0,
                        RatioNumerator: m.Kind == "bonus" ? 0 : effect.AfterQuantity,
                        RatioDenominator: m.Kind == "bonus" ? 0 : effect.BeforeQuantity, Cost: dto.Cost);
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            { throw new InputValidationException(message); }
            Require(op!.Cost == effect.AfterCost - effect.BeforeCost && m.Amount == op.Cost && op.Amount == 0 &&
                op.UnitPrice == 0 && op.Fees == 0 && !op.CashAssetId.HasValue && !op.DestinationId.HasValue && !op.BaseAmount.HasValue, message);
            Money(effect.BeforeCost, true); Money(effect.AfterCost, true); Money(op.Cost, true);
            if (m.Kind == "bonus")
            {
                Require(op.RatioNumerator == 0 && op.RatioDenominator == 0 && op.Quantity == effect.AfterQuantity - effect.BeforeQuantity, message);
                Quantity(op.Quantity);
            }
            else
            {
                Quantity(op.RatioNumerator); Quantity(op.RatioDenominator);
                Require(op.Cost == 0 && op.Quantity == 0 && (m.Kind == "split" ? effect.AfterQuantity > effect.BeforeQuantity : effect.AfterQuantity < effect.BeforeQuantity), message);
                Require(new System.Numerics.BigInteger(effect.BeforeQuantity * 1000000m) * new System.Numerics.BigInteger(op.RatioNumerator * 1000000m)
                    == new System.Numerics.BigInteger(effect.AfterQuantity * 1000000m) * new System.Numerics.BigInteger(op.RatioDenominator * 1000000m), message);
            }
            return op;
        }

        private static CorrectionStep ReplayPositionAdjustment(CorrectionState s, FinancialMovement old,
            Dictionary<int, PortfolioAsset> positions, HashSet<int> pids)
        {
            var message = $"O ajuste de posição #{old.Id} não contém efeitos suficientes ou seus campos não coincidem com a auditoria.";
            Require(old.Effects.Count > 0 && old.Effects.All(e => e.PositionId.HasValue && !e.CashAssetId.HasValue)
                && old.Effects.Select(e => e.PositionId).Distinct().Count() == old.Effects.Count
                && old.Amount == 0 && old.Trade is null && old.IncomeReceipt is null && !s.Flows.Any(f => f.MovementId == old.Id), message);
            var targets = old.Effects.Select(e => new PositionTarget(e.PositionId!.Value,
                e.BeforeQuantity != e.AfterQuantity ? e.AfterQuantity : null,
                e.BeforeCost != e.AfterCost ? e.AfterCost : null,
                e.BeforeValue != e.AfterValue ? e.AfterValue : null,
                e.BeforeIncome != e.AfterIncome ? e.AfterIncome : null)).ToArray();
            if (!string.IsNullOrWhiteSpace(old.RequestPayload))
            {
                try
                {
                    using var json = JsonDocument.Parse(old.RequestPayload);
                    if (json.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        var quotes = JsonSerializer.Deserialize<InvestmentTracker.Application.Portfolios.QuoteUpdate[]>(old.RequestPayload);
                        Require(quotes is not null && quotes.Length == targets.Length && quotes.All(q => q is not null)
                            && quotes.Select(q => q.PositionId).Distinct().Count() == quotes.Length, message);
                        targets = quotes!.Select(q =>
                        {
                            var effect = old.Effects.SingleOrDefault(e => e.PositionId == q.PositionId);
                            Require(effect is not null && q.ExpectedQuantity == effect.BeforeQuantity && q.ExpectedValue == effect.BeforeValue
                                && q.CurrencyCode == s.Currencies.Single(c => c.Id == effect.CurrencyId).Code, message);
                            return new PositionTarget(q.PositionId, null, null, null, null, q.UnitPrice, q.Source, q.QuoteDate);
                        }).ToArray();
                    }
                    else if (json.RootElement.TryGetProperty("PositionTargets", out _))
                    {
                        var payload = JsonSerializer.Deserialize<PositionPayload>(old.RequestPayload);
                        Require(payload?.PositionTargets is not null && payload.PositionTargets.Count == targets.Length, message);
                        targets = payload!.PositionTargets.ToArray();
                    }
                    else throw new InputValidationException(message);
                }
                catch (Exception ex) when (ex is JsonException or InvalidOperationException)
                { throw new InputValidationException(message); }
            }
            Require(targets.All(t => t is not null) && targets.Select(t => t.PositionId).Distinct().Count() == targets.Length, message);
            var movement = new FinancialMovement { PortfolioId = s.Portfolio.Id, RequestId = Guid.NewGuid(), Kind = old.Kind,
                Date = old.Date, CurrencyCode = old.CurrencyCode, Description = old.Description, ReplacesMovementId = old.Id,
                RequestPayload = JsonSerializer.Serialize(new PositionPayload(targets)) };
            foreach (var target in targets)
            {
                var audit = old.Effects.SingleOrDefault(e => e.PositionId == target.PositionId)
                    ?? throw new InputValidationException(message);
                Require(positions.ContainsKey(target.PositionId), message);
                Require(!target.UnitPrice.HasValue || (target.Quantity is null && target.Cost is null && target.Value is null && target.Income is null), message);
                var auditedValue = target.UnitPrice.HasValue
                    ? InvestmentTracker.Application.Portfolios.QuoteBatchService.CalculateValue(audit.BeforeQuantity, target.UnitPrice.Value)
                    : target.Value ?? audit.BeforeValue;
                Require((target.Quantity ?? audit.BeforeQuantity) == audit.AfterQuantity && (target.Cost ?? audit.BeforeCost) == audit.AfterCost
                    && auditedValue == audit.AfterValue && (target.Income ?? audit.BeforeIncome) == audit.AfterIncome, message);
                var p = positions[target.PositionId]; var before = Copy(p);
                p.Quantity = target.Quantity ?? p.Quantity; p.InvestedAmount = target.Cost ?? p.InvestedAmount;
                p.CurrentValue = target.UnitPrice.HasValue ? InvestmentTracker.Application.Portfolios.QuoteBatchService.CalculateValue(p.Quantity, target.UnitPrice.Value)
                    : target.Value ?? p.CurrentValue;
                p.Income = target.Income ?? p.Income;
                Require(p.Quantity >= 0 && p.Quantity <= MaxQuantity && decimal.Round(p.Quantity, 6) == p.Quantity, message);
                Money(p.InvestedAmount, true); Money(p.CurrentValue, true); Money(p.Income, true);
                pids.Add(p.Id);
                movement.Effects.Add(new MovementEffect { PositionId = p.Id, CurrencyId = audit.CurrencyId, Name = audit.Name,
                    BeforeQuantity = before.Quantity, AfterQuantity = p.Quantity, BeforeCost = before.InvestedAmount, AfterCost = p.InvestedAmount,
                    BeforeValue = before.CurrentValue, AfterValue = p.CurrentValue, BeforeIncome = before.Income, AfterIncome = p.Income });
            }
            return new(movement, []);
        }

        private static decimal Equivalent(decimal amount, string currency, string target, decimal? equivalent)
        {
            if (currency == target) { Require(!equivalent.HasValue || equivalent == amount, "Na mesma moeda, o equivalente deve ser igual ao total."); return amount; }
            Require(equivalent.HasValue, $"Informe o equivalente histórico em {target}; a correção não usa câmbio atual.");
            Money(equivalent!.Value); return equivalent.Value;
        }
    }
}
