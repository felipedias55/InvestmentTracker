using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Portfolios.Interfaces;
using InvestmentTracker.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.Infrastructure.Persistence.Repositories
{
    public sealed class PortfolioRepository(InvestmentTrackerDbContext context) : IPortfolioRepository
    {
        public async Task<IReadOnlyList<Portfolio>> GetAllAsync(CancellationToken cancellationToken)
            => await context.Portfolios.AsNoTracking().Include(p => p.BaseCurrency).OrderBy(p => p.Id).ToListAsync(cancellationToken);
        public Task<Portfolio?> GetByIdAsync(int id, CancellationToken cancellationToken)
            => context.Portfolios.Include(p => p.BaseCurrency).SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        public async Task<IReadOnlyList<PortfolioAsset>> GetPositionsAsync(int portfolioId, CancellationToken cancellationToken)
            => await context.PortfolioAssets.AsNoTracking().Include(p => p.Asset).ThenInclude(a => a.Currency)
                .Include(p => p.Asset).ThenInclude(a => a.AssetType)
                .Include(p => p.Asset).ThenInclude(a => a.AssetCategory)
                .Include(p => p.Asset).ThenInclude(a => a.Sector)
                .Include(p => p.Asset).ThenInclude(a => a.Country)
                .Where(p => p.PortfolioId == portfolioId).OrderBy(p => p.Asset.Ticker).ToListAsync(cancellationToken);
        public Task<PortfolioAsset?> GetPositionAsync(int portfolioId, int id, CancellationToken cancellationToken)
            => context.PortfolioAssets.SingleOrDefaultAsync(p => p.PortfolioId == portfolioId && p.Id == id, cancellationToken);
        public Task<bool> HasAssetAsync(int portfolioId, int assetId, int? excludingId, CancellationToken cancellationToken)
            => context.PortfolioAssets.AnyAsync(p => p.PortfolioId == portfolioId && p.AssetId == assetId
                && (!excludingId.HasValue || p.Id != excludingId), cancellationToken);
        public async Task AddAsync(Portfolio portfolio, CancellationToken cancellationToken)
            => await context.Portfolios.AddAsync(portfolio, cancellationToken);
        public async Task AddPositionAsync(PortfolioAsset position, CancellationToken cancellationToken)
            => await context.PortfolioAssets.AddAsync(position, cancellationToken);
        public void RemovePosition(PortfolioAsset position) => context.PortfolioAssets.Remove(position);
        public async Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            try { await context.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException ex)
            { throw new ResourceConflictException("A posição foi alterada ou removida. Atualize a carteira.", ex); }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            { throw new ResourceConflictException("O ativo já possui uma posição nesta carteira.", ex); }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
            { throw new ResourceConflictException("A moeda, carteira ou ativo não está mais disponível, ou os valores são inválidos.", ex); }
        }
    }
}
