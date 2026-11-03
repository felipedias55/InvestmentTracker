using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InvestmentTracker.Application.Allocation.Dtos;
using InvestmentTracker.Application.Common.Exceptions;

namespace InvestmentTracker.Application.Allocation.Tracking
{
    public static class ContributionTrackingCalculator
    {
        public static string Token(ContributionPlanDto plan) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { Currency = plan.Dashboard.Summary.Portfolio.BaseCurrencyCode, plan.Plan, plan.StrategyNotes, plan.Bands }))));
        public static TrackingPlanDetail Detail(TrackingData data)
        {
            var snapshot = JsonSerializer.Deserialize<ContributionPlanSnapshot>(data.Plan.SnapshotJson)!;
            var rows = snapshot.Plan.Rows.Select(row =>
            {
                var actual = data.Purchases.Where(p => p.RowKey == row.Key && p.Active && p.RemovedAtUtc is null).Sum(p => p.BaseAmount);
                return new TrackingRow(row, actual, row.Amount - actual, Status(row.Amount, actual));
            }).ToArray();
            var realized = rows.Sum(r => r.Realized);
            // Unallocated money remains part of the original budget, not a purchase target.
            var target = snapshot.Plan.Rows.Sum(r => r.Amount);
            return new(new(data.Plan.Id, data.Plan.Name, data.Plan.From, data.Plan.To, DateTime.SpecifyKind(data.Plan.CreatedAtUtc, DateTimeKind.Utc)), snapshot,
                rows, data.Purchases, realized, target - realized, rows.Length == 0 ? "unallocated"
                    : rows.All(r => r.Realized >= r.Planned.Amount) ? (realized > target ? "exceeded" : "completed")
                    : realized > 0 ? "partial" : "pending");
        }
        private static string Status(decimal planned, decimal actual) => actual > planned ? "exceeded" : actual == planned ? "completed" : actual > 0 ? "partial" : "pending";
        public static (decimal Amount, string Source) ValidatePurchase(ContributionPlanSnapshot snapshot, DateOnly from, DateOnly to,
            PurchaseCandidate trade, LinkPurchase request)
        {
            var row = snapshot.Plan.Rows.SingleOrDefault(r => r.Key == request.RowKey);
            if (!trade.Active || trade.Date < from || trade.Date > to || row is null
                || trade.CategoryId != row.CategoryId || trade.SectorId != row.SectorId || !row.Currencies.Any(c => c.Currency == trade.Currency))
                throw new InputValidationException("Selecione uma compra ativa do período, categoria, setor e moeda deste grupo.");
            var amount = trade.Currency == snapshot.Currency ? trade.Amount : trade.BaseAmount;
            if (amount.HasValue && request.BaseAmount.HasValue && amount != request.BaseAmount)
                throw new InputValidationException("O equivalente deve preservar o valor histórico da compra.");
            var source = trade.Currency == snapshot.Currency ? "same-currency" : amount.HasValue ? "historical" : "manual";
            amount ??= request.BaseAmount;
            if (amount is null or <= 0 or > 999999999999999.9999m || decimal.Round(amount.Value, 4) != amount)
                throw new InputValidationException("Informe o equivalente histórico positivo na moeda do plano, com até quatro casas decimais.");
            return (amount.Value, source);
        }
    }
}
