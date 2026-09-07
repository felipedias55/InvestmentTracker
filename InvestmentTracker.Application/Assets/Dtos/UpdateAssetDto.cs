namespace InvestmentTracker.Application.Assets.Dtos
{
    public sealed record UpdateAssetDto(string Ticker, string Name, int AssetTypeId, int CountryId, int CurrencyId, int AssetCategoryId, int SectorId);
}
