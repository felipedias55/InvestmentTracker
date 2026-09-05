using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using InvestmentTracker.Application.AssetTypes.Interfaces;

namespace InvestmentTracker.Infrastructure.Persistence.Repositories
{
    public sealed class AssetTypeRepository(
    InvestmentTrackerDbContext context) : IAssetTypeRepository
    {
        public async Task<IReadOnlyList<AssetType>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return await context.AssetTypes
                .AsNoTracking()
                .OrderBy(assetType => assetType.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<AssetType?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await context.AssetTypes
                .FirstOrDefaultAsync(
                    assetType => assetType.Id == id,
                    cancellationToken);
        }

        public async Task<bool> ExistsByNameAsync(
            string name,
            int? excludingId = null,
            CancellationToken cancellationToken = default)
        {
            var query = context.AssetTypes
                .AsNoTracking()
                .Where(assetType => assetType.Name == name);

            if (excludingId.HasValue)
            {
                query = query.Where(
                    assetType => assetType.Id != excludingId.Value);
            }

            return await query.AnyAsync(cancellationToken);
        }

        public async Task AddAsync(
            AssetType assetType,
            CancellationToken cancellationToken = default)
        {
            await context.AssetTypes.AddAsync(
                assetType,
                cancellationToken);
        }

        public Task DeleteAsync(
            AssetType assetType,
            CancellationToken cancellationToken = default)
        {
            context.AssetTypes.Remove(assetType);

            return Task.CompletedTask;
        }

        public async Task SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
