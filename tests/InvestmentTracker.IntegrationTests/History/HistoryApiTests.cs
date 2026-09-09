using InvestmentTracker.Application.ExchangeRates.Interfaces;
using System.Net;
using System.Net.Http.Json;
using InvestmentTracker.Application.History.Dtos;
using InvestmentTracker.Domain.Entities;
using InvestmentTracker.Infrastructure.Persistence;
using InvestmentTracker.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InvestmentTracker.IntegrationTests.History
{
    [Collection(DatabaseCollection.Name)]
    public class HistoryApiTests(DatabaseFixture fixture)
    {
        private sealed class NoRates : IExchangeRateProvider
        {
            public Task<ExchangeRate?> FetchAsync(string b, string q, CancellationToken ct) => Task.FromResult<ExchangeRate?>(null);
        }

        [Fact]
        public async Task Capture_ShouldRejectMissingRatesWithoutSavingAPartialPhotograph()
        {
            var (id, _, _, usd, _) = await SeedAsync();
            await using (var db = fixture.Database.CreateContext())
            {
                (await db.ExternalAssets.SingleAsync()).CurrencyId = usd;
                await db.SaveChangesAsync();
            }
            using var factory = fixture.ApiFactory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IExchangeRateProvider, NoRates>()));
            using var client = factory.CreateClient();
            var response = await client.PostAsJsonAsync($"/api/portfolios/{id}/history/snapshots", new { });
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Contains("câmbio", await response.Content.ReadAsStringAsync());
            await using var verification = fixture.Database.CreateContext();
            Assert.Empty(await verification.PortfolioSnapshots.ToListAsync());
            Assert.Equal(100m, (await verification.PortfolioAssets.SingleAsync()).CurrentValue);
        }

        private sealed class Clock : TimeProvider
        {
            public DateTimeOffset Now { get; set; } = new(2026, 9, 8, 15, 0, 0, TimeSpan.Zero);
            public override DateTimeOffset GetUtcNow() => Now;
        }
        private async Task<(int Id, int Other, int Brl, int Usd, int Asset)> SeedAsync()
        {
            await fixture.ResetAsync();
            await using var db = fixture.Database.CreateContext();
            await CatalogSeed.ApplyAsync(db);
            var portfolio = await db.Portfolios.SingleAsync();
            var usd = (await db.Currencies.SingleAsync(c => c.Code == "USD")).Id;
            var other = new Portfolio { Name = "Outra", BaseCurrencyId = portfolio.BaseCurrencyId, CreatedAt = DateTime.UtcNow };
            db.Portfolios.Add(other);
            var asset = new Asset { Name = "Nome original", Ticker = "HIST", CurrencyId = portfolio.BaseCurrencyId,
                AssetTypeId = (await db.AssetTypes.FirstAsync()).Id, CountryId = (await db.Countries.FirstAsync()).Id,
                AssetCategoryId = (await db.AssetCategories.FirstAsync()).Id, SectorId = (await db.Sectors.FirstAsync()).Id };
            db.PortfolioAssets.Add(new PortfolioAsset { PortfolioId = portfolio.Id, Asset = asset, Quantity = 2m,
                InvestedAmount = 80m, CurrentValue = 100m, Income = 5m });
            db.ExternalAssets.Add(new ExternalAsset { PortfolioId = portfolio.Id, CurrencyId = portfolio.BaseCurrencyId, Name = "Reserva original", Value = 50m });
            await db.SaveChangesAsync();
            return (portfolio.Id, other.Id, portfolio.BaseCurrencyId, usd, asset.Id);
        }

        [Fact]
        public async Task Snapshots_ShouldFreezeValuesAndMetadataAndOnlyReplaceTheCurrentMonth()
        {
            var (id, other, _, _, asset) = await SeedAsync();
            var clock = new Clock();
            using var factory = fixture.ApiFactory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(clock)));
            using var client = factory.CreateClient();
            var path = $"/api/portfolios/{id}/history";
            var response = await client.PostAsJsonAsync(path + "/snapshots", new { });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var first = (await response.Content.ReadFromJsonAsync<SnapshotDetailDto>())!;
            Assert.Equal(new DateOnly(2026, 9, 1), first.Month);
            Assert.Equal(150m, first.Dashboard.TotalWealth);
            Assert.Equal(5m, first.Dashboard.Summary.TotalIncome);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/snapshots", new { })).StatusCode);
            await using (var db = fixture.Database.CreateContext())
            {
                (await db.Assets.SingleAsync(a => a.Id == asset)).Name = "Nome novo";
                (await db.PortfolioAssets.SingleAsync()).CurrentValue = 200m;
                (await db.ExternalAssets.SingleAsync()).Name = "Reserva nova";
                await db.SaveChangesAsync();
            }
            var frozen = (await client.GetFromJsonAsync<SnapshotDetailDto>(path + $"/snapshots/{first.Id}"))!;
            Assert.Equal("Nome original", Assert.Single(frozen.Dashboard.Summary.Positions).Name);
            Assert.Equal("Reserva original", Assert.Single(frozen.Dashboard.ExternalAssets!.Items).Name);
            Assert.Equal(150m, frozen.Dashboard.TotalWealth);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/portfolios/{other}/history/snapshots/{first.Id}")).StatusCode);
            var updated = await client.PutAsJsonAsync(path + "/snapshots/current", new { });
            updated.EnsureSuccessStatusCode();
            Assert.Equal(first.Id, (await updated.Content.ReadFromJsonAsync<SnapshotDetailDto>())!.Id);
            clock.Now = new DateTimeOffset(2026, 10, 8, 15, 0, 0, TimeSpan.Zero);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(path + "/snapshots/current", new { })).StatusCode);
            (await client.PostAsJsonAsync(path + "/snapshots", new { })).EnsureSuccessStatusCode();
            var history = (await client.GetFromJsonAsync<HistoryDto>(path))!;
            Assert.Equal(2, history.Months.Count);
            Assert.Equal(250m, history.Months[0].TotalWealth);
            Assert.Equal(0m, history.Months[1].Change);
            Assert.Equal(250m, Assert.Single(history.Years).TotalWealth);
        }

        [Fact]
        public async Task CashFlows_ShouldPreserveBalancesSupportCorrectionsAndRecalculateComparisons()
        {
            var (id, other, brl, usd, _) = await SeedAsync();
            var clock = new Clock();
            using var factory = fixture.ApiFactory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(clock)));
            using var client = factory.CreateClient();
            var path = $"/api/portfolios/{id}/history";
            (await client.PostAsJsonAsync(path + "/snapshots", new { })).EnsureSuccessStatusCode();
            clock.Now = new DateTimeOffset(2026, 10, 8, 15, 0, 0, TimeSpan.Zero);
            var dto = new SaveCashFlowDto(new DateOnly(2026, 9, 9), "contribution", usd, 10m, 50m, " Aporte real ");
            (await client.PostAsJsonAsync(path + "/cash-flows", dto)).EnsureSuccessStatusCode();
            await using (var db = fixture.Database.CreateContext())
            {
                Assert.Equal(100m, (await db.PortfolioAssets.SingleAsync()).CurrentValue);
                (await db.PortfolioAssets.SingleAsync()).CurrentValue = 180m;
                await db.SaveChangesAsync();
            }
            (await client.PostAsJsonAsync(path + "/snapshots", new { })).EnsureSuccessStatusCode();
            var history = (await client.GetFromJsonAsync<HistoryDto>(path))!;
            var flow = Assert.Single(history.CashFlows);
            Assert.Equal("Aporte real", flow.Notes); Assert.Equal(50m, flow.BaseAmount);
            Assert.Equal(30m, history.Months.Last().ChangeExcludingFlows);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/portfolios/{other}/history/cash-flows/{flow.Id}", dto)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/portfolios/{other}/history/cash-flows/{flow.Id}")).StatusCode);
            (await client.PutAsJsonAsync(path + $"/cash-flows/{flow.Id}", dto with { Kind = "withdrawal", CurrencyId = brl, Amount = 20m, BaseAmount = null })).EnsureSuccessStatusCode();
            history = (await client.GetFromJsonAsync<HistoryDto>(path))!;
            Assert.Equal(100m, history.Months.Last().ChangeExcludingFlows);
            Assert.Equal(230m, history.Months.Last().TotalWealth);
            (await client.DeleteAsync(path + $"/cash-flows/{flow.Id}")).EnsureSuccessStatusCode();
            history = (await client.GetFromJsonAsync<HistoryDto>(path))!;
            Assert.Empty(history.CashFlows); Assert.Equal(80m, history.Months.Last().ChangeExcludingFlows);
        }

        [Fact]
        public async Task ForeignFlows_ShouldKeepOriginalAndHistoricBaseWhenPortfolioBaseChanges()
        {
            var (id, _, brl, usd, _) = await SeedAsync();
            var clock = new Clock();
            using var factory = fixture.ApiFactory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(clock)));
            using var client = factory.CreateClient();
            var path = $"/api/portfolios/{id}/history";
            var dto = new SaveCashFlowDto(new DateOnly(2026, 9, 1), "contribution", usd, 10.1234m);
            (await client.PostAsJsonAsync(path + "/cash-flows", dto)).EnsureSuccessStatusCode();
            var flow = Assert.Single((await client.GetFromJsonAsync<HistoryDto>(path))!.CashFlows);
            Assert.Null(flow.BaseAmount); Assert.Equal("BRL", flow.BaseCurrencyCode);
            await using (var db = fixture.Database.CreateContext())
            {
                (await db.Portfolios.SingleAsync(p => p.Id == id)).BaseCurrencyId = usd;
                await db.SaveChangesAsync();
            }
            (await client.PutAsJsonAsync(path + $"/cash-flows/{flow.Id}", dto with { BaseAmount = 55m })).EnsureSuccessStatusCode();
            var result = (await client.GetFromJsonAsync<HistoryDto>(path))!;
            Assert.Equal("BRL", Assert.Single(result.CashFlows).BaseCurrencyCode);
            Assert.Equal(55m, result.CashFlows[0].BaseAmount);
            Assert.Equal(10.1234m, Assert.Single(result.Months).Contributions);
        }

        [Fact]
        public async Task Capture_ShouldSerializeConcurrentRequestsAndAvoidDuplicateMonths()
        {
            var (id, _, _, _, _) = await SeedAsync();
            using var client = fixture.ApiFactory.CreateClient();
            var path = $"/api/portfolios/{id}/history/snapshots";
            var responses = await Task.WhenAll(client.PostAsJsonAsync(path, new { }), client.PostAsJsonAsync(path, new { }));
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
            await using var db = fixture.Database.CreateContext();
            Assert.Single(await db.PortfolioSnapshots.ToListAsync());
        }

        [Fact]
        public async Task Validation_ShouldUseBrazilianDateAndRejectInvalidMovements()
        {
            var (id, _, brl, _, _) = await SeedAsync();
            var clock = new Clock { Now = new DateTimeOffset(2026, 10, 1, 1, 0, 0, TimeSpan.Zero) };
            using var factory = fixture.ApiFactory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(clock)));
            using var client = factory.CreateClient();
            var path = $"/api/portfolios/{id}/history";
            Assert.Equal(new DateOnly(2026, 9, 30), (await client.GetFromJsonAsync<HistoryDto>(path))!.Today);
            var valid = new SaveCashFlowDto(new DateOnly(2026, 9, 30), "contribution", brl, 100m);
            foreach (var invalid in new[] { valid with { Date = new DateOnly(2026, 10, 1) }, valid with { Kind = "invalid" },
                valid with { Amount = 0m }, valid with { Amount = -1m }, valid with { Amount = 0.00001m },
                valid with { CurrencyId = int.MaxValue }, valid with { BaseAmount = 50m }, valid with { Date = default } })
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + "/cash-flows", invalid)).StatusCode);
            var photo = await client.PostAsJsonAsync(path + "/snapshots", new { });
            Assert.Equal(new DateOnly(2026, 9, 1), (await photo.Content.ReadFromJsonAsync<SnapshotDetailDto>())!.Month);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/portfolios/2147483647/history")).StatusCode);
        }
    }
}
