using InvestmentTracker.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvestmentTracker.Application.AssetTypes.Interfaces
{
    public interface IAssetTypeRepository
    {
        Task<IReadOnlyList<AssetType>> GetAllAsync(
            CancellationToken cancellationToken = default);

        Task<AssetType?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsByNameAsync(
            string name,
            int? excludingId = null,
            CancellationToken cancellationToken = default);

        Task AddAsync(
            AssetType assetType,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(
            AssetType assetType,
            CancellationToken cancellationToken = default);

        Task SaveChangesAsync(
            CancellationToken cancellationToken = default);
    }
}
