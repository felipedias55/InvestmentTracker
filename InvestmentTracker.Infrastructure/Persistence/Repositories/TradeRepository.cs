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
        public async Task<TradePageDto> PageAsync(int portfolioId, TradePageQuery query, CancellationToken ct)
        {
            var filtered = db.Set<PortfolioTrade>().AsNoTracking().Where(x => x.PortfolioId == portfolioId);
            if (!string.IsNullOrEmpty(query.Search)) filtered = filtered.Where(x => x.Ticker.Contains(query.Search));
            if (!string.IsNullOrEmpty(query.Kind)) filtered = filtered.Where(x => x.Kind == query.Kind);
            if (query.From.HasValue) filtered = filtered.Where(x => x.Date >= query.From.Value);
            if (query.To.HasValue) filtered = filtered.Where(x => x.Date <= query.To.Value);
            var total = await filtered.CountAsync(ct);
            var page = Math.Min(query.PageIndex, Math.Max(0, (total - 1) / query.PageSize));
            var movements = db.Set<FinancialMovement>().Where(x => x.PortfolioId == portfolioId);
            var items = await filtered.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
                .Skip(page * query.PageSize).Take(query.PageSize)
                .Select(x => new TradeHistoryItemDto(new TradeDto(x.Id, x.Date, x.Kind, x.Ticker, x.CurrencyCode,
                    x.Quantity, x.UnitPrice, x.Amount, x.CashAssetName, x.Fees),
                    movements.Where(m => m.TradeId == x.Id && m.ReversalOfId == null).Select(m => (int?)m.Id).FirstOrDefault(),
                    movements.Where(r => movements.Any(m => m.TradeId == x.Id && m.ReversalOfId == null && r.ReversalOfId == m.Id))
                        .Select(r => (int?)r.Id).FirstOrDefault()))
                .ToListAsync(ct);
            return new TradePageDto(items, total, page, query.PageSize);
        }
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
            var journal = await db.Set<FinancialMovement>().Where(x => x.PortfolioId == portfolioId).MaxAsync(x => (DateOnly?)x.Date, ct);
            if (journal > trade || trade is null) trade = journal;
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
            if (existing is null)
                await MovementRecorder.SaveAsync(db, new FinancialMovement {
                    PortfolioId = portfolioId, RequestId = requestId, Date = trade.Date,
                    Kind = trade.Kind, Amount = trade.Amount, CurrencyCode = trade.CurrencyCode,
                    Description = trade.Ticker, CreatedAtUtc = trade.CreatedAtUtc,
                    Trade = trade }, ct);
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
