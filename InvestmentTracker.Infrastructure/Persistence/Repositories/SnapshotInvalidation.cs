using System.Data;
using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.Infrastructure.Persistence.Repositories
{
    internal static class SnapshotInvalidation
    {
        public static async Task SaveAsync(InvestmentTrackerDbContext db, CancellationToken ct)
        {
            db.ChangeTracker.DetectChanges();
            var changes = db.ChangeTracker.Entries<PortfolioAsset>().Where(e => e.State is EntityState.Added or EntityState.Deleted)
                .Select(e => (e.Entity.PortfolioId, e.Entity.UpdatedOn))
                .Concat(db.ChangeTracker.Entries<ExternalAsset>().Where(e => e.State is EntityState.Added or EntityState.Deleted
                    || (e.State == EntityState.Modified && e.Property(x => x.Value).IsModified))
                    .Select(e => (e.Entity.PortfolioId, e.Entity.UpdatedOn))).Where(x => x.PortfolioId > 0)
                .GroupBy(x => x.PortfolioId).Select(g => (Id: g.Key, Date: g.Min(x => x.UpdatedOn))).OrderBy(x => x.Id).ToList();
            if (changes.Count == 0) { await db.SaveChangesAsync(ct); return; }
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            foreach (var change in changes)
            {
                await db.Portfolios.FromSqlInterpolated($"SELECT * FROM Portfolio WITH (UPDLOCK, HOLDLOCK) WHERE Id = {change.Id}").AsNoTracking().SingleAsync(ct);
                var snapshots = await db.PortfolioSnapshots.Where(x => x.PortfolioId == change.Id && x.SnapshotDate >= change.Date).ToListAsync(ct);
                foreach (var snapshot in snapshots) snapshot.IsOutdated = true;
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
    }
}
