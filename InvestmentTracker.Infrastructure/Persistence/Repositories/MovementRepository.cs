using System.Data;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Movements;
using InvestmentTracker.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.Infrastructure.Persistence.Repositories
{
    public sealed class MovementRepository(InvestmentTrackerDbContext db) : IMovementRepository
    {
        public Task<int?> ReversalIdAsync(int portfolioId, int id, CancellationToken ct)
            => db.Set<FinancialMovement>().Where(x => x.PortfolioId == portfolioId && x.ReversalOfId == id)
                .Select(x => (int?)x.Id).SingleOrDefaultAsync(ct);
        public async Task<MovementPageDto> PageAsync(int portfolioId, MovementPageQuery query, CancellationToken ct)
        {
            var all = db.Set<FinancialMovement>().AsNoTracking().Where(x => x.PortfolioId == portfolioId);
            var filtered = all;
            if (!string.IsNullOrEmpty(query.Search))
            {
                var numericId = int.TryParse(query.Search.TrimStart('#'), out var id) ? id : -1;
                filtered = filtered.Where(x => x.Id == numericId || x.Description.Contains(query.Search)
                    || x.Effects.Any(e => e.Name.Contains(query.Search)));
            }
            if (!string.IsNullOrEmpty(query.Kind)) filtered = filtered.Where(x => x.Kind == query.Kind);
            if (query.From.HasValue) filtered = filtered.Where(x => x.Date >= query.From.Value);
            if (query.To.HasValue) filtered = filtered.Where(x => x.Date <= query.To.Value);
            if (query.Status == "reversed") filtered = filtered.Where(x => all.Any(r => r.ReversalOfId == x.Id));
            if (query.Status == "reversal") filtered = filtered.Where(x => x.ReversalOfId != null);
            if (query.Status == "active") filtered = filtered.Where(x => x.ReversalOfId == null && !all.Any(r => r.ReversalOfId == x.Id));
            var total = await filtered.CountAsync(ct);
            var page = Math.Min(query.PageIndex, Math.Max(0, (total - 1) / query.PageSize));
            var items = await filtered.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
                .Skip(page * query.PageSize).Take(query.PageSize)
                .Select(x => new MovementListItemDto(x.Id, x.Date, x.Kind, x.Description, x.CurrencyCode, x.Amount,
                    x.ReversalOfId, all.Where(r => r.ReversalOfId == x.Id).Select(r => (int?)r.Id).FirstOrDefault()))
                .ToListAsync(ct);
            return new MovementPageDto(items, total, page, query.PageSize);
        }
        public async Task<IReadOnlyList<FinancialMovement>> ListAsync(int portfolioId, CancellationToken ct)
            => await db.Set<FinancialMovement>().AsNoTracking().Include(x => x.Effects).Where(x => x.PortfolioId == portfolioId)
                .OrderByDescending(x => x.Id).ToListAsync(ct);
        public Task<FinancialMovement?> GetAsync(int portfolioId, int id, CancellationToken ct)
            => db.Set<FinancialMovement>().Include(x => x.Effects).SingleOrDefaultAsync(x => x.PortfolioId == portfolioId && x.Id == id, ct);
        public Task<bool> IsReversedAsync(int id, CancellationToken ct)
            => db.Set<FinancialMovement>().AnyAsync(x => x.ReversalOfId == id, ct);
        public Task<bool> HasDependentAsync(FinancialMovement movement, CancellationToken ct)
        {
            var positions = movement.Effects.Where(x => x.PositionId.HasValue).Select(x => x.PositionId!.Value).ToArray();
            var balances = movement.Effects.Where(x => x.CashAssetId.HasValue).Select(x => x.CashAssetId!.Value).ToArray();
            return db.Set<FinancialMovement>().AnyAsync(m => m.PortfolioId == movement.PortfolioId && m.Id > movement.Id
                && m.ReversalOfId == null && !db.Set<FinancialMovement>().Any(r => r.ReversalOfId == m.Id)
                && m.Effects.Any(e => (e.PositionId.HasValue && positions.Contains(e.PositionId.Value)) ||
                    (e.CashAssetId.HasValue && balances.Contains(e.CashAssetId.Value))), ct);
        }
        public async Task<int> ReopenAsync(int portfolioId, DateOnly from, CancellationToken ct)
        {
            var snapshots = await db.PortfolioSnapshots.Where(x => x.PortfolioId == portfolioId && x.SnapshotDate >= from).ToListAsync(ct);
            foreach (var snapshot in snapshots) { snapshot.IsReopened = true; snapshot.IsOutdated = true; }
            return snapshots.Count;
        }
        public async Task<DateOnly?> LatestDateAsync(int portfolioId, CancellationToken ct)
        {
            var movement = await db.Set<FinancialMovement>().Where(x => x.PortfolioId == portfolioId).MaxAsync(x => (DateOnly?)x.Date, ct);
            var snapshot = await db.PortfolioSnapshots.Where(x => x.PortfolioId == portfolioId).MaxAsync(x => (DateOnly?)x.SnapshotDate, ct);
            return movement > snapshot || snapshot is null ? movement : snapshot;
        }
        public Task<PortfolioAsset?> PositionAsync(int portfolioId, int id, CancellationToken ct)
            => db.PortfolioAssets.SingleOrDefaultAsync(x => x.PortfolioId == portfolioId && x.Id == id, ct);
        public async Task<IReadOnlyList<PortfolioCashFlow>> FlowsAsync(int movementId, CancellationToken ct)
            => await db.PortfolioCashFlows.AsNoTracking().Where(x => x.MovementId == movementId).ToListAsync(ct);
        public void AddFlow(PortfolioCashFlow flow, FinancialMovement movement)
        { flow.Movement = movement; db.PortfolioCashFlows.Add(flow); }
        public async Task<FinancialMovement?> ExecuteAsync(int portfolioId, Guid requestId,
            Func<Portfolio, FinancialMovement?, CancellationToken, Task<FinancialMovement>> apply, CancellationToken ct)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var portfolio = await db.Portfolios.FromSqlInterpolated($"SELECT * FROM Portfolio WITH (UPDLOCK, HOLDLOCK) WHERE Id = {portfolioId}")
                    .Include(x => x.BaseCurrency).SingleOrDefaultAsync(ct);
                if (portfolio is null) return null;
                var existing = await db.Set<FinancialMovement>().Include(x => x.Effects)
                    .SingleOrDefaultAsync(x => x.PortfolioId == portfolioId && x.RequestId == requestId, ct);
                var result = await apply(portfolio, existing, ct);
                if (existing is null) await MovementRecorder.SaveAsync(db, result, ct);
                await transaction.CommitAsync(ct);
                return result;
            }
            catch (DbUpdateConcurrencyException ex) { throw new ResourceConflictException("Os saldos mudaram. Atualize a página.", ex); }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 or 2601 or 2627 or 1205 })
            { throw new ResourceConflictException("As referências ou movimentações mudaram. Atualize e tente novamente.", ex); }
            catch (SqlException ex) when (ex.Number == 1205)
            { throw new ResourceConflictException("A carteira mudou durante a operação. Tente novamente.", ex); }
        }
    }
}
