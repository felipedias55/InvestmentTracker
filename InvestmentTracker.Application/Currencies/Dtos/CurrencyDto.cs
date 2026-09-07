namespace InvestmentTracker.Application.Currencies.Dtos
{
    public sealed record CurrencyDto(int Id, string Code, string Name, string? Symbol);
}
