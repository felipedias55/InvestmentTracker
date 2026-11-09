using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using InvestmentTracker.Application.Assets;
using InvestmentTracker.Application.Assets.Dtos;
using InvestmentTracker.Application.Portfolios.Dtos;
using InvestmentTracker.Domain.Entities;
using InvestmentTracker.Infrastructure.Persistence;
using InvestmentTracker.IntegrationTests.Infrastructure;
using InvestmentTracker.Testing;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.IntegrationTests.Catalogs
{
    [Collection(DatabaseCollection.Name)]
    public class AssetImagesApiTests(DatabaseFixture fixture)
    {
        [Fact]
        public async Task ImageMetadataIsOptionalAndUpdatesAreAtomicAndDoNotChangeFinancialData()
        {
            await fixture.ResetAsync(); CreateAssetDto create; int portfolioId;
            await using (var db = fixture.Database.CreateContext())
            {
                await CatalogSeed.ApplyAsync(db); var portfolio = await db.Portfolios.SingleAsync(); portfolioId = portfolio.Id;
                create = new("IMAGE", "Imagem do ativo", (await db.AssetTypes.FirstAsync()).Id, (await db.Countries.FirstAsync()).Id,
                    portfolio.BaseCurrencyId, (await db.AssetCategories.FirstAsync()).Id, (await db.Sectors.FirstAsync()).Id);
            }
            using var client = fixture.ApiFactory.CreateClient();
            var png = TestPng.Create(); var created = await client.PostAsJsonAsync("/api/assets", create with { Image = new(png) }); created.EnsureSuccessStatusCode();
            var asset = (await created.Content.ReadFromJsonAsync<AssetDto>())!; Assert.NotNull(asset.ImageVersion);
            var url = $"/api/assets/{asset.Id}";
            var image = await client.GetAsync(url + "/image?v=" + asset.ImageVersion); image.EnsureSuccessStatusCode(); Assert.Equal(png, await image.Content.ReadAsByteArrayAsync());
            Assert.Equal("image/png", image.Content.Headers.ContentType!.MediaType); Assert.Equal("nosniff", Assert.Single(image.Headers.GetValues("X-Content-Type-Options")));
            using var cached = new HttpRequestMessage(HttpMethod.Get, url + "/image?v=" + asset.ImageVersion); cached.Headers.IfNoneMatch.Add(image.Headers.ETag!);
            Assert.Equal(HttpStatusCode.NotModified, (await client.SendAsync(cached)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(url + "/image?v=old")).StatusCode);
            await using (var db = fixture.Database.CreateContext())
            {
                db.Add(new PortfolioAsset { PortfolioId = portfolioId, AssetId = asset.Id, Quantity = 2, InvestedAmount = 100, CurrentValue = 120, Income = 5, UpdatedOn = new(2026, 11, 3) }); await db.SaveChangesAsync();
            }
            var input = new UpdateAssetDto(create.Ticker, "Nome revisado", create.AssetTypeId, create.CountryId, create.CurrencyId, create.AssetCategoryId, create.SectorId);
            var legacy = await client.PutAsJsonAsync(url, input); legacy.EnsureSuccessStatusCode(); Assert.Equal(asset.ImageVersion, (await legacy.Content.ReadFromJsonAsync<AssetDto>())!.ImageVersion);
            var invalid = await client.PutAsJsonAsync(url, input with { Name = "Não salvar", Image = new(new byte[] { 1, 2, 3 }, asset.ImageVersion) }); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.Equal("Nome revisado", (await client.GetFromJsonAsync<AssetDto>(url))!.Name);
            var next = await client.PutAsJsonAsync(url, input with { Image = new(TestPng.Create(40), asset.ImageVersion) }); next.EnsureSuccessStatusCode();
            var updated = (await next.Content.ReadFromJsonAsync<AssetDto>())!; Assert.NotEqual(asset.ImageVersion, updated.ImageVersion);
            var stale = await client.PutAsJsonAsync(url, input with { Name = "Não substituir", Image = new(TestPng.Create(80), asset.ImageVersion) }); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            var summary = (await client.GetFromJsonAsync<PortfolioSummaryDto>($"/api/portfolios/{portfolioId}"))!;
            Assert.Equal(updated.ImageVersion, Assert.Single(summary.Positions).ImageVersion); Assert.Equal(120, summary.CurrentValue); Assert.Equal(2, summary.Positions[0].Quantity);
            var removed = await client.PutAsJsonAsync(url, input with { Image = new(null, updated.ImageVersion) }); removed.EnsureSuccessStatusCode(); Assert.Null((await removed.Content.ReadFromJsonAsync<AssetDto>())!.ImageVersion);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(url + "/image")).StatusCode);
            await using var check = fixture.Database.CreateContext(); Assert.Empty(await check.Set<AssetImage>().ToListAsync());
            Assert.Empty(await check.Set<FinancialMovement>().ToListAsync()); Assert.Empty(await check.PortfolioCashFlows.ToListAsync()); Assert.Empty(await check.PortfolioSnapshots.ToListAsync());
            Assert.Equal(100, (await check.PortfolioAssets.SingleAsync()).InvestedAmount); Assert.Equal(5, (await check.PortfolioAssets.SingleAsync()).Income); Assert.False(check.Database.HasPendingModelChanges());
        }
        [Fact]
        public async Task AssetWithoutAnImageKeepsTheExistingContractAndImagesFollowAssetDeletion()
        {
            await fixture.ResetAsync(); CreateAssetDto input;
            await using (var db = fixture.Database.CreateContext())
            {
                await CatalogSeed.ApplyAsync(db); input = new("NOIMAGE", "Sem imagem", (await db.AssetTypes.FirstAsync()).Id, (await db.Countries.FirstAsync()).Id,
                    (await db.Currencies.FirstAsync()).Id, (await db.AssetCategories.FirstAsync()).Id, (await db.Sectors.FirstAsync()).Id);
            }
            using var client = fixture.ApiFactory.CreateClient(); var response = await client.PostAsJsonAsync("/api/assets", input); response.EnsureSuccessStatusCode();
            var asset = (await response.Content.ReadFromJsonAsync<AssetDto>())!; Assert.Null(asset.ImageVersion); Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/assets/{asset.Id}/image")).StatusCode);
            var imageResponse = await client.PostAsJsonAsync("/api/assets", input with { Ticker = "WITHIMAGE", Image = new(TestPng.Create()) }); imageResponse.EnsureSuccessStatusCode();
            var withImage = (await imageResponse.Content.ReadFromJsonAsync<AssetDto>())!; Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/assets/{withImage.Id}")).StatusCode);
            await using var check = fixture.Database.CreateContext(); Assert.Empty(await check.Set<AssetImage>().ToListAsync());
        }
    }
}
