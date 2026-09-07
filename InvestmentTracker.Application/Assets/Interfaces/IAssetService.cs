using System;
using System.Collections.Generic;
using System.Text;
using InvestmentTracker.Application.Assets.Dtos;

namespace InvestmentTracker.Application.Assets.Interfaces
{
    public interface IAssetService
    {
        Task<IReadOnlyList<AssetDto>> GetAllAsync(
            CancellationToken cancellationToken = default);

        Task<AssetDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<AssetDto> CreateAsync(
            CreateAssetDto dto,
            CancellationToken cancellationToken = default);

        Task<AssetDto?> UpdateAsync(
            int id,
            UpdateAssetDto dto,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteAsync(
            int id,
            CancellationToken cancellationToken = default);
    }
}
