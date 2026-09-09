namespace InvestmentTracker.Application.Allocation.Dtos
{
    public sealed record SaveTargetsDto(
        IReadOnlyList<TargetEntryDto> Targets);
}
