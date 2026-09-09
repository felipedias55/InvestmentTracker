using System.Net.Http.Json;
using InvestmentTracker.Application.ExternalAssets.Dtos;
using InvestmentTracker.Application.Portfolios.Dtos;
using InvestmentTracker.Domain.Entities;
using InvestmentTracker.Infrastructure.Persistence;
using InvestmentTracker.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InvestmentTracker.IntegrationTests.ExternalAssets
{
    [Collection(DatabaseCollection.Name)]
    public class UpdateDatesTests(DatabaseFixture fixture)
    {
        private sealed class Clock : TimeProvider
        {
            public DateTimeOffset Now { get; set; } = new(2026, 9, 9, 1, 0, 0, TimeSpan.Zero);
            public override DateTimeOffset GetUtcNow() => Now;
        }

        [Fact]
        public async Task Dates_ShouldUseSaoPauloAndChangeOnlyOnSuccessfulSave()
        {
            await fixture.ResetAsync();
            int portfolioId, assetId, currencyId;
            await using (var db = fixture.Database.CreateContext())
            {
                await CatalogSeed.ApplyAsync(db);
                var portfolio = await db.Portfolios.SingleAsync();
                portfolioId = portfolio.Id; currencyId = portfolio.BaseCurrencyId;
                var asset = new Asset { Name = "Data", Ticker = "DATE", CurrencyId = currencyId,
                    AssetTypeId = (await db.AssetTypes.FirstAsync()).Id, CountryId = (await db.Countries.FirstAsync()).Id,
                    AssetCategoryId = (await db.AssetCategories.FirstAsync()).Id, SectorId = (await db.Sectors.FirstAsync()).Id };
                db.Assets.Add(asset); await db.SaveChangesAsync(); assetId = asset.Id;
            }
            var clock = new Clock();
            using var factory = fixture.ApiFactory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(clock)));
            using var client = factory.CreateClient();
            var portfolioPath = $"/api/portfolios/{portfolioId}";
            var externalPath = portfolioPath + "/external-assets";
            var positionInput = new SavePositionDto(assetId, 1m, 10m, 20m);
            var externalInput = new SaveExternalAssetDto("Reserva", currencyId, 30m);
            (await client.PostAsJsonAsync(portfolioPath + "/assets", positionInput)).EnsureSuccessStatusCode();
            (await client.PostAsJsonAsync(externalPath, externalInput)).EnsureSuccessStatusCode();
            var position = Assert.Single((await client.GetFromJsonAsync<PortfolioSummaryDto>(portfolioPath))!.Positions);
            var external = Assert.Single((await client.GetFromJsonAsync<ExternalAssetSummaryDto>(externalPath))!.Items);
            Assert.Equal(new DateOnly(2026, 9, 8), position.UpdatedOn);
            Assert.Equal(position.UpdatedOn, external.UpdatedOn);
            clock.Now = clock.Now.AddDays(1);
            Assert.Equal(position.UpdatedOn, Assert.Single((await client.GetFromJsonAsync<PortfolioSummaryDto>(portfolioPath))!.Positions).UpdatedOn);
            Assert.False((await client.PutAsJsonAsync(externalPath + $"/{external.Id}", externalInput with { Value = -1m })).IsSuccessStatusCode);
            Assert.Equal(external.UpdatedOn, Assert.Single((await client.GetFromJsonAsync<ExternalAssetSummaryDto>(externalPath))!.Items).UpdatedOn);
            (await client.PutAsJsonAsync(portfolioPath + $"/assets/{position.Id}", positionInput)).EnsureSuccessStatusCode();
            (await client.PutAsJsonAsync(externalPath + $"/{external.Id}", externalInput)).EnsureSuccessStatusCode();
            Assert.Equal(new DateOnly(2026, 9, 9), Assert.Single((await client.GetFromJsonAsync<PortfolioSummaryDto>(portfolioPath))!.Positions).UpdatedOn);
            Assert.Equal(new DateOnly(2026, 9, 9), Assert.Single((await client.GetFromJsonAsync<ExternalAssetSummaryDto>(externalPath))!.Items).UpdatedOn);
        }
    }
}
