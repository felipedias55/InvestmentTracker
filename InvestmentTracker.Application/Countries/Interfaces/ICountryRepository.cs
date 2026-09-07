using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.Countries.Interfaces
{
    public interface ICountryRepository
    {
        Task<IReadOnlyList<Country>> GetAllAsync(
            CancellationToken cancellationToken = default);

        Task<Country?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsByNameAsync(
            string name,
            int? excludingId = null,
            CancellationToken cancellationToken = default);

        Task AddAsync(
            Country country,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(
            Country country,
            CancellationToken cancellationToken = default);

        Task SaveChangesAsync(
            CancellationToken cancellationToken = default);
    }
}
