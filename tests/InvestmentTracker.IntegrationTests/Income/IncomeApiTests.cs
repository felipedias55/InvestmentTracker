using InvestmentTracker.Application.Movements;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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
        private sealed class HistoricalRates : IHistoricalExchangeRateProvider
        {
            public decimal Rate { get; set; } = 5.2m;
            public int Calls { get; private set; }
            public DateOnly? RequestedDate { get; private set; }
            public Task<ExchangeRate?> FetchAsync(string b, string q, DateOnly date, CancellationToken ct)
            { Calls++; RequestedDate = date; return Task.FromResult<ExchangeRate?>(new ExchangeRate { BaseCode = b, QuoteCode = q, Rate = Rate, RateDate = date.AddDays(-2) }); }
        }
        [Fact]
        public async Task AutomaticConversion_ShouldFreezeOnRetryAndPreserveOnReversal()
        {
            var (portfolio, asset, cash) = await Seed(true); var rates = new HistoricalRates();
            using var client = fixture.ApiFactory.WithWebHostBuilder(b => b.ConfigureTestServices(s => {
                s.AddSingleton<TimeProvider, Clock>(); s.AddSingleton<IHistoricalExchangeRateProvider>(rates);
            })).CreateClient();
            var path = $"/api/portfolios/{portfolio}";
            var input = new SaveIncomeDto(Guid.NewGuid(), Today, asset, 10, cash);
            var response = await client.PostAsJsonAsync(path + "/income", input); response.EnsureSuccessStatusCode();
            var original = (await response.Content.ReadFromJsonAsync<IncomeDto>())!;
            Assert.Equal(52m, Assert.Single(original.Conversions).BaseAmount); Assert.Equal(Today, rates.RequestedDate);
            rates.Rate = 7;
            response = await client.PostAsJsonAsync(path + "/income", input); response.EnsureSuccessStatusCode();
            Assert.Equal(52m, (await response.Content.ReadFromJsonAsync<IncomeDto>())!.Conversions[0].BaseAmount); Assert.Equal(1, rates.Calls);
            var movement = Assert.Single((await client.GetFromJsonAsync<List<MovementDto>>(path + "/movements"))!);
            (await client.PostAsJsonAsync(path + $"/movements/{movement.Id}/reversal", new ReverseMovementDto(Guid.NewGuid(), "Duplicado"))).EnsureSuccessStatusCode();
            var report = (await client.GetFromJsonAsync<IncomeAnalysisDto>(path + "/income/analysis"))!;
            var row = Assert.Single(report.ConvertedAssets); Assert.Equal(52m, row.Received); Assert.Equal(52m, row.Reversed); Assert.Equal(0m, row.Net);
            Assert.Equal(1, rates.Calls);
        }
        [Fact]
        public async Task PendingConversion_ShouldSupportAuditedCorrectionsWithoutChangingBalancesOrSnapshots()
        {
            var (portfolio, asset, cash) = await Seed(true); using var client = Client(); var path = $"/api/portfolios/{portfolio}";
            var response = await client.PostAsJsonAsync(path + "/income", new SaveIncomeDto(Guid.NewGuid(), Today, asset, 10, cash)); response.EnsureSuccessStatusCode();
            var receipt = (await response.Content.ReadFromJsonAsync<IncomeDto>())!; Assert.Empty(receipt.Conversions);
            var pending = (await client.GetFromJsonAsync<IncomeAnalysisDto>(path + "/income/analysis"))!;
            Assert.Null(pending.ConvertedAssets.Single().Net); Assert.Equal(10m, pending.Assets.Single().Net);
            var snapshotResponse = await client.PostAsync(path + "/history/snapshots", null); snapshotResponse.EnsureSuccessStatusCode();
            var snapshot = (await snapshotResponse.Content.ReadFromJsonAsync<SnapshotDetailDto>())!;
            var frozen = await client.GetStringAsync(path + $"/history/snapshots/{snapshot.Id}");
            var endpoint = path + $"/income/{receipt.Id}/conversions";
            var input = new SaveIncomeConversionDto(Guid.NewGuid(), "BRL", 0, "Extrato conferido", 51.2345m);
            (await client.PostAsJsonAsync(endpoint, input)).EnsureSuccessStatusCode();
            (await client.PostAsJsonAsync(endpoint, input)).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(endpoint, input with { BaseAmount = 53 })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(endpoint, input with { RequestId = Guid.NewGuid() })).StatusCode);
            (await client.PostAsJsonAsync(endpoint, input with { RequestId = Guid.NewGuid(), ExpectedRevision = 1, BaseAmount = 52, Reason = "Retificação do extrato" })).EnsureSuccessStatusCode();
            var listed = Assert.Single((await client.GetFromJsonAsync<List<IncomeDto>>(path + "/income"))!);
            Assert.Equal(2, listed.Conversions.Count); Assert.Equal(51.2345m, listed.Conversions.Single(x => x.Revision == 1).BaseAmount);
            Assert.Equal(52m, listed.Conversions.Single(x => x.Revision == 2).BaseAmount);
            Assert.Equal(frozen, await client.GetStringAsync(path + $"/history/snapshots/{snapshot.Id}"));
            await using var db = fixture.Database.CreateContext();
            Assert.Equal(1010m, (await db.ExternalAssets.SingleAsync()).Value); Assert.Equal(25m, (await db.PortfolioAssets.SingleAsync()).Income);
            Assert.Empty(await db.PortfolioCashFlows.ToListAsync());
            var movements = (await client.GetFromJsonAsync<List<MovementDto>>(path + "/movements"))!;
            Assert.Equal(2, movements.Count(x => x.Kind == "income-conversion"));
            Assert.True(movements.Single(x => x.Kind == "income").CanReverse);
            Assert.All(movements.Where(x => x.Kind == "income-conversion"), m => { Assert.Empty(m.Effects); Assert.False(m.CanReverse); });
        }
        [Fact]
        public async Task Conversion_ShouldRespectPortfolioScopeCurrencyChangesAndConcurrentRevision()
        {
            var (portfolio, asset, cash) = await Seed(true); using var client = Client(); var path = $"/api/portfolios/{portfolio}";
            var response = await client.PostAsJsonAsync(path + "/income", new SaveIncomeDto(Guid.NewGuid(), Today, asset, 10, cash, BaseAmount: 50)); response.EnsureSuccessStatusCode();
            var receipt = (await response.Content.ReadFromJsonAsync<IncomeDto>())!;
            int other;
            await using (var db = fixture.Database.CreateContext()) {
                var p = new Portfolio { Name = "Outra", BaseCurrencyId = (await db.Currencies.SingleAsync(x => x.Code == "BRL")).Id, CreatedAt = DateTime.UtcNow };
                db.Add(p); await db.SaveChangesAsync(); other = p.Id;
            }
            var input = new SaveIncomeConversionDto(Guid.NewGuid(), "BRL", 1, "Conferência", 55);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/portfolios/{other}/income/{receipt.Id}/conversions", input)).StatusCode);
            var endpoint = path + $"/income/{receipt.Id}/conversions";
            var concurrent = await Task.WhenAll(client.PostAsJsonAsync(endpoint, input), client.PostAsJsonAsync(endpoint, input with { RequestId = Guid.NewGuid(), BaseAmount = 56 }));
            Assert.Single(concurrent, x => x.IsSuccessStatusCode); Assert.Single(concurrent, x => x.StatusCode == HttpStatusCode.Conflict);
            await using (var db = fixture.Database.CreateContext()) { (await db.Portfolios.SingleAsync(x => x.Id == portfolio)).BaseCurrencyId = (await db.Currencies.SingleAsync(x => x.Code == "USD")).Id; await db.SaveChangesAsync(); }
            var report = (await client.GetFromJsonAsync<IncomeAnalysisDto>(path + "/income/analysis"))!;
            Assert.Equal("USD", report.BaseCurrencyCode); Assert.Equal(10m, report.ConvertedAssets.Single().Net);
            Assert.Equal("BRL", (await client.GetFromJsonAsync<List<IncomeDto>>(path + "/income"))!.Single().BaseCurrencyCode);
        }
        [Fact]
        public async Task MigrationAndStartupCheck_ShouldDetectMissingTableAndPreserveLegacyIncome()
        {
            var (portfolio, asset, _) = await Seed(true);
            await using var db = fixture.Database.CreateContext();
            db.Add(new IncomeReceipt { PortfolioId = portfolio, AssetId = asset, PositionId = (await db.PortfolioAssets.SingleAsync()).Id,
                RequestId = Guid.NewGuid(), Date = Today, CurrencyCode = "USD", Ticker = "LEGACY", Amount = 10, CreatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
            var migrator = db.GetService<IMigrator>();
            try {
                await migrator.MigrateAsync("20260913180243_ClosingsFeesAndSnapshotVersions");
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => DatabaseSchemaGuard.EnsureCurrentAsync(db));
                Assert.Contains("migrations pendentes", error.Message);
                await migrator.MigrateAsync(); db.ChangeTracker.Clear();
                await DatabaseSchemaGuard.EnsureCurrentAsync(db);
                var receipt = await db.Set<IncomeReceipt>().Include(x => x.Conversions).SingleAsync();
                Assert.Equal(10m, receipt.Amount); Assert.Null(receipt.BaseCurrencyCode); Assert.Empty(receipt.Conversions);
            } finally { await migrator.MigrateAsync(); }
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
            Assert.Equal(before.Replace("\"isOutdated\":false", "\"isOutdated\":true"), await client.GetStringAsync($"/api/portfolios/{portfolio}/history/snapshots/{id}"));
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, dto with { RequestId = Guid.NewGuid(), Date = Today.AddDays(-1) })).StatusCode);
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
