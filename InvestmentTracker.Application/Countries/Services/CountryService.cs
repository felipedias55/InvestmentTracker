using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Countries.Dtos;
using InvestmentTracker.Application.Countries.Interfaces;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.Countries.Services
{
    public sealed class CountryService(
    ICountryRepository repository) : ICountryService
    {
        public async Task<IReadOnlyList<CountryDto>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            var countries = await repository.GetAllAsync(cancellationToken);

            return countries
                .Select(MapToDto)
                .ToList();
        }

        public async Task<CountryDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            var country = await repository.GetByIdAsync(
                id,
                cancellationToken);

            return country is null
                ? null
                : MapToDto(country);
        }

        public async Task<CountryDto> CreateAsync(
            CreateCountryDto dto,
            CancellationToken cancellationToken = default)
        {
            var name = ValidateName(dto.Name);

            var exists = await repository.ExistsByNameAsync(
                name,
                cancellationToken: cancellationToken);

            if (exists)
            {
                throw new ResourceConflictException(
                    "Já existe um país com esse nome.");
            }

            var country = new Country
            {
                Name = name
            };

            await repository.AddAsync(
                country,
                cancellationToken);

            await repository.SaveChangesAsync(
                cancellationToken);

            return MapToDto(country);
        }

        public async Task<CountryDto?> UpdateAsync(
            int id,
            UpdateCountryDto dto,
            CancellationToken cancellationToken = default)
        {
            var name = ValidateName(dto.Name);

            var country = await repository.GetByIdAsync(
                id,
                cancellationToken);

            if (country is null)
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
                    "Já existe um país com esse nome.");
            }

            country.Name = name;

            await repository.SaveChangesAsync(
                cancellationToken);

            return MapToDto(country);
        }

        public async Task<bool> DeleteAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            var country = await repository.GetByIdAsync(
                id,
                cancellationToken);

            if (country is null)
            {
                return false;
            }

            await repository.DeleteAsync(
                country,
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
                    "O nome do país é obrigatório.");
            }

            var name = value.Trim();
            if (name.Length > Country.NameMaxLength)
            {
                throw new InputValidationException(
                    $"O nome do país deve ter no máximo {Country.NameMaxLength} caracteres.");
            }

            return name;
        }

        private static CountryDto MapToDto(Country country)
        {
            return new CountryDto(
                country.Id,
                country.Name);
        }
    }
}
