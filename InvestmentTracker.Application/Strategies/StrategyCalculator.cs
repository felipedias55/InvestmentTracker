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
            var combinations = c.ExcludedCombinations ?? [];
            if (combinations.Count > 500 || combinations.Any(g => g is null || g.CategoryId <= 0 || g.SectorId <= 0)
                || combinations.Distinct().Count() != combinations.Count)
                throw new InputValidationException("Informe até 500 combinações distintas de categoria e setor existentes.");
            var limits = c.ConcentrationLimits ?? [];
            if (limits.Count > 200 || limits.Any(l => l is null || l.Dimension is not ("category" or "sector") || l.GroupId <= 0
                    || l.MaximumPercentage < 0 || l.MaximumPercentage > 100 || decimal.Round(l.MaximumPercentage, 2) != l.MaximumPercentage)
                || limits.Select(l => (l.Dimension, l.GroupId)).Distinct().Count() != limits.Count)
                throw new InputValidationException("Informe até 200 limites distintos por categoria/setor, entre 0% e 100%, com até duas casas decimais.");
            var preferences = c.GroupPreferences ?? [];
            if (preferences.Count > 500 || preferences.Any(p => p is null || p.CategoryId <= 0 || p.SectorId <= 0
                    || p.Priority is < 1 or > 1000 || p.PreferredCurrency is not (null or "BRL" or "USD"))
                || preferences.Select(p => (p.CategoryId, p.SectorId)).Distinct().Count() != preferences.Count)
                throw new InputValidationException("Preferências inválidas: use até 500 combinações distintas, prioridade de 1 a 1000 e moeda BRL/USD ou sem preferência.");
            if (c.Rules.Any(r => r is null || r.CategoryId <= 0 || r.Kind is not ("fixed" or "percent") || r.Value < 0
                || r.Value > (r.Kind == "percent" ? 100m : 999999999999999.99m) || decimal.Round(r.Value, 2) != r.Value || r.Priority is < 1 or > 1000)
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
            var combinations = (config.ExcludedCombinations ?? []).ToHashSet();
            var preferences = (config.GroupPreferences ?? []).ToDictionary(p => (p.CategoryId, p.SectorId));
            var positions = original.Summary.Positions.Where(p => ContributionPlanCalculator.IsEligible(p)
                && !combinations.Contains(new(p.AssetCategoryId, p.SectorId))).ToList();
            var known = original.Allocation.Categories.Select(r => r.GroupId).ToHashSet();
            var knownSectors = original.Allocation.Sectors.Select(r => r.GroupId).ToHashSet();
            if ((config.ConcentrationLimits ?? []).Any(l => !(l.Dimension == "category" ? known : knownSectors).Contains(l.GroupId)))
                throw new InputValidationException("Uma categoria ou setor dos limites não está disponível nesta carteira/metas.");
            if (config.Rules.Any(r => !known.Contains(r.CategoryId)) || config.ExcludedCategoryIds.Any(i => !known.Contains(i))
                || combinations.Any(g => !known.Contains(g.CategoryId) || !knownSectors.Contains(g.SectorId))
                || preferences.Values.Any(g => !known.Contains(g.CategoryId) || !knownSectors.Contains(g.SectorId)))
                throw new InputValidationException("Uma categoria ou setor da estratégia não está disponível nesta carteira/metas.");
            var reserved = config.Rules.Select(r => (Rule: r, Amount: r.Kind == "fixed" ? r.Value : decimal.Round(amount * (r.Value / 100m), 2, MidpointRounding.ToEven))).ToList();
            if (!config.UsePriorities && reserved.Sum(r => r.Amount) > amount)
                throw new InputValidationException("O aporte não cobre as reservas fixas e percentuais. Ajuste as regras ou o aporte.");
            var budgets = config.UsePriorities
                ? PriorityBudgetCalculator.Allocate(reserved.Select(r => (r.Rule.CategoryId, r.Rule.Priority, r.Amount)).ToArray(), amount)
                : reserved.ToDictionary(r => r.Rule.CategoryId, r => r.Amount);
            var rows = new List<ContributionPlanRowDto>();
            var notes = new List<string> { "Simulação apenas de novos aportes: nenhuma venda ou alteração de saldo é executada." };
            if (config.UsePriorities) notes.Add("Prioridades ativas: números menores vêm primeiro. Reservas precedem o restante; empates de orçamento dividem o valor proporcionalmente à demanda. Limites e elegibilidade continuam obrigatórios.");
            if (config.UsePriorities) notes.Add("Sob tetos, o cálculo mantém o volume viável da distribuição e favorece as prioridades nos desempates. Prioridade de grupo ordena sugestões e centavos de empate, sem substituir a divisão igual ou os pesos das metas.");
            if (combinations.Count > 0) notes.Add("Combinações bloqueadas não recebem reservas nem restante e não compõem a base elegível; as posições continuam no patrimônio e nos gráficos.");
            if (original.Summary.Positions.Any(p => p.Quantity > 0 && !ContributionPlanCalculator.IsEligible(p)))
                notes.Add("Há posições inelegíveis: revise a classificação em Tipos de ativo e a marcação imobiliária em Setores. FII/REIT só recebe aporte em setor explicitamente imobiliário; tipos sem classificação ficam fora da distribuição.");
            decimal unallocated = 0;
            void Distribute(decimal budget, IEnumerable<PositionDto> source, string prefix, int rulePriority = 100)
            {
                var groups = source.Where(ContributionPlanCalculator.IsEligible).GroupBy(p => (p.AssetCategoryId, p.SectorId))
                    .OrderBy(g => config.UsePriorities ? preferences.GetValueOrDefault(g.Key)?.Priority ?? 100 : 100)
                    .ThenBy(g => g.Key.AssetCategoryId).ThenBy(g => g.Key.SectorId).ToList();
                if (groups.Count == 0) { unallocated += budget; return; }
                var cents = decimal.Floor(budget * 100 / groups.Count);
                var extra = (int)(budget * 100 - cents * groups.Count);
                for (var i = 0; i < groups.Count; i++)
                {
                    var value = (cents + (i < extra ? 1 : 0)) / 100;
                    if (value == 0) continue;
                    var first = groups[i].First();
                    rows.Add(new(prefix + "-" + first.AssetCategoryId + "-" + first.SectorId, first.AssetCategoryName, first.SectorName, value,
                        ContributionPlanCalculator.CurrencyOptions(value, groups[i], original.Summary.Portfolio.BaseCurrencyCode))
                        { CategoryId = first.AssetCategoryId, SectorId = first.SectorId, Stage = prefix == "rule" ? "reservation" : "remainder", RulePriority = rulePriority,
                            GroupPriority = preferences.GetValueOrDefault((first.AssetCategoryId, first.SectorId))?.Priority ?? 100 });
                }
            }
            foreach (var r in reserved.OrderBy(r => config.UsePriorities ? r.Rule.Priority : 100).ThenBy(r => r.Rule.CategoryId))
            {
                Distribute(budgets[r.Rule.CategoryId], positions.Where(p => p.AssetCategoryId == r.Rule.CategoryId), "rule", r.Rule.Priority);
                notes.Add($"Categoria #{r.Rule.CategoryId}: reserva {(r.Rule.Kind == "fixed" ? "fixa" : "percentual do aporte")}; divisão igual entre combinações de setor existentes.");
            }
            var remaining = amount - budgets.Values.Sum();
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
            if (remaining > 0 && config.Remainder == "targets" && eligible.Count == 0)
            {
                unallocated += remaining;
                notes.Add("Não há posições elegíveis para o restante; o valor permanece sem distribuição.");
            }
            else if (remaining > 0 && config.Remainder == "targets")
            {
                if (!allocation.CategoryTargetsConfigured || !allocation.SectorTargetsConfigured)
                    throw new InputValidationException("Configure metas válidas de categoria e setor entre os grupos elegíveis, ou escolha divisão igual/manter sem distribuição.");
                rest = ContributionPlanCalculator.Calculate(simulation, remaining);
                rows.AddRange(rest.Plan!.Rows.Select(r => r with { Key = "remainder-" + r.Key,
                    GroupPriority = preferences.GetValueOrDefault((r.CategoryId, r.SectorId))?.Priority ?? 100 }));
                unallocated += rest.Plan.Unallocated;
            }
            else if (config.Remainder == "equal") Distribute(remaining, eligible, "equal");
            else unallocated += remaining;
            notes.Add("As exclusões afetam apenas a base do restante. Metas remanescentes são normalizadas proporcionalmente para 100%; categorias reservadas só deixam de receber o restante se forem excluídas.");
            notes.Add("Percentuais das regras incidem sobre o aporte total. Moedas exibidas são alternativas de conversão, não valores adicionais a somar.");
            var plan = new ContributionJointPlanDto(rows, unallocated);
            var limited = (config.ConcentrationLimits?.Count ?? 0) > 0;
            var result = limited ? ConcentrationCalculator.Apply(original, plan, config.ConcentrationLimits!, positions, config.UsePriorities) : null;
            if (limited)
            {
                notes.Add("Os tetos usam o valor de todas as posições da carteira na moeda-base, incluindo grupos excluídos, mais somente o aporte efetivamente distribuído. Patrimônio externo e saldo não distribuído não diluem a concentração.");
                notes.Add("Limites prevalecem sobre reservas e metas. O plano final substitui as análises separadas do restante; valores reduzidos ficam sem distribuição, sem vendas nem aumento das sugestões originais de outras combinações.");
                if (result!.Conservative) notes.Add("O ajuste dos tetos usou uma solução conservadora. Pode haver outra distribuição possível; os limites não prometem maximizar o valor investido.");
            }
            var final = ContributionSuggestionCalculator.Enrich(result?.Plan ?? plan, config.GroupPreferences ?? []);
            if (config.UsePriorities) final = final with { Rows = final.Rows.OrderBy(r => r.Stage == "reservation" ? 0 : 1)
                .ThenBy(r => r.RulePriority).ThenBy(r => r.GroupPriority).ThenBy(r => r.CategoryId).ThenBy(r => r.SectorId).ToArray() };
            return new(original, limited ? null : rest?.Category, limited ? null : rest?.Sector, final)
                { StrategyNotes = notes, EligibleBase = basis, Concentration = result?.Statuses ?? [], PrioritiesApplied = config.UsePriorities,
                    RuleFunding = reserved.Select(r => new RuleFundingDto(r.Rule.CategoryId, original.Allocation.Categories.Single(c => c.GroupId == r.Rule.CategoryId).Name,
                        r.Rule.Priority, r.Amount, budgets[r.Rule.CategoryId], final.Rows.Where(row => row.Stage == "reservation" && row.CategoryId == r.Rule.CategoryId).Sum(row => row.Amount))).ToArray() };
        }
    }
}
