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
        public async Task<IncomePageDto> PageAsync(int portfolioId, IncomePageQuery query, CancellationToken ct)
        {
            var filtered = db.Set<IncomeReceipt>().AsNoTracking().Where(x => x.PortfolioId == portfolioId);
            if (!string.IsNullOrEmpty(query.Search)) filtered = filtered.Where(x => x.Ticker.Contains(query.Search));
            if (query.From.HasValue) filtered = filtered.Where(x => x.Date >= query.From.Value);
            if (query.To.HasValue) filtered = filtered.Where(x => x.Date <= query.To.Value);
            if (query.PendingOnly)
            {
                var code = await db.Portfolios.Where(x => x.Id == portfolioId).Select(x => x.BaseCurrency.Code).SingleOrDefaultAsync(ct);
                filtered = filtered.Where(x => x.CurrencyCode != code && !x.Conversions.Any(c => c.BaseCurrencyCode == code));
            }
            var total = await filtered.CountAsync(ct);
            var page = Math.Min(query.PageIndex, Math.Max(0, (total - 1) / query.PageSize));
            var receipts = await filtered.Include(x => x.Conversions).OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
                .Skip(page * query.PageSize).Take(query.PageSize).ToListAsync(ct);
            var ids = receipts.Select(x => x.Id).ToArray();
            var movements = db.Set<FinancialMovement>().Where(x => x.PortfolioId == portfolioId);
            var reversals = await movements.Where(x => x.IncomeReceiptId.HasValue && ids.Contains(x.IncomeReceiptId.Value)
                && x.Kind == "income" && x.ReversalOfId == null)
                .Select(x => new { Id = x.IncomeReceiptId!.Value,
                    ReversedBy = movements.Where(r => r.ReversalOfId == x.Id).Select(r => (int?)r.Id).FirstOrDefault() }).ToListAsync(ct);
            var items = receipts.Select(x => IncomeService.Map(x) with { ReversedById = reversals.FirstOrDefault(r => r.Id == x.Id)?.ReversedBy }).ToList();
            return new IncomePageDto(items, total, page, query.PageSize);
        }
        public async Task<IReadOnlyList<IncomeReceipt>> ListAsync(int portfolioId, CancellationToken ct)
            => await db.Set<IncomeReceipt>().AsNoTracking().Include(x => x.Conversions).Where(x => x.PortfolioId == portfolioId)
                .OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).ToListAsync(ct);
        public Task<IncomeReceipt?> GetAsync(int portfolioId, int id, CancellationToken ct)
            => db.Set<IncomeReceipt>().Include(x => x.Conversions).SingleOrDefaultAsync(x => x.PortfolioId == portfolioId && x.Id == id, ct);
        public Task<PortfolioAsset?> PositionAsync(int portfolioId, int assetId, CancellationToken ct)
            => db.PortfolioAssets.SingleOrDefaultAsync(x => x.PortfolioId == portfolioId && x.AssetId == assetId, ct);
        public async Task<DateOnly?> LatestDateAsync(int portfolioId, CancellationToken ct)
        {
            var trade = await db.Set<IncomeReceipt>().Where(x => x.PortfolioId == portfolioId).MaxAsync(x => (DateOnly?)x.Date, ct);
            var operation = await db.Set<PortfolioTrade>().Where(x => x.PortfolioId == portfolioId).MaxAsync(x => (DateOnly?)x.Date, ct);
            if (operation > trade || trade is null) trade = operation;
            var journal = await db.Set<FinancialMovement>().Where(x => x.PortfolioId == portfolioId).MaxAsync(x => (DateOnly?)x.Date, ct);
            if (journal > trade || trade is null) trade = journal;
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
            var existing = await db.Set<IncomeReceipt>().Include(x => x.Conversions).SingleOrDefaultAsync(x => x.PortfolioId == portfolioId && x.RequestId == requestId, ct);
            var trade = await apply(portfolio, existing, ct);
            if (existing is null) db.Set<IncomeReceipt>().Add(trade);
            if (existing is null)
                await MovementRecorder.SaveAsync(db, new FinancialMovement {
                    PortfolioId = portfolioId, RequestId = requestId, Date = trade.Date,
                    Kind = "income", Amount = trade.Amount, CurrencyCode = trade.CurrencyCode,
                    Description = trade.Ticker, CreatedAtUtc = trade.CreatedAtUtc,
                    IncomeReceipt = trade }, ct);
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
