using InvestmentTracker.Domain.Entities;
using InvestmentTracker.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.Infrastructure.Persistence.Repositories
{
    // Called inside the operation's transaction. Records actual before/after balances, not inferred history.
    internal static class MovementRecorder
    {
        public static async Task SaveAsync(InvestmentTrackerDbContext db, FinancialMovement movement, CancellationToken ct)
        {
            db.ChangeTracker.DetectChanges();
            var positions = db.ChangeTracker.Entries<PortfolioAsset>().Where(e => e.State is EntityState.Modified or EntityState.Added)
                .Select(e => (Entry: e, Effect: new MovementEffect {
                    BeforeQuantity = e.State == EntityState.Added ? 0 : e.OriginalValues.GetValue<decimal>("Quantity"),
                    BeforeCost = e.State == EntityState.Added ? 0 : e.OriginalValues.GetValue<decimal>("InvestedAmount"),
                    BeforeValue = e.State == EntityState.Added ? 0 : e.OriginalValues.GetValue<decimal>("CurrentValue"),
                    BeforeIncome = e.State == EntityState.Added ? 0 : e.OriginalValues.GetValue<decimal>("Income"),
                    AfterQuantity = e.Entity.Quantity, AfterCost = e.Entity.InvestedAmount,
                    AfterValue = e.Entity.CurrentValue, AfterIncome = e.Entity.Income })).ToList();
            var balances = db.ChangeTracker.Entries<ExternalAsset>().Where(e => e.State is EntityState.Modified or EntityState.Added)
                .Where(e => e.State == EntityState.Added || e.OriginalValues.GetValue<decimal>("Value") != e.Entity.Value)
                .Select(e => (Entry: e, Effect: new MovementEffect {
                    BeforeValue = e.State == EntityState.Added ? 0 : e.OriginalValues.GetValue<decimal>("Value"),
                    AfterValue = e.Entity.Value, CurrencyId = e.Entity.CurrencyId, Name = e.Entity.Name })).ToList();
            if (movement.Kind is not ("reopen" or "income-conversion"))
            {
                var snapshots = await db.PortfolioSnapshots.Where(x => x.PortfolioId == movement.PortfolioId && x.SnapshotDate >= movement.Date).ToListAsync(ct);
                if (movement.Kind is not ("historical" or "reversal" or "position-adjustment") && snapshots.Any(x => x.SnapshotDate > movement.Date && !x.IsReopened))
                    throw new ResourceConflictException("O período está fechado. Reabra-o em Evolução e fechamentos, informando o motivo.");
                if (movement.Kind is not ("historical" or "reversal" or "position-adjustment"))
                {
                    var positionIds = positions.Select(x => x.Entry.Entity.Id).Where(x => x != 0).ToArray();
                    var cashIds = balances.Select(x => x.Entry.Entity.Id).Where(x => x != 0).ToArray();
                    var dependent = await db.Set<FinancialMovement>().AnyAsync(x => x.PortfolioId == movement.PortfolioId && x.Date > movement.Date
                        && x.ReversalOfId == null && x.Kind != "reopen" && x.Kind != "historical" && x.Kind != "income-conversion"
                        && !db.Set<FinancialMovement>().Any(r => r.ReversalOfId == x.Id)
                        && (!x.Effects.Any() || x.Effects.Any(e => (e.PositionId.HasValue && positionIds.Contains(e.PositionId.Value))
                            || (e.CashAssetId.HasValue && cashIds.Contains(e.CashAssetId.Value)))), ct);
                    if (dependent) throw new ResourceConflictException("Há operações posteriores neste ativo ou saldo. Estorne as dependentes da mais recente para a mais antiga, lance a operação atrasada e registre novamente as posteriores em ordem de data. Fotografias antigas não serão recalculadas automaticamente.");
                }
                foreach (var snapshot in snapshots) snapshot.IsOutdated = true;
            }
            db.Set<FinancialMovement>().Add(movement);
            foreach (var flow in db.ChangeTracker.Entries<PortfolioCashFlow>().Where(e => e.State == EntityState.Added))
                flow.Entity.Movement = movement;
            await db.SaveChangesAsync(ct);
            foreach (var (entry, effect) in positions)
            {
                var asset = await db.Assets.AsNoTracking().SingleAsync(x => x.Id == entry.Entity.AssetId, ct);
                effect.PositionId = entry.Entity.Id; effect.CurrencyId = asset.CurrencyId; effect.Name = asset.Ticker;
                movement.Effects.Add(effect);
            }
            foreach (var (entry, effect) in balances) { effect.CashAssetId = entry.Entity.Id; movement.Effects.Add(effect); }
            await db.SaveChangesAsync(ct);
        }
    }
}
