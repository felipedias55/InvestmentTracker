using System.Net;
using System.Net.Http.Json;
using InvestmentTracker.Application.Allocation.Dtos;
using InvestmentTracker.Application.Allocation.Tracking;
using InvestmentTracker.Application.Movements;
using InvestmentTracker.Application.Strategies;
using InvestmentTracker.Application.Trades;
using InvestmentTracker.Domain.Entities;
using InvestmentTracker.Infrastructure.Persistence;
using InvestmentTracker.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.IntegrationTests.Strategies
{
    [Collection(DatabaseCollection.Name)]
    public class ContributionTrackingApiTests(DatabaseFixture fixture)
    {
        private static readonly DateOnly Day = new(2026, 9, 8);
        private async Task<(int Portfolio, int Other, int Asset)> Seed()
        {
            await fixture.ResetAsync(); await using var db = fixture.Database.CreateContext(); await CatalogSeed.ApplyAsync(db);
            var portfolio = await db.Portfolios.SingleAsync();
            var other = new Portfolio { Name = "Other", BaseCurrencyId = portfolio.BaseCurrencyId, CreatedAt = DateTime.UtcNow };
            var asset = new Asset { Ticker = "TRACK", Name = "Tracking", CurrencyId = portfolio.BaseCurrencyId,
                AssetTypeId = (await db.AssetTypes.FirstAsync(t => t.InstrumentClass == "general")).Id,
                CountryId = (await db.Countries.FirstAsync()).Id, AssetCategoryId = (await db.AssetCategories.FirstAsync()).Id, SectorId = (await db.Sectors.FirstAsync()).Id };
            db.Add(other); db.Add(asset); await db.SaveChangesAsync();
            db.Add(new PortfolioAsset { PortfolioId = portfolio.Id, AssetId = asset.Id, Quantity = 1, CurrentValue = 100, InvestedAmount = 100, UpdatedOn = Day });
            await db.SaveChangesAsync(); return (portfolio.Id, other.Id, asset.Id);
        }
        private static async Task<SaveContributionPlan> Draft(HttpClient client, string path)
        {
            var request = new ContributionPlanRequestDto(100) { Strategy = new StrategyConfig("Regular", "regular", "equal", [], []) };
            var response = await client.PostAsJsonAsync(path + "/contribution-plan", request); response.EnsureSuccessStatusCode();
            var plan = (await response.Content.ReadFromJsonAsync<ContributionPlanDto>())!;
            return new(Guid.NewGuid(), "Plano setembro", Day, Day, request, plan.PlanToken!);
        }
        [Fact]
        public async Task SavesSnapshotLinksExactlyOnceAndTracksReversalWithoutFinancialWrites()
        {
            var (portfolio, other, asset) = await Seed(); using var client = fixture.ApiFactory.CreateClient(); var path = $"/api/portfolios/{portfolio}";
            var input = await Draft(client, path);
            var response = await client.PostAsJsonAsync(path + "/contribution-plans", input); response.EnsureSuccessStatusCode();
            var saved = (await response.Content.ReadFromJsonAsync<TrackingPlanDetail>())!; var planPath = path + "/contribution-plans/" + saved.Plan.Id;
            Assert.Equal("pending", saved.Status); Assert.Equal(100, Assert.Single(saved.Rows).Planned.Amount);
            var retry = await client.PostAsJsonAsync(path + "/contribution-plans", input); retry.EnsureSuccessStatusCode();
            Assert.Equal(saved.Plan.Id, (await retry.Content.ReadFromJsonAsync<TrackingPlanDetail>())!.Plan.Id);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/contribution-plans", input with { Name = "Changed" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/portfolios/{other}/contribution-plans/{saved.Plan.Id}")).StatusCode);
            await using (var db = fixture.Database.CreateContext())
            {
                Assert.Empty(await db.Set<FinancialMovement>().ToListAsync()); Assert.Empty(await db.PortfolioSnapshots.ToListAsync());
                Assert.Equal(100, (await db.PortfolioAssets.SingleAsync()).CurrentValue); Assert.False(db.Database.HasPendingModelChanges());
            }
            var buy = await client.PostAsJsonAsync(path + "/trades", new SaveTradeDto(Guid.NewGuid(), Day, "buy", asset, 1, 100, Fees: 1)); buy.EnsureSuccessStatusCode();
            var trade = (await buy.Content.ReadFromJsonAsync<TradeDto>())!;
            var before = await client.GetStringAsync(path); var beforeMovements = await client.GetStringAsync(path + "/movements");
            var candidates = await client.GetFromJsonAsync<PurchasePage>(planPath + "/purchases"); Assert.Equal(trade.Id, Assert.Single(candidates!.Items).TradeId);
            var link = new LinkPurchase(trade.Id, saved.Rows[0].Planned.Key);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(planPath + "/purchases", link)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(planPath + "/purchases", link)).StatusCode);
            var actual = (await client.GetFromJsonAsync<TrackingPlanDetail>(planPath))!;
            Assert.Equal(101, actual.Realized); Assert.Equal(-1, actual.Difference); Assert.Equal("exceeded", actual.Status); Assert.Single(actual.Purchases);
            Assert.Empty((await client.GetFromJsonAsync<PurchasePage>(planPath + "/purchases"))!.Items);
            Assert.Equal(before, await client.GetStringAsync(path)); Assert.Equal(beforeMovements, await client.GetStringAsync(path + "/movements"));
            var secondInput = await Draft(client, path); var secondResponse = await client.PostAsJsonAsync(path + "/contribution-plans", secondInput); secondResponse.EnsureSuccessStatusCode();
            var second = (await secondResponse.Content.ReadFromJsonAsync<TrackingPlanDetail>())!;
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + $"/contribution-plans/{second.Plan.Id}/purchases", link)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/portfolios/{other}/contribution-plans/{saved.Plan.Id}/purchases/{actual.Purchases[0].Id}")).StatusCode);
            var movement = Assert.Single((await client.GetFromJsonAsync<MovementDto[]>(path + "/movements"))!);
            var reversed = await client.PostAsJsonAsync(path + $"/movements/{movement.Id}/reversal", new ReverseMovementDto(Guid.NewGuid(), "Revisão de compra")); reversed.EnsureSuccessStatusCode();
            actual = (await client.GetFromJsonAsync<TrackingPlanDetail>(planPath))!;
            Assert.Equal(0, actual.Realized); Assert.False(Assert.Single(actual.Purchases).Active); Assert.Equal(100, actual.Rows[0].Planned.Amount);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(planPath + "/purchases", link)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(planPath + $"/purchases/{actual.Purchases[0].Id}")).StatusCode);
            Assert.NotNull(Assert.Single((await client.GetFromJsonAsync<TrackingPlanDetail>(planPath))!.Purchases).RemovedAtUtc);
        }
        [Fact]
        public async Task RejectsChangedSimulationInvalidDatesPagingAndCrossPortfolioPurchases()
        {
            var (portfolio, other, asset) = await Seed(); using var client = fixture.ApiFactory.CreateClient(); var path = $"/api/portfolios/{portfolio}";
            var input = await Draft(client, path);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/contribution-plans", input with { ExpectedToken = new string('0', 64) })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + "/contribution-plans", input with { From = Day.AddDays(1) })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + "/contribution-plans?pageSize=100")).StatusCode);
            var response = await client.PostAsJsonAsync(path + "/contribution-plans", input); response.EnsureSuccessStatusCode();
            var saved = (await response.Content.ReadFromJsonAsync<TrackingPlanDetail>())!;
            var buy = await client.PostAsJsonAsync($"/api/portfolios/{other}/trades", new SaveTradeDto(Guid.NewGuid(), Day, "buy", asset, 1, 100)); buy.EnsureSuccessStatusCode();
            var trade = (await buy.Content.ReadFromJsonAsync<TradeDto>())!;
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + $"/contribution-plans/{saved.Plan.Id}/purchases", new LinkPurchase(trade.Id, saved.Rows[0].Planned.Key))).StatusCode);
            var page = (await client.GetFromJsonAsync<TrackingPlanPage>(path + "/contribution-plans?pageIndex=999"))!;
            Assert.Equal(0, page.PageIndex); Assert.Single(page.Items);
        }
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ForeignPurchasesUseHistoricalOrExplicitEquivalentWithoutCurrentExchangeRate(bool fromCash)
        {
            var (portfolio, _, assetId) = await Seed(); int planId, cashId;
            await using (var db = fixture.Database.CreateContext())
            {
                var usd = await db.Currencies.SingleAsync(c => c.Code == "USD"); var asset = await db.Assets.SingleAsync(a => a.Id == assetId); asset.CurrencyId = usd.Id;
                var cash = new ExternalAsset { PortfolioId = portfolio, CurrencyId = usd.Id, Name = "USD cash", Value = 100, UpdatedOn = Day }; db.Add(cash);
                var row = new ContributionPlanRowDto("usd", "Categoria", "Setor", 100, [new("USD", 20, 5, "2026-09-08", false)]) { CategoryId = asset.AssetCategoryId, SectorId = asset.SectorId };
                var snapshot = new ContributionPlanSnapshot("BRL", new(100), new([row], 0), []);
                var plan = new SavedContributionPlan { PortfolioId = portfolio, RequestId = Guid.NewGuid(), Name = "USD", From = Day, To = Day,
                    SnapshotJson = System.Text.Json.JsonSerializer.Serialize(snapshot), RequestJson = "{}", CreatedAtUtc = DateTime.UtcNow };
                db.Add(plan); await db.SaveChangesAsync(); planId = plan.Id; cashId = cash.Id;
            }
            using var client = fixture.ApiFactory.CreateClient(); var path = $"/api/portfolios/{portfolio}";
            var response = await client.PostAsJsonAsync(path + "/trades", new SaveTradeDto(Guid.NewGuid(), Day, "buy", assetId, 1, 10, fromCash ? cashId : null, fromCash ? null : 50m)); response.EnsureSuccessStatusCode();
            var trade = (await response.Content.ReadFromJsonAsync<TradeDto>())!;
            var planPath = path + $"/contribution-plans/{planId}";
            if (fromCash) Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(planPath + "/purchases", new LinkPurchase(trade.Id, "usd"))).StatusCode);
            var link = new LinkPurchase(trade.Id, "usd", fromCash ? 49.1234m : null);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(planPath + "/purchases", link)).StatusCode);
            var result = (await client.GetFromJsonAsync<TrackingPlanDetail>(planPath))!;
            Assert.Equal(fromCash ? 49.1234m : 50m, result.Realized); Assert.Equal(fromCash ? "manual" : "historical", Assert.Single(result.Purchases).ConversionSource);
            await using var check = fixture.Database.CreateContext(); Assert.Empty(await check.ExchangeRates.ToListAsync());
        }
    }
}
