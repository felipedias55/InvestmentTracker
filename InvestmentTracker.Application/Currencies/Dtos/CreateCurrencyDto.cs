namespace InvestmentTracker.Application.Currencies.Dtos
{
    public sealed record CreateCurrencyDto(string Code, string Name, string? Symbol = null);
}
