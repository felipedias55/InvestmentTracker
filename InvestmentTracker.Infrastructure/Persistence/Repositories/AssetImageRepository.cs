using InvestmentTracker.Application.Assets;
using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.Infrastructure.Persistence.Repositories
{
    public sealed class AssetImageRepository(InvestmentTrackerDbContext db) : IAssetImageRepository
    {
        public Task<AssetImageFile?> ReadAsync(int assetId, CancellationToken ct) => db.Set<AssetImage>().AsNoTracking()
            .Where(x => x.AssetId == assetId && x.Asset.ImageVersion != null).Select(x => new AssetImageFile(x.Data, x.Asset.ImageVersion!)).SingleOrDefaultAsync(ct);
        public async Task StageAsync(Asset asset, byte[]? data, CancellationToken ct)
        {
            var row = asset.Id == 0 ? null : await db.Set<AssetImage>().SingleOrDefaultAsync(x => x.AssetId == asset.Id, ct);
            if (data is null) { if (row is not null) db.Remove(row); }
            else if (row is not null) row.Data = data;
            else db.Add(new AssetImage { Asset = asset, Data = data });
        }
    }
}
