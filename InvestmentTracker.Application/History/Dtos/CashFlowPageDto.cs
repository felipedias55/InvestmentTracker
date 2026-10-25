namespace InvestmentTracker.Application.History.Dtos
{
    public sealed record CashFlowPageQuery(int PageIndex = 0, int PageSize = 10, int? Year = null, string? Kind = null);
    public sealed record CashFlowPageDto(IReadOnlyList<CashFlowDto> Items, int Total, int PageIndex, int PageSize);
}
