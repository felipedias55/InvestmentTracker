using InvestmentTracker.Application.Assets.Interfaces;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Portfolios;

namespace InvestmentTracker.Application.Quotes
{
    public sealed record QuoteCoverageRow(int AssetId, string Ticker, string Provider, string Status,
        string Message, DateOnly? Date, bool Requested, bool FromCache, DateTimeOffset? RetryAfter);

    // Read-only diagnostic: no positions, balances, writes or automatic symbol corrections.
    public sealed class QuoteCoverageService(IAssetRepository assets, IAssetQuoteProvider provider, TimeProvider clock)
    {
        public async Task<IReadOnlyList<QuoteCoverageRow>> CheckAsync(CancellationToken ct)
        {
            var rows = new List<QuoteCoverageRow>();
            foreach (var asset in (await assets.GetAllAsync(ct)).OrderBy(asset => asset.Ticker))
            {
                ct.ThrowIfCancellationRequested();
                var market = asset.Country.Name switch
                {
                    "Brasil" when asset.Currency.Code == "BRL" => "BR",
                    "Estados Unidos" when asset.Currency.Code == "USD" => "US",
                    _ => ""
                };
                var quote = market.Length == 0
                    ? new AssetQuote("", asset.Ticker, asset.Currency.Code, null, null, "unsupported", "País/moeda sem suporte.")
                    : await provider.FetchAsync(market, asset.Ticker, ct);
                if (quote.Price.HasValue)
                {
                    var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
                    if (quote.Symbol != asset.Ticker || quote.Currency != asset.Currency.Code || quote.Date is null ||
                        quote.Date > today || quote.Date < today.AddDays(-7))
                        quote = quote with { Status = "invalid", Message = "Ticker, moeda ou data incompatíveis." };
                    else
                    {
                        try { QuoteBatchService.CalculateValue(1, quote.Price.Value); }
                        catch (InputValidationException)
                        { quote = quote with { Status = "invalid", Message = "Preço fora dos limites suportados." }; }
                    }
                }
                rows.Add(new(asset.Id, asset.Ticker, quote.Provider, quote.Status, quote.Message,
                    quote.Date, quote.Requested, quote.FromCache, quote.RetryAfter));
            }
            return rows;
        }
    }
}
