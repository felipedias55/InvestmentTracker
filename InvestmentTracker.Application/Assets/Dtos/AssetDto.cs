namespace InvestmentTracker.Application.Assets.Dtos
{
    public sealed record AssetDto(int Id, string Ticker, string Name, int AssetTypeId, int CountryId, int CurrencyId, int AssetCategoryId, int SectorId, DateTime CreatedAt);
}
