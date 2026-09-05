using System;
using System.Collections.Generic;
using System.Text;
using InvestmentTracker.Application.AssetTypes.Dtos;

namespace InvestmentTracker.Application.AssetTypes.Interfaces
{
    public interface IAssetTypeService
    {
        Task<IReadOnlyList<AssetTypeDto>> GetAllAsync(
            CancellationToken cancellationToken = default);

        Task<AssetTypeDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<AssetTypeDto> CreateAsync(
            CreateAssetTypeDto dto,
            CancellationToken cancellationToken = default);

        Task<AssetTypeDto?> UpdateAsync(
            int id,
            UpdateAssetTypeDto dto,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteAsync(
            int id,
            CancellationToken cancellationToken = default);
    }
}
