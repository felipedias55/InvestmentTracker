using System.Net;
using System.Net.Http.Json;
using InvestmentTracker.Application.Trades;
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

namespace InvestmentTracker.IntegrationTests.Trades
{
    [Collection(DatabaseCollection.Name)]
    public class TradesApiTests(DatabaseFixture fixture)
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
            return (portfolio.Id, asset.Id, cash.Id);
        }
        private static SaveTradeDto Buy(int asset, decimal quantity = 10m, decimal price = 20m, int? cash = null)
            => new(Guid.NewGuid(), Today, "buy", asset, quantity, price, cash);

        [Fact]
        public async Task BuyAndSell_ShouldUpdateCostQuantityAndCashFlowsOnceAndPreserveIncome()
        {
            var (portfolio, asset, _) = await Seed(); using var client = Client();
            var path = $"/api/portfolios/{portfolio}/trades";
            var input = Buy(asset);
            var response = await client.PostAsJsonAsync(path, input); response.EnsureSuccessStatusCode();
            var first = (await response.Content.ReadFromJsonAsync<TradeDto>())!;
            var retry = await client.PostAsJsonAsync(path, input); retry.EnsureSuccessStatusCode();
            Assert.Equal(first.Id, (await retry.Content.ReadFromJsonAsync<TradeDto>())!.Id);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, input with { Quantity = 11m })).StatusCode);
            (await client.PostAsJsonAsync(path, Buy(asset, 10m, 30m))).EnsureSuccessStatusCode();
            await using (var db = fixture.Database.CreateContext())
            {
                var position = await db.PortfolioAssets.SingleAsync();
                Assert.Equal(20m, position.Quantity); Assert.Equal(500m, position.InvestedAmount); Assert.Equal(600m, position.CurrentValue);
                position.Income = 15m; await db.SaveChangesAsync();
            }
            (await client.PostAsJsonAsync(path, Buy(asset, 5m, 40m) with { Kind = "sell" })).EnsureSuccessStatusCode();
            await using (var db = fixture.Database.CreateContext())
            {
                var position = await db.PortfolioAssets.SingleAsync();
                Assert.Equal(15m, position.Quantity); Assert.Equal(375m, position.InvestedAmount); Assert.Equal(600m, position.CurrentValue);
                Assert.Equal(15m, position.Income); Assert.Equal(Today, position.UpdatedOn);
                Assert.Equal(3, await db.Set<PortfolioTrade>().CountAsync());
                Assert.Equal(500m, await db.PortfolioCashFlows.Where(f => f.Kind == "contribution").SumAsync(f => f.Amount));
                Assert.Equal(200m, (await db.PortfolioCashFlows.SingleAsync(f => f.Kind == "withdrawal")).Amount);
            }
            (await client.PostAsJsonAsync(path, Buy(asset, 15m, 40m) with { Kind = "sell" })).EnsureSuccessStatusCode();
            await using var check = fixture.Database.CreateContext();
            var closed = await check.PortfolioAssets.SingleAsync();
            Assert.Equal(0m, closed.Quantity); Assert.Equal(0m, closed.CurrentValue); Assert.Equal(0m, closed.InvestedAmount); Assert.Equal(15m, closed.Income);
        }

        [Fact]
        public async Task Reinvestment_ShouldMoveExistingCashWithoutCreatingContributionsOrWithdrawals()
        {
            var (portfolio, asset, cash) = await Seed(); using var client = Client();
            var path = $"/api/portfolios/{portfolio}/trades";
            (await client.PostAsJsonAsync(path, Buy(asset, cash: cash))).EnsureSuccessStatusCode();
            await using (var db = fixture.Database.CreateContext())
            {
                Assert.Equal(800m, (await db.ExternalAssets.SingleAsync()).Value);
                Assert.Empty(await db.PortfolioCashFlows.ToListAsync());
            }
            (await client.PostAsJsonAsync(path, Buy(asset, 10m, 25m, cash) with { Kind = "sell" })).EnsureSuccessStatusCode();
            await using var check = fixture.Database.CreateContext();
            Assert.Equal(1050m, (await check.ExternalAssets.SingleAsync()).Value);
            Assert.Empty(await check.PortfolioCashFlows.ToListAsync());
            Assert.Equal(2, await check.Set<PortfolioTrade>().CountAsync());
        }

        [Fact]
        public async Task InvalidTrades_ShouldRollbackEveryChangeAndRespectPortfolioBoundaries()
        {
            var (portfolio, asset, cash) = await Seed(); using var client = Client();
            var path = $"/api/portfolios/{portfolio}/trades";
            foreach (var input in new[] { Buy(asset) with { Kind = "sell" }, Buy(asset, 100m, 20m, cash),
                Buy(asset, cash: int.MaxValue), Buy(asset) with { Date = Today.AddDays(1) }, Buy(asset, -1m), Buy(asset, 0m),
                Buy(asset, 0.000001m, 0.0001m), Buy(asset, 9999999999999m, 999999999999999m) })
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, input)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/portfolios/2147483647/trades", Buy(asset))).StatusCode);
            await using var check = fixture.Database.CreateContext();
            Assert.Empty(await check.PortfolioAssets.ToListAsync()); Assert.Empty(await check.Set<PortfolioTrade>().ToListAsync());
            Assert.Empty(await check.PortfolioCashFlows.ToListAsync()); Assert.Equal(1000m, (await check.ExternalAssets.SingleAsync()).Value);
        }

        [Fact]
        public async Task ConcurrentBuys_ShouldNotLoseUpdatesOrDuplicateRetries()
        {
            var (portfolio, asset, _) = await Seed(); using var client = Client();
            var path = $"/api/portfolios/{portfolio}/trades"; var input = Buy(asset);
            var responses = await Task.WhenAll(client.PostAsJsonAsync(path, input), client.PostAsJsonAsync(path, input));
            foreach (var r in responses) r.EnsureSuccessStatusCode();
            responses = await Task.WhenAll(client.PostAsJsonAsync(path, Buy(asset)), client.PostAsJsonAsync(path, Buy(asset)));
            foreach (var r in responses) r.EnsureSuccessStatusCode();
            await using var db = fixture.Database.CreateContext();
            Assert.Equal(30m, (await db.PortfolioAssets.SingleAsync()).Quantity);
            Assert.Equal(3, await db.Set<PortfolioTrade>().CountAsync()); Assert.Equal(3, await db.PortfolioCashFlows.CountAsync());
        }

        [Fact]
        public async Task ForeignTrades_ShouldFreezeConversionAndProtectGeneratedFlow()
        {
            var (portfolio, asset, _) = await Seed(true); using var client = Client();
            var path = $"/api/portfolios/{portfolio}/trades";
            (await client.PostAsJsonAsync(path, Buy(asset))).EnsureSuccessStatusCode();
            var history = (await client.GetFromJsonAsync<HistoryDto>($"/api/portfolios/{portfolio}/history"))!;
            var flow = Assert.Single(history.CashFlows);
            Assert.NotNull(flow.TradeId); Assert.Equal(1000m, flow.BaseAmount); Assert.Equal("USD", flow.CurrencyCode);
            var flowPath = $"/api/portfolios/{portfolio}/history/cash-flows/{flow.Id}";
            Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync(flowPath)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(flowPath,
                new SaveCashFlowDto(Today, "contribution", flow.CurrencyId, 1m, 1m, null))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, Buy(asset) with { Date = Today.AddDays(-1) })).StatusCode);
        }

        [Fact]
        public async Task ForeignPastTrade_ShouldRequireHistoricalEquivalentAndFreezeIt()
        {
            var (portfolio, asset, _) = await Seed(true); using var client = Client();
            var path = $"/api/portfolios/{portfolio}/trades";
            var input = Buy(asset, 0.125m, 80m) with { Date = Today.AddDays(-1) };
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, input)).StatusCode);
            await using (var empty = fixture.Database.CreateContext())
            { Assert.Empty(await empty.PortfolioAssets.ToListAsync()); Assert.Empty(await empty.PortfolioCashFlows.ToListAsync()); }
            (await client.PostAsJsonAsync(path, input with { BaseAmount = 48.5m })).EnsureSuccessStatusCode();
            await using var db = fixture.Database.CreateContext();
            Assert.Equal(0.125m, (await db.PortfolioAssets.SingleAsync()).Quantity);
            Assert.Equal(48.5m, (await db.PortfolioCashFlows.SingleAsync()).BaseAmount);
        }

        [Fact]
        public async Task Operations_ShouldRespectExistingPositionsSnapshotsAndCashOwnership()
        {
            var (portfolio, asset, cash) = await Seed(); using var client = Client();
            int wrongCash, otherCurrencyCash;
            await using (var db = fixture.Database.CreateContext())
            {
                var currency = (await db.Portfolios.SingleAsync()).BaseCurrencyId;
                var other = new Portfolio { Name = "Outra", BaseCurrencyId = currency };
                var balance = new ExternalAsset { Name = "Outro saldo", Portfolio = other, CurrencyId = currency, Value = 1000m };
                var usd = new ExternalAsset { Name = "Dólares", PortfolioId = portfolio,
                    CurrencyId = (await db.Currencies.SingleAsync(c => c.Code == "USD")).Id, Value = 1000m };
                db.ExternalAssets.AddRange(balance, usd);
                db.PortfolioAssets.Add(new PortfolioAsset { PortfolioId = portfolio, AssetId = asset, Quantity = 10m,
                    InvestedAmount = 150m, CurrentValue = 200m, Income = 2m, UpdatedOn = Today });
                await db.SaveChangesAsync(); wrongCash = balance.Id; otherCurrencyCash = usd.Id;
            }
            var path = $"/api/portfolios/{portfolio}/trades";
            Assert.Empty((await client.GetFromJsonAsync<TradeDto[]>(path))!);
            foreach (var input in new[] { Buy(asset, cash: wrongCash), Buy(asset, cash: otherCurrencyCash), Buy(asset, 11m) with { Kind = "sell" } })
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, input)).StatusCode);
            // Use a historical snapshot directly, without requesting external exchange rates.
            await using (var db = fixture.Database.CreateContext())
            {
                db.PortfolioSnapshots.Add(new PortfolioSnapshot { PortfolioId = portfolio, Month = new DateOnly(2026, 8, 1),
                    SnapshotDate = new DateOnly(2026, 8, 31), BaseCurrencyCode = "BRL", DashboardJson = "{}", PayloadVersion = 1 });
                await db.SaveChangesAsync();
            }
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, Buy(asset) with { Date = new DateOnly(2026, 8, 30) })).StatusCode);
            (await client.PostAsJsonAsync(path, Buy(asset, 5m, 30m))).EnsureSuccessStatusCode();
            await using var check = fixture.Database.CreateContext();
            var p = await check.PortfolioAssets.SingleAsync();
            Assert.Equal(15m, p.Quantity); Assert.Equal(300m, p.InvestedAmount); Assert.Equal(2m, p.Income);
            Assert.Equal("{}", (await check.PortfolioSnapshots.SingleAsync()).DashboardJson);
            Assert.Single(await check.PortfolioCashFlows.ToListAsync());
        }

        [Fact]
        public async Task StaleManualSave_ShouldNotOverwriteAnOperation()
        {
            var (portfolio, asset, cash) = await Seed(); using var client = Client();
            var path = $"/api/portfolios/{portfolio}/trades";
            (await client.PostAsJsonAsync(path, Buy(asset, cash: cash))).EnsureSuccessStatusCode();
            await using var stale = fixture.Database.CreateContext();
            var position = await stale.PortfolioAssets.SingleAsync();
            var balance = await stale.ExternalAssets.SingleAsync();
            (await client.PostAsJsonAsync(path, Buy(asset, cash: cash))).EnsureSuccessStatusCode();
            position.CurrentValue = 999m;
            await Assert.ThrowsAsync<ResourceConflictException>(() => new PortfolioRepository(stale).SaveChangesAsync(default));
            stale.ChangeTracker.Clear();
            stale.ExternalAssets.Attach(balance); balance.Value = 500m;
            await Assert.ThrowsAsync<ResourceConflictException>(() => new ExternalAssetRepository(stale).SaveChangesAsync(default));
            await using var check = fixture.Database.CreateContext();
            Assert.Equal(20m, (await check.PortfolioAssets.SingleAsync()).Quantity);
            Assert.Equal(600m, (await check.ExternalAssets.SingleAsync()).Value);
        }
    }
}
