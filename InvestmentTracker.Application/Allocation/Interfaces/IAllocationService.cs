using InvestmentTracker.Application.Allocation.Dtos;

namespace InvestmentTracker.Application.Allocation.Interfaces
{
    public interface IAllocationService
    {
        Task<ContributionPlanDto?> PlanContributionAsync(int portfolioId, ContributionPlanRequestDto dto, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<AllocationTargetDto>?> GetTargetsAsync(int portfolioId, AllocationDimension dimension, CancellationToken cancellationToken = default);
        Task<bool> SaveTargetsAsync(int portfolioId, AllocationDimension dimension, SaveTargetsDto dto, CancellationToken cancellationToken = default);
        Task<ContributionAnalysisDto?> AnalyzeContributionAsync(int portfolioId, ContributionRequestDto dto, CancellationToken cancellationToken = default);
        Task<DashboardDto?> GetDashboardAsync(int portfolioId, CancellationToken cancellationToken = default);
    }
}
