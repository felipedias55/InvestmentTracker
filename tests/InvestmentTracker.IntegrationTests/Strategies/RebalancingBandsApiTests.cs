using System.Net;
using System.Net.Http.Json;
using InvestmentTracker.Application.Allocation.Dtos;
using InvestmentTracker.Application.Allocation.Tracking;
using InvestmentTracker.Application.Strategies;
using InvestmentTracker.Domain.Entities;
using InvestmentTracker.Infrastructure.Persistence;
using InvestmentTracker.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.IntegrationTests.Strategies
{
    [Collection(DatabaseCollection.Name)]
    public class RebalancingBandsApiTests(DatabaseFixture fixture)
    {
        [Fact]
        public async Task BandsPersistApplyAndStayFrozenInSavedPlansWithoutFinancialWrites()
        {
            await fixture.ResetAsync(); int portfolioId, categoryId, sectorId;
            await using (var db = fixture.Database.CreateContext())
            {
                await CatalogSeed.ApplyAsync(db); var portfolio = await db.Portfolios.SingleAsync(); portfolioId = portfolio.Id;
                var categories = await db.AssetCategories.OrderBy(c => c.Id).Take(2).ToListAsync(); var sectors = await db.Sectors.OrderBy(s => s.Id).Take(2).ToListAsync();
                categoryId = categories[0].Id; sectorId = sectors[0].Id;
                for (var i = 0; i < 2; i++)
                {
                    db.Add(new PortfolioAsset { PortfolioId = portfolio.Id, Quantity = 1, CurrentValue = i == 0 ? 40 : 60, InvestedAmount = i == 0 ? 40 : 60, UpdatedOn = new(2026, 11, 3),
                        Asset = new Asset { Ticker = "BAND" + i, Name = "Faixa", CurrencyId = portfolio.BaseCurrencyId, AssetTypeId = (await db.AssetTypes.FirstAsync(t => t.InstrumentClass == "general")).Id,
                            CountryId = (await db.Countries.FirstAsync()).Id, AssetCategoryId = categories[i].Id, SectorId = sectors[i].Id } });
                    db.Add(new CategoryAllocationTarget { PortfolioId = portfolio.Id, AssetCategoryId = categories[i].Id, TargetPercentage = .5m });
                    db.Add(new SectorAllocationTarget { PortfolioId = portfolio.Id, SectorId = sectors[i].Id, TargetPercentage = .5m });
                }
                await db.SaveChangesAsync();
            }
            using var client = fixture.ApiFactory.CreateClient(); var path = $"/api/portfolios/{portfolioId}";
            var before = await client.GetStringAsync(path);
            var config = new StrategyConfig("Faixas locais", "bands", "targets", [], [], .2m, Bands: [new("category", categoryId, .03m), new("sector", sectorId, 0)]);
            var request = new SaveStrategy(Guid.NewGuid(), 0, config);
            var savedResponse = await client.PostAsJsonAsync(path + "/strategies", request); savedResponse.EnsureSuccessStatusCode();
            var restored = Assert.Single((await client.GetFromJsonAsync<SavedStrategy[]>(path + "/strategies"))!);
            Assert.Equal(0, restored.Config.Bands!.Single(b => b.Dimension == "sector").Tolerance);
            var simulation = new ContributionPlanRequestDto(100) { Strategy = restored.Config };
            var resultResponse = await client.PostAsJsonAsync(path + "/contribution-plan", simulation); resultResponse.EnsureSuccessStatusCode();
            var result = (await resultResponse.Content.ReadFromJsonAsync<ContributionPlanDto>())!;
            Assert.Equal(100, Assert.Single(result.Plan!.Rows).Amount); Assert.Equal(4, result.Bands.Count);
            var specific = result.Bands.Single(b => b.Dimension == "category" && b.GroupId == categoryId);
            Assert.Equal("below", specific.State); Assert.Equal(3, specific.TolerancePoints); Assert.Equal(47, specific.LowerPercentage);
            var date = new DateOnly(2026, 11, 3);
            var savePlan = new SaveContributionPlan(Guid.NewGuid(), "Plano por faixas", date, date, simulation, result.PlanToken!);
            var storedResponse = await client.PostAsJsonAsync(path + "/contribution-plans", savePlan); storedResponse.EnsureSuccessStatusCode();
            var stored = (await storedResponse.Content.ReadFromJsonAsync<TrackingPlanDetail>())!;
            Assert.Equal(result.Bands.ToArray(), stored.Snapshot.Bands.ToArray());
            var wide = config with { Bands = [new("category", categoryId, .2m), new("sector", sectorId, .2m)] };
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(path + "/strategies", request with { ExpectedRevision = 1, Config = wide })).StatusCode);
            var changed = await client.PostAsJsonAsync(path + "/contribution-plan", simulation with { Strategy = wide }); changed.EnsureSuccessStatusCode();
            Assert.Equal(100, (await changed.Content.ReadFromJsonAsync<ContributionPlanDto>())!.Plan!.Unallocated);
            var copy = (await client.GetFromJsonAsync<TrackingPlanDetail>(path + $"/contribution-plans/{stored.Plan.Id}"))!;
            Assert.Equal(result.Bands.ToArray(), copy.Snapshot.Bands.ToArray()); Assert.Equal(100, Assert.Single(copy.Rows).Planned.Amount);
            // A changed tolerance can preserve the amount while changing the explanation: the token must still protect the saved copy.
            var alternate = config with { Bands = [new("category", categoryId, .04m), new("sector", sectorId, 0)] };
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/contribution-plans", savePlan with { RequestId = Guid.NewGuid(), Simulation = simulation with { Strategy = alternate } })).StatusCode);
            Assert.Equal(before, await client.GetStringAsync(path));
            await using var check = fixture.Database.CreateContext(); Assert.Empty(await check.Set<FinancialMovement>().ToListAsync()); Assert.Empty(await check.PortfolioSnapshots.ToListAsync()); Assert.Empty(await check.PortfolioCashFlows.ToListAsync());
            Assert.Equal(2, await check.Set<PortfolioStrategyRevision>().CountAsync()); Assert.False(check.Database.HasPendingModelChanges());
        }
        [Fact]
        public async Task UnknownBandReferencesAndInvalidTolerancesAreRejected()
        {
            await fixture.ResetAsync(); int id;
            await using (var db = fixture.Database.CreateContext()) { await CatalogSeed.ApplyAsync(db); id = (await db.Portfolios.SingleAsync()).Id; }
            using var client = fixture.ApiFactory.CreateClient(); var path = $"/api/portfolios/{id}/strategies";
            var config = new StrategyConfig("Faixas", "bands", "targets", [], [], Bands: [new("category", int.MaxValue, .01m)]);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new SaveStrategy(Guid.NewGuid(), 0, config))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new SaveStrategy(Guid.NewGuid(), 0, config with { Bands = [new("sector", int.MaxValue, .01m)] }))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new SaveStrategy(Guid.NewGuid(), 0, config with { Bands = [new("category", 1, .00001m)] }))).StatusCode);
        }
    }
}
