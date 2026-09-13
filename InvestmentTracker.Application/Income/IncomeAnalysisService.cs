using System.Text.Json.Serialization;
using InvestmentTracker.Application.Movements;

namespace InvestmentTracker.Application.Income
{
    public sealed record IncomeEvent(DateOnly Date, string Ticker, int AssetId, string CurrencyCode, decimal Amount, bool Retained, bool IsReversal);
    public sealed record IncomeTotalDto(string Period, string Ticker, int? AssetId, string CurrencyCode,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Received,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Reversed,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Net);
    public sealed record IncomeAnalysisDto(IReadOnlyList<IncomeTotalDto> Months, IReadOnlyList<IncomeTotalDto> Years, IReadOnlyList<IncomeTotalDto> Assets);
    public interface IIncomeAnalysisService
    {
        Task<IReadOnlyList<IncomeEvent>> EventsAsync(int portfolioId, CancellationToken ct);
        Task<IncomeAnalysisDto> GetAsync(int portfolioId, CancellationToken ct);
    }
    public sealed class IncomeAnalysisService(IIncomeRepository income, IMovementRepository movements) : IIncomeAnalysisService
    {
        public async Task<IReadOnlyList<IncomeEvent>> EventsAsync(int portfolioId, CancellationToken ct)
        {
            var receipts = await income.ListAsync(portfolioId, ct);
            var journal = await movements.ListAsync(portfolioId, ct);
            var result = new List<IncomeEvent>();
            foreach (var receipt in receipts)
            {
                var entry = new IncomeEvent(receipt.Date, receipt.Ticker, receipt.AssetId, receipt.CurrencyCode, receipt.Amount, receipt.CashAssetId.HasValue, false);
                result.Add(entry);
                var original = journal.FirstOrDefault(m => m.IncomeReceiptId == receipt.Id);
                var reversal = original is null ? null : journal.FirstOrDefault(m => m.ReversalOfId == original.Id);
                if (reversal is not null) result.Add(entry with { Date = reversal.Date, IsReversal = true });
            }
            return result;
        }
        public async Task<IncomeAnalysisDto> GetAsync(int portfolioId, CancellationToken ct) => Calculate(await EventsAsync(portfolioId, ct));
        public static IncomeAnalysisDto Calculate(IReadOnlyList<IncomeEvent> events)
        {
            IReadOnlyList<IncomeTotalDto> Group(Func<IncomeEvent, (string Period, int? AssetId, string Currency)> key)
                => events.GroupBy(key).Select(g => new IncomeTotalDto(g.Key.Period, g.OrderByDescending(x => x.Date).First().Ticker, g.Key.AssetId, g.Key.Currency,
                    g.Where(x => !x.IsReversal).Sum(x => x.Amount), g.Where(x => x.IsReversal).Sum(x => x.Amount),
                    g.Sum(x => x.IsReversal ? -x.Amount : x.Amount))).OrderByDescending(x => x.Period).ThenBy(x => x.Ticker).ThenBy(x => x.CurrencyCode).ToList();
            return new(Group(x => (x.Date.ToString("yyyy-MM"), x.AssetId, x.CurrencyCode)),
                Group(x => (x.Date.Year.ToString(), x.AssetId, x.CurrencyCode)),
                Group(x => ("Todo o histórico", x.AssetId, x.CurrencyCode)));
        }
    }
}
