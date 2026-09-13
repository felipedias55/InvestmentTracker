using InvestmentTracker.Application.Allocation.Dtos;

namespace InvestmentTracker.Application.History.Dtos
{
    public sealed record SnapshotDetailDto(
        int Id,
        DateOnly Month,
        DateOnly SnapshotDate,
        DateTime CapturedAtUtc,
        int PayloadVersion,
        DashboardDto Dashboard)
    {
        public bool IsOutdated { get; init; }
        public bool IsReopened { get; init; }
        public int Revision { get; init; } = 1;
        public IReadOnlyList<SnapshotDetailDto> PreviousVersions { get; init; } = [];
    }
}
