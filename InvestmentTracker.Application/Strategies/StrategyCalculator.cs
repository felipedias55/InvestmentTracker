using InvestmentTracker.Application.Allocation.Dtos;
using InvestmentTracker.Application.Allocation.Services;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Portfolios.Dtos;

namespace InvestmentTracker.Application.Strategies
{
    public static class StrategyCalculator
    {
        public static readonly string[] Models = ["targets", "regular", "buy-hold", "income", "core-satellite", "bands", "real-estate", "custom"];
        public static void Validate(StrategyConfig c)
        {
            if (c is null || string.IsNullOrWhiteSpace(c.Name) || c.Name.Length > 100 || !Models.Contains(c.Model)
                || c.Remainder is not ("targets" or "equal" or "reserve") || c.Rules is null || c.ExcludedCategoryIds is null
                || c.Rules.Count > 100 || c.ExcludedCategoryIds.Count > 100 || c.ExcludedCategoryIds.Any(i => i <= 0)
                || c.ExcludedCategoryIds.Distinct().Count() != c.ExcludedCategoryIds.Count
                || c.Tolerance < 0 || c.Tolerance > 1 || decimal.Round(c.Tolerance, 4) != c.Tolerance)
                throw new InputValidationException("Estratégia inválida. Confira nome, regras, exclusões e tolerância.");
            if (c.Rules.Any(r => r is null || r.CategoryId <= 0 || r.Kind is not ("fixed" or "percent") || r.Value < 0
                || r.Value > (r.Kind == "percent" ? 100m : 999999999999999.99m) || decimal.Round(r.Value, 2) != r.Value)
                || c.Rules.Select(r => r.CategoryId).Distinct().Count() != c.Rules.Count
                || c.Rules.Where(r => r.Kind == "percent").Sum(r => r.Value) > 100)
                throw new InputValidationException("Use uma regra por categoria; percentuais do aporte devem somar no máximo 100%.");
            if (c.Model == "real-estate" && !c.Rules.Any(r => r.Kind == "fixed" && c.ExcludedCategoryIds.Contains(r.CategoryId)))
                throw new InputValidationException("No modelo imobiliário fixo, configure uma reserva fixa e exclua essa categoria da base do restante.");
            if (c.Model == "core-satellite" && !c.Rules.Any(r => r.Kind == "percent"))
                throw new InputValidationException("Defina percentuais do aporte para o núcleo e os complementos.");
        }

