using System.Text.Json.Serialization;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Portfolios;
using InvestmentTracker.Application.Portfolios.Interfaces;

namespace InvestmentTracker.Application.Quotes
{
    public sealed record AssetQuoteRequest(IReadOnlyList<int> PositionIds);
    public sealed record AssetQuote(string Provider, string Symbol, string Currency,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? Price,
        DateOnly? Date, string Status, string Message)
    {
        public DateTimeOffset? MarketTime { get; init; }
        public DateTimeOffset? RetrievedAt { get; init; }
        public bool FromCache { get; init; }
        public bool Requested { get; init; }
        public DateTimeOffset? RetryAfter { get; init; }
    }
    public sealed record PositionQuote(int PositionId, AssetQuote Quote);
    public interface IAssetQuoteProvider
    {
        Task<AssetQuote> FetchAsync(string market, string symbol, CancellationToken ct);
    }
    public sealed class AssetQuoteService(IPortfolioRepository portfolios, IAssetQuoteProvider provider, TimeProvider clock)
    {
        public async Task<IReadOnlyList<PositionQuote>?> PreviewAsync(int portfolioId, AssetQuoteRequest request, CancellationToken ct)
        {
            if (request.PositionIds is null || request.PositionIds.Count is < 1 or > 200 ||
                request.PositionIds.Any(id => id <= 0) || request.PositionIds.Distinct().Count() != request.PositionIds.Count)
                throw new InputValidationException("Selecione de 1 a 200 posições distintas.");
            if (await portfolios.GetByIdAsync(portfolioId, ct) is null) return null;
            var positions = (await portfolios.GetPositionsAsync(portfolioId, ct)).Where(p => request.PositionIds.Contains(p.Id)).ToList();
            if (positions.Count != request.PositionIds.Count)
                throw new ResourceConflictException("Uma posição não pertence mais à carteira. Atualize os dados.");
            var result = new List<PositionQuote>();
            foreach (var position in positions)
            {
                var asset = position.Asset;
                var market = asset.Country.Name switch
                {
                    "Brasil" when asset.Currency.Code == "BRL" => "BR",
                    "Estados Unidos" when asset.Currency.Code == "USD" => "US",
                    _ => ""
                };
                AssetQuote quote;
                if (market.Length == 0 || position.Quantity <= 0)
                    quote = new("", asset.Ticker, asset.Currency.Code, null, null, "unsupported", "País/moeda não suportados ou posição sem quantidade positiva.");
                else
                {
                    quote = await provider.FetchAsync(market, asset.Ticker, ct);
                    if (quote.Price.HasValue)
                    {
                        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
                        if (quote.Symbol != asset.Ticker || quote.Currency != asset.Currency.Code || quote.Date is null ||
                            quote.Date > today || quote.Date < today.AddDays(-7))
                            quote = quote with { Price = null, Status = "invalid", Message = "Ticker, moeda ou data incompatíveis. Confira o cadastro e atualize manualmente." };
                        else
                        {
                            try { QuoteBatchService.CalculateValue(position.Quantity, quote.Price.Value); }
                            catch (InputValidationException) { quote = quote with { Price = null, Status = "invalid", Message = "Preço ou valor calculado fora dos limites da posição." }; }
                        }
                    }
                }
                result.Add(new(position.Id, quote));
            }
            return result;
        }
    }
}
