namespace InvestmentTracker.Application.Allocation.Dtos
{
    public sealed record AllocationDto(
        bool CategoryTargetsConfigured,
        bool SectorTargetsConfigured,
        IReadOnlyList<AllocationRowDto> Categories,
        IReadOnlyList<AllocationRowDto> Sectors,
        IReadOnlyList<AllocationRowDto> Countries);
}
