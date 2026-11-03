
namespace InvestmentTracker.Application.Sectors.Dtos
{
    public sealed record CreateSectorDto(string Name, bool IsRealEstate = false);
}
