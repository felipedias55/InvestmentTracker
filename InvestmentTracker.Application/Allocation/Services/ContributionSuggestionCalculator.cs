using InvestmentTracker.Application.Allocation.Dtos;
using InvestmentTracker.Application.Strategies;

namespace InvestmentTracker.Application.Allocation.Services
{
    public static class ContributionSuggestionCalculator
    {
        public static ContributionJointPlanDto Enrich(ContributionJointPlanDto plan, IReadOnlyList<StrategyGroupPreference> preferences)
            => plan with { Rows = plan.Rows.Select(row =>
            {
                var preferred = preferences.SingleOrDefault(p => p.CategoryId == row.CategoryId && p.SectorId == row.SectorId)?.PreferredCurrency;
                var valid = row.Currencies.Where(c => c.Amount > 0 && c.Rate > 0).ToArray();
                if (preferred is not null)
                    return row with { SuggestedCurrency = valid.Any(c => c.Currency == preferred) ? preferred : null,
                        SuggestionReason = valid.Any(c => c.Currency == preferred)
                            ? $"Moeda {preferred} escolhida na estratégia para esta combinação de categoria e setor."
                            : $"Preferência {preferred} indisponível neste grupo ou sem equivalente positivo. Confira as alternativas antes de decidir." };
                if (valid.Length == 1)
                    return row with { SuggestedCurrency = valid[0].Currency,
                        SuggestionReason = $"{valid[0].Currency} é a única moeda elegível com equivalente positivo neste grupo." };
                return row with { SuggestedCurrency = null, SuggestionReason = valid.Length > 1
                    ? "Há mais de uma moeda elegível: escolha uma alternativa ou configure a preferência do grupo."
                    : "Sem equivalente positivo disponível. Confira câmbio, valor do aporte e moedas dos ativos." };
            }).ToArray() };
    }
}
