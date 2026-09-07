using InvestmentTracker.Application.Sectors.Dtos;

namespace InvestmentTracker.Application.Sectors.Interfaces
{
    public interface ISectorService
    {
        Task<IReadOnlyList<SectorDto>> GetAllAsync(
            CancellationToken cancellationToken = default);

        Task<SectorDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<SectorDto> CreateAsync(
            CreateSectorDto dto,
            CancellationToken cancellationToken = default);

        Task<SectorDto?> UpdateAsync(
            int id,
            UpdateSectorDto dto,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteAsync(
            int id,
            CancellationToken cancellationToken = default);
    }
}
