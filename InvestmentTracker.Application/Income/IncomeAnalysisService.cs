using System.Text.Json.Serialization;
using InvestmentTracker.Application.Movements;
using InvestmentTracker.Application.Portfolios.Interfaces;

namespace InvestmentTracker.Application.Income
{
    public sealed record IncomeEvent(DateOnly Date, string Ticker, int AssetId, string CurrencyCode, decimal Amount, bool Retained, bool IsReversal)
    {
        public IReadOnlyList<IncomeConversionDto> Conversions { get; init; } = [];
        public decimal? InCurrency(string currency) => CurrencyCode == currency ? Amount :
            Conversions.Where(x => x.BaseCurrencyCode == currency).MaxBy(x => x.Revision)?.BaseAmount;
    }
    public sealed record IncomeTotalDto(string Period, string Ticker, int? AssetId, string CurrencyCode,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? Received,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? Reversed,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? Net)
    {
        public int MissingConversions { get; init; }
    }
    public sealed record IncomeAnalysisDto(IReadOnlyList<IncomeTotalDto> Months, IReadOnlyList<IncomeTotalDto> Years, IReadOnlyList<IncomeTotalDto> Assets)
    {
        public string? BaseCurrencyCode { get; init; }
        public IReadOnlyList<IncomeTotalDto> ConvertedMonths { get; init; } = [];
        public IReadOnlyList<IncomeTotalDto> ConvertedYears { get; init; } = [];
        public IReadOnlyList<IncomeTotalDto> ConvertedAssets { get; init; } = [];
    }
    public interface IIncomeAnalysisService
    {
        Task<IReadOnlyList<IncomeEvent>> EventsAsync(int portfolioId, CancellationToken ct);
        Task<IncomeAnalysisDto> GetAsync(int portfolioId, CancellationToken ct);
    }
    public sealed class IncomeAnalysisService(IIncomeRepository income, IMovementRepository movements, IPortfolioRepository portfolios) : IIncomeAnalysisService
    {
        public async Task<IReadOnlyList<IncomeEvent>> EventsAsync(int portfolioId, CancellationToken ct)
        {
            var receipts = await income.ListAsync(portfolioId, ct);
            var journal = await movements.ListAsync(portfolioId, ct);
            var result = new List<IncomeEvent>();
            foreach (var receipt in receipts)
            {
                var entry = new IncomeEvent(receipt.Date, receipt.Ticker, receipt.AssetId, receipt.CurrencyCode, receipt.Amount, receipt.CashAssetId.HasValue, false)
                    { Conversions = receipt.Conversions.Select(IncomeConversionService.Map).ToList() };
                result.Add(entry);
                var original = journal.FirstOrDefault(m => m.IncomeReceiptId == receipt.Id);
                var reversal = original is null ? null : journal.FirstOrDefault(m => m.ReversalOfId == original.Id);
                if (reversal is not null) result.Add(entry with { Date = reversal.Date, IsReversal = true });
            }
            return result;
        }
        public async Task<IncomeAnalysisDto> GetAsync(int portfolioId, CancellationToken ct)
        {
            var portfolio = await portfolios.GetByIdAsync(portfolioId, ct);
            return Calculate(await EventsAsync(portfolioId, ct), portfolio?.BaseCurrency.Code);
        }
        public static IncomeAnalysisDto Calculate(IReadOnlyList<IncomeEvent> events, string? currency = null)
        {
            IReadOnlyList<IncomeTotalDto> Group(Func<IncomeEvent, string> period, bool converted)
                => events.GroupBy(x => (Period: period(x), x.AssetId, Currency: converted ? currency! : x.CurrencyCode))
                .Select(g =>
                {
                    var missing = g.Count(x => converted && !x.InCurrency(currency!).HasValue);
                    decimal? Sum(bool reversed) => missing > 0 ? null : g.Where(x => x.IsReversal == reversed)
                        .Sum(x => converted ? x.InCurrency(currency!)!.Value : x.Amount);
                    var received = Sum(false); var reversed = Sum(true);
                    return new IncomeTotalDto(g.Key.Period, g.OrderByDescending(x => x.Date).First().Ticker,
                        g.Key.AssetId, g.Key.Currency, received, reversed, received - reversed) { MissingConversions = missing };
                }).OrderByDescending(x => x.Period).ThenBy(x => x.Ticker).ThenBy(x => x.CurrencyCode).ToList();
            return new(Group(x => x.Date.ToString("yyyy-MM"), false), Group(x => x.Date.Year.ToString(), false), Group(_ => "Todo o histórico", false))
            {
                BaseCurrencyCode = currency,
                ConvertedMonths = currency is null ? [] : Group(x => x.Date.ToString("yyyy-MM"), true),
                ConvertedYears = currency is null ? [] : Group(x => x.Date.Year.ToString(), true),
                ConvertedAssets = currency is null ? [] : Group(_ => "Todo o histórico", true)
            };
        }
    }
}
