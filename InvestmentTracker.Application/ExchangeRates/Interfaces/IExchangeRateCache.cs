using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.ExchangeRates.Interfaces
{
    public interface IExchangeRateCache
    {
        Task<ExchangeRate?> GetAsync(string baseCode, string quoteCode, CancellationToken cancellationToken);
        Task StoreAsync(ExchangeRate rate, CancellationToken cancellationToken);
    }
}
