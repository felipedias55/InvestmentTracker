using InvestmentTracker.Application.Common.Exceptions;
using Microsoft.Data.SqlClient;
using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using InvestmentTracker.Application.Countries.Interfaces;

namespace InvestmentTracker.Infrastructure.Persistence.Repositories
{
    public sealed class CountryRepository(
    InvestmentTrackerDbContext context) : ICountryRepository
    {
        public async Task<IReadOnlyList<Country>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return await context.Countries
                .AsNoTracking()
                .OrderBy(item => item.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<Country?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await context.Countries
                .FirstOrDefaultAsync(
                    item => item.Id == id,
                    cancellationToken);
        }

        public async Task<bool> ExistsByNameAsync(
            string name,
            int? excludingId = null,
            CancellationToken cancellationToken = default)
        {
            var query = context.Countries
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
            Country item,
            CancellationToken cancellationToken = default)
        {
            await context.Countries.AddAsync(
                item,
                cancellationToken);
        }

        public Task DeleteAsync(
            Country item,
            CancellationToken cancellationToken = default)
        {
            context.Countries.Remove(item);

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
                && ex.Entries.All(entry => entry.Entity is Country))
            {
                throw new ResourceConflictException(
                    "Já existe um país com esse nome.", ex);
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException { Number: 547 }
                && ex.Entries.Count > 0
                && ex.Entries.All(entry => entry.Entity is Country
                    && entry.State == EntityState.Deleted))
            {
                throw new ResourceConflictException(
                    "O país está em uso e não pode ser excluído.", ex);
            }
        }
    }
}
