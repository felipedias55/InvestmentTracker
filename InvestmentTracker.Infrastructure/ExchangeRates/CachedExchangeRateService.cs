using System.Text.Json;
using InvestmentTracker.Application.ExchangeRates.Interfaces;
using InvestmentTracker.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace InvestmentTracker.Infrastructure.ExchangeRates
{
    public sealed class CachedExchangeRateService(IExchangeRateProvider provider, IExchangeRateCache cache,
        ExchangeRateRefreshCoordinator coordinator, TimeProvider clock, ILogger<CachedExchangeRateService> logger)
        : IExchangeRateService
    {
        public async Task<ExchangeRateQuote?> GetAsync(string baseCode, string quoteCode, CancellationToken cancellationToken = default)
        {
            var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            if (baseCode == quoteCode) return new(baseCode, quoteCode, 1m, today, false, false);
            var state = coordinator.For($"{baseCode}/{quoteCode}");
            await state.Gate.WaitAsync(cancellationToken);
            try
            {
                var stored = await cache.GetAsync(baseCode, quoteCode, cancellationToken);
                if (stored is not null && DateOnly.FromDateTime(stored.FetchedAtUtc) == today)
                    return Map(stored, today, false);
                if (clock.GetUtcNow() < state.RetryAfter) return Map(stored, today, true);
                try
                {
                    var fetched = await provider.FetchAsync(baseCode, quoteCode, cancellationToken);
                    if (fetched is not null && (stored is null || fetched.RateDate >= stored.RateDate))
                    {
                        await cache.StoreAsync(fetched, cancellationToken);
                        state.RetryAfter = default;
                        return Map(fetched, today, false);
                    }
                }
                catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidDataException
                    || exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
                {
                    logger.LogWarning("Não foi possível atualizar a cotação {Base}/{Quote} via Frankfurter ({ErrorType}).",
                        baseCode, quoteCode, exception.GetType().Name);
                }
                cancellationToken.ThrowIfCancellationRequested();
                state.RetryAfter = clock.GetUtcNow().AddMinutes(15);
                return Map(stored, today, true);
            }
            finally { state.Gate.Release(); }
        }

        private static ExchangeRateQuote? Map(ExchangeRate? rate, DateOnly today, bool fallback)
        {
            return rate is null ? null : new(rate.BaseCode, rate.QuoteCode, rate.Rate, rate.RateDate,
                fallback || rate.RateDate < today, fallback);
        }
    }
}
