using System.Text.Json.Serialization;
using InvestmentTracker.Application.Allocation.Dtos;

namespace InvestmentTracker.Application.Strategies
{
    public sealed record RebalancingBandStatusDto(string Dimension, int GroupId, string Name, string State, bool HasOverride,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal TolerancePoints,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? CurrentPercentage,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? TargetPercentage,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? LowerPercentage,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? UpperPercentage)
    {
        public string Reason { get; init; } = "";
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] public decimal RequestedRemainder { get; init; }
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] public decimal SuggestedRemainder { get; init; }
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] public decimal ReservedContribution { get; init; }
    }
    public static class RebalancingBandCalculator
    {
        public static RebalancingBandStatusDto Evaluate(string dimension, AllocationRowDto row, decimal tolerance, bool hasOverride, string? unavailable = null)
        {
            var current = unavailable is null ? row.CurrentPercentage : null;
            var target = unavailable is null ? row.TargetPercentage : null;
            var difference = target - current;
            var state = unavailable ?? (!difference.HasValue ? "no-target" : difference > tolerance ? "below" : difference < -tolerance ? "above" : "within");
            static decimal? Percent(decimal? value) => value.HasValue ? decimal.Round(value.Value * 100m, 4) : null;
            return new(dimension, row.GroupId, row.Name, state, hasOverride, tolerance * 100m,
                Percent(current), Percent(target), Percent(target.HasValue ? Math.Max(0, target.Value - tolerance) : null),
                Percent(target.HasValue ? Math.Min(1, target.Value + tolerance) : null));
        }
        public static IReadOnlyList<RebalancingBandStatusDto> Explain(IReadOnlyList<RebalancingBandStatusDto> bands,
            ContributionJointPlanDto requested, ContributionJointPlanDto final, decimal remaining)
        {
            return bands.Select(b =>
            {
                bool Matches(ContributionPlanRowDto r) => (b.Dimension == "category" ? r.CategoryId : r.SectorId) == b.GroupId;
                var original = requested.Rows.Where(r => Matches(r) && r.Stage != "reservation").Sum(r => r.Amount);
                var suggested = final.Rows.Where(r => Matches(r) && r.Stage != "reservation").Sum(r => r.Amount);
                var reserved = final.Rows.Where(r => Matches(r) && r.Stage == "reservation").Sum(r => r.Amount);
                var reason = b.State switch
                {
                    "excluded" => "Categoria excluída da base do restante; reservas explícitas têm regras próprias.",
                    "no-eligible" => "Sem posições elegíveis para o restante após exclusões, bloqueios e classificação dos instrumentos.",
                    "no-target" => "Sem meta válida para calcular a faixa nesta base.",
                    "within" => "Dentro da faixa, inclusive nas bordas: não recebe peso no restante por metas.",
                    "above" => "Acima da faixa: não recebe peso no restante por metas; não há sugestão de venda.",
                    _ when remaining == 0 => "Abaixo da faixa, mas não há orçamento restante após as reservas.",
                    _ when original == 0 => "Abaixo da faixa, mas a combinação com a outra dimensão, as posições disponíveis ou os centavos não produziram valor no plano conjunto.",
                    _ when suggested < original => "Abaixo da faixa: os limites de concentração reduziram a sugestão original do restante.",
                    _ => "Abaixo da faixa: o peso usa o desvio integral até a meta. O plano conjunto respeita categorias, setores e posições elegíveis."
                };
                return b with { Reason = reason, RequestedRemainder = original, SuggestedRemainder = suggested, ReservedContribution = reserved };
            }).ToArray();
        }
    }
}
