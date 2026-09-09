using System.Data;
using Microsoft.Data.SqlClient;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Income;
using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.Infrastructure.Persistence.Repositories
{
    public sealed class IncomeRepository(InvestmentTrackerDbContext db) : IIncomeRepository
    {
        public async Task<IReadOnlyList<IncomeReceipt>> ListAsync(int portfolioId, CancellationToken ct)
            => await db.Set<IncomeReceipt>().AsNoTracking().Where(x => x.PortfolioId == portfolioId)
                .OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).ToListAsync(ct);
        public Task<PortfolioAsset?> PositionAsync(int portfolioId, int assetId, CancellationToken ct)
            => db.PortfolioAssets.SingleOrDefaultAsync(x => x.PortfolioId == portfolioId && x.AssetId == assetId, ct);
        public async Task<DateOnly?> LatestDateAsync(int portfolioId, CancellationToken ct)
        {
            var trade = await db.Set<IncomeReceipt>().Where(x => x.PortfolioId == portfolioId).MaxAsync(x => (DateOnly?)x.Date, ct);
            var operation = await db.Set<PortfolioTrade>().Where(x => x.PortfolioId == portfolioId).MaxAsync(x => (DateOnly?)x.Date, ct);
            if (operation > trade || trade is null) trade = operation;
            var snapshot = await db.PortfolioSnapshots.Where(x => x.PortfolioId == portfolioId).MaxAsync(x => (DateOnly?)x.SnapshotDate, ct);
            return trade > snapshot || snapshot is null ? trade : snapshot;
        }
        public async Task<IncomeReceipt?> ExecuteAsync(int portfolioId, Guid requestId,
            Func<Portfolio, IncomeReceipt?, CancellationToken, Task<IncomeReceipt>> apply, CancellationToken ct)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
            // Use the same portfolio lock as purchases and sales to protect shared balances.
            var portfolio = await db.Portfolios.FromSqlInterpolated($"SELECT * FROM Portfolio WITH (UPDLOCK, HOLDLOCK) WHERE Id = {portfolioId}")
                .Include(x => x.BaseCurrency).SingleOrDefaultAsync(ct);
            if (portfolio is null) return null;
            var existing = await db.Set<IncomeReceipt>().SingleOrDefaultAsync(x => x.PortfolioId == portfolioId && x.RequestId == requestId, ct);
            var trade = await apply(portfolio, existing, ct);
            if (existing is null) db.Set<IncomeReceipt>().Add(trade);
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
