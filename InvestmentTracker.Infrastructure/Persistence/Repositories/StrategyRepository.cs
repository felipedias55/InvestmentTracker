using System.Data;
using System.Text.Json;
using InvestmentTracker.Application.Strategies;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.Infrastructure.Persistence.Repositories
{
    public sealed class StrategyRepository(InvestmentTrackerDbContext db, TimeProvider clock) : IStrategyRepository
    {
        private static SavedStrategy Read(PortfolioStrategyRevision r) => new(r.ProfileId, r.Revision, r.CreatedAtUtc,
            JsonSerializer.Deserialize<StrategyConfig>(r.ConfigurationJson)!);
        public async Task<IReadOnlyList<SavedStrategy>?> ListAsync(int portfolioId, CancellationToken ct)
        {
            if (!await db.Portfolios.AnyAsync(p => p.Id == portfolioId, ct)) return null;
            var rows = await db.Set<PortfolioStrategyRevision>().AsNoTracking().Where(r => r.PortfolioId == portfolioId).OrderByDescending(r => r.Id).ToListAsync(ct);
            return rows.GroupBy(r => r.ProfileId).Select(g => Read(g.First())).ToList();
        }
        public async Task<SavedStrategy?> SaveAsync(int portfolioId, SaveStrategy request, CancellationToken ct)
        {


            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (!await db.Portfolios.AnyAsync(p => p.Id == portfolioId, ct)) return null;
            var revision = await db.Set<PortfolioStrategyRevision>().Where(r => r.PortfolioId == portfolioId && r.ProfileId == request.ProfileId)
                .Select(r => (int?)r.Revision).MaxAsync(ct) ?? 0;
            if (revision != request.ExpectedRevision)
                throw new ResourceConflictException("A estratégia mudou em outra sessão. Recarregue antes de salvar.");
            var ids = request.Config.Rules.Select(r => r.CategoryId).Concat(request.Config.ExcludedCategoryIds).Distinct().ToList();
            if (await db.AssetCategories.CountAsync(c => ids.Contains(c.Id), ct) != ids.Count)
                throw new InputValidationException("Categoria inexistente na estratégia.");
            var row = new PortfolioStrategyRevision { PortfolioId = portfolioId, ProfileId = request.ProfileId, Revision = revision + 1,
                CreatedAtUtc = clock.GetUtcNow().UtcDateTime, ConfigurationJson = JsonSerializer.Serialize(request.Config) };
            db.Add(row);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Read(row);
        }
    }
}
