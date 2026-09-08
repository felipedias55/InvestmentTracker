using InvestmentTracker.Application.ExchangeRates.Interfaces;
using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.Infrastructure.Persistence.Repositories
{
    public sealed class ExchangeRateCache(InvestmentTrackerDbContext context) : IExchangeRateCache
    {
        public Task<ExchangeRate?> GetAsync(string baseCode, string quoteCode, CancellationToken cancellationToken)
        {
            return context.ExchangeRates.AsNoTracking()
                .SingleOrDefaultAsync(x => x.BaseCode == baseCode && x.QuoteCode == quoteCode, cancellationToken);
        }

        public async Task StoreAsync(ExchangeRate rate, CancellationToken cancellationToken)
        {
            // Atomic across API instances. Cache persistence must not flush tracked portfolio changes.
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                MERGE [ExchangeRate] WITH (HOLDLOCK) AS target
                USING (SELECT {rate.BaseCode} AS BaseCode, {rate.QuoteCode} AS QuoteCode) AS source
                ON target.BaseCode = source.BaseCode AND target.QuoteCode = source.QuoteCode
                WHEN MATCHED AND target.RateDate <= {rate.RateDate} THEN
                    UPDATE SET Rate = {rate.Rate}, RateDate = {rate.RateDate}, FetchedAtUtc = {rate.FetchedAtUtc}
                WHEN NOT MATCHED THEN
                    INSERT (BaseCode, QuoteCode, Rate, RateDate, FetchedAtUtc)
                    VALUES ({rate.BaseCode}, {rate.QuoteCode}, {rate.Rate}, {rate.RateDate}, {rate.FetchedAtUtc});
                """, cancellationToken);
        }
    }
}
