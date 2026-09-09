using InvestmentTracker.Application.Allocation.Dtos;
using InvestmentTracker.Application.Common.Exceptions;

namespace InvestmentTracker.Application.Allocation.Services
{
    public static class ContributionCalculator
    {
        public static void ValidateAmount(decimal amount)
        {
            if (amount < 0m || amount > 999999999999999.99m || decimal.Round(amount, 2) != amount)
                throw new InputValidationException("O aporte deve ser não negativo, com até 15 inteiros e duas casas decimais.");
        }

        public static ContributionAnalysisDto Calculate(DashboardDto dashboard, ContributionRequestDto request)
        {
            ValidateAmount(request.Amount);
            var category = request.Dimension == "category";
            if (!category && request.Dimension != "sector")
                throw new InputValidationException("Escolha a análise por categoria ou por setor.");
            if (!(category ? dashboard.Allocation.CategoryTargetsConfigured : dashboard.Allocation.SectorTargetsConfigured))
                throw new InputValidationException("Configure as metas somando 100% antes de simular o aporte.");
            if (!dashboard.Summary.ConversionAvailable)
                throw new ResourceConflictException("O câmbio está indisponível. Não é possível simular usando um total parcial.");
            var rows = category ? dashboard.Allocation.Categories : dashboard.Allocation.Sectors;
            var weights = rows.Select(r => Math.Max(r.Difference!.Value, 0m)).ToArray();
            var sum = weights.Sum();
            var amounts = new decimal[rows.Count];
            if (sum > 0m)
            {
                var exact = weights.Select(w => w / sum * request.Amount).ToArray();
                for (var i = 0; i < amounts.Length; i++) amounts[i] = decimal.Floor(exact[i] * 100m) / 100m;
                // Largest remainders conserve cents; ties use the group ID for deterministic results.
                var cents = (int)decimal.Round((request.Amount - amounts.Sum()) * 100m, 0);
                foreach (var index in Enumerable.Range(0, rows.Count).Where(i => weights[i] > 0m)
                    .OrderByDescending(i => exact[i] - amounts[i]).ThenBy(i => rows[i].GroupId).Take(cents))
                    amounts[index] += 0.01m;
            }
            return new ContributionAnalysisDto(dashboard.Summary.Portfolio.BaseCurrencyCode, request.Dimension,
                request.Amount, sum, request.Amount - amounts.Sum(), dashboard.Summary.HasStaleRates,
                dashboard.Summary.HasFallbackRates, rows.Select((r, i) => new ContributionRowDto(r.GroupId, r.Name,
                    r.CurrentPercentage!.Value, r.TargetPercentage!.Value, r.Difference!.Value, weights[i], amounts[i]))
                    .OrderByDescending(r => r.SuggestedContribution).ThenBy(r => r.GroupId).ToList());
        }
    }
}
