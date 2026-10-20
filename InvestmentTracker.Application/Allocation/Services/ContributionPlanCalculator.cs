using System.Globalization;
using System.Text.RegularExpressions;
using InvestmentTracker.Application.Allocation.Dtos;
using InvestmentTracker.Application.Portfolios.Dtos;
using InvestmentTracker.Application.Common.Exceptions;

namespace InvestmentTracker.Application.Allocation.Services
{
    public static class ContributionPlanCalculator
    {
        public static ContributionPlanDto Calculate(DashboardDto dashboard, decimal amount)
        {
            ContributionCalculator.ValidateAmount(amount);
            if (!dashboard.Summary.ConversionAvailable)
                throw new ResourceConflictException("O câmbio está indisponível. Atualize a carteira antes de simular.");
            var category = dashboard.Allocation.CategoryTargetsConfigured
                ? Enrich(ContributionCalculator.Calculate(dashboard, new("category", amount)), dashboard.Summary.Positions) : null;
            var sector = dashboard.Allocation.SectorTargetsConfigured
                ? Enrich(ContributionCalculator.Calculate(dashboard, new("sector", amount)), dashboard.Summary.Positions) : null;
            return new(dashboard, category, sector,
                category is not null && sector is not null ? Build(category, sector, dashboard.Summary.Positions) : null);
        }

        private static ContributionAnalysisDto Enrich(ContributionAnalysisDto analysis, IReadOnlyList<PositionDto> positions)
            => analysis with { Rows = analysis.Rows.Select(row => row with
            {
                Currencies = CurrencyOptions(row.SuggestedContribution, positions.Where(p => p.Quantity > 0 &&
                    (analysis.Dimension == "category" ? p.AssetCategoryId : p.SectorId) == row.GroupId), analysis.CurrencyCode)
            }).ToList() };

        public static IReadOnlyList<ContributionCurrencyDto> CurrencyOptions(decimal amount, IEnumerable<PositionDto> positions, string baseCurrency)
            => positions.GroupBy(p => p.CurrencyCode).OrderBy(g => g.Key, StringComparer.Ordinal).Select(group =>
            {
                var rates = group.Select(p => p.ExchangeRate).Distinct().ToList();
                decimal? rate = group.Key == baseCurrency ? 1m : rates.Count == 1 ? rates[0] : null;
                decimal? converted = null;
                if (rate > 0)
                {
                    try { converted = decimal.Round(amount / rate.Value, 2, MidpointRounding.AwayFromZero); }
                    catch (OverflowException) { /* No representable equivalent: return unavailable. */ }
                }
                return new ContributionCurrencyDto(group.Key, converted, rate,
                    string.Join(", ", group.Where(p => p.RateDate.HasValue).Select(p => p.RateDate!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Distinct()),
                    group.Any(p => p.IsStale || p.IsFallback));
            }).ToList();

        public static bool IsEligible(PositionDto p)
            => p.Quantity > 0 && (!Regex.IsMatch($"{p.AssetCategoryName} {p.AssetTypeName}",
                @"\b(fii|fiis|reit|reits)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                || Regex.IsMatch(p.SectorName, "imobili|real estate", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
        public static ContributionJointPlanDto Build(ContributionAnalysisDto category, ContributionAnalysisDto sector, IReadOnlyList<PositionDto> positions)
        {
            var cats = category.Rows.OrderBy(r => r.GroupId).ToList();
            var sectors = sector.Rows.OrderBy(r => r.GroupId).ToList();
            var size = cats.Count + sectors.Count + 2;
            var sink = size - 1;
            // Decimal capacities hold exact integral cents, including the maximum supported contribution.
            var capacity = new decimal[size, size];
            var eligible = positions.Where(IsEligible).ToList();
            for (var i = 0; i < cats.Count; i++)
            {
                capacity[0, i + 1] = cats[i].SuggestedContribution * 100m;
                for (var j = 0; j < sectors.Count; j++)
                    if (eligible.Any(p => p.AssetCategoryId == cats[i].GroupId && p.SectorId == sectors[j].GroupId))
                        capacity[i + 1, cats.Count + j + 1] = category.Amount * 100m;
            }
            for (var j = 0; j < sectors.Count; j++)
                capacity[cats.Count + j + 1, sink] = sectors[j].SuggestedContribution * 100m;
            decimal allocated = 0;
            while (true)
            {
                var parent = Enumerable.Repeat(-1, size).ToArray();
                var queue = new Queue<int>();
                parent[0] = 0; queue.Enqueue(0);
                while (queue.Count > 0 && parent[sink] < 0)
                {
                    var node = queue.Dequeue();
                    for (var next = 1; next < size; next++)
                        if (parent[next] < 0 && capacity[node, next] > 0)
                        { parent[next] = node; queue.Enqueue(next); }
                }
                if (parent[sink] < 0) break;
                var flow = category.Amount * 100m;
                for (var v = sink; v != 0; v = parent[v]) flow = Math.Min(flow, capacity[parent[v], v]);
                for (var v = sink; v != 0; v = parent[v])
                { capacity[parent[v], v] -= flow; capacity[v, parent[v]] += flow; }
                allocated += flow;
            }
            var rows = new List<ContributionPlanRowDto>();
            for (var i = 0; i < cats.Count; i++)
                for (var j = 0; j < sectors.Count; j++)
                {
                    var value = capacity[cats.Count + j + 1, i + 1] / 100m;
                    if (value <= 0) continue;
                    rows.Add(new($"{cats[i].GroupId}-{sectors[j].GroupId}", cats[i].Name, sectors[j].Name, value,
                        CurrencyOptions(value, eligible.Where(p => p.AssetCategoryId == cats[i].GroupId && p.SectorId == sectors[j].GroupId), category.CurrencyCode)));
                }
            return new(rows, category.Amount - allocated / 100m);
        }
    }
}
