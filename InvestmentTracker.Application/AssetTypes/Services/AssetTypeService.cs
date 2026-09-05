using InvestmentTracker.Application.AssetTypes.Dtos;
using InvestmentTracker.Application.AssetTypes.Interfaces;
using InvestmentTracker.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvestmentTracker.Application.AssetTypes.Services
{
    public sealed class AssetTypeService(
    IAssetTypeRepository repository) : IAssetTypeService
    {
        public async Task<IReadOnlyList<AssetTypeDto>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            var assetTypes = await repository.GetAllAsync(cancellationToken);

            return assetTypes
                .Select(MapToDto)
                .ToList();
        }

        public async Task<AssetTypeDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            var assetType = await repository.GetByIdAsync(
                id,
                cancellationToken);

            return assetType is null
                ? null
                : MapToDto(assetType);
        }

        public async Task<AssetTypeDto> CreateAsync(
            CreateAssetTypeDto dto,
            CancellationToken cancellationToken = default)
        {
            var name = dto.Name.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "O nome do tipo de ativo é obrigatório.",
                    nameof(dto));
            }

            var exists = await repository.ExistsByNameAsync(
                name,
                cancellationToken: cancellationToken);

            if (exists)
            {
                throw new InvalidOperationException(
                    "Já existe um tipo de ativo com esse nome.");
            }

            var assetType = new AssetType
            {
                Name = name
            };

            await repository.AddAsync(
                assetType,
                cancellationToken);

            await repository.SaveChangesAsync(
                cancellationToken);

            return MapToDto(assetType);
        }

        public async Task<AssetTypeDto?> UpdateAsync(
            int id,
            UpdateAssetTypeDto dto,
            CancellationToken cancellationToken = default)
        {
            var name = dto.Name.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "O nome do tipo de ativo é obrigatório.",
                    nameof(dto));
            }

            var assetType = await repository.GetByIdAsync(
                id,
                cancellationToken);

            if (assetType is null)
            {
                return null;
            }

            var exists = await repository.ExistsByNameAsync(
                name,
                id,
                cancellationToken);

            if (exists)
            {
                throw new InvalidOperationException(
                    "Já existe um tipo de ativo com esse nome.");
            }

            assetType.Name = name;

            await repository.SaveChangesAsync(
                cancellationToken);

            return MapToDto(assetType);
        }

        public async Task<bool> DeleteAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            var assetType = await repository.GetByIdAsync(
                id,
                cancellationToken);

            if (assetType is null)
            {
                return false;
            }

            await repository.DeleteAsync(
                assetType,
                cancellationToken);

            await repository.SaveChangesAsync(
                cancellationToken);

            return true;
        }

        private static AssetTypeDto MapToDto(AssetType assetType)
        {
            return new AssetTypeDto(
                assetType.Id,
                assetType.Name);
        }
    }
}
