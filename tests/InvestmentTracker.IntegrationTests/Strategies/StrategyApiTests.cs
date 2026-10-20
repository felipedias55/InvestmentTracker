using System.Net;
using System.Net.Http.Json;
using InvestmentTracker.Application.Strategies;
using InvestmentTracker.Domain.Entities;
using InvestmentTracker.Infrastructure.Persistence;
using InvestmentTracker.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.IntegrationTests.Strategies
{
    [Collection(DatabaseCollection.Name)]
    public class StrategyApiTests(DatabaseFixture fixture)
    {
        [Fact]
        public async Task ProfilesAreVersionedIsolatedAndDoNotChangeFinancialData()
        {
            await fixture.ResetAsync();
            int id, otherId, categoryId;
            await using (var db = fixture.Database.CreateContext())
            {
                await CatalogSeed.ApplyAsync(db);
                var first = await db.Portfolios.SingleAsync();
                var other = new Portfolio { Name = "Other", BaseCurrencyId = first.BaseCurrencyId, CreatedAt = DateTime.UtcNow };
                db.Add(other); await db.SaveChangesAsync();
                id = first.Id; otherId = other.Id; categoryId = await db.AssetCategories.Select(c => c.Id).FirstAsync();
            }
            using var client = fixture.ApiFactory.CreateClient();
            var path = $"/api/portfolios/{id}/strategies";
            var config = new StrategyConfig("Buy and hold", "buy-hold", "targets", [], []);
            var request = new SaveStrategy(Guid.NewGuid(), 0, config);
            var response = await client.PostAsJsonAsync(path,request);
            response.EnsureSuccessStatusCode();
            var saved = (await response.Content.ReadFromJsonAsync<SavedStrategy>())!;
            Assert.Equal(1,saved.Revision);
            var next = request with { ExpectedRevision = 1, Config = config with { Name = "Revised" } };
            Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync(path,next)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(path,request)).StatusCode);
            Assert.Equal(2,Assert.Single((await client.GetFromJsonAsync<SavedStrategy[]>(path))!).Revision);
            Assert.Empty((await client.GetFromJsonAsync<SavedStrategy[]>($"/api/portfolios/{otherId}/strategies"))!);
            Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(path,new SaveStrategy(Guid.NewGuid(),0,config with { Rules = [new(999999,"fixed",100)] }))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/api/portfolios/2147483647/strategies")).StatusCode);
            var preview = await client.PostAsJsonAsync($"/api/portfolios/{id}/contribution-plan",
                new InvestmentTracker.Application.Allocation.Dtos.ContributionPlanRequestDto(123.45m)
                { Strategy = config with { Model = "regular", Remainder = "equal" } });
            preview.EnsureSuccessStatusCode();
            var plan = (await preview.Content.ReadFromJsonAsync<InvestmentTracker.Application.Allocation.Dtos.ContributionPlanDto>())!;
            Assert.Equal(123.45m, plan.Plan!.Unallocated);
            Assert.NotEmpty(plan.StrategyNotes);
            await using var check = fixture.Database.CreateContext();
            Assert.Equal(2,await check.Set<PortfolioStrategyRevision>().CountAsync());
            Assert.Empty(await check.PortfolioAssets.ToListAsync());
            Assert.Empty(await check.Set<FinancialMovement>().ToListAsync());
        }
    }
}
