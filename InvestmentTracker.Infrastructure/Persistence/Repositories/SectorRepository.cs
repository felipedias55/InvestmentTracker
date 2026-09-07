using InvestmentTracker.Application.Common.Exceptions;
using Microsoft.Data.SqlClient;
using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using InvestmentTracker.Application.Sectors.Interfaces;

namespace InvestmentTracker.Infrastructure.Persistence.Repositories
{
    public sealed class SectorRepository(
    InvestmentTrackerDbContext context) : ISectorRepository
    {
        public async Task<IReadOnlyList<Sector>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return await context.Sectors
                .AsNoTracking()
                .OrderBy(item => item.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<Sector?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await context.Sectors
                .FirstOrDefaultAsync(
                    item => item.Id == id,
                    cancellationToken);
        }

        public async Task<bool> ExistsByNameAsync(
            string name,
            int? excludingId = null,
            CancellationToken cancellationToken = default)
        {
            var query = context.Sectors
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
            Sector item,
            CancellationToken cancellationToken = default)
        {
            await context.Sectors.AddAsync(
                item,
                cancellationToken);
        }

        public Task DeleteAsync(
            Sector item,
            CancellationToken cancellationToken = default)
        {
            context.Sectors.Remove(item);

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
                && ex.Entries.All(entry => entry.Entity is Sector))
            {
                throw new ResourceConflictException(
                    "Já existe um setor com esse nome.", ex);
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException { Number: 547 }
                && ex.Entries.Count > 0
                && ex.Entries.All(entry => entry.Entity is Sector
                    && entry.State == EntityState.Deleted))
            {
                throw new ResourceConflictException(
                    "O setor está em uso e não pode ser excluído.", ex);
            }
        }
    }
}
