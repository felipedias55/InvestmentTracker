using System.Data;
using Microsoft.Data.SqlClient;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Trades;
using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.Infrastructure.Persistence.Repositories
{
    public sealed class TradeRepository(InvestmentTrackerDbContext db) : ITradeRepository
    {
        public async Task<IReadOnlyList<PortfolioTrade>> ListAsync(int portfolioId, CancellationToken ct)
            => await db.Set<PortfolioTrade>().AsNoTracking().Where(x => x.PortfolioId == portfolioId)
                .OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).ToListAsync(ct);
        public Task<PortfolioAsset?> PositionAsync(int portfolioId, int assetId, CancellationToken ct)
            => db.PortfolioAssets.SingleOrDefaultAsync(x => x.PortfolioId == portfolioId && x.AssetId == assetId, ct);
        public async Task<DateOnly?> LatestDateAsync(int portfolioId, CancellationToken ct)
        {
            var trade = await db.Set<PortfolioTrade>().Where(x => x.PortfolioId == portfolioId).MaxAsync(x => (DateOnly?)x.Date, ct);
            var income = await db.Set<IncomeReceipt>().Where(x => x.PortfolioId == portfolioId).MaxAsync(x => (DateOnly?)x.Date, ct);
            if (income > trade || trade is null) trade = income;
            var snapshot = await db.PortfolioSnapshots.Where(x => x.PortfolioId == portfolioId).MaxAsync(x => (DateOnly?)x.SnapshotDate, ct);
            return trade > snapshot || snapshot is null ? trade : snapshot;
        }
        public void AddPosition(PortfolioAsset position) => db.PortfolioAssets.Add(position);
        public void AddFlow(PortfolioCashFlow flow, PortfolioTrade trade)
        {
            flow.Trade = trade;
            db.PortfolioCashFlows.Add(flow);
        }
        public async Task<PortfolioTrade?> ExecuteAsync(int portfolioId, Guid requestId,
            Func<Portfolio, PortfolioTrade?, CancellationToken, Task<PortfolioTrade>> apply, CancellationToken ct)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
            // Serialize operations on a portfolio, including the first purchase of an asset.
            var portfolio = await db.Portfolios.FromSqlInterpolated($"SELECT * FROM Portfolio WITH (UPDLOCK, HOLDLOCK) WHERE Id = {portfolioId}")
                .Include(x => x.BaseCurrency).SingleOrDefaultAsync(ct);
            if (portfolio is null) return null;
            var existing = await db.Set<PortfolioTrade>().SingleOrDefaultAsync(x => x.PortfolioId == portfolioId && x.RequestId == requestId, ct);
            var trade = await apply(portfolio, existing, ct);
            if (existing is null) db.Set<PortfolioTrade>().Add(trade);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return trade;
            }
            catch (DbUpdateConcurrencyException ex)
            { throw new ResourceConflictException("Os saldos mudaram. Atualize a carteira e tente novamente.", ex); }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 or 2601 or 2627 or 1205 })
            { throw new ResourceConflictException("A operação ou suas referências mudaram. Atualize a página e tente novamente.", ex); }
            catch (SqlException ex) when (ex.Number == 1205)
            { throw new ResourceConflictException("A carteira mudou durante a operação. Tente novamente.", ex); }
        }
    }
}
