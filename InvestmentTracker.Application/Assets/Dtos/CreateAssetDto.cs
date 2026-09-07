namespace InvestmentTracker.Application.Assets.Dtos
{
    public sealed record CreateAssetDto(string Ticker, string Name, int AssetTypeId, int CountryId, int CurrencyId, int AssetCategoryId, int SectorId);
}
