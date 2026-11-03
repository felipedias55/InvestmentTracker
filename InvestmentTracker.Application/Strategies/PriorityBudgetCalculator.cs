using System.Numerics;

namespace InvestmentTracker.Application.Strategies
{
    public static class PriorityBudgetCalculator
    {
        // Exact pro-rata cents within a priority tier, then stable group-ID tie breaks.
        public static IReadOnlyDictionary<int, decimal> Allocate(IReadOnlyList<(int Id, int Priority, decimal Requested)> requests, decimal amount)
        {
            var result = requests.ToDictionary(r => r.Id, _ => 0m);
            foreach (var tier in requests.GroupBy(r => r.Priority).OrderBy(g => g.Key))
            {
                var rows = tier.OrderBy(r => r.Id).ToArray(); var total = rows.Sum(r => r.Requested);
                var budget = Math.Min(amount, total);
                if (total <= budget) foreach (var row in rows) result[row.Id] = row.Requested;
                else if (budget > 0 && total > 0)
                {
                    var cents = new BigInteger(budget * 100m); var weight = new BigInteger(total * 100m);
                    var shares = rows.Select(r => { var value = BigInteger.DivRem(new BigInteger(r.Requested * 100m) * cents, weight, out var remainder);
                        return (r.Id, Value: value, Remainder: remainder); }).ToArray();
                    foreach (var row in shares) result[row.Id] = (decimal)row.Value / 100m;
                    var extra = (int)(cents - shares.Aggregate(BigInteger.Zero, (sum, r) => sum + r.Value));
                    foreach (var row in shares.OrderByDescending(r => r.Remainder).ThenBy(r => r.Id).Take(extra)) result[row.Id] += .01m;
                }
                amount -= budget;
            }
            return result;
        }
    }
}
