using System.Data;
using System.Text.Json;
using InvestmentTracker.Application.Allocation.Tracking;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.Infrastructure.Persistence.Repositories
{
    public sealed class ContributionTrackingRepository(InvestmentTrackerDbContext db, TimeProvider clock) : IContributionTrackingRepository
    {
        private IQueryable<SavedContributionPlan> Plans(int portfolioId) => db.Set<SavedContributionPlan>().Where(p => p.PortfolioId == portfolioId);
        private static ContributionPlanSnapshot Snapshot(SavedContributionPlan plan) => JsonSerializer.Deserialize<ContributionPlanSnapshot>(plan.SnapshotJson)!;
        private sealed class PurchaseData
        {
            public int TradeId { get; init; }
            public DateOnly Date { get; init; }
            public string Ticker { get; init; } = "";
            public string Currency { get; init; } = "";
            public int CategoryId { get; init; }
            public int SectorId { get; init; }
            public decimal Amount { get; init; }
            public decimal? BaseAmount { get; init; }
            public bool Active { get; init; }
            public PurchaseCandidate Candidate() => new(TradeId, Date, Ticker, Currency, CategoryId, SectorId, Amount, BaseAmount, Active);
        }
        private IQueryable<PurchaseData> Purchases(int portfolioId, string currency)
        {
            var movements = db.Set<FinancialMovement>().Where(m => m.PortfolioId == portfolioId);
            return from t in db.Set<PortfolioTrade>().AsNoTracking()
                join a in db.Assets on t.AssetId equals a.Id
                where t.PortfolioId == portfolioId && t.Kind == "buy"
                select new PurchaseData { TradeId = t.Id, Date = t.Date, Ticker = t.Ticker, Currency = t.CurrencyCode,
                    CategoryId = a.AssetCategoryId, SectorId = a.SectorId, Amount = t.Amount,
                    BaseAmount = db.PortfolioCashFlows.Where(f => f.TradeId == t.Id && !f.IsReversal && f.BaseCurrencyCode == currency)
                        .Select(f => f.BaseAmount).FirstOrDefault(),
                    Active = movements.Any(m => m.TradeId == t.Id && m.ReversalOfId == null && !movements.Any(r => r.ReversalOfId == m.Id)) };
        }
        public async Task<TrackingPlanPage?> PageAsync(int portfolioId, int page, int size, CancellationToken ct)
        {
            if (!await db.Portfolios.AnyAsync(p => p.Id == portfolioId, ct)) return null;
            var rows = Plans(portfolioId).AsNoTracking(); var total = await rows.CountAsync(ct);
            page = Math.Min(page, Math.Max(0, (total - 1) / size));
            var items = await rows.OrderByDescending(p => p.Id).Skip(page * size).Take(size)
                .Select(p => new TrackingPlanSummary(p.Id, p.Name, p.From, p.To, p.CreatedAtUtc)).ToListAsync(ct);
            return new(items.Select(p => p with { CreatedAtUtc = DateTime.SpecifyKind(p.CreatedAtUtc, DateTimeKind.Utc) }).ToArray(), total, page, size);
        }
        public async Task<TrackingData?> ReadAsync(int portfolioId, int id, CancellationToken ct)
        {
            var plan = await Plans(portfolioId).AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);
            if (plan is null) return null;
            var purchases = Purchases(portfolioId, Snapshot(plan).Currency);
            var links = await (from l in db.Set<ContributionPurchaseLink>().AsNoTracking()
                join t in purchases on l.TradeId equals t.TradeId
                where l.SavedContributionPlanId == id
                orderby l.Id descending
                select new TrackedPurchase(l.Id, t.TradeId, l.RowKey, t.Date, t.Ticker, t.Currency, t.Amount, l.BaseAmount,
                    l.ConversionSource, l.CreatedAtUtc, l.RemovedAtUtc, t.Active)).ToListAsync(ct);
            return new(plan, links);
        }
        private Task<Portfolio?> LockAsync(int id, CancellationToken ct) => db.Portfolios
            .FromSqlInterpolated($"SELECT * FROM Portfolio WITH (UPDLOCK, HOLDLOCK) WHERE Id = {id}").SingleOrDefaultAsync(ct);
        public async Task<int?> SaveAsync(int portfolioId, SaveContributionPlan request, Func<Task<ContributionPlanSnapshot>> create, CancellationToken ct)
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (await LockAsync(portfolioId, ct) is null) return null;
            var json = JsonSerializer.Serialize(request);
            var existing = await Plans(portfolioId).SingleOrDefaultAsync(p => p.RequestId == request.RequestId, ct);
            if (existing is not null)
            {
                if (existing.RequestJson != json) throw new ResourceConflictException("Solicitação já utilizada com outros dados.");
                return existing.Id;
            }
            var snapshot = await create();
            var plan = new SavedContributionPlan { PortfolioId = portfolioId, RequestId = request.RequestId, Name = request.Name.Trim(),
                From = request.From, To = request.To, CreatedAtUtc = clock.GetUtcNow().UtcDateTime, RequestJson = json,
                SnapshotJson = JsonSerializer.Serialize(snapshot) };
            db.Add(plan); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return plan.Id;
        }
        public async Task<PurchasePage?> CandidatesAsync(int portfolioId, int planId, int page, int size, CancellationToken ct)
        {
            var plan = await Plans(portfolioId).AsNoTracking().SingleOrDefaultAsync(p => p.Id == planId, ct);
            if (plan is null) return null;
            var rows = Purchases(portfolioId, Snapshot(plan).Currency).Where(t => t.Active && t.Date >= plan.From && t.Date <= plan.To
                && !db.Set<ContributionPurchaseLink>().Any(l => l.TradeId == t.TradeId && l.RemovedAtUtc == null));
            var total = await rows.CountAsync(ct); page = Math.Min(page, Math.Max(0, (total - 1) / size));
            var items = await rows.OrderByDescending(t => t.Date).ThenByDescending(t => t.TradeId).Skip(page * size).Take(size).ToListAsync(ct);
            return new(items.Select(t => t.Candidate()).ToArray(), total, page, size);
        }
        public async Task<bool> LinkAsync(int portfolioId, int planId, LinkPurchase request, CancellationToken ct)
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (await LockAsync(portfolioId, ct) is null) return false;
            var plan = await Plans(portfolioId).SingleOrDefaultAsync(p => p.Id == planId, ct);
            if (plan is null) return false;
            var snapshot = Snapshot(plan);
            var trade = await Purchases(portfolioId, snapshot.Currency).SingleOrDefaultAsync(t => t.TradeId == request.TradeId, ct)
                ?? throw new InputValidationException("Selecione uma compra desta carteira.");
            var equivalent = ContributionTrackingCalculator.ValidatePurchase(snapshot, plan.From, plan.To, trade.Candidate(), request);
            var existing = await db.Set<ContributionPurchaseLink>().SingleOrDefaultAsync(l => l.TradeId == request.TradeId && l.RemovedAtUtc == null, ct);
            if (existing is not null)
            {
                if (existing.SavedContributionPlanId != planId || existing.RowKey != request.RowKey || existing.BaseAmount != equivalent.Amount)
                    throw new ResourceConflictException("Esta compra já está vinculada. Remova o vínculo anterior antes de transferi-la.");
                return true;
            }
            db.Add(new ContributionPurchaseLink { SavedContributionPlanId = planId, TradeId = request.TradeId, RowKey = request.RowKey,
                BaseAmount = equivalent.Amount, ConversionSource = equivalent.Source, CreatedAtUtc = clock.GetUtcNow().UtcDateTime });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
        }
        public async Task<bool> UnlinkAsync(int portfolioId, int planId, int linkId, CancellationToken ct)
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (await LockAsync(portfolioId, ct) is null || !await Plans(portfolioId).AnyAsync(p => p.Id == planId, ct)) return false;
            var link = await db.Set<ContributionPurchaseLink>().SingleOrDefaultAsync(l => l.Id == linkId && l.SavedContributionPlanId == planId, ct);
            if (link is null) return false;
            link.RemovedAtUtc ??= clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
        }
    }
}
