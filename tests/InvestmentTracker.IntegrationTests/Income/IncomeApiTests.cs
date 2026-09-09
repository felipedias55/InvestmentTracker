using System.Net;
using System.Net.Http.Json;
using InvestmentTracker.Application.Income;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Infrastructure.Persistence.Repositories;
using InvestmentTracker.Application.History.Dtos;
using InvestmentTracker.Application.ExchangeRates.Interfaces;
using InvestmentTracker.Domain.Entities;
using InvestmentTracker.Infrastructure.Persistence;
using InvestmentTracker.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InvestmentTracker.IntegrationTests.Income
{
    [Collection(DatabaseCollection.Name)]
    public class IncomeApiTests(DatabaseFixture fixture)
    {
        private static readonly DateOnly Today = new(2026, 9, 8);
        private sealed class Clock : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => new(2026, 9, 8, 15, 0, 0, TimeSpan.Zero);
        }
        private sealed class Rates : IExchangeRateProvider
        {
            public Task<ExchangeRate?> FetchAsync(string b, string q, CancellationToken ct) => Task.FromResult<ExchangeRate?>(
                new ExchangeRate { BaseCode = b, QuoteCode = q, Rate = 5m, RateDate = Today, FetchedAtUtc = DateTime.UtcNow });
        }
        private HttpClient Client()
            => fixture.ApiFactory.WithWebHostBuilder(b => b.ConfigureTestServices(s => {
                s.AddSingleton<TimeProvider, Clock>(); s.AddSingleton<IExchangeRateProvider, Rates>();
            })).CreateClient();
        private async Task<(int Portfolio, int Asset, int Cash)> Seed(bool foreign = false)
        {
            await fixture.ResetAsync();
            await using var db = fixture.Database.CreateContext();
            await CatalogSeed.ApplyAsync(db);
            var portfolio = await db.Portfolios.SingleAsync();
            var currency = foreign ? (await db.Currencies.SingleAsync(c => c.Code == "USD")).Id : portfolio.BaseCurrencyId;
            var asset = new Asset { Ticker = "TRADE", Name = "Operação", CurrencyId = currency,
                AssetTypeId = (await db.AssetTypes.FirstAsync()).Id, CountryId = (await db.Countries.FirstAsync()).Id,
                AssetCategoryId = (await db.AssetCategories.FirstAsync()).Id, SectorId = (await db.Sectors.FirstAsync()).Id };
            var cash = new ExternalAsset { PortfolioId = portfolio.Id, CurrencyId = currency, Name = "Saldo disponível", Value = 1000m, UpdatedOn = Today };
            db.Assets.Add(asset); db.ExternalAssets.Add(cash); await db.SaveChangesAsync();
            db.PortfolioAssets.Add(new PortfolioAsset { PortfolioId = portfolio.Id, AssetId = asset.Id, Quantity = 0, Income = 15m, UpdatedOn = Today });
            await db.SaveChangesAsync();
            return (portfolio.Id, asset.Id, cash.Id);
        }
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Receipt_ShouldAccumulateOnceOnClosedPositionWithoutCreatingAnExternalFlow(bool creditCash)
        {
            var (portfolio, asset, cash) = await Seed();
            using var client = Client();
            var path = $"/api/portfolios/{portfolio}/income";
            var dto = new SaveIncomeDto(Guid.NewGuid(), Today, asset, 12.3456m, creditCash ? cash : null, "Dividendos");
            var response = await client.PostAsJsonAsync(path, dto);
            response.EnsureSuccessStatusCode();
            var first = (await response.Content.ReadFromJsonAsync<IncomeDto>())!;
            var retry = await client.PostAsJsonAsync(path, dto);
            retry.EnsureSuccessStatusCode();
            Assert.Equal(first.Id, (await retry.Content.ReadFromJsonAsync<IncomeDto>())!.Id);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, dto with { Amount = 20m })).StatusCode);
            await using var db = fixture.Database.CreateContext();
            var position = await db.PortfolioAssets.SingleAsync();
            Assert.Equal(27.3456m, position.Income);
            Assert.Equal(0m, position.Quantity);
            Assert.Equal(0m, position.InvestedAmount);
            Assert.Equal(0m, position.CurrentValue);
            Assert.Equal(Today, position.UpdatedOn);
            Assert.Equal(creditCash ? 1012.3456m : 1000m, (await db.ExternalAssets.SingleAsync()).Value);
            Assert.Empty(await db.PortfolioCashFlows.ToListAsync());
            Assert.Single(await db.Set<IncomeReceipt>().ToListAsync());
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("1.12345")]
        [InlineData("1000000000000000")]
        public async Task InvalidAmount_ShouldLeaveBalancesUntouched(string amount)
        {
            var (portfolio, asset, cash) = await Seed(); using var client = Client();
            var response = await client.PostAsJsonAsync($"/api/portfolios/{portfolio}/income", new {
                requestId = Guid.NewGuid(), date = Today, assetId = asset, amount, cashAssetId = cash });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await using var db = fixture.Database.CreateContext();
            Assert.Equal(15m, (await db.PortfolioAssets.SingleAsync()).Income);
            Assert.Equal(1000m, (await db.ExternalAssets.SingleAsync()).Value);
            Assert.Empty(await db.Set<IncomeReceipt>().ToListAsync());
        }

        [Fact]
        public async Task WrongCurrencyOrMissingPositionOrFutureDate_ShouldRejectAtomically()
        {
            var (portfolio, asset, cash) = await Seed(); using var client = Client();
            await using (var db = fixture.Database.CreateContext())
            {
                (await db.ExternalAssets.SingleAsync()).CurrencyId = (await db.Currencies.SingleAsync(c => c.Code == "USD")).Id;
                await db.SaveChangesAsync();
            }
            var path = $"/api/portfolios/{portfolio}/income";
            var dto = new SaveIncomeDto(Guid.NewGuid(), Today, asset, 10, cash);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, dto)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, dto with { AssetId = int.MaxValue, CashAssetId = null })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, dto with { Date = Today.AddDays(1), CashAssetId = null })).StatusCode);
            await using var check = fixture.Database.CreateContext();
            Assert.Equal(15m, (await check.PortfolioAssets.SingleAsync()).Income);
            Assert.Equal(1000m, (await check.ExternalAssets.SingleAsync()).Value);
            Assert.Empty(await check.Set<IncomeReceipt>().ToListAsync());
        }

        [Fact]
        public async Task Receipt_ShouldPreserveSnapshotAndRejectEarlierReceipts()
        {
            var (portfolio, asset, cash) = await Seed(); using var client = Client();
            var photoResponse = await client.PostAsync($"/api/portfolios/{portfolio}/history/snapshots", null);
            photoResponse.EnsureSuccessStatusCode();
            var before = await photoResponse.Content.ReadAsStringAsync();
            using var photo = System.Text.Json.JsonDocument.Parse(before);
            var id = photo.RootElement.GetProperty("id").GetInt32();
            before = await client.GetStringAsync($"/api/portfolios/{portfolio}/history/snapshots/{id}");
            var path = $"/api/portfolios/{portfolio}/income";
            var dto = new SaveIncomeDto(Guid.NewGuid(), Today, asset, 10, cash);
            (await client.PostAsJsonAsync(path, dto)).EnsureSuccessStatusCode();
            Assert.Equal(before, await client.GetStringAsync($"/api/portfolios/{portfolio}/history/snapshots/{id}"));
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, dto with { RequestId = Guid.NewGuid(), Date = Today.AddDays(-1) })).StatusCode);
        }

        [Fact]
        public async Task ConcurrentReceipts_ShouldNotLoseIncomeOrDuplicateReplays()
        {
            var (portfolio, asset, cash) = await Seed(); using var client = Client();
            var path = $"/api/portfolios/{portfolio}/income";
            var dto = new SaveIncomeDto(Guid.NewGuid(), Today, asset, 10, cash);
            var responses = await Task.WhenAll(client.PostAsJsonAsync(path, dto), client.PostAsJsonAsync(path, dto),
                client.PostAsJsonAsync(path, dto with { RequestId = Guid.NewGuid() }));
            foreach (var response in responses) response.EnsureSuccessStatusCode();
            await using var db = fixture.Database.CreateContext();
            Assert.Equal(35m, (await db.PortfolioAssets.SingleAsync()).Income);
            Assert.Equal(1020m, (await db.ExternalAssets.SingleAsync()).Value);
            Assert.Equal(2, await db.Set<IncomeReceipt>().CountAsync());
        }
    }
}
