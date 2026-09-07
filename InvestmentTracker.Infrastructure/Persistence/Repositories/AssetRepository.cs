using InvestmentTracker.Application.Common.Exceptions;
using Microsoft.Data.SqlClient;
using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using InvestmentTracker.Application.Assets.Interfaces;

namespace InvestmentTracker.Infrastructure.Persistence.Repositories
{
    public sealed class AssetRepository(
    InvestmentTrackerDbContext context) : IAssetRepository
    {
        public async Task<IReadOnlyList<Asset>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return await context.Assets
                .AsNoTracking()
                .OrderBy(item => item.Ticker)
                .ToListAsync(cancellationToken);
        }

        public async Task<Asset?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await context.Assets
                .FirstOrDefaultAsync(
                    item => item.Id == id,
                    cancellationToken);
        }

        public async Task<bool> ExistsByTickerAsync(
            string ticker,
            int? excludingId = null,
            CancellationToken cancellationToken = default)
        {
            var query = context.Assets
                .AsNoTracking()
                .Where(item => item.Ticker == ticker);

            if (excludingId.HasValue)
            {
                query = query.Where(
                    item => item.Id != excludingId.Value);
            }

            return await query.AnyAsync(cancellationToken);
        }

        public async Task AddAsync(
            Asset item,
            CancellationToken cancellationToken = default)
        {
            await context.Assets.AddAsync(
                item,
                cancellationToken);
        }

        public Task DeleteAsync(
            Asset item,
            CancellationToken cancellationToken = default)
        {
            context.Assets.Remove(item);

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
                && ex.Entries.All(entry => entry.Entity is Asset))
            {
                throw new ResourceConflictException(
                    "Já existe um ativo com esse ticker.", ex);
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException { Number: 547 }
                && ex.Entries.Count > 0
                && ex.Entries.All(entry => entry.Entity is Asset
                    && entry.State == EntityState.Deleted))
            {
                throw new ResourceConflictException(
                    "O ativo está em uso e não pode ser excluído.", ex);
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException { Number: 547 }
                && ex.Entries.Count > 0
                && ex.Entries.All(entry => entry.Entity is Asset
                    && entry.State is EntityState.Added or EntityState.Modified))
            {
                throw new ResourceConflictException(
                    "Uma classificação do ativo não está mais disponível. Atualize os cadastros e tente novamente.", ex);
            }
        }
    }
}
