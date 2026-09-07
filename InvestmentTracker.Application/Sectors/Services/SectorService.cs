using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Sectors.Dtos;
using InvestmentTracker.Application.Sectors.Interfaces;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.Sectors.Services
{
    public sealed class SectorService(
    ISectorRepository repository) : ISectorService
    {
        public async Task<IReadOnlyList<SectorDto>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            var sectors = await repository.GetAllAsync(cancellationToken);

            return sectors
                .Select(MapToDto)
                .ToList();
        }

        public async Task<SectorDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            var sector = await repository.GetByIdAsync(
                id,
                cancellationToken);

            return sector is null
                ? null
                : MapToDto(sector);
        }

        public async Task<SectorDto> CreateAsync(
            CreateSectorDto dto,
            CancellationToken cancellationToken = default)
        {
            var name = ValidateName(dto.Name);

            var exists = await repository.ExistsByNameAsync(
                name,
                cancellationToken: cancellationToken);

            if (exists)
            {
                throw new ResourceConflictException(
                    "Já existe um setor com esse nome.");
            }

            var sector = new Sector
            {
                Name = name
            };

            await repository.AddAsync(
                sector,
                cancellationToken);

            await repository.SaveChangesAsync(
                cancellationToken);

            return MapToDto(sector);
        }

        public async Task<SectorDto?> UpdateAsync(
            int id,
            UpdateSectorDto dto,
            CancellationToken cancellationToken = default)
        {
            var name = ValidateName(dto.Name);

            var sector = await repository.GetByIdAsync(
                id,
                cancellationToken);

            if (sector is null)
            {
                return null;
            }

            var exists = await repository.ExistsByNameAsync(
                name,
                id,
                cancellationToken);

            if (exists)
            {
                throw new ResourceConflictException(
                    "Já existe um setor com esse nome.");
            }

            sector.Name = name;

            await repository.SaveChangesAsync(
                cancellationToken);

            return MapToDto(sector);
        }

        public async Task<bool> DeleteAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            var sector = await repository.GetByIdAsync(
                id,
                cancellationToken);

            if (sector is null)
            {
                return false;
            }

            await repository.DeleteAsync(
                sector,
                cancellationToken);

            await repository.SaveChangesAsync(
                cancellationToken);

            return true;
        }

        private static string ValidateName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InputValidationException(
                    "O nome do setor é obrigatório.");
            }

            var name = value.Trim();
            if (name.Length > Sector.NameMaxLength)
            {
                throw new InputValidationException(
                    $"O nome do setor deve ter no máximo {Sector.NameMaxLength} caracteres.");
            }

            return name;
        }

        private static SectorDto MapToDto(Sector sector)
        {
            return new SectorDto(
                sector.Id,
                sector.Name);
        }
    }
}
