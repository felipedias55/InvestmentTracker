using InvestmentTracker.Application.Allocation;
using InvestmentTracker.Application.Allocation.Dtos;
using InvestmentTracker.Application.Allocation.Interfaces;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.Infrastructure.Persistence.Repositories
{
    public sealed class AllocationRepository(InvestmentTrackerDbContext context) : IAllocationRepository
    {
        public async Task<IReadOnlyList<AllocationTargetDto>> GetTargetsAsync(int portfolioId, AllocationDimension dimension,
            CancellationToken cancellationToken)
        {
            if (dimension == AllocationDimension.Category)
                return await context.CategoryAllocationTargets.AsNoTracking().Where(t => t.PortfolioId == portfolioId)
                    .OrderBy(t => t.AssetCategory.Name).Select(t => new AllocationTargetDto(t.AssetCategoryId, t.AssetCategory.Name, t.TargetPercentage))
                    .ToListAsync(cancellationToken);
            return await context.SectorAllocationTargets.AsNoTracking().Where(t => t.PortfolioId == portfolioId)
                .OrderBy(t => t.Sector.Name).Select(t => new AllocationTargetDto(t.SectorId, t.Sector.Name, t.TargetPercentage))
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> GroupsExistAsync(IReadOnlyList<int> ids, AllocationDimension dimension, CancellationToken cancellationToken)
        {
            var count = dimension == AllocationDimension.Category
                ? await context.AssetCategories.CountAsync(c => ids.Contains(c.Id), cancellationToken)
                : await context.Sectors.CountAsync(s => ids.Contains(s.Id), cancellationToken);
            return count == ids.Count;
        }

        public async Task ReplaceTargetsAsync(int portfolioId, AllocationDimension dimension, IReadOnlyList<TargetEntryDto> targets,
            CancellationToken cancellationToken)
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Serialize replacements for this portfolio, including the initially empty target set.
                var portfolio = await context.Portfolios.FromSqlInterpolated(
                    $"SELECT * FROM Portfolio WITH (UPDLOCK, HOLDLOCK) WHERE Id = {portfolioId}")
                    .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
                if (portfolio is null) throw new ResourceConflictException("A carteira foi removida. Atualize a página.");
                if (dimension == AllocationDimension.Category)
                {
                    await context.CategoryAllocationTargets.Where(t => t.PortfolioId == portfolioId).ExecuteDeleteAsync(cancellationToken);
                    context.CategoryAllocationTargets.AddRange(targets.Select(t => new CategoryAllocationTarget
                    { PortfolioId = portfolioId, AssetCategoryId = t.GroupId, TargetPercentage = t.TargetPercentage }));
                }
                else
                {
                    await context.SectorAllocationTargets.Where(t => t.PortfolioId == portfolioId).ExecuteDeleteAsync(cancellationToken);
                    context.SectorAllocationTargets.AddRange(targets.Select(t => new SectorAllocationTarget
                    { PortfolioId = portfolioId, SectorId = t.GroupId, TargetPercentage = t.TargetPercentage }));
                }
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 or 2601 or 2627 or 1205 })
            { throw new ResourceConflictException("As referências ou metas mudaram. Atualize a página e tente novamente.", ex); }
            catch (SqlException ex) when (ex.Number == 1205)
            { throw new ResourceConflictException("As metas foram alteradas simultaneamente. Tente novamente.", ex); }
        }
    }
}
