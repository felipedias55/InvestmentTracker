using System.Numerics;
using InvestmentTracker.Application.Allocation.Dtos;
using InvestmentTracker.Application.Allocation.Services;
using InvestmentTracker.Application.Portfolios.Dtos;

namespace InvestmentTracker.Application.Strategies
{
    public sealed record ConcentrationResult(ContributionJointPlanDto Plan, IReadOnlyList<ConcentrationStatusDto> Statuses, bool Conservative);

    public static class ConcentrationCalculator
    {
        private static readonly BigInteger Cent = BigInteger.Pow(10, 26);
        // Preserve every decimal digit of converted holdings when deriving integer-cent capacities.
        private static BigInteger Units(decimal value)
        {
            var bits = decimal.GetBits(value);
            var integer = (new BigInteger((uint)bits[2]) << 64) + (new BigInteger((uint)bits[1]) << 32) + (uint)bits[0];
            return integer * BigInteger.Pow(10, 28 - ((bits[3] >> 16) & 255)) * (bits[3] < 0 ? -1 : 1);
        }

        public static ConcentrationResult Apply(DashboardDto dashboard, ContributionJointPlanDto requested,
            IReadOnlyList<ConcentrationLimit> limits, IReadOnlyList<PositionDto> eligible, bool usePriorities = false)
        {
            var holdings = dashboard.Summary.Positions;
            var basis = holdings.Sum(p => p.BaseCurrentValue!.Value);
            var budget = requested.Rows.Sum(r => r.Amount);
            var pairs = requested.Rows.GroupBy(r => (r.CategoryId, r.SectorId)).ToDictionary(g => g.Key, g => g.Sum(r => r.Amount) * 100m);
            var byGroup = limits.ToDictionary(l => (l.Dimension, l.GroupId));
            decimal Current(ConcentrationLimit l) => holdings.Where(p => (l.Dimension == "category" ? p.AssetCategoryId : p.SectorId) == l.GroupId)
                .Sum(p => p.BaseCurrentValue!.Value);
            var current = limits.ToDictionary(l => (l.Dimension, l.GroupId), Current);
            decimal Capacity(string dimension, int id, decimal distributed, bool ownDenominator)
            {
                if (!byGroup.TryGetValue((dimension, id), out var limit)) return budget * 100m;
                var percentage = (int)(limit.MaximumPercentage * 100m);
                if (ownDenominator && percentage == 10000) return budget * 100m;
                var headroom = percentage * (Units(basis) + Units(distributed)) - 10000 * Units(current[(dimension, id)]);
                var denominator = (ownDenominator ? 10000 - percentage : 10000) * Cent;
                return (decimal)BigInteger.Min(new BigInteger(budget * 100m), BigInteger.Max(BigInteger.Zero, headroom / denominator));
            }

            Dictionary<(int CategoryId, int SectorId), decimal> flow = [];
            var target = budget;
            var conservative = false;
            for (var iteration = 0; ; iteration++)
            {
                flow = usePriorities ? PriorityFlow(pairs, (dimension, id) => Capacity(dimension, id, target, false), requested.Rows)
                    : Flow(pairs, (dimension, id) => Capacity(dimension, id, target, false));
                var actual = flow.Values.Sum() / 100m;
                if (actual == target) break;
                if (iteration == 63)
                {
                    // A bounded fallback avoids slow convergence near 100%. Keep unconstrained rows,
                    // then give each limited group only headroom justified by its own contribution.
                    conservative = true;
                    var unrestricted = pairs.Where(p => !byGroup.ContainsKey(("category", p.Key.CategoryId)) && !byGroup.ContainsKey(("sector", p.Key.SectorId)))
                        .ToDictionary(p => p.Key, p => p.Value);
                    var guaranteed = unrestricted.Values.Sum() / 100m;
                    var restricted = pairs.Where(p => !unrestricted.ContainsKey(p.Key)).ToDictionary(p => p.Key, p => p.Value);
                    flow = usePriorities ? PriorityFlow(restricted, (dimension, id) => Capacity(dimension, id, guaranteed, true), requested.Rows)
                        : Flow(restricted, (dimension, id) => Capacity(dimension, id, guaranteed, true));
                    foreach (var pair in unrestricted) flow.Add(pair.Key, pair.Value);
                    break;
                }
                target = actual;
            }
            var rows = new List<ContributionPlanRowDto>();
            var orderedRows = usePriorities ? requested.Rows.OrderBy(r => r.Stage == "reservation" ? 0 : 1)
                .ThenBy(r => r.RulePriority).ThenBy(r => r.GroupPriority).ThenBy(r => r.CategoryId).ThenBy(r => r.SectorId).ToArray() : requested.Rows;
            foreach (var row in orderedRows)
            {
                var key = (row.CategoryId, row.SectorId);
                var amount = Math.Min(row.Amount, flow.GetValueOrDefault(key) / 100m);
                flow[key] = flow.GetValueOrDefault(key) - amount * 100m;
                if (amount <= 0) continue;
                rows.Add(row with { Amount = amount, Currencies = ContributionPlanCalculator.CurrencyOptions(amount,
                    eligible.Where(p => p.AssetCategoryId == row.CategoryId && p.SectorId == row.SectorId), dashboard.Summary.Portfolio.BaseCurrencyCode) });
            }
            var distributedTotal = rows.Sum(r => r.Amount);
            var projectedUnits = Units(basis) + Units(distributedTotal);
            decimal? Percentage(decimal value, decimal total) => total > 0 ? decimal.Round(value / total * 100m, 4) : null;
            var statuses = limits.Select(l =>
            {
                bool Matches(ContributionPlanRowDto r) => (l.Dimension == "category" ? r.CategoryId : r.SectorId) == l.GroupId;
                var added = rows.Where(Matches).Sum(r => r.Amount);
                var value = current[(l.Dimension, l.GroupId)];
                var percentage = (int)(l.MaximumPercentage * 100m);
                var above = (Units(value) + Units(added)) * 10000 > projectedUnits * percentage;
                if (above && added > 0) throw new InvalidOperationException("O plano de concentração não passou pela verificação final.");
                var name = (l.Dimension == "category" ? dashboard.Allocation.Categories : dashboard.Allocation.Sectors)
                    .Single(g => g.GroupId == l.GroupId).Name;
                return new ConcentrationStatusDto(l.Dimension, l.GroupId, name, l.MaximumPercentage,
                    Percentage(value, basis), Percentage(value + added, basis + distributedTotal), requested.Rows.Where(Matches).Sum(r => r.Amount), added,
                    Units(value) * 10000 > Units(basis) * percentage, above);
            }).ToArray();
            return new(new(rows, requested.Unallocated + budget - distributedTotal), statuses, conservative);
        }

