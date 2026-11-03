using System.Net;
using System.Net.Http.Json;
using InvestmentTracker.Application.Income;
using InvestmentTracker.Application.Planning;
using InvestmentTracker.Domain.Entities;
using InvestmentTracker.Infrastructure.Persistence;
using InvestmentTracker.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InvestmentTracker.IntegrationTests.Strategies
{
    [Collection(DatabaseCollection.Name)]
    public class PlanningGoalsApiTests(DatabaseFixture fixture)
    {
        private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 11, 3, 15, 0, 0, TimeSpan.Zero); }
        private HttpClient Client() => fixture.ApiFactory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<TimeProvider, Clock>())).CreateClient();
        private async Task<(int Portfolio, int Other, int Asset)> Seed(bool foreign = false)
        {
            await fixture.ResetAsync(); await using var db = fixture.Database.CreateContext(); await CatalogSeed.ApplyAsync(db);
            var portfolio = await db.Portfolios.SingleAsync();
            var other = new Portfolio { Name = "Other", BaseCurrencyId = portfolio.BaseCurrencyId, CreatedAt = DateTime.UtcNow };
            var asset = new Asset { Ticker = "OBJECTIVE", Name = "Objective", CurrencyId = foreign ? (await db.Currencies.SingleAsync(c => c.Code == "USD")).Id : portfolio.BaseCurrencyId,
                AssetTypeId = (await db.AssetTypes.FirstAsync(t => t.InstrumentClass == "general")).Id, CountryId = (await db.Countries.FirstAsync()).Id,
                AssetCategoryId = (await db.AssetCategories.FirstAsync()).Id, SectorId = (await db.Sectors.FirstAsync()).Id };
            db.Add(other); db.Add(asset); await db.SaveChangesAsync();
            db.Add(new PortfolioAsset { PortfolioId = portfolio.Id, AssetId = asset.Id, Quantity = 1, CurrentValue = 100, InvestedAmount = 100, UpdatedOn = new(2026, 1, 1) });
            db.Add(new ExternalAsset { PortfolioId = portfolio.Id, CurrencyId = portfolio.BaseCurrencyId, Name = "Outside", Value = 100000, UpdatedOn = new(2026, 1, 1) });
            await db.SaveChangesAsync(); return (portfolio.Id, other.Id, asset.Id);
        }
        private static SavePlanningGoal Request(string name = "Patrimônio", string kind = "wealth") => new(Guid.NewGuid(), 0, new(name, kind, "BRL", 1000, new(2027, 1, 31), kind == "wealth" ? 250 : 0));
        [Fact]
        public async Task GoalsAreVersionedFilteredIsolatedAndNeverChangeFinancialState()
        {
            var (id, other, _) = await Seed(); using var client = Client(); var path = $"/api/portfolios/{id}/planning-goals";
            var input = Request(); var before = await client.GetStringAsync($"/api/portfolios/{id}");
            var response = await client.PostAsJsonAsync(path, input); response.EnsureSuccessStatusCode();
            Assert.Equal(1, (await response.Content.ReadFromJsonAsync<SavedPlanningGoal>())!.Revision);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(path, input)).StatusCode);
            var page = (await client.GetFromJsonAsync<PlanningGoalPage>(path))!; var item = Assert.Single(page.Items);
            Assert.Equal(100, item.Current); Assert.Equal(300, item.RequiredMonthlyContribution); Assert.Equal(850, item.ScenarioValue);
            Assert.Equal(new DateOnly(2026, 11, 3), page.Today); Assert.Equal(new DateOnly(2025, 11, 1), page.IncomeFrom); Assert.Equal(new DateOnly(2026, 10, 31), page.IncomeTo);
            var next = input with { ExpectedRevision = 1, Config = input.Config with { Target = 2000 } };
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(path, next)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, input)).StatusCode);
            var history = (await client.GetFromJsonAsync<SavedPlanningGoal[]>(path + $"/{input.GoalId}/revisions"))!;
            Assert.Equal(2, history.Length); Assert.Equal(1000, history[1].Config.Target);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/portfolios/{other}/planning-goals/{input.GoalId}/revisions")).StatusCode);
            Assert.Empty((await client.GetFromJsonAsync<PlanningGoalPage>($"/api/portfolios/{other}/planning-goals"))!.Items);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(path, next with { ExpectedRevision = 2, Config = next.Config with { Archived = true } })).StatusCode);
            Assert.Empty((await client.GetFromJsonAsync<PlanningGoalPage>(path))!.Items);
            var archived = (await client.GetFromJsonAsync<PlanningGoalPage>(path + "?status=archived&pageIndex=999"))!;
            Assert.Equal(0, archived.PageIndex); Assert.Equal("archived", Assert.Single(archived.Items).Status);
            Assert.Empty((await client.GetFromJsonAsync<PlanningGoalPage>(path + "?status=all&search=Missing"))!.Items);
            Assert.Equal(before, await client.GetStringAsync($"/api/portfolios/{id}"));
            await using var check = fixture.Database.CreateContext();
            Assert.Empty(await check.Set<FinancialMovement>().ToListAsync()); Assert.Empty(await check.PortfolioCashFlows.ToListAsync()); Assert.Empty(await check.PortfolioSnapshots.ToListAsync());
            Assert.Equal(100000, (await check.ExternalAssets.SingleAsync()).Value); Assert.Equal(3, await check.Set<PlanningGoalRevision>().CountAsync()); Assert.False(check.Database.HasPendingModelChanges());
        }
        [Fact]
        public async Task IncomeUsesHistoricalEquivalentsAndBlocksMissingConversions()
        {
            var (id, _, asset) = await Seed(true); using var client = Client(); var root = $"/api/portfolios/{id}";
            var income = await client.PostAsJsonAsync(root + "/income", new SaveIncomeDto(Guid.NewGuid(), new(2026, 9, 30), asset, 1200, BaseAmount: 6000)); income.EnsureSuccessStatusCode();
            var input = Request("Renda", "monthly-income"); Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(root + "/planning-goals", input)).StatusCode);
            var item = Assert.Single((await client.GetFromJsonAsync<PlanningGoalPage>(root + "/planning-goals"))!.Items);
            Assert.Equal(500, item.Current); Assert.Equal(50, item.Percentage); Assert.Null(item.RequiredMonthlyContribution);
            var pending = await client.PostAsJsonAsync(root + "/income", new SaveIncomeDto(Guid.NewGuid(), new(2026, 10, 30), asset, 10)); pending.EnsureSuccessStatusCode();
            var page = (await client.GetFromJsonAsync<PlanningGoalPage>(root + "/planning-goals"))!;
            Assert.Equal(1, page.MissingIncomeConversions); Assert.Equal("unavailable", Assert.Single(page.Items).Status); Assert.Null(page.Items[0].Current);
            var receipt = (await pending.Content.ReadFromJsonAsync<IncomeDto>())!;
            var conversion = await client.PostAsJsonAsync(root + $"/income/{receipt.Id}/conversions", new SaveIncomeConversionDto(Guid.NewGuid(), "BRL", 0, "Histórico confirmado", 60)); conversion.EnsureSuccessStatusCode();
            page = (await client.GetFromJsonAsync<PlanningGoalPage>(root + "/planning-goals"))!;
            Assert.Equal(0, page.MissingIncomeConversions); Assert.Equal(505, Assert.Single(page.Items).Current);
        }
        [Fact]
        public async Task CurrencyChangeDoesNotSilentlyConvertGoalsAndInvalidRequestsAreRejected()
        {
            var (id, _, _) = await Seed(); using var client = Client(); var path = $"/api/portfolios/{id}/planning-goals";
            var input = Request(); Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(path, input)).StatusCode);
            await using (var db = fixture.Database.CreateContext()) { var portfolio = await db.Portfolios.SingleAsync(p => p.Id == id); portfolio.BaseCurrencyId = (await db.Currencies.SingleAsync(c => c.Code == "USD")).Id; await db.SaveChangesAsync(); }
            var item = Assert.Single((await client.GetFromJsonAsync<PlanningGoalPage>(path))!.Items); Assert.Equal("currency-changed", item.Status); Assert.Null(item.Current); Assert.Equal("BRL", item.Goal.Config.Currency);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, input with { ExpectedRevision = 1, Config = input.Config with { Currency = "USD" } })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(path, input with { ExpectedRevision = 1, Config = input.Config with { Archived = true } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, Request() with { Config = input.Config with { Target = .001m } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + "?pageSize=100")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + "?kind=yield")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/portfolios/2147483647/planning-goals")).StatusCode);
        }
    }
}
