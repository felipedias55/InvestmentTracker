using InvestmentTracker.Application.AssetTypes.Exceptions;
using Microsoft.Data.SqlClient;
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
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException { Number: 2601 or 2627 }
                && ex.Entries.Count > 0
                && ex.Entries.All(entry => entry.Entity is AssetType))
            {
                throw new AssetTypeConflictException(
                    "Já existe um tipo de ativo com esse nome.", ex);
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException { Number: 547 }
                && ex.Entries.Count > 0
                && ex.Entries.All(entry => entry.Entity is AssetType
                    && entry.State == EntityState.Deleted))
            {
                throw new AssetTypeConflictException(
                    "O tipo de ativo está em uso e não pode ser excluído.", ex);
            }
        }
    }
}
