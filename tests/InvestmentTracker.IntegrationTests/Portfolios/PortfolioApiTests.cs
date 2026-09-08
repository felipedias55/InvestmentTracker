using System.Net;
using System.Net.Http.Json;
using InvestmentTracker.Application.ExchangeRates.Interfaces;
using InvestmentTracker.Application.Portfolios.Dtos;
using InvestmentTracker.Domain.Entities;
using InvestmentTracker.Infrastructure.Persistence;
using InvestmentTracker.Infrastructure.Persistence.Repositories;
using InvestmentTracker.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace InvestmentTracker.IntegrationTests.Portfolios
{
    [Collection(DatabaseCollection.Name)]
    public class PortfolioApiTests(DatabaseFixture fixture)
    {
        private sealed class Provider(bool available) : IExchangeRateProvider
        {
            public Task<ExchangeRate?> FetchAsync(string b, string q, CancellationToken ct)
                => Task.FromResult(available ? new ExchangeRate { BaseCode = b, QuoteCode = q, Rate = b == "USD" ? 5m : 0.2m,
                    RateDate = DateOnly.FromDateTime(DateTime.UtcNow), FetchedAtUtc = DateTime.UtcNow } : null);
        }

        private async Task<(int PortfolioId, Asset Asset, int BrlId)> SeedAsync()
        {
            await fixture.ResetAsync();
            await using var context = fixture.Database.CreateContext();
            await CatalogSeed.ApplyAsync(context);
            var asset = new Asset { Ticker = "TEST", Name = "Ativo em USD", CreatedAt = DateTime.UtcNow,
                AssetTypeId = (await context.AssetTypes.FirstAsync()).Id,
                CountryId = (await context.Countries.FirstAsync()).Id,
                CurrencyId = (await context.Currencies.SingleAsync(x => x.Code == "USD")).Id,
                AssetCategoryId = (await context.AssetCategories.FirstAsync()).Id,
                SectorId = (await context.Sectors.FirstAsync()).Id };
            context.Assets.Add(asset); await context.SaveChangesAsync();
            return ((await context.Portfolios.SingleAsync()).Id, asset, (await context.Currencies.SingleAsync(x => x.Code == "BRL")).Id);
        }

        [Fact]
        public async Task Positions_ShouldPersistOriginalValuesConvertAndSupportEditingAndRemoval()
        {
            var (id, asset, brlId) = await SeedAsync();
            using var factory = fixture.ApiFactory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IExchangeRateProvider>(new Provider(true))));
            using var client = factory.CreateClient();
            var empty = (await client.GetFromJsonAsync<PortfolioSummaryDto>($"/api/portfolios/{id}"))!;
            Assert.Equal(0m, empty.CurrentValue);
            var dto = new SavePositionDto(asset.Id, 1.123456m, 10.1234m, 20.5678m);
            var created = await client.PostAsJsonAsync($"/api/portfolios/{id}/assets", dto);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/portfolios/{id}/assets", dto)).StatusCode);
            var summary = (await client.GetFromJsonAsync<PortfolioSummaryDto>($"/api/portfolios/{id}"))!;
            var position = Assert.Single(summary.Positions);
            Assert.Equal(20.5678m, position.CurrentValue);
            Assert.Equal(102.8390m, summary.CurrentValue);
            Assert.Equal("USD", position.CurrencyCode);
            Assert.Equal("BRL", summary.Portfolio.BaseCurrencyCode);
            await using (var context = fixture.Database.CreateContext())
            {
                Assert.Equal(20.5678m, (await context.PortfolioAssets.SingleAsync()).CurrentValue);
                Assert.Single(await context.ExchangeRates.ToListAsync());
            }
            var update = new UpdatePortfolioDto("Em dólares", null, asset.CurrencyId);
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/portfolios/{id}", update)).StatusCode);
            summary = (await client.GetFromJsonAsync<PortfolioSummaryDto>($"/api/portfolios/{id}"))!;
            Assert.Equal(20.5678m, summary.CurrentValue);
            Assert.Equal(20.5678m, summary.Positions[0].CurrentValue);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/portfolios/{id}/assets/{position.Id}", dto with { CurrentValue = 30m })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/portfolios/2147483647/assets/{position.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/portfolios/{id}/assets/{position.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/portfolios/{id}/assets/{position.Id}")).StatusCode);
        }

        [Fact]
        public async Task Outage_ShouldReturnCachedRateAndNeverReturnPartialTotalWithoutCache()
        {
            var (id, asset, _) = await SeedAsync();
            using var factory = fixture.ApiFactory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IExchangeRateProvider>(new Provider(false))));
            using var client = factory.CreateClient();
            (await client.PostAsJsonAsync($"/api/portfolios/{id}/assets", new SavePositionDto(asset.Id, 1, 10, 20))).EnsureSuccessStatusCode();
            var summary = (await client.GetFromJsonAsync<PortfolioSummaryDto>($"/api/portfolios/{id}"))!;
            Assert.Null(summary.CurrentValue); Assert.False(summary.ConversionAvailable);
            Assert.Equal(20m, summary.Positions[0].CurrentValue);
            await using var context = fixture.Database.CreateContext();
            await new ExchangeRateCache(context).StoreAsync(new ExchangeRate { BaseCode = "USD", QuoteCode = "BRL", Rate = 4m,
                RateDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)), FetchedAtUtc = DateTime.UtcNow.AddDays(-1) }, default);
            summary = (await client.GetFromJsonAsync<PortfolioSummaryDto>($"/api/portfolios/{id}"))!;
            Assert.Equal(80m, summary.CurrentValue); Assert.True(summary.HasStaleRates); Assert.True(summary.HasFallbackRates);
        }

        [Fact]
        public async Task Position_ShouldValidateReferencesPrecisionAndProtectCurrencyMeaning()
        {
            var (id, asset, brlId) = await SeedAsync();
            using var client = fixture.ApiFactory.CreateClient();
            var path = $"/api/portfolios/{id}/assets";
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new SavePositionDto(asset.Id, -1, 0, 0))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new SavePositionDto(asset.Id, 1, 0.00001m, 0))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new SavePositionDto(int.MaxValue, 1, 0, 0))).StatusCode);
            (await client.PostAsJsonAsync(path, new SavePositionDto(asset.Id, 1, 10, 20))).EnsureSuccessStatusCode();
            var updateAsset = new { asset.Ticker, asset.Name, asset.AssetTypeId, asset.CountryId,
                CurrencyId = brlId, asset.AssetCategoryId, asset.SectorId };
            Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/assets/{asset.Id}", updateAsset)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/currencies/{asset.CurrencyId}", new { Code = "EUR", Name = "Euro" })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/currencies/{brlId}")).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/assets/{asset.Id}")).StatusCode);
        }

        [Fact]
        public async Task CreatePortfolio_ShouldUseDefaultAndAllowAssetInDifferentPortfolios()
        {
            var (id, asset, _) = await SeedAsync();
            using var client = fixture.ApiFactory.CreateClient();
            var created = await client.PostAsJsonAsync("/api/portfolios", new CreatePortfolioDto("Outra"));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var portfolio = (await created.Content.ReadFromJsonAsync<PortfolioDto>())!;
            Assert.Equal("BRL", portfolio.BaseCurrencyCode);
            var position = new SavePositionDto(asset.Id, 0, 0, 0);
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/portfolios/{id}/assets", position)).StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/portfolios/{portfolio.Id}/assets", position)).StatusCode);
        }

        [Fact]
        public async Task Migration_ShouldBackfillExistingPortfolioWithBrl()
        {
            await fixture.ResetAsync();
            await using var context = fixture.Database.CreateContext();
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync("20260831135959_InitialCreate");
            try
            {
                await context.Database.ExecuteSqlRawAsync("INSERT INTO Portfolio (Name, CreatedAt) VALUES (N'Legada', SYSUTCDATETIME())");
            }
            finally { await migrator.MigrateAsync(); }
            var portfolio = await context.Portfolios.Include(p => p.BaseCurrency).SingleAsync();
            Assert.Equal("Legada", portfolio.Name); Assert.Equal("BRL", portfolio.BaseCurrency.Code);
        }
    }
}
