using InvestmentTracker.Application.Portfolios.Dtos;

namespace InvestmentTracker.Application.Portfolios.Interfaces
{
    public interface IPortfolioService
    {
        Task<IReadOnlyList<PortfolioDto>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<PortfolioDto> CreateAsync(CreatePortfolioDto dto, CancellationToken cancellationToken = default);
        Task<PortfolioDto?> UpdateAsync(int id, UpdatePortfolioDto dto, CancellationToken cancellationToken = default);
        Task<PortfolioSummaryDto?> GetSummaryAsync(int id, CancellationToken cancellationToken = default);
        Task<int?> AddPositionAsync(int portfolioId, SavePositionDto dto, CancellationToken cancellationToken = default);
        Task<bool> UpdatePositionAsync(int portfolioId, int positionId, SavePositionDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeletePositionAsync(int portfolioId, int positionId, CancellationToken cancellationToken = default);
    }
}