        private sealed class Edge(int to, int reverse, decimal capacity, BigInteger cost = default)
        {
            public int To { get; } = to;
            public int Reverse { get; } = reverse;
            public decimal Capacity { get; set; } = capacity;
            public BigInteger Cost { get; } = cost;
        }

        private static Dictionary<(int CategoryId, int SectorId), decimal> PriorityFlow(
            Dictionary<(int CategoryId, int SectorId), decimal> pairs, Func<string, int, decimal> capacity, IReadOnlyList<ContributionPlanRowDto> requested)
        {
            if (pairs.Count == 0) return [];
            var rows = requested.Where(r => pairs.ContainsKey((r.CategoryId, r.SectorId))).ToArray();
            var categories = pairs.Keys.Select(k => k.CategoryId).Distinct().Order().ToArray();
            var sectors = pairs.Keys.Select(k => k.SectorId).Distinct().Order().ToArray();
            var categoryNodes = categories.Select((id, i) => (id, node: i + 1)).ToDictionary(x => x.id, x => x.node);
            var sectorNodes = sectors.Select((id, i) => (id, node: categories.Length + i + 1)).ToDictionary(x => x.id, x => x.node);
            var sink = categories.Length + sectors.Length + 1;
            var graph = Enumerable.Range(0, sink + 1).Select(_ => new List<Edge>()).ToArray();
            static (int Stage, int Rule, int Group) Rank(ContributionPlanRowDto row) => (row.Stage == "reservation" ? 0 : 1, row.RulePriority, row.GroupPriority);
            var ranks = rows.Select(Rank).Distinct().OrderBy(r => r.Stage).ThenBy(r => r.Rule).ThenBy(r => r.Group).ToArray();
            // One cent of a higher tier outweighs all lower tiers. No floating-point cost weights.
            var radix = new BigInteger(rows.Sum(r => r.Amount) * 100m) + 1;
            var costs = ranks.Select((rank, i) => (rank, cost: -BigInteger.Pow(radix, ranks.Length - i - 1))).ToDictionary(r => r.rank, r => r.cost);
            Edge Add(int from, int to, decimal amount, BigInteger cost)
            {
                var edge = new Edge(to, graph[to].Count, amount, cost);
                graph[from].Add(edge); graph[to].Add(new Edge(from, graph[from].Count - 1, 0, -cost)); return edge;
            }
            foreach (var id in categories) Add(0, categoryNodes[id], capacity("category", id), 0);
            var tracked = rows.Select(row => (Row: row, Edge: Add(categoryNodes[row.CategoryId], sectorNodes[row.SectorId], row.Amount * 100m, costs[Rank(row)]))).ToArray();
            foreach (var id in sectors) Add(sectorNodes[id], sink, capacity("sector", id), 0);
            while (true)
            {
                var distance = new BigInteger?[graph.Length]; var parent = Enumerable.Repeat(-1, graph.Length).ToArray(); var indexes = new int[graph.Length];
                distance[0] = BigInteger.Zero;
                for (var pass = 0; pass < graph.Length - 1; pass++)
                {
                    var changed = false;
                    for (var node = 0; node < graph.Length; node++)
                    {
                        if (!distance[node].HasValue) continue;
                        for (var i = 0; i < graph[node].Count; i++)
                        {
                            var edge = graph[node][i]; if (edge.Capacity <= 0) continue;
                            var next = distance[node]!.Value + edge.Cost;
                            if (distance[edge.To].HasValue && distance[edge.To]!.Value <= next) continue;
                            distance[edge.To] = next; parent[edge.To] = node; indexes[edge.To] = i; changed = true;
                        }
                    }
                    if (!changed) break;
                }
                if (parent[sink] < 0) break;
                var amount = decimal.MaxValue;
                for (var node = sink; node != 0; node = parent[node]) amount = Math.Min(amount, graph[parent[node]][indexes[node]].Capacity);
                for (var node = sink; node != 0; node = parent[node])
                {
                    var edge = graph[parent[node]][indexes[node]]; edge.Capacity -= amount; graph[node][edge.Reverse].Capacity += amount;
                }
            }
            return tracked.GroupBy(r => (r.Row.CategoryId, r.Row.SectorId)).ToDictionary(g => g.Key, g => g.Sum(r => r.Row.Amount * 100m - r.Edge.Capacity));
        }

