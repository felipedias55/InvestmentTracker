using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.Sectors.Interfaces
{
    public interface ISectorRepository
    {
        Task<IReadOnlyList<Sector>> GetAllAsync(
            CancellationToken cancellationToken = default);

        Task<Sector?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsByNameAsync(
            string name,
            int? excludingId = null,
            CancellationToken cancellationToken = default);

        Task AddAsync(
            Sector sector,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(
            Sector sector,
            CancellationToken cancellationToken = default);

        Task SaveChangesAsync(
            CancellationToken cancellationToken = default);
    }
}
