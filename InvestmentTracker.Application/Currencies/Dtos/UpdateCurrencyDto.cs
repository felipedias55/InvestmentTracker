namespace InvestmentTracker.Application.Currencies.Dtos
{
    public sealed record UpdateCurrencyDto(string Code, string Name, string? Symbol = null);
}
