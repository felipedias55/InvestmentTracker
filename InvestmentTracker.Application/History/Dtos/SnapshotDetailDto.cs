using InvestmentTracker.Application.Allocation.Dtos;

namespace InvestmentTracker.Application.History.Dtos
{
    public sealed record SnapshotDetailDto(
        int Id,
        DateOnly Month,
        DateOnly SnapshotDate,
        DateTime CapturedAtUtc,
        int PayloadVersion,
        DashboardDto Dashboard);
}
