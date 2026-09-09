using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.ExternalAssets.Interfaces;
using InvestmentTracker.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.Infrastructure.Persistence.Repositories
{
    public sealed class ExternalAssetRepository(InvestmentTrackerDbContext context) : IExternalAssetRepository
    {
        public async Task<IReadOnlyList<ExternalAsset>> GetAllAsync(int portfolioId, CancellationToken cancellationToken)
            => await context.ExternalAssets.AsNoTracking().Include(a => a.Currency).Where(a => a.PortfolioId == portfolioId)
                .OrderBy(a => a.Name).ThenBy(a => a.Id).ToListAsync(cancellationToken);
        public Task<ExternalAsset?> GetByIdAsync(int portfolioId, int id, CancellationToken cancellationToken)
            => context.ExternalAssets.SingleOrDefaultAsync(a => a.PortfolioId == portfolioId && a.Id == id, cancellationToken);
        public async Task AddAsync(ExternalAsset asset, CancellationToken cancellationToken)
            => await context.ExternalAssets.AddAsync(asset, cancellationToken);
        public void Remove(ExternalAsset asset) => context.ExternalAssets.Remove(asset);
        public async Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            try { await context.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException ex)
            { throw new ResourceConflictException("O patrimônio externo foi alterado ou removido. Atualize a página.", ex); }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
            { throw new ResourceConflictException("A carteira ou moeda foi removida, ou o valor é inválido. Atualize a página.", ex); }
        }
    }
}
