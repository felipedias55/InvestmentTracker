using InvestmentTracker.Application.History.Dtos;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.History.Services
{
    public static class HistoryCalculator
    {
        public static IReadOnlyList<HistoryPeriodDto> Months(IReadOnlyList<PortfolioSnapshot> snapshots,
            IReadOnlyList<PortfolioCashFlow> flows, DateOnly today, string currency)
        {
            var first = snapshots.Select(s => s.Month).Concat(flows.Select(f => new DateOnly(f.Date.Year, f.Date.Month, 1)))
                .Append(new DateOnly(today.Year, today.Month, 1)).Min();
            var result = new List<HistoryPeriodDto>();
            for (var month = first; month <= today; month = month.AddMonths(1))
            {
                var snapshot = snapshots.SingleOrDefault(s => s.Month == month);
                var previous = snapshots.SingleOrDefault(s => s.Month == month.AddMonths(-1));
                result.Add(Build(month.ToString("yyyy-MM"), snapshot, previous,
                    flows.Where(f => f.Date.Year == month.Year && f.Date.Month == month.Month).ToList(), flows, currency));
            }
            return result;
        }

        public static IReadOnlyList<HistoryPeriodDto> Years(IReadOnlyList<PortfolioSnapshot> snapshots,
            IReadOnlyList<PortfolioCashFlow> flows, DateOnly today, string currency)
        {
            var first = snapshots.Select(s => s.Month.Year).Concat(flows.Select(f => f.Date.Year)).Append(today.Year).Min();
            return Enumerable.Range(first, today.Year - first + 1).Select(year => Build(year.ToString(),
                snapshots.Where(s => s.Month.Year == year).MaxBy(s => s.Month),
                snapshots.Where(s => s.Month.Year == year - 1).MaxBy(s => s.Month),
                flows.Where(f => f.Date.Year == year).ToList(), flows, currency)).ToList();
        }

        private static HistoryPeriodDto Build(string period, PortfolioSnapshot? snapshot, PortfolioSnapshot? previous,
            IReadOnlyList<PortfolioCashFlow> periodFlows, IReadOnlyList<PortfolioCashFlow> allFlows, string currentCurrency)
        {
            var currency = snapshot?.BaseCurrencyCode ?? currentCurrency;
            var contributions = Sum(periodFlows.Where(f => f.Kind == "contribution"), currency);
            var withdrawals = Sum(periodFlows.Where(f => f.Kind == "withdrawal"), currency);
            decimal? change = null, netFlows = null;
            string? note = null;
            if (snapshot is null) note = "Sem fotografia neste período.";
            else if (previous is null) note = "Sem fotografia no período anterior para comparar.";
            else if (previous.BaseCurrencyCode != currency) note = "Moedas-base diferentes entre as fotografias.";
            else
            {
                change = snapshot.TotalWealth - previous.TotalWealth;
                // A photograph includes balances through its local calendar date, inclusive.
                var interval = allFlows.Where(f => f.Date > previous.SnapshotDate && f.Date <= snapshot.SnapshotDate).ToList();
                netFlows = Sum(interval.Where(f => f.Kind == "contribution"), currency)
                    - Sum(interval.Where(f => f.Kind == "withdrawal"), currency);
                if (!netFlows.HasValue) note = "Há movimentos sem valor equivalente na moeda desta fotografia.";
            }
            return new HistoryPeriodDto(period, snapshot?.Id, snapshot?.SnapshotDate, currency,
                snapshot?.PortfolioValue, snapshot?.ExternalValue, snapshot?.TotalWealth, snapshot?.TotalIncome,
                contributions, withdrawals, change, netFlows, change - netFlows,
                previous?.SnapshotDate, note, snapshot?.HasStaleRates ?? false, snapshot?.HasFallbackRates ?? false);
        }

        private static decimal? Sum(IEnumerable<PortfolioCashFlow> flows, string currency)
        {
            decimal total = 0m;
            foreach (var flow in flows)
            {
                decimal? value = flow.Currency.Code == currency ? flow.Amount
                    : flow.BaseCurrencyCode == currency ? flow.BaseAmount : null;
                if (!value.HasValue) return null;
                total += value.Value;
            }
            return total;
        }
    }
}
