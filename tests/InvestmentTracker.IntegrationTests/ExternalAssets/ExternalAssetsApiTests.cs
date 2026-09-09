using System.Net;
using System.Net.Http.Json;
using InvestmentTracker.Application.Allocation.Dtos;
using InvestmentTracker.Application.ExchangeRates.Interfaces;
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
    public class ExternalAssetsApiTests(DatabaseFixture fixture)
    {
        private sealed class NoRates : IExchangeRateProvider
        {
            public Task<ExchangeRate?> FetchAsync(string b, string q, CancellationToken ct) => Task.FromResult<ExchangeRate?>(null);
        }

        [Fact]
        public async Task ExternalWealthAndIncome_ShouldPersistConvertAndRespectPortfolioBoundaries()
        {
            await fixture.ResetAsync();
            int id, otherId, brlId, usdId, assetId;
            await using (var db = fixture.Database.CreateContext())
            {
                await CatalogSeed.ApplyAsync(db);
                id = (await db.Portfolios.SingleAsync()).Id;
                brlId = (await db.Currencies.SingleAsync(c => c.Code == "BRL")).Id;
                usdId = (await db.Currencies.SingleAsync(c => c.Code == "USD")).Id;
                var other = new Portfolio { Name = "Outra", BaseCurrencyId = brlId, CreatedAt = DateTime.UtcNow };
                var asset = new Asset { Ticker = "INC", Name = "Proventos", CurrencyId = usdId,
                    AssetTypeId = (await db.AssetTypes.FirstAsync()).Id, CountryId = (await db.Countries.FirstAsync()).Id,
                    SectorId = (await db.Sectors.FirstAsync()).Id, AssetCategoryId = (await db.AssetCategories.FirstAsync()).Id,
                    CreatedAt = DateTime.UtcNow };
                db.Portfolios.Add(other); db.Assets.Add(asset);
                db.ExchangeRates.Add(new ExchangeRate { BaseCode = "USD", QuoteCode = "BRL", Rate = 5m,
                    RateDate = DateOnly.FromDateTime(DateTime.UtcNow), FetchedAtUtc = DateTime.UtcNow });
                await db.SaveChangesAsync(); otherId = other.Id; assetId = asset.Id;
            }
            using var client = fixture.ApiFactory.CreateClient();
            var positions = $"/api/portfolios/{id}/assets";
            (await client.PostAsJsonAsync(positions, new SavePositionDto(assetId, 1m, 10m, 20m, 2.1234m))).EnsureSuccessStatusCode();
            var path = $"/api/portfolios/{id}/external-assets";
            var created = await client.PostAsJsonAsync(path, new SaveExternalAssetDto(" Reserva ", usdId, 10.1234m, " Saldo "));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var external = (await client.GetFromJsonAsync<ExternalAssetSummaryDto>(path))!;
            var item = Assert.Single(external.Items);
            Assert.Equal("Reserva", item.Name); Assert.Equal("Saldo", item.Description);
            Assert.Equal(10.1234m, item.Value); Assert.Equal(50.617m, external.TotalValue);
            var dashboard = (await client.GetFromJsonAsync<DashboardDto>($"/api/portfolios/{id}/dashboard"))!;
            Assert.Equal(100m, dashboard.Summary.CurrentValue);
            Assert.Equal(10.617m, dashboard.Summary.TotalIncome);
            Assert.Equal(150.617m, dashboard.TotalWealth);
            Assert.Equal(2.1234m, Assert.Single(dashboard.Summary.OriginalSubtotals).Income);
            Assert.Equal(1m, Assert.Single(dashboard.Allocation.Categories).CurrentPercentage);
            var position = Assert.Single(dashboard.Summary.Positions);
            Assert.Equal(2.1234m, position.Income);
            Assert.Equal(10.617m, position.BaseIncome);
            // An older client omitting income must not erase a previously entered amount.
            (await client.PutAsJsonAsync(positions + $"/{position.Id}", new SavePositionDto(assetId, 1m, 10m, 20m))).EnsureSuccessStatusCode();
            Assert.Equal(2.1234m, Assert.Single((await client.GetFromJsonAsync<PortfolioSummaryDto>($"/api/portfolios/{id}"))!.Positions).Income);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(positions + $"/{position.Id}", new SavePositionDto(assetId, 1m, 10m, 20m, -1m))).StatusCode);
            Assert.Empty((await client.GetFromJsonAsync<ExternalAssetSummaryDto>($"/api/portfolios/{otherId}/external-assets"))!.Items);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/portfolios/{otherId}/external-assets/{item.Id}", new SaveExternalAssetDto("Outra", brlId, 1m))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/portfolios/{otherId}/external-assets/{item.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new SaveExternalAssetDto("Reserva", usdId, -1m))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new SaveExternalAssetDto("Reserva", usdId, 0.00001m))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new SaveExternalAssetDto("Reserva", int.MaxValue, 1m))).StatusCode);
            (await client.PutAsJsonAsync(path + $"/{item.Id}", new SaveExternalAssetDto("Atualizada", brlId, 30m))).EnsureSuccessStatusCode();
            Assert.Equal(130m, (await client.GetFromJsonAsync<DashboardDto>($"/api/portfolios/{id}/dashboard"))!.TotalWealth);
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(path + $"/{item.Id}")).StatusCode);
            Assert.Equal(0m, (await client.GetFromJsonAsync<ExternalAssetSummaryDto>(path))!.TotalValue);
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(path + $"/{item.Id}")).StatusCode);
        }

        [Fact]
        public async Task MissingExternalRate_ShouldNotBlockPortfolioAllocationAndShouldProtectCurrencyReferences()
        {
            await fixture.ResetAsync();
            int id, usdId, categoryId;
            await using (var db = fixture.Database.CreateContext())
            {
                await CatalogSeed.ApplyAsync(db);
                id = (await db.Portfolios.SingleAsync()).Id;
                usdId = (await db.Currencies.SingleAsync(c => c.Code == "USD")).Id;
                categoryId = (await db.AssetCategories.FirstAsync()).Id;
            }
            using var factory = fixture.ApiFactory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IExchangeRateProvider, NoRates>()));
            using var client = factory.CreateClient();
            var path = $"/api/portfolios/{id}";
            (await client.PostAsJsonAsync(path + "/external-assets", new SaveExternalAssetDto("Reserva", usdId, 100m))).EnsureSuccessStatusCode();
            var dashboard = (await client.GetFromJsonAsync<DashboardDto>(path + "/dashboard"))!;
            Assert.Null(dashboard.TotalWealth); Assert.Null(dashboard.ExternalAssets!.TotalValue);
            Assert.Equal(0m, dashboard.Summary.CurrentValue); Assert.Equal(0m, dashboard.Summary.TotalIncome);
            Assert.True(dashboard.Summary.ConversionAvailable); Assert.False(dashboard.ExternalAssets.ConversionAvailable);
            Assert.Equal(100m, Assert.Single(dashboard.ExternalAssets.Items).Value);
            (await client.PutAsJsonAsync(path + "/category-targets", new SaveTargetsDto([new(categoryId, 1m)]))).EnsureSuccessStatusCode();
            var simulation = await client.PostAsJsonAsync(path + "/contribution-analysis", new ContributionRequestDto("category", 100m));
            simulation.EnsureSuccessStatusCode();
            Assert.Equal(100m, Assert.Single((await simulation.Content.ReadFromJsonAsync<ContributionAnalysisDto>())!.Rows).SuggestedContribution);
            Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/currencies/{usdId}")).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/currencies/{usdId}", new { Code = "EUR", Name = "Euro" })).StatusCode);
        }
    }
}
