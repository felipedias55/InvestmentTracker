using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Income;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.Planning
{
    public static class PlanningGoalCalculator
    {
        public static void Validate(SavePlanningGoal request)
        {
            var c = request.Config;
            if (request.GoalId == Guid.Empty || request.ExpectedRevision < 0 || c is null || string.IsNullOrWhiteSpace(c.Name)
                || c.Name.Length > 100 || c.Kind is not ("wealth" or "monthly-income") || c.Currency?.Length != 3
                || c.Currency.Any(ch => ch is < 'A' or > 'Z') || c.Deadline < new DateOnly(1900, 1, 1) || c.Deadline > new DateOnly(2100, 12, 31)
                || c.Target <= 0 || !Money(c.Target) || c.MonthlyContribution < 0 || !Money(c.MonthlyContribution)
                || (c.Kind == "monthly-income" && c.MonthlyContribution != 0))
                throw new InputValidationException("Informe nome, tipo, moeda, prazo e valores válidos com até 15 inteiros e duas casas decimais. Aporte mensal é exclusivo do objetivo de patrimônio.");
        }
        private static bool Money(decimal value) => value <= 999999999999999.99m && decimal.Round(value, 2) == value;
        public static SavedPlanningGoal Read(PlanningGoalRevision row) => new(row.GoalId, row.Revision,
            DateTime.SpecifyKind(row.CreatedAtUtc, DateTimeKind.Utc), new(row.Name, row.Kind, row.Currency, row.Target, row.Deadline, row.MonthlyContribution, row.Archived));
        public static (DateOnly From, DateOnly To) IncomeWindow(DateOnly today)
        {
            var startOfMonth = new DateOnly(today.Year, today.Month, 1);
            return (startOfMonth.AddMonths(-12), startOfMonth.AddDays(-1));
        }
        public static (decimal? Average, int Missing) IncomeAverage(IReadOnlyList<IncomeEvent> events, string currency, DateOnly today)
        {
            var window = IncomeWindow(today);
            var included = events.Where(e => e.Date >= window.From && e.Date <= window.To).ToArray();
            var missing = included.Count(e => !e.InCurrency(currency).HasValue);
            return (missing > 0 ? null : included.Sum(e => e.InCurrency(currency)!.Value * (e.IsReversal ? -1 : 1)) / 12m, missing);
        }
        public static PlanningProgress Calculate(SavedPlanningGoal goal, string currency, DateOnly today, decimal? current)
        {
            var c = goal.Config;
            var months = c.Deadline < today ? 0 : (c.Deadline.Year - today.Year) * 12 + c.Deadline.Month - today.Month + 1;
            PlanningProgress Unavailable(string status, string reason) => new(goal, status, reason, months, null, null, null, null, null, null);
            if (c.Archived) return Unavailable("archived", "Objetivo arquivado; revisões preservadas.");
            if (c.Currency != currency) return Unavailable("currency-changed", "A moeda-base da carteira mudou. O valor do objetivo foi preservado; crie um objetivo na moeda atual, sem conversão implícita.");
            if (!current.HasValue) return Unavailable("unavailable", c.Kind == "wealth"
                ? "Valor da carteira indisponível: confira o câmbio antes de avaliar o progresso."
                : "Há proventos sem equivalente histórico na moeda-base na janela de análise. Complete as conversões em Proventos.");
            var gap = Math.Max(0, c.Target - current.Value);
            var status = gap == 0 ? "achieved" : c.Deadline < today ? "overdue" : "in-progress";
            decimal? required = c.Kind == "wealth" ? gap == 0 ? 0 : months > 0 ? decimal.Ceiling(gap / months * 100m) / 100m : null : null;
            decimal? scenario = c.Kind == "wealth" && months > 0 ? current + c.MonthlyContribution * months : null;
            return new(goal, status, c.Kind == "wealth"
                ? "Todas as posições da carteira, sem Patrimônio Externo. Cenário sem rentabilidade, sem retiradas e com parcelas mensais incluindo o mês atual."
                : "Média mensal dos proventos líquidos registrados nos 12 meses completos anteriores; meses sem registros contam como zero. Não é previsão de renda futura.",
                months, current, gap, current <= 0 ? 0 : current >= c.Target ? 100 : decimal.Round(current.Value / c.Target * 100m, 4), required, scenario,
                scenario.HasValue ? Math.Max(0, c.Target - scenario.Value) : null);
        }
    }
}
