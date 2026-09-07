using InvestmentTracker.Application.Common.Exceptions;
using Microsoft.Data.SqlClient;
using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using InvestmentTracker.Application.AssetCategories.Interfaces;

namespace InvestmentTracker.Infrastructure.Persistence.Repositories
{
    public sealed class AssetCategoryRepository(
    InvestmentTrackerDbContext context) : IAssetCategoryRepository
    {
        public async Task<IReadOnlyList<AssetCategory>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return await context.AssetCategories
                .AsNoTracking()
                .OrderBy(item => item.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<AssetCategory?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await context.AssetCategories
                .FirstOrDefaultAsync(
                    item => item.Id == id,
                    cancellationToken);
        }

        public async Task<bool> ExistsByNameAsync(
            string name,
            int? excludingId = null,
            CancellationToken cancellationToken = default)
        {
            var query = context.AssetCategories
                .AsNoTracking()
                .Where(item => item.Name == name);

            if (excludingId.HasValue)
            {
                query = query.Where(
                    item => item.Id != excludingId.Value);
            }

            return await query.AnyAsync(cancellationToken);
        }

        public async Task AddAsync(
            AssetCategory item,
            CancellationToken cancellationToken = default)
        {
            await context.AssetCategories.AddAsync(
                item,
                cancellationToken);
        }

        public Task DeleteAsync(
            AssetCategory item,
            CancellationToken cancellationToken = default)
        {
            context.AssetCategories.Remove(item);

            return Task.CompletedTask;
        }

        public async Task SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException { Number: 2601 or 2627 }
                && ex.Entries.Count > 0
                && ex.Entries.All(entry => entry.Entity is AssetCategory))
            {
                throw new ResourceConflictException(
                    "Já existe uma categoria com esse nome.", ex);
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException { Number: 547 }
                && ex.Entries.Count > 0
                && ex.Entries.All(entry => entry.Entity is AssetCategory
                    && entry.State == EntityState.Deleted))
            {
                throw new ResourceConflictException(
                    "A categoria está em uso e não pode ser excluída.", ex);
            }
        }
    }
}
