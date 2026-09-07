using InvestmentTracker.Application.Countries.Dtos;

namespace InvestmentTracker.Application.Countries.Interfaces
{
    public interface ICountryService
    {
        Task<IReadOnlyList<CountryDto>> GetAllAsync(
            CancellationToken cancellationToken = default);

        Task<CountryDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<CountryDto> CreateAsync(
            CreateCountryDto dto,
            CancellationToken cancellationToken = default);

        Task<CountryDto?> UpdateAsync(
            int id,
            UpdateCountryDto dto,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteAsync(
            int id,
            CancellationToken cancellationToken = default);
    }
}
