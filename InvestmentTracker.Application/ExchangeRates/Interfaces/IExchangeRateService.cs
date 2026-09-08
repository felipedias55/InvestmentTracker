namespace InvestmentTracker.Application.ExchangeRates.Interfaces
{
    public sealed record ExchangeRateQuote(string BaseCode, string QuoteCode, decimal Rate,
        DateOnly RateDate, bool IsStale, bool IsFallback);

    public interface IExchangeRateService
    {
        Task<ExchangeRateQuote?> GetAsync(string baseCode, string quoteCode, CancellationToken cancellationToken = default);
    }
}
