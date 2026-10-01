using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.ExchangeRates.Interfaces
{
    public interface IHistoricalExchangeRateProvider
    {
        Task<ExchangeRate?> FetchAsync(string baseCode, string quoteCode, DateOnly date, CancellationToken ct);
    }
}