        public static ContributionPlanDto Calculate(DashboardDto original, decimal amount, StrategyConfig config)
        {
            Validate(config);
            ContributionCalculator.ValidateAmount(amount);
            if (!original.Summary.ConversionAvailable)
                throw new ResourceConflictException("Câmbio indisponível. Não é possível simular valores parciais.");
            var positions = original.Summary.Positions.Where(p => p.Quantity > 0).ToList();
            var known = original.Allocation.Categories.Select(r => r.GroupId).ToHashSet();
            if (config.Rules.Any(r => !known.Contains(r.CategoryId)) || config.ExcludedCategoryIds.Any(i => !known.Contains(i)))
                throw new InputValidationException("Uma categoria da estratégia não está disponível nesta carteira/metas.");
            var reserved = config.Rules.Select(r => (Rule: r, Amount: r.Kind == "fixed" ? r.Value : decimal.Round(amount * (r.Value / 100m), 2, MidpointRounding.ToEven))).ToList();
            if (reserved.Sum(r => r.Amount) > amount)
                throw new InputValidationException("O aporte não cobre as reservas fixas e percentuais. Ajuste as regras ou o aporte.");
            var rows = new List<ContributionPlanRowDto>();
            var notes = new List<string> { "Simulação apenas de novos aportes: nenhuma venda ou alteração de saldo é executada." };
            decimal unallocated = 0;
            void Distribute(decimal budget, IEnumerable<PositionDto> source, string prefix)
            {
                var groups = source.Where(ContributionPlanCalculator.IsEligible).GroupBy(p => (p.AssetCategoryId, p.SectorId)).OrderBy(g => g.Key.AssetCategoryId).ThenBy(g => g.Key.SectorId).ToList();
                if (groups.Count == 0) { unallocated += budget; return; }
                var cents = decimal.Floor(budget * 100 / groups.Count);
                var extra = (int)(budget * 100 - cents * groups.Count);
                for (var i = 0; i < groups.Count; i++)
                {
                    var value = (cents + (i < extra ? 1 : 0)) / 100;
                    if (value == 0) continue;
                    var first = groups[i].First();
                    rows.Add(new(prefix + "-" + first.AssetCategoryId + "-" + first.SectorId, first.AssetCategoryName, first.SectorName, value,
                        ContributionPlanCalculator.CurrencyOptions(value, groups[i], original.Summary.Portfolio.BaseCurrencyCode)));
                }
            }
            foreach (var r in reserved)
            {
                Distribute(r.Amount, positions.Where(p => p.AssetCategoryId == r.Rule.CategoryId), "rule");
                notes.Add($"Categoria #{r.Rule.CategoryId}: reserva {(r.Rule.Kind == "fixed" ? "fixa" : "percentual do aporte")}; divisão igual entre combinações de setor existentes.");
            }
            var remaining = amount - reserved.Sum(r => r.Amount);
            var eligible = positions.Where(p => !config.ExcludedCategoryIds.Contains(p.AssetCategoryId)).ToList();
            var basis = eligible.Sum(p => p.BaseCurrentValue!.Value);
            IReadOnlyList<AllocationRowDto> Rebase(IReadOnlyList<AllocationRowDto> source, Func<PositionDto, int> key)
            {
                var included = source.Where(r => eligible.Any(p => key(p) == r.GroupId)).ToList();
                var targetSum = included.Sum(r => r.TargetPercentage ?? 0);
                return included.Select(r =>
                {
                    var value = eligible.Where(p => key(p) == r.GroupId).Sum(p => p.BaseCurrentValue!.Value);
                    decimal current = basis > 0 ? value / basis : 0;
                    decimal? target = targetSum > 0 ? (r.TargetPercentage ?? 0) / targetSum : null;
                    var difference = target - current;
                    if (config.Model == "bands" && difference.HasValue && Math.Abs(difference.Value) <= config.Tolerance) difference = 0;
                    return r with { CurrentValue = value, CurrentPercentage = current, TargetPercentage = target, Difference = difference };
                }).ToList();
            }
            var categories = Rebase(original.Allocation.Categories, p => p.AssetCategoryId);
            var sectors = Rebase(original.Allocation.Sectors, p => p.SectorId);
            var allocation = original.Allocation with { Categories = categories, Sectors = sectors,
                CategoryTargetsConfigured = original.Allocation.CategoryTargetsConfigured && categories.Any(r => r.TargetPercentage > 0),
                SectorTargetsConfigured = original.Allocation.SectorTargetsConfigured && sectors.Any(r => r.TargetPercentage > 0) };
            // Only this simulated base is filtered. The stored portfolio/dashboard remains unchanged.
            var simulation = original with { Summary = original.Summary with { Positions = eligible, CurrentValue = basis }, Allocation = allocation };
            ContributionPlanDto? rest = null;
            if (remaining > 0 && config.Remainder == "targets")
            {
                if (!allocation.CategoryTargetsConfigured || !allocation.SectorTargetsConfigured)
                    throw new InputValidationException("Configure metas válidas de categoria e setor entre os grupos elegíveis, ou escolha divisão igual/manter sem distribuição.");
                rest = ContributionPlanCalculator.Calculate(simulation, remaining);
                rows.AddRange(rest.Plan!.Rows.Select(r => r with { Key = "remainder-" + r.Key }));
                unallocated += rest.Plan.Unallocated;
            }
            else if (config.Remainder == "equal") Distribute(remaining, eligible, "equal");
            else unallocated += remaining;
            notes.Add("As exclusões afetam apenas a base do restante. Metas remanescentes são normalizadas proporcionalmente para 100%; categorias reservadas só deixam de receber o restante se forem excluídas.");
            notes.Add("Percentuais das regras incidem sobre o aporte total. Moedas exibidas são alternativas de conversão, não valores adicionais a somar.");
            return new(original, rest?.Category, rest?.Sector, new(rows, unallocated))
                { StrategyNotes = notes, EligibleBase = basis };
        }
    }
}