        private static Dictionary<(int CategoryId, int SectorId), decimal> Flow(
            Dictionary<(int CategoryId, int SectorId), decimal> pairs, Func<string, int, decimal> capacity)
        {
            var categories = pairs.Keys.Select(k => k.CategoryId).Distinct().Order().ToArray();
            var sectors = pairs.Keys.Select(k => k.SectorId).Distinct().Order().ToArray();
            var categoryNodes = categories.Select((id, i) => (id, node: i + 1)).ToDictionary(x => x.id, x => x.node);
            var sectorNodes = sectors.Select((id, i) => (id, node: categories.Length + i + 1)).ToDictionary(x => x.id, x => x.node);
            var sink = categories.Length + sectors.Length + 1;
            var graph = Enumerable.Range(0, sink + 1).Select(_ => new List<Edge>()).ToArray();
            Edge Add(int from, int to, decimal amount)
            {
                var edge = new Edge(to, graph[to].Count, amount);
                graph[from].Add(edge); graph[to].Add(new Edge(from, graph[from].Count - 1, 0));
                return edge;
            }
            foreach (var id in categories) Add(0, categoryNodes[id], capacity("category", id));
            var tracked = pairs.OrderBy(p => p.Key.CategoryId).ThenBy(p => p.Key.SectorId)
                .ToDictionary(p => p.Key, p => Add(categoryNodes[p.Key.CategoryId], sectorNodes[p.Key.SectorId], p.Value));
            foreach (var id in sectors) Add(sectorNodes[id], sink, capacity("sector", id));
            while (true)
            {
                var parent = Enumerable.Repeat(-1, graph.Length).ToArray();
                var edgeIndex = new int[graph.Length]; var queue = new Queue<int>();
                parent[0] = 0; queue.Enqueue(0);
                while (queue.Count > 0 && parent[sink] < 0)
                {
                    var node = queue.Dequeue();
                    for (var i = 0; i < graph[node].Count; i++)
                    {
                        var edge = graph[node][i];
                        if (edge.Capacity <= 0 || parent[edge.To] >= 0) continue;
                        parent[edge.To] = node; edgeIndex[edge.To] = i; queue.Enqueue(edge.To);
                    }
                }
                if (parent[sink] < 0) break;
                var amount = decimal.MaxValue;
                for (var node = sink; node != 0; node = parent[node]) amount = Math.Min(amount, graph[parent[node]][edgeIndex[node]].Capacity);
                for (var node = sink; node != 0; node = parent[node])
                {
                    var edge = graph[parent[node]][edgeIndex[node]];
                    edge.Capacity -= amount; graph[node][edge.Reverse].Capacity += amount;
                }
            }
            return tracked.ToDictionary(p => p.Key, p => pairs[p.Key] - p.Value.Capacity);
        }
    }
}
