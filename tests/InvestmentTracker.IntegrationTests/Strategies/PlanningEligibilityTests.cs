using System.Net;
using System.Net.Http.Json;
using InvestmentTracker.Application.Allocation.Dtos;
using InvestmentTracker.Application.AssetTypes.Dtos;
using InvestmentTracker.Application.Sectors.Dtos;
using InvestmentTracker.Application.Strategies;
using InvestmentTracker.Domain.Entities;
using InvestmentTracker.Infrastructure.Persistence;
using InvestmentTracker.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.IntegrationTests.Strategies
{
    [Collection(DatabaseCollection.Name)]
    public sealed class PlanningEligibilityTests(DatabaseFixture fixture)
    {
        [Fact]
        public async Task ClassificationShouldSurviveRenameAndOmittedFieldsWithoutInferringFromNames()
        {
            await fixture.ResetAsync(); using var client = fixture.ApiFactory.CreateClient();
            var created = await client.PostAsJsonAsync("/api/asset-types", new CreateAssetTypeDto("Renda urbana", "fii"));
            created.EnsureSuccessStatusCode();
            var type = (await created.Content.ReadFromJsonAsync<AssetTypeDto>())!;
            var renamed = await client.PutAsJsonAsync($"/api/asset-types/{type.Id}", new { name = "Tecnologia" });
            renamed.EnsureSuccessStatusCode();
            Assert.Equal("fii", (await renamed.Content.ReadFromJsonAsync<AssetTypeDto>())!.InstrumentClass);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/asset-types/{type.Id}", new { name = "Não salvar", instrumentClass = "unknown" })).StatusCode);
            var unchanged = (await client.GetFromJsonAsync<AssetTypeDto>($"/api/asset-types/{type.Id}"))!;
            Assert.Equal("Tecnologia", unchanged.Name); Assert.Equal("fii", unchanged.InstrumentClass);
            var namedFii = await client.PostAsJsonAsync("/api/asset-types", new CreateAssetTypeDto("FII"));
            namedFii.EnsureSuccessStatusCode(); Assert.Equal("general", (await namedFii.Content.ReadFromJsonAsync<AssetTypeDto>())!.InstrumentClass);
            var sectorResponse = await client.PostAsJsonAsync("/api/sectors", new CreateSectorDto("Área A", true));
            sectorResponse.EnsureSuccessStatusCode(); var sector = (await sectorResponse.Content.ReadFromJsonAsync<SectorDto>())!;
            var sectorRename = await client.PutAsJsonAsync($"/api/sectors/{sector.Id}", new { name = "Outro nome" });
            sectorRename.EnsureSuccessStatusCode(); Assert.True((await sectorRename.Content.ReadFromJsonAsync<SectorDto>())!.IsRealEstate);
            await using var db = fixture.Database.CreateContext();
            Assert.False(await db.Set<FinancialMovement>().AnyAsync()); Assert.False(await db.PortfolioSnapshots.AnyAsync());
        }

        [Fact]
        public async Task PairRestrictionsShouldPersistAndApplyToReservesAndRemainingWithoutChangingFinancialData()
        {
            await fixture.ResetAsync();
            int portfolioId, categoryId, sectorId, otherCategoryId, realEstateSectorId;
            await using (var db = fixture.Database.CreateContext())
            {
                await CatalogSeed.ApplyAsync(db);
                var portfolio = await db.Portfolios.SingleAsync(); portfolioId = portfolio.Id;
                var categories = await db.AssetCategories.OrderBy(c => c.Id).Take(2).ToListAsync(); categoryId = categories[0].Id;
                otherCategoryId = categories[1].Id;
                var first = new Sector { Name = "Atividades gerais" }; var second = new Sector { Name = "Parques", IsRealEstate = true };
                var general = new AssetType { Name = "Instrumento comum" }; var fii = new AssetType { Name = "Fundos urbanos", InstrumentClass = "fii" };
                db.AddRange(first, second, general, fii); await db.SaveChangesAsync(); sectorId = first.Id; realEstateSectorId = second.Id;
                var country = await db.Countries.FirstAsync();
                for (var i = 0; i < 4; i++)
                {
                    db.PortfolioAssets.Add(new PortfolioAsset { PortfolioId = portfolio.Id, Quantity = 1, InvestedAmount = (i + 1) * 100, CurrentValue = (i + 1) * 100,
                        UpdatedOn = new(2026, 10, 30), Asset = new Asset { Ticker = "META" + i, Name = "Sintético", CurrencyId = portfolio.BaseCurrencyId,
                            CountryId = country.Id, AssetCategoryId = categories[i < 2 ? 0 : 1].Id, AssetTypeId = i < 2 ? general.Id : fii.Id,
                            SectorId = i % 2 == 0 ? first.Id : second.Id } });
                }
                await db.SaveChangesAsync();
            }
            using var client = fixture.ApiFactory.CreateClient();
            var config = new StrategyConfig("Elegibilidade", "custom", "equal", [], [new(categoryId, "fixed", 30)], ExcludedCombinations: [new(categoryId, sectorId)]);
            var save = await client.PostAsJsonAsync($"/api/portfolios/{portfolioId}/strategies", new SaveStrategy(Guid.NewGuid(), 0, config)); save.EnsureSuccessStatusCode();
            var saved = (await client.GetFromJsonAsync<SavedStrategy[]>($"/api/portfolios/{portfolioId}/strategies"))!.Single();
            Assert.Equal(new StrategyCombination(categoryId, sectorId), saved.Config.ExcludedCombinations!.Single());
            var response = await client.PostAsJsonAsync($"/api/portfolios/{portfolioId}/contribution-plan", new ContributionPlanRequestDto(100) { Strategy = saved.Config });
            response.EnsureSuccessStatusCode(); var result = (await response.Content.ReadFromJsonAsync<ContributionPlanDto>())!;
            Assert.Equal(600, result.EligibleBase); Assert.Equal(1000, result.Dashboard.Summary.CurrentValue);
            Assert.Equal(100, result.Plan!.Rows.Sum(r => r.Amount)); Assert.Equal(0, result.Plan.Unallocated);
            Assert.All(result.Plan.Rows, row => Assert.Equal("Parques", row.Sector));
            Assert.Contains(result.StrategyNotes, note => note.Contains("inelegíveis"));
            var cappedConfig = config with { ConcentrationLimits = [new("category", categoryId, 25)] };
            var revise = await client.PostAsJsonAsync($"/api/portfolios/{portfolioId}/strategies", new SaveStrategy(saved.ProfileId, 1, cappedConfig));
            revise.EnsureSuccessStatusCode();
            var restored = (await client.GetFromJsonAsync<SavedStrategy[]>($"/api/portfolios/{portfolioId}/strategies"))!.Single();
            Assert.Equal(2, restored.Revision); Assert.Equal(25, restored.Config.ConcentrationLimits!.Single().MaximumPercentage);
            var cappedResponse = await client.PostAsJsonAsync($"/api/portfolios/{portfolioId}/contribution-plan", new ContributionPlanRequestDto(100) { Strategy = restored.Config });
            cappedResponse.EnsureSuccessStatusCode(); var capped = (await cappedResponse.Content.ReadFromJsonAsync<ContributionPlanDto>())!;
            Assert.Equal(35, capped.Plan!.Rows.Sum(r => r.Amount)); Assert.Equal(65, capped.Plan.Unallocated);
            Assert.Equal(0, capped.Concentration.Single().SuggestedContribution); Assert.True(capped.Concentration.Single().AboveLimit);
            Assert.Null(capped.Category); Assert.Null(capped.Sector);
            Assert.Equal(1000, capped.Dashboard.Summary.CurrentValue);
            var priorityConfig = config with { UsePriorities = true, Rules = [new(categoryId, "fixed", 80, 5), new(otherCategoryId, "fixed", 80, 1)],
                GroupPreferences = [new(categoryId, realEstateSectorId, 1, "BRL")] };
            var prioritySave = await client.PostAsJsonAsync($"/api/portfolios/{portfolioId}/strategies", new SaveStrategy(saved.ProfileId, 2, priorityConfig));
            prioritySave.EnsureSuccessStatusCode();
            var prioritySaved = (await client.GetFromJsonAsync<SavedStrategy[]>($"/api/portfolios/{portfolioId}/strategies"))!.Single();
            Assert.True(prioritySaved.Config.UsePriorities); Assert.Equal("BRL", prioritySaved.Config.GroupPreferences!.Single().PreferredCurrency);
            var priorityResponse = await client.PostAsJsonAsync($"/api/portfolios/{portfolioId}/contribution-plan", new ContributionPlanRequestDto(100) { Strategy = prioritySaved.Config });
            priorityResponse.EnsureSuccessStatusCode(); var priorityPlan = (await priorityResponse.Content.ReadFromJsonAsync<ContributionPlanDto>())!;
            Assert.True(priorityPlan.PrioritiesApplied); Assert.Equal(80, priorityPlan.RuleFunding.Single(r => r.CategoryId == otherCategoryId).Budget);
            Assert.Equal(20, priorityPlan.RuleFunding.Single(r => r.CategoryId == categoryId).Budget);
            Assert.Equal(otherCategoryId, priorityPlan.Plan!.Rows[0].CategoryId); Assert.Equal(100, priorityPlan.Plan.Rows.Sum(r => r.Amount));
            Assert.All(priorityPlan.Plan.Rows, row => { Assert.Equal("BRL", row.SuggestedCurrency); Assert.NotEmpty(row.Currencies.Single().Tickers); });
            var invalid = await client.PostAsJsonAsync($"/api/portfolios/{portfolioId}/strategies", new SaveStrategy(Guid.NewGuid(), 0,
                config with { ExcludedCombinations = [new(categoryId, int.MaxValue)] }));
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            var badLimit = await client.PostAsJsonAsync($"/api/portfolios/{portfolioId}/strategies", new SaveStrategy(Guid.NewGuid(), 0,
                config with { ConcentrationLimits = [new("sector", int.MaxValue, 50)] }));
            Assert.Equal(HttpStatusCode.BadRequest, badLimit.StatusCode);
            await using var check = fixture.Database.CreateContext();
            Assert.Equal(1000, await check.PortfolioAssets.SumAsync(p => p.CurrentValue));
            Assert.False(await check.Set<FinancialMovement>().AnyAsync()); Assert.False(await check.PortfolioSnapshots.AnyAsync());
            Assert.Equal(3, await check.Set<PortfolioStrategyRevision>().CountAsync());
        }
    }
}
