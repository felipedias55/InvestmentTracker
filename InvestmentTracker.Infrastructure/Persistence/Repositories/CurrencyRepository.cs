using InvestmentTracker.Application.Common.Exceptions;
using Microsoft.Data.SqlClient;
using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using InvestmentTracker.Application.Currencies.Interfaces;

namespace InvestmentTracker.Infrastructure.Persistence.Repositories
{
    public sealed class CurrencyRepository(
    InvestmentTrackerDbContext context) : ICurrencyRepository
    {
        public async Task<IReadOnlyList<Currency>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return await context.Currencies
                .AsNoTracking()
                .OrderBy(item => item.Code)
                .ToListAsync(cancellationToken);
        }

        public async Task<Currency?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await context.Currencies
                .FirstOrDefaultAsync(
                    item => item.Id == id,
                    cancellationToken);
        }

        public async Task<bool> ExistsByCodeAsync(
            string code,
            int? excludingId = null,
            CancellationToken cancellationToken = default)
        {
            var query = context.Currencies
                .AsNoTracking()
                .Where(item => item.Code == code);

            if (excludingId.HasValue)
            {
                query = query.Where(
                    item => item.Id != excludingId.Value);
            }

            return await query.AnyAsync(cancellationToken);
        }

        public async Task AddAsync(
            Currency item,
            CancellationToken cancellationToken = default)
        {
            await context.Currencies.AddAsync(
                item,
                cancellationToken);
        }

        public Task DeleteAsync(
            Currency item,
            CancellationToken cancellationToken = default)
        {
            context.Currencies.Remove(item);

            return Task.CompletedTask;
        }

        public async Task<bool> IsInUseAsync(int currencyId, CancellationToken cancellationToken = default)
            => await context.Assets.AnyAsync(x => x.CurrencyId == currencyId, cancellationToken)
                || await context.Portfolios.AnyAsync(x => x.BaseCurrencyId == currencyId, cancellationToken)
                || await context.ExternalAssets.AnyAsync(x => x.CurrencyId == currencyId, cancellationToken)
                || await context.PortfolioCashFlows.AnyAsync(x => x.CurrencyId == currencyId, cancellationToken);

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
                && ex.Entries.All(entry => entry.Entity is Currency))
            {
                throw new ResourceConflictException(
                    "Já existe uma moeda com esse código.", ex);
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException { Number: 547 }
                && ex.Entries.Count > 0
                && ex.Entries.All(entry => entry.Entity is Currency
                    && entry.State == EntityState.Deleted))
            {
                throw new ResourceConflictException(
                    "A moeda está em uso e não pode ser excluída.", ex);
            }
        }
    }
}
