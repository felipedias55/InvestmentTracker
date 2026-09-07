using InvestmentTracker.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvestmentTracker.Application.Assets.Interfaces
{
    public interface IAssetRepository
    {
        Task<IReadOnlyList<Asset>> GetAllAsync(
            CancellationToken cancellationToken = default);

        Task<Asset?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsByTickerAsync(
            string ticker,
            int? excludingId = null,
            CancellationToken cancellationToken = default);

        Task AddAsync(
            Asset asset,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(
            Asset asset,
            CancellationToken cancellationToken = default);

        Task SaveChangesAsync(
            CancellationToken cancellationToken = default);
    }
}
