using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.ExchangeRates.Interfaces
{
    public interface IExchangeRateProvider
    {
        Task<ExchangeRate?> FetchAsync(string baseCode, string quoteCode, CancellationToken cancellationToken);
    }
}
