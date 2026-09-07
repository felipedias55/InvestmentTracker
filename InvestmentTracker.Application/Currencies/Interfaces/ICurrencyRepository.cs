using InvestmentTracker.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvestmentTracker.Application.Currencies.Interfaces
{
    public interface ICurrencyRepository
    {
        Task<IReadOnlyList<Currency>> GetAllAsync(
            CancellationToken cancellationToken = default);

        Task<Currency?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsByCodeAsync(
            string code,
            int? excludingId = null,
            CancellationToken cancellationToken = default);

        Task AddAsync(
            Currency currency,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(
            Currency currency,
            CancellationToken cancellationToken = default);

        Task SaveChangesAsync(
            CancellationToken cancellationToken = default);
    }
}
