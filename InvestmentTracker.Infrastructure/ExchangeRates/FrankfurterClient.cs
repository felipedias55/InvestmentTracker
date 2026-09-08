using System.Net;
using System.Net.Http.Json;
using InvestmentTracker.Application.ExchangeRates.Interfaces;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Infrastructure.ExchangeRates
{
    public sealed class FrankfurterClient(HttpClient client, TimeProvider clock) : IExchangeRateProvider
    {
        public async Task<ExchangeRate?> FetchAsync(string baseCode, string quoteCode, CancellationToken cancellationToken)
        {
            // Only currency codes are sent. Portfolio identifiers and amounts stay in this application.
            using var response = await client.GetAsync(
                $"v2/rate/{Uri.EscapeDataString(baseCode)}/{Uri.EscapeDataString(quoteCode)}", cancellationToken);
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity) return null;
            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadFromJsonAsync<RateResponse>(cancellationToken);
            var now = clock.GetUtcNow();
            if (data is null || data.Base != baseCode || data.Quote != quoteCode || data.Rate <= 0
                || data.Rate > 1000000000000m || data.Date == default || data.Date > DateOnly.FromDateTime(now.UtcDateTime))
                throw new InvalidDataException("A resposta da API de câmbio é inválida.");
            return new ExchangeRate { BaseCode = data.Base, QuoteCode = data.Quote, Rate = data.Rate,
                RateDate = data.Date, FetchedAtUtc = now.UtcDateTime };
        }

        private sealed record RateResponse(string Base, string Quote, decimal Rate, DateOnly Date);
    }
}
