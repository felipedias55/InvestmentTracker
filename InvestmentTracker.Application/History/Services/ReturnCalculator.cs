using InvestmentTracker.Application.History.Dtos;
using InvestmentTracker.Application.Income;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.History.Services
{
    public static class ReturnCalculator
    {
        public static ReturnMetricsDto Calculate(PortfolioSnapshot? start, PortfolioSnapshot? end,
            IReadOnlyList<PortfolioCashFlow> flows, IReadOnlyList<IncomeEvent>? income)
        {
            ReturnMetricsDto Unavailable(string reason) => new(null, null, reason);
            if (start is null || end is null) return Unavailable("São necessárias duas fotografias para comparar.");
            if (start.IsOutdated || end.IsOutdated || start.IsReopened || end.IsReopened)
                return Unavailable("Fotografia desatualizada ou reaberta: revise o fechamento.");
            if (start.BaseCurrencyCode != end.BaseCurrencyCode)
                return Unavailable("As fotografias usam moedas-base diferentes.");
            var days = end.SnapshotDate.DayNumber - start.SnapshotDate.DayNumber;
            if (days <= 0 || start.TotalWealth <= 0 || end.TotalWealth < 0)
                return Unavailable("O intervalo deve ser positivo, com patrimônio inicial maior que zero e final não negativo.");
            if (income is null) return Unavailable("Histórico de proventos indisponível.");
            var dated = new List<(DateOnly Date, decimal Amount)>();
            bool Within(DateOnly date) => date > start.SnapshotDate && date <= end.SnapshotDate;
            foreach (var flow in flows.Where(f => Within(f.Date)))
            {
                var amount = flow.Currency.Code == end.BaseCurrencyCode ? flow.Amount
                    : flow.BaseCurrencyCode == end.BaseCurrencyCode ? flow.BaseAmount : null;
                if (amount is null) return Unavailable("Há movimentos sem equivalente histórico na moeda das fotografias.");
                if (flow.Kind is not ("contribution" or "withdrawal"))
                    return Unavailable("Há movimentos com classificação desconhecida.");
                dated.Add((flow.Date, amount.Value * (flow.Kind == "contribution" ? 1 : -1) * (flow.IsReversal ? -1 : 1)));
            }
            foreach (var item in income.Where(i => Within(i.Date)))
            {
                var amount = item.InCurrency(end.BaseCurrencyCode);
                if (amount is null) return Unavailable("Há proventos sem equivalente histórico na moeda das fotografias.");
                // Retained income already belongs to the ending valuation. Distributed income leaves the portfolio.
                if (!item.Retained) dated.Add((item.Date, amount.Value * (item.IsReversal ? 1 : -1)));
            }
            var net = dated.Sum(x => x.Amount);
            var capital = start.TotalWealth + dated.Sum(x => x.Amount * (end.SnapshotDate.DayNumber - x.Date.DayNumber) / days);
            decimal? dietz = capital > 0 ? (end.TotalWealth - start.TotalWealth - net) / capital : null;
            var investor = dated.Select(x => (x.Date, Amount: -x.Amount))
                .Append((Date: start.SnapshotDate, Amount: -start.TotalWealth)).Append((Date: end.SnapshotDate, Amount: end.TotalWealth))
                .GroupBy(x => x.Date).OrderBy(g => g.Key)
                .Select(g => (Date: g.Key, Amount: g.Sum(x => x.Amount))).Where(x => x.Amount != 0).ToList();
            var xirr = Solve(investor, start.SnapshotDate);
            var notes = new List<string>();
            if (dietz is null) notes.Add("Dietz indisponível: capital ponderado nulo ou negativo.");
            if (xirr is null) notes.Add("XIRR indisponível: fluxos não convencionais, ausência de raiz única garantida ou taxa fora do limite numérico.");
            if (days < 365) notes.Add("XIRR anualiza um intervalo inferior a um ano; não é uma previsão.");
            if (start.HasStaleRates || end.HasStaleRates || start.HasFallbackRates || end.HasFallbackRates)
                notes.Add("As fotografias usam câmbio anterior ou alternativo.");
            return new(dietz, xirr, notes.Count == 0 ? null : string.Join(" ", notes));
        }

        private static decimal? Solve(List<(DateOnly Date, decimal Amount)> values, DateOnly start)
        {
            // One negative-to-positive sign change guarantees uniqueness. Avoid selecting an arbitrary IRR root.
            var signs = values.Select(x => Math.Sign(x.Amount)).ToArray();
            if (signs.Length < 2 || signs[0] != -1 || signs[^1] != 1
                || signs.Zip(signs.Skip(1)).Count(x => x.First != x.Second) != 1) return null;
            var scale = (double)values.Max(x => Math.Abs(x.Amount));
            double Npv(double logRate)
            {
                var offset = values.Max(x => -logRate * (x.Date.DayNumber - start.DayNumber) / 365d);
                return values.Sum(x => (double)x.Amount / scale
                    * Math.Exp(-logRate * (x.Date.DayNumber - start.DayNumber) / 365d - offset));
            }
            // Solve in log(1+r), with a bounded rate domain (-99.9999%, 1,000,000%).
            double low = Math.Log(0.000001), high = Math.Log(10001);
            if (!(Npv(low) > 0 && Npv(high) < 0)) return null;
            for (var i = 0; i < 160; i++)
            {
                var middle = (low + high) / 2;
                if (Npv(middle) > 0) low = middle; else high = middle;
            }
            var rate = Math.Exp((low + high) / 2) - 1;
            return double.IsFinite(rate) ? (decimal)rate : null;
        }
    }
}
