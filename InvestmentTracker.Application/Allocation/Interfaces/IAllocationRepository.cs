using InvestmentTracker.Application.Allocation.Dtos;

namespace InvestmentTracker.Application.Allocation.Interfaces
{
    public interface IAllocationRepository
    {
        Task<IReadOnlyList<AllocationTargetDto>> GetTargetsAsync(int portfolioId, AllocationDimension dimension, CancellationToken cancellationToken);
        Task<bool> GroupsExistAsync(IReadOnlyList<int> ids, AllocationDimension dimension, CancellationToken cancellationToken);
        Task ReplaceTargetsAsync(int portfolioId, AllocationDimension dimension, IReadOnlyList<TargetEntryDto> targets, CancellationToken cancellationToken);
    }
}
