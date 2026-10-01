using System.Net;
using System.Net.Http.Json;
using InvestmentTracker.Application.ExchangeRates.Interfaces;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Infrastructure.ExchangeRates
{
    public sealed class FrankfurterClient(HttpClient client, TimeProvider clock) : IExchangeRateProvider, IHistoricalExchangeRateProvider
    {
        public async Task<ExchangeRate?> FetchAsync(string baseCode, string quoteCode, CancellationToken cancellationToken)
            => await FetchRateAsync(baseCode, quoteCode, null, cancellationToken);

        public Task<ExchangeRate?> FetchAsync(string baseCode, string quoteCode, DateOnly date, CancellationToken ct)
            => FetchRateAsync(baseCode, quoteCode, date, ct);

        private async Task<ExchangeRate?> FetchRateAsync(string baseCode, string quoteCode, DateOnly? date, CancellationToken cancellationToken)
        {
            // Only currency codes are sent. Portfolio identifiers and amounts stay in this application.
            using var response = await client.GetAsync(
                $"v2/rate/{Uri.EscapeDataString(baseCode)}/{Uri.EscapeDataString(quoteCode)}" + (date.HasValue ? $"?date={date:yyyy-MM-dd}" : ""), cancellationToken);
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity) return null;
            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadFromJsonAsync<RateResponse>(cancellationToken);
            var now = clock.GetUtcNow();
            if (data is null || data.Base != baseCode || data.Quote != quoteCode || data.Rate <= 0
                || data.Rate > 1000000000000m || data.Date == default || data.Date > (date ?? DateOnly.FromDateTime(now.UtcDateTime)))
                throw new InvalidDataException("A resposta da API de câmbio é inválida.");
            if (date.HasValue && data.Date < date.Value.AddDays(-7)) return null;
            return new ExchangeRate { BaseCode = data.Base, QuoteCode = data.Quote, Rate = data.Rate,
                RateDate = data.Date, FetchedAtUtc = now.UtcDateTime };
        }

        private sealed record RateResponse(string Base, string Quote, decimal Rate, DateOnly Date);
    }
}
